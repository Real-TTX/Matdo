using Matdo.Web.Data;
using Matdo.Web.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace Matdo.Web.Services.Calendar;

/// <summary>
/// Verwaltung der Kalender-Verbindungen eines Benutzers und die eigentliche Synchronisation
/// (Termine lesen/zwischenspeichern; optional Aufgaben als Termine exportieren).
/// </summary>
public class CalendarService
{
    private readonly MatdoDbContext _db;
    private readonly ICurrentUserAccessor _me;
    private readonly IcsCalendarReader _ics;
    private readonly IEnumerable<ICalendarProvider> _providers;
    private readonly TokenProtector _tokens;
    private readonly JsonConfigService _config;
    private readonly ILogger<CalendarService> _logger;

    public CalendarService(MatdoDbContext db, ICurrentUserAccessor me, IcsCalendarReader ics,
        IEnumerable<ICalendarProvider> providers, TokenProtector tokens, JsonConfigService config,
        ILogger<CalendarService> logger)
    {
        _db = db;
        _me = me;
        _ics = ics;
        _providers = providers;
        _tokens = tokens;
        _config = config;
        _logger = logger;
    }

    private long Uid => _me.UserId ?? throw new InvalidOperationException("Kein angemeldeter Benutzer.");

    public ICalendarProvider? ProviderFor(CalendarProvider p) => _providers.FirstOrDefault(x => x.Provider == p);
    public IEnumerable<ICalendarProvider> OAuthProviders => _providers.Where(p => p.Provider != CalendarProvider.Ics);

    // ----- Benutzer-Operationen -----

    public Task<List<CalendarConnection>> GetConnectionsAsync(long? userId = null) =>
        _db.CalendarConnections.Where(c => c.UserId == (userId ?? Uid)).OrderBy(c => c.Id).ToListAsync();

    public async Task AddIcsAsync(string url, string displayName, string color)
    {
        _db.CalendarConnections.Add(new CalendarConnection
        {
            UserId = Uid,
            Provider = CalendarProvider.Ics,
            IcsUrl = url.Trim(),
            DisplayName = string.IsNullOrWhiteSpace(displayName) ? "ICS-Kalender" : displayName.Trim(),
            Color = color,
            IsEnabled = true
        });
        await _db.SaveChangesAsync();
    }

    public string BuildRedirectUri(CalendarProvider provider) =>
        _config.Current.PublicBaseUrl.TrimEnd('/') + "/calendar/callback/" + provider.ToString().ToLowerInvariant();

    /// <summary>Speichert eine neue OAuth-Verbindung nach erfolgreichem Callback.</summary>
    public async Task StoreOAuthConnectionAsync(long userId, CalendarProvider provider, OAuthTokens tokens, string displayName)
    {
        _db.CalendarConnections.Add(new CalendarConnection
        {
            UserId = userId,
            Provider = provider,
            DisplayName = displayName,
            Color = provider == CalendarProvider.Google ? "#1a73e8" : "#0078d4",
            ExternalCalendarId = "primary",
            AccessTokenEnc = _tokens.Protect(tokens.AccessToken),
            RefreshTokenEnc = _tokens.Protect(tokens.RefreshToken),
            TokenExpiresAt = tokens.ExpiresAtUtc,
            IsEnabled = true
        });
        await _db.SaveChangesAsync();
    }

    public async Task DisconnectAsync(long connectionId)
    {
        var c = await _db.CalendarConnections.FirstOrDefaultAsync(x => x.Id == connectionId && x.UserId == Uid);
        if (c is null) return;
        _db.CalendarConnections.Remove(c);
        await _db.SaveChangesAsync();
    }

    public async Task SetOptionsAsync(long connectionId, bool isEnabled, bool exportTasks)
    {
        var c = await _db.CalendarConnections.FirstOrDefaultAsync(x => x.Id == connectionId && x.UserId == Uid);
        if (c is null) return;
        c.IsEnabled = isEnabled;
        c.ExportTasks = exportTasks;
        await _db.SaveChangesAsync();
    }

    public async Task SyncNowAsync(long connectionId)
    {
        var c = await _db.CalendarConnections.FirstOrDefaultAsync(x => x.Id == connectionId && x.UserId == Uid);
        if (c is not null) await SyncConnectionAsync(c, CancellationToken.None);
    }

    /// <summary>Zwischengespeicherte Termine im Zeitraum (nur aktivierte Verbindungen).</summary>
    public async Task<List<CalendarEventDto>> GetEventsAsync(long userId, DateTime fromUtc, DateTime toUtc)
    {
        return await _db.ExternalEvents
            .Where(e => e.UserId == userId && e.StartUtc < toUtc && e.EndUtc >= fromUtc && e.Connection!.IsEnabled)
            .Select(e => new CalendarEventDto(e.Title, e.StartUtc, e.EndUtc, e.AllDay, e.Connection!.Color))
            .ToListAsync();
    }

    // ----- Synchronisation -----

    public async Task SyncAllAsync(CancellationToken ct)
    {
        var conns = await _db.CalendarConnections.ToListAsync(ct);
        foreach (var c in conns)
        {
            try { await SyncConnectionAsync(c, ct); }
            catch (Exception ex) { _logger.LogWarning(ex, "Kalender-Sync für Verbindung {Id} fehlgeschlagen.", c.Id); }
        }
    }

