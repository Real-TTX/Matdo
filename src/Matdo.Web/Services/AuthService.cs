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

    // Fester Dummy-Hash: gleicht die Antwortzeit bei nicht existierenden/gesperrten Konten an
    // die echte BCrypt-Prüfung an, damit über Timing nicht auf Konto-Existenz geschlossen werden kann.
    private static readonly string DummyHash = BCrypt.Net.BCrypt.HashPassword("timing-equalizer");

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

    /// <summary><paramref name="Authenticated"/> = es wurde eine Session erzeugt (nur bei der
    /// Erst-Einrichtung des ersten Benutzers). Bei der normalen Registrierung ist die Antwort
    /// bewusst neutral (kein Auto-Login), damit sie nicht verrät, ob das Konto schon existiert.</summary>
    public record AuthResult(bool Success, string? Error = null, bool Authenticated = false);

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

    /// <summary>
    /// Registrierung. Der allererste Benutzer (Ersteinrichtung) wird Admin und direkt eingeloggt.
    /// Alle weiteren Registrierungen antworten <b>immer neutral</b> (kein Auto-Login, keine
    /// Fehlermeldung „Konto existiert bereits"), damit von außen nicht erkennbar ist, ob eine
    /// Adresse bereits ein Konto hat (keine Konto-Enumeration):
    ///  - neue Adresse        → unbestätigtes Konto anlegen + Bestätigungslink mailen
    ///  - bestehende Adresse  → Hinweis-Mail „Konto existiert bereits" an den Inhaber
    ///  - nicht registrierbar → nichts tun (invite-only: verrät keinen Einladungsstatus)
    /// In allen drei Fällen ist die Antwort identisch. Auch das Timing wird angeglichen:
    /// der (teure) Passwort-Hash wird immer berechnet und E-Mails werden ohne await verschickt.
    /// </summary>
    public async Task<AuthResult> RegisterAsync(string email, string password, string displayName)
    {
        email = email.Trim().ToLowerInvariant();
        displayName = string.IsNullOrWhiteSpace(displayName) ? email.Split('@')[0] : displayName.Trim();

        // Immer hashen (auch wenn kein Konto angelegt wird): angeglichene Antwortzeit
        // unabhängig davon, ob die Adresse existiert oder registrierbar ist (kein Timing-Orakel).
        var passwordHash = BCrypt.Net.BCrypt.HashPassword(password);

        User? firstUser = null;
        User? newUser = null;
        string? existingName = null;

        // Registrierung serialisieren (Postgres-Advisory-Lock), damit bei gleichzeitiger
        // Erst-Registrierung auf leerer DB nicht zwei Benutzer parallel Admin werden (TOCTOU)
        // und keine doppelten Konten entstehen.
        await using (var tx = await _db.Database.BeginTransactionAsync())
        {
            await _db.Database.ExecuteSqlRawAsync("SELECT pg_advisory_xact_lock(4444777)");

            var isFirst = !await _db.Users.AnyAsync();
            if (isFirst)
            {
                var role = await _db.Roles.FirstAsync(r => r.Name == Role.Admin);
                firstUser = new User
                {
                    Email = email,
                    DisplayName = displayName,
                    PasswordHash = passwordHash,
                    RoleId = role.Id,
                    IsActive = true,
                    EmailConfirmed = true,   // Ersteinrichtung gilt sofort als bestätigt
                    EmailConfirmToken = null
                };
                _db.Users.Add(firstUser);
                await _db.SaveChangesAsync();
            }
            else if (await CanRegisterAsync(email))
            {
                var existing = await _db.Users.FirstOrDefaultAsync(u => u.Email == email);
                if (existing is null)
                {
                    var role = await _db.Roles.FirstAsync(r => r.Name == Role.User);
                    newUser = new User
                    {
                        Email = email,
                        DisplayName = displayName,
                        PasswordHash = passwordHash,
                        RoleId = role.Id,
                        IsActive = true,
                        EmailConfirmed = false,       // muss per Link bestätigt werden
                        EmailConfirmToken = Guid.NewGuid()
                    };
                    _db.Users.Add(newUser);
                    await _db.SaveChangesAsync();

                    // Einladungen werden NICHT automatisch übernommen – der neue Nutzer nimmt
                    // sie bewusst unter „Einladungen" an (Zustimmung).
                }
                else
                {
                    // Konto besteht bereits → nur den Inhaber per Mail informieren (unten).
                    existingName = existing.DisplayName;
                }
            }
            // else: invite-only und nicht eingeladen → nichts anlegen, aber neutral antworten.

            await tx.CommitAsync();
        }

        // --- Ersteinrichtung: ersten Benutzer direkt einloggen. ---
        if (firstUser is not null)
        {
            await CreateSessionAsync(firstUser);
            return new AuthResult(true, Authenticated: true);
        }

        // --- Alle weiteren: neutrale Antwort, Mails ohne await (Timing unabhängig vom SMTP). ---
        if (newUser is not null)
            _ = SendVerificationEmailAsync(newUser, newUser.EmailConfirmToken!.Value);
        else if (existingName is not null)
            _ = SendAlreadyRegisteredEmailAsync(email, existingName);

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

        if (user is null || !user.IsActive)
        {
            BCrypt.Net.BCrypt.Verify(password, DummyHash);   // Timing angleichen
            return new AuthResult(false, invalid);
        }
        if (user.LockoutUntilUtc is DateTime until && until > now)
        {
            BCrypt.Net.BCrypt.Verify(password, DummyHash);
            return new AuthResult(false, invalid);
        }

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
        var now = DateTime.UtcNow;
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Email == email && u.IsActive);
        if (user is null) return;   // neutral: kein Hinweis auf Existenz

        // Cooldown: höchstens alle 2 Minuten eine Reset-Mail je Konto (kein Mail-Bombing).
        var issuedAt = user.PasswordResetExpiresUtc?.AddHours(-ResetValidHours);
        if (issuedAt is DateTime t && t > now.AddMinutes(-2)) return;

        user.PasswordResetToken = Guid.NewGuid();
        user.PasswordResetExpiresUtc = now.AddHours(ResetValidHours);
        await _db.SaveChangesAsync();

        var link = BuildLink($"/Account/ResetPassword?token={user.PasswordResetToken}");
        var html = $"<p>Hallo {WebUtility.HtmlEncode(user.DisplayName)},</p>"
            + "<p>zum Zurücksetzen deines Matdo-Passworts klicke auf den folgenden Link "
            + $"(gültig {ResetValidHours} Stunden):</p><p><a href=\"{link}\">{link}</a></p>"
            + "<p>Wenn du das nicht angefordert hast, ignoriere diese E-Mail einfach.</p>";
        // Nicht awaiten: Antwortzeit unabhängig vom (langsamen) SMTP-Versand halten -> kein Timing-Orakel.
        _ = SendMailOrLogAsync(user.Email, user.DisplayName, "Passwort zurücksetzen · Matdo", html, link);
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

    /// <summary>Informiert den Inhaber einer bereits registrierten Adresse über den
    /// Registrierungs-Versuch – so ist der „Konto existiert bereits"-Fall von außen nicht
    /// von einer echten Neuregistrierung unterscheidbar (keine Enumeration).</summary>
    private async Task SendAlreadyRegisteredEmailAsync(string email, string displayName)
    {
        var loginLink = BuildLink("/Account/Login");
        var resetLink = BuildLink("/Account/ForgotPassword");
        var html = $"<p>Hallo {WebUtility.HtmlEncode(displayName)},</p>"
            + "<p>für diese E-Mail-Adresse besteht bereits ein Matdo-Konto. Soeben wurde versucht, "
            + "damit ein neues Konto zu registrieren.</p>"
            + $"<p>Warst du das? Dann melde dich einfach an: <a href=\"{loginLink}\">{loginLink}</a>. "
            + $"Passwort vergessen? <a href=\"{resetLink}\">{resetLink}</a></p>"
            + "<p>Andernfalls kannst du diese E-Mail ignorieren – es wurde kein neues Konto angelegt "
            + "und dein bestehendes Konto ist unverändert.</p>";
        await SendMailOrLogAsync(email, displayName, "Konto besteht bereits · Matdo", html, loginLink);
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
