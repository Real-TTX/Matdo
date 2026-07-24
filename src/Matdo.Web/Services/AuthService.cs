using System.Net;
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
    private const int MaxFailedLogins = 5;      // danach temporäre Sperre
    private const int LockoutMinutes = 15;
    private const int ResetValidHours = 2;      // Gültigkeit des Passwort-Reset-Links

    private readonly MatdoDbContext _db;
    private readonly IHttpContextAccessor _http;
    private readonly JsonConfigService _config;
    private readonly EmailSender _email;
    private readonly ILogger<AuthService> _logger;

    public AuthService(MatdoDbContext db, IHttpContextAccessor http, JsonConfigService config,
        EmailSender email, ILogger<AuthService> logger)
    {
        _db = db;
        _http = http;
        _config = config;
        _email = email;
        _logger = logger;
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
                IsActive = true,
                // Der erste Nutzer (Admin, Ersteinrichtung) gilt sofort als bestätigt; alle
                // weiteren müssen ihre E-Mail per Link bestätigen.
                EmailConfirmed = isFirst,
                EmailConfirmToken = isFirst ? null : Guid.NewGuid()
            };
            _db.Users.Add(user);
            await _db.SaveChangesAsync();

            // Einladungen werden NICHT mehr automatisch übernommen – der neue Nutzer nimmt sie
            // bewusst unter „Einladungen" an (Zustimmung). Sie bleiben als offen bestehen.

            await tx.CommitAsync();
        }

        await CreateSessionAsync(user);
        // Bestätigungs-Mail nach dem Commit verschicken (weiches Gate: Login ist schon möglich).
        if (!user.EmailConfirmed && user.EmailConfirmToken is Guid vtok)
            await SendVerificationEmailAsync(user, vtok);
        return new AuthResult(true);
    }

    public async Task<AuthResult> LoginAsync(string email, string password)
    {
        email = email.Trim().ToLowerInvariant();
        var now = DateTime.UtcNow;
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Email == email);
        // Einheitliche Fehlermeldung – verrät nicht, ob das Konto existiert oder gesperrt ist
        // (keine Nutzer-Enumeration). Die Sperre wirkt unabhängig von der Meldung.
        const string invalid = "E-Mail-Adresse oder Passwort ist ungültig.";

        if (user is null || !user.IsActive) return new AuthResult(false, invalid);
        if (user.LockoutUntilUtc is DateTime until && until > now) return new AuthResult(false, invalid);

        if (!BCrypt.Net.BCrypt.Verify(password, user.PasswordHash))
        {
            user.FailedLoginCount++;
            if (user.FailedLoginCount >= MaxFailedLogins)
            {
                user.LockoutUntilUtc = now.AddMinutes(LockoutMinutes);
                user.FailedLoginCount = 0;
            }
            await _db.SaveChangesAsync();
            return new AuthResult(false, invalid);
        }

        if (user.FailedLoginCount != 0 || user.LockoutUntilUtc != null)
        {
            user.FailedLoginCount = 0;
            user.LockoutUntilUtc = null;
            await _db.SaveChangesAsync();
        }
        await CreateSessionAsync(user);
        return new AuthResult(true);
    }

    // ----- E-Mail-Bestätigung -----

    /// <summary>Bestätigt die E-Mail-Adresse anhand des Tokens aus dem Link.</summary>
    public async Task<bool> ConfirmEmailAsync(Guid token)
    {
        var user = await _db.Users.FirstOrDefaultAsync(u => u.EmailConfirmToken == token);
        if (user is null) return false;
        user.EmailConfirmed = true;
        user.EmailConfirmToken = null;
        await _db.SaveChangesAsync();
        return true;
    }

    /// <summary>Verschickt dem (noch unbestätigten) Nutzer erneut den Bestätigungslink.</summary>
    public async Task ResendVerificationAsync(long userId)
    {
        var user = await _db.Users.FindAsync(userId);
        if (user is null || user.EmailConfirmed) return;
        user.EmailConfirmToken ??= Guid.NewGuid();
        await _db.SaveChangesAsync();
        await SendVerificationEmailAsync(user, user.EmailConfirmToken.Value);
    }

    // ----- Passwort zurücksetzen -----

    /// <summary>Erzeugt (falls das Konto existiert) einen Reset-Token und mailt den Link.
    /// Gibt bewusst nichts zurück – der Aufrufer zeigt immer dieselbe neutrale Meldung.</summary>
    public async Task RequestPasswordResetAsync(string email)
    {
        email = (email ?? "").Trim().ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(email)) return;
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Email == email && u.IsActive);
        if (user is null) return;   // neutral: kein Hinweis auf Existenz

        user.PasswordResetToken = Guid.NewGuid();
        user.PasswordResetExpiresUtc = DateTime.UtcNow.AddHours(ResetValidHours);
        await _db.SaveChangesAsync();

        var link = BuildLink($"/Account/ResetPassword?token={user.PasswordResetToken}");
        var html = $"<p>Hallo {WebUtility.HtmlEncode(user.DisplayName)},</p>"
            + "<p>zum Zurücksetzen deines Matdo-Passworts klicke auf den folgenden Link "
            + $"(gültig {ResetValidHours} Stunden):</p><p><a href=\"{link}\">{link}</a></p>"
            + "<p>Wenn du das nicht angefordert hast, ignoriere diese E-Mail einfach.</p>";
        await SendMailOrLogAsync(user.Email, user.DisplayName, "Passwort zurücksetzen · Matdo", html, link);
    }

    /// <summary>Setzt das Passwort per gültigem Token. Invalidiert alle bestehenden Sessions.</summary>
    public async Task<AuthResult> ResetPasswordAsync(Guid token, string newPassword)
    {
        var now = DateTime.UtcNow;
        var user = await _db.Users.FirstOrDefaultAsync(u => u.PasswordResetToken == token && u.PasswordResetExpiresUtc > now);
        if (user is null) return new AuthResult(false, "Der Link ist ungültig oder abgelaufen.");

        user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(newPassword);
        user.PasswordResetToken = null;
        user.PasswordResetExpiresUtc = null;
        user.FailedLoginCount = 0;
        user.LockoutUntilUtc = null;
        user.EmailConfirmed = true;   // Zugriff auf das Postfach ist damit belegt
        await _db.SaveChangesAsync();

        await RevokeAllSessionsAsync(user.Id);   // Sicherheit: alte Sessions entwerten
        return new AuthResult(true);
    }

    /// <summary>Entfernt alle Sessions eines Nutzers (z.B. nach Passwortänderung/-reset).</summary>
    public async Task RevokeAllSessionsAsync(long userId)
    {
        var sessions = await _db.UserSessions.Where(s => s.UserId == userId).ToListAsync();
        if (sessions.Count == 0) return;
        _db.UserSessions.RemoveRange(sessions);
        await _db.SaveChangesAsync();
    }

    private string BuildLink(string path) => _config.Current.PublicBaseUrl.TrimEnd('/') + path;

    private async Task SendVerificationEmailAsync(User u, Guid token)
    {
        var link = BuildLink($"/Account/ConfirmEmail?token={token}");
        var html = $"<p>Hallo {WebUtility.HtmlEncode(u.DisplayName)},</p>"
            + "<p>bitte bestätige deine E-Mail-Adresse für Matdo:</p>"
            + $"<p><a href=\"{link}\">{link}</a></p>";
        await SendMailOrLogAsync(u.Email, u.DisplayName, "E-Mail bestätigen · Matdo", html, link);
    }

    private async Task SendMailOrLogAsync(string to, string name, string subject, string html, string link)
    {
        var sent = await _email.SendAsync(to, name, subject, html);
        // SMTP-los (Dev/Self-Host ohne Mail): Link protokollieren, damit der Ablauf trotzdem nutzbar ist.
        if (!sent) _logger.LogWarning("E-Mail an {To} nicht versendet (SMTP aus/fehlgeschlagen). Link: {Link}", to, link);
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
