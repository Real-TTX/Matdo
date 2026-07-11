using Matdo.Web.Data;
using Matdo.Web.Data.Entities;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace Matdo.Web.Services;

/// <summary>
/// Registrierung, Anmeldung und Abmeldung. Passwörter werden mit BCrypt gehasht,
/// Sessions als persistente Datensätze (Token = UUID) angelegt.
/// </summary>
public class AuthService
{
    public const int SessionDays = 30;

    private readonly MatdoDbContext _db;
    private readonly IHttpContextAccessor _http;
    private readonly JsonConfigService _config;

    public AuthService(MatdoDbContext db, IHttpContextAccessor http, JsonConfigService config)
    {
        _db = db;
        _http = http;
        _config = config;
    }

    public record AuthResult(bool Success, string? Error = null);

    /// <summary>Ob überhaupt schon ein Benutzer existiert (für die Ersteinrichtung).</summary>
    public Task<bool> AnyUsersAsync() => _db.Users.AnyAsync();

    /// <summary>
    /// Ob sich neue Benutzer aktuell selbst registrieren dürfen. Ist die offene Registrierung
    /// abgeschaltet, dürfen sich nur eingeladene E-Mail-Adressen registrieren.
    /// </summary>
    public async Task<bool> CanRegisterAsync(string? email = null)
    {
        // Der allererste Benutzer muss sich immer anlegen können (wird Admin).
        if (!await _db.Users.AnyAsync()) return true;
        if (_config.Current.AllowRegistration) return true;
        if (string.IsNullOrWhiteSpace(email)) return false;
        var e = email.Trim().ToLowerInvariant();
        return await _db.Invitations.AnyAsync(i => i.Email == e && !i.Accepted);
    }

    public async Task<AuthResult> RegisterAsync(string email, string password, string displayName)
    {
        email = email.Trim().ToLowerInvariant();
        if (!await CanRegisterAsync(email))
            return new AuthResult(false, "Die Registrierung ist derzeit deaktiviert.");
        if (await _db.Users.AnyAsync(u => u.Email == email))
            return new AuthResult(false, "Es existiert bereits ein Konto mit dieser E-Mail-Adresse.");

        User user;
        // Registrierung serialisieren (Postgres-Advisory-Lock), damit bei gleichzeitiger
        // Erst-Registrierung auf leerer DB nicht zwei Benutzer parallel Admin werden (TOCTOU).
        await using (var tx = await _db.Database.BeginTransactionAsync())
        {
            await _db.Database.ExecuteSqlRawAsync("SELECT pg_advisory_xact_lock(4444777)");

            // Nach Erhalt des Locks erneut prüfen (ein paralleler Lauf kann inzwischen angelegt haben).
            if (await _db.Users.AnyAsync(u => u.Email == email))
                return new AuthResult(false, "Es existiert bereits ein Konto mit dieser E-Mail-Adresse.");

            // Erster Benutzer wird automatisch Admin, alle weiteren normale User.
            var isFirst = !await _db.Users.AnyAsync();
            var roleName = isFirst ? Role.Admin : Role.User;
            var role = await _db.Roles.FirstAsync(r => r.Name == roleName);

            user = new User
            {
                Email = email,
                DisplayName = string.IsNullOrWhiteSpace(displayName) ? email.Split('@')[0] : displayName.Trim(),
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(password),
                RoleId = role.Id,
                IsActive = true
            };
            _db.Users.Add(user);
            await _db.SaveChangesAsync();

            // Ausstehende Einladungen (Team/Projekt) für diese E-Mail automatisch übernehmen.
            await TeamService.ApplyPendingInvitationsAsync(_db, user);

            await tx.CommitAsync();
        }

        await CreateSessionAsync(user);
        return new AuthResult(true);
    }

    public async Task<AuthResult> LoginAsync(string email, string password)
    {
        email = email.Trim().ToLowerInvariant();
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Email == email);
        if (user is null || !user.IsActive || !BCrypt.Net.BCrypt.Verify(password, user.PasswordHash))
            return new AuthResult(false, "E-Mail-Adresse oder Passwort ist ungültig.");

        await CreateSessionAsync(user);
        return new AuthResult(true);
    }

    public async Task LogoutAsync()
    {
        var ctx = _http.HttpContext;
        if (ctx is not null && ctx.Request.Cookies.TryGetValue(SessionAuthenticationHandler.CookieName, out var raw)
            && Guid.TryParse(raw, out var token))
        {
            var session = await _db.UserSessions.FirstOrDefaultAsync(s => s.Token == token);
            if (session is not null)
            {
                _db.UserSessions.Remove(session);
                await _db.SaveChangesAsync();
            }
            ctx.Response.Cookies.Delete(SessionAuthenticationHandler.CookieName);
        }
    }

    private async Task CreateSessionAsync(User user)
    {
        var ctx = _http.HttpContext;
        var session = new UserSession
        {
            UserId = user.Id,
            Token = Guid.NewGuid(),
            ExpiresAt = DateTime.UtcNow.AddDays(SessionDays),
            LastSeenAt = DateTime.UtcNow,
            UserAgent = ctx?.Request.Headers.UserAgent.ToString(),
            IpAddress = ctx?.Connection.RemoteIpAddress?.ToString(),
            CreateUserId = user.Id,
            UpdateUserId = user.Id
        };
        _db.UserSessions.Add(session);
        await _db.SaveChangesAsync();

        ctx?.Response.Cookies.Append(SessionAuthenticationHandler.CookieName, session.Token.ToString(), new CookieOptions
        {
            HttpOnly = true,
            IsEssential = true,
            SameSite = SameSiteMode.Lax,
            Secure = ctx.Request.IsHttps,
            Expires = session.ExpiresAt,
            Path = "/"
        });

        // Darstellungs-/Sprach-Einstellungen des Benutzers in Cookies übernehmen (geräteübergreifend).
        if (ctx is not null)
        {
            var prefOpts = new CookieOptions
            {
                Expires = DateTimeOffset.UtcNow.AddYears(1),
                IsEssential = true,
                SameSite = SameSiteMode.Lax,
                Secure = ctx.Request.IsHttps,
                Path = "/"
            };
            ctx.Response.Cookies.Append(UiPreferences.SchemeCookie, user.ColorScheme, prefOpts);
            ctx.Response.Cookies.Append(UiPreferences.ThemeCookie, user.Theme, prefOpts);
            ctx.Response.Cookies.Append(UiPreferences.LangCookie, user.Language, prefOpts);
        }
    }
}
