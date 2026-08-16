using System.Text.Json;
using System.Text.Json.Serialization;
using Matdo.Web.Data;
using Microsoft.EntityFrameworkCore;

namespace Matdo.Web.Services;

/// <summary>
/// Persönliche Daten eines Nutzers (DSGVO Art. 15/20): Vollständiger Export der eigenen
/// Daten als portables JSON. Geheimnisse (Passwort-Hash, Tokens, verschlüsselte OAuth-
/// Tokens, Push-Schlüssel, Session-Tokens) werden bewusst NICHT exportiert.
/// </summary>
public class AccountDataService
{
    private readonly MatdoDbContext _db;
    private readonly ICurrentUserAccessor _me;

    public AccountDataService(MatdoDbContext db, ICurrentUserAccessor me)
    {
        _db = db;
        _me = me;
    }

    private long Uid => _me.UserId ?? throw new InvalidOperationException("Kein angemeldeter Benutzer.");

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter() }
    };

    /// <summary>Baut den vollständigen Personendaten-Export als eingerücktes JSON (UTF-8-Bytes).</summary>
    public async Task<byte[]> ExportJsonAsync(DateTime generatedAtUtc, CancellationToken ct = default)
    {
        var uid = Uid;

        var user = await _db.Users.AsNoTracking()
            .Where(u => u.Id == uid)
            .Select(u => new
            {
                u.Email,
                u.DisplayName,
                Role = u.Role!.Name,
                u.IsActive,
                u.EmailConfirmed,
                u.TimeZone,
                u.ColorScheme,
                u.Theme,
                u.Language,
                CreatedAtUtc = u.CreateDate
            })
            .FirstOrDefaultAsync(ct);

        var projects = await _db.Projects.AsNoTracking()
            .Where(p => p.OwnerId == uid)
            .OrderBy(p => p.Id)
            .Select(p => new
            {
                p.Id,
                p.Name,
                p.Color,
                p.ViewType,
                p.IsFavorite,
                p.IsArchived,
                p.Position,
                p.TeamId,
                p.ParentProjectId,
                CreatedAtUtc = p.CreateDate,
                Columns = p.Columns.OrderBy(c => c.Position)
                    .Select(c => new { c.Id, c.Name, c.Position }).ToList()
            })
            .ToListAsync(ct);

        var tasks = await _db.Tasks.AsNoTracking()
            .Where(t => t.OwnerId == uid)
            .OrderBy(t => t.Id)
            .Select(t => new
            {
                t.Id,
                t.Title,
                t.Description,
                t.ProjectId,
                t.KanbanColumnId,
                t.ParentTaskId,
                t.AssigneeId,
                t.Priority,
                t.DueDate,
                t.DueHasTime,
                t.DeadlineDate,
                t.DeadlineHasTime,
                t.RecurrenceUnit,
                t.RecurrenceInterval,
                t.IsCompleted,
                t.CompletedAt,
                t.Position,
                CreatedAtUtc = t.CreateDate,
                LabelIds = t.TaskLabels.Select(tl => tl.LabelId).ToList(),
                Reminders = t.Reminders.Select(r => new
                {
                    r.Type,
                    r.RemindAt,
                    r.OffsetMinutes,
                    r.Channel,
                    r.IsSent,
                    r.SentAt
                }).ToList()
            })
            .ToListAsync(ct);

        var labels = await _db.Labels.AsNoTracking()
            .Where(l => l.OwnerId == uid)
            .OrderBy(l => l.Id)
            .Select(l => new { l.Id, l.Name, l.Color, l.IsFavorite })
            .ToListAsync(ct);

        var notes = await _db.Notes.AsNoTracking()
            .Where(n => n.OwnerId == uid)
            .OrderBy(n => n.Id)
            .Select(n => new
            {
                n.Id,
                n.Title,
                n.Body,
                n.ProjectId,
                n.IsPinned,
                CreatedAtUtc = n.CreateDate,
                UpdatedAtUtc = n.UpdateDate
            })
            .ToListAsync(ct);

        // Kalender-Verbindungen OHNE Tokens (AccessTokenEnc/RefreshTokenEnc werden nicht exportiert).
        var calendars = await _db.CalendarConnections.AsNoTracking()
            .Where(c => c.UserId == uid)
            .OrderBy(c => c.Id)
            .Select(c => new
            {
                c.Id,
                c.Provider,
                c.DisplayName,
                c.Color,
                c.IsEnabled,
                c.IcsUrl,
                c.ExternalCalendarId,
                c.ExportTasks,
                c.LastSyncAt
            })
            .ToListAsync(ct);

        var teamsOwned = await _db.Teams.AsNoTracking()
            .Where(t => t.OwnerId == uid)
            .OrderBy(t => t.Id)
            .Select(t => new { t.Id, t.Name })
            .ToListAsync(ct);

        var memberships = await _db.TeamMembers.AsNoTracking()
            .Where(m => m.UserId == uid)
            .OrderBy(m => m.TeamId)
            .Select(m => new { m.TeamId, TeamName = m.Team!.Name, m.Role })
            .ToListAsync(ct);

        var sharesGivenProjects = await _db.ProjectShares.AsNoTracking()
            .Where(s => s.Project!.OwnerId == uid)
            .Select(s => new { s.ProjectId, s.SharedWithUserId, s.Permission })
            .ToListAsync(ct);
        var sharesGivenTasks = await _db.TaskShares.AsNoTracking()
            .Where(s => s.TaskItem!.OwnerId == uid)
            .Select(s => new { s.TaskItemId, s.SharedWithUserId, s.Permission })
            .ToListAsync(ct);
        var sharesReceivedProjects = await _db.ProjectShares.AsNoTracking()
            .Where(s => s.SharedWithUserId == uid)
            .Select(s => new { s.ProjectId, OwnerId = s.Project!.OwnerId, s.Permission })
            .ToListAsync(ct);
        var sharesReceivedTasks = await _db.TaskShares.AsNoTracking()
            .Where(s => s.SharedWithUserId == uid)
            .Select(s => new { s.TaskItemId, OwnerId = s.TaskItem!.OwnerId, s.Permission })
            .ToListAsync(ct);

        // Sessions OHNE Token – nur Metadaten (Gerät/IP/Zeiten).
        var sessions = await _db.UserSessions.AsNoTracking()
            .Where(s => s.UserId == uid)
            .OrderBy(s => s.Id)
            .Select(s => new { CreatedAtUtc = s.CreateDate, s.LastSeenAt, s.ExpiresAt, s.UserAgent, s.IpAddress })
            .ToListAsync(ct);

        var export = new
        {
            export = new
            {
                format = "matdo-personal-data-export",
                version = 1,
                generatedAtUtc,
                note = "Enthält alle personenbezogenen Daten dieses Kontos. Geheimnisse (Passwort, Tokens) sind bewusst ausgenommen."
            },
            profile = user,
            projects,
            tasks,
            labels,
            notes,
            calendarConnections = calendars,
            teamsOwned,
            teamMemberships = memberships,
            sharesGiven = new { projects = sharesGivenProjects, tasks = sharesGivenTasks },
            sharesReceived = new { projects = sharesReceivedProjects, tasks = sharesReceivedTasks },
            sessions
        };

        return JsonSerializer.SerializeToUtf8Bytes(export, JsonOpts);
    }
}