    public async Task SyncConnectionAsync(CalendarConnection conn, CancellationToken ct)
    {
        var fromUtc = DateTime.UtcNow.AddDays(-31);
        var toUtc = DateTime.UtcNow.AddDays(120);

        try
        {
            List<SimpleEvent> events;
            if (conn.Provider == CalendarProvider.Ics)
            {
                if (string.IsNullOrWhiteSpace(conn.IcsUrl)) return;
                events = await _ics.ReadAsync(conn.IcsUrl, fromUtc, toUtc, ct);
            }
            else
            {
                var token = await GetValidAccessTokenAsync(conn, ct);
                var provider = ProviderFor(conn.Provider);
                if (token is null || provider is null) return;
                events = await provider.ListEventsAsync(token, conn.ExternalCalendarId ?? "primary", fromUtc, toUtc, ct);

                if (conn.ExportTasks)
                    await ExportTasksAsync(conn, provider, token, ct);
            }

            await CacheEventsAsync(conn, events, ct);
            conn.LastSyncAt = DateTime.UtcNow;
            conn.LastError = null;
        }
        catch (Exception ex)
        {
            conn.LastError = ex.Message;
            _logger.LogWarning(ex, "Sync für Verbindung {Id} fehlgeschlagen.", conn.Id);
        }
        await _db.SaveChangesAsync(ct);
    }

    private async Task CacheEventsAsync(CalendarConnection conn, List<SimpleEvent> events, CancellationToken ct)
    {
        // Doppelte ExternalIds im Feed entschärfen (sonst verletzt der Unique-Index den Insert).
        events = events.GroupBy(e => e.ExternalId).Select(g => g.First()).ToList();
        var existing = await _db.ExternalEvents.Where(e => e.CalendarConnectionId == conn.Id).ToListAsync(ct);
        var byId = existing.ToDictionary(e => e.ExternalId);
        var seen = new HashSet<string>();

        foreach (var ev in events)
        {
            seen.Add(ev.ExternalId);
            if (byId.TryGetValue(ev.ExternalId, out var row))
            {
                row.Title = ev.Title; row.StartUtc = ev.StartUtc; row.EndUtc = ev.EndUtc; row.AllDay = ev.AllDay;
            }
            else
            {
                _db.ExternalEvents.Add(new ExternalEvent
                {
                    CalendarConnectionId = conn.Id, UserId = conn.UserId,
                    ExternalId = ev.ExternalId, Title = ev.Title,
                    StartUtc = ev.StartUtc, EndUtc = ev.EndUtc, AllDay = ev.AllDay
                });
            }
        }
        foreach (var stale in existing.Where(e => !seen.Contains(e.ExternalId)))
            _db.ExternalEvents.Remove(stale);
    }

    /// <summary>Aufgaben mit Fälligkeit als Termine in den externen Kalender exportieren/aktualisieren.</summary>
    private async Task ExportTasksAsync(CalendarConnection conn, ICalendarProvider provider, string token, CancellationToken ct)
    {
        var cal = conn.ExternalCalendarId ?? "primary";
        var tasks = await _db.Tasks
            .Where(t => t.OwnerId == conn.UserId && t.ParentTaskId == null && t.DueDate != null && !t.IsCompleted)
            .ToListAsync(ct);
        var links = await _db.TaskCalendarLinks.Where(l => l.CalendarConnectionId == conn.Id).ToListAsync(ct);
        var linkByTask = links.ToDictionary(l => l.TaskItemId);

        // Anlegen/aktualisieren
        foreach (var task in tasks)
        {
            linkByTask.TryGetValue(task.Id, out var link);
            var extId = await provider.UpsertEventAsync(token, cal, link?.ExternalEventId, task, ct);
            // Falls extern gelöscht (Update schlägt fehl): neu anlegen.
            if (extId is null && link is not null)
                extId = await provider.UpsertEventAsync(token, cal, null, task, ct);
            if (extId is null) continue;
            if (link is null)
                _db.TaskCalendarLinks.Add(new TaskCalendarLink { TaskItemId = task.Id, CalendarConnectionId = conn.Id, ExternalEventId = extId });
            else
                link.ExternalEventId = extId;
        }

        // Entfernen für nicht mehr exportierbare Aufgaben (erledigt/gelöscht/ohne Fälligkeit)
        var activeIds = tasks.Select(t => t.Id).ToHashSet();
        foreach (var link in links.Where(l => !activeIds.Contains(l.TaskItemId)))
        {
            try { await provider.DeleteEventAsync(token, cal, link.ExternalEventId, ct); } catch { }
            _db.TaskCalendarLinks.Remove(link);
        }
    }

    private async Task<string?> GetValidAccessTokenAsync(CalendarConnection conn, CancellationToken ct)
    {
        if (conn.TokenExpiresAt.HasValue && conn.TokenExpiresAt.Value > DateTime.UtcNow)
            return _tokens.Unprotect(conn.AccessTokenEnc);

        var refresh = _tokens.Unprotect(conn.RefreshTokenEnc);
        var provider = ProviderFor(conn.Provider);
        if (refresh is null || provider is null) return _tokens.Unprotect(conn.AccessTokenEnc);

        var fresh = await provider.RefreshAsync(refresh, ct);
        if (fresh is null) return null;

        conn.AccessTokenEnc = _tokens.Protect(fresh.AccessToken);
        conn.RefreshTokenEnc = _tokens.Protect(fresh.RefreshToken ?? refresh);
        conn.TokenExpiresAt = fresh.ExpiresAtUtc;
        await _db.SaveChangesAsync(ct);
        return fresh.AccessToken;
    }
}

/// <summary>Termin für die Anzeige (mit Farbe der Verbindung).</summary>
public record CalendarEventDto(string Title, DateTime StartUtc, DateTime EndUtc, bool AllDay, string Color);
