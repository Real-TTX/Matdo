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

    public AuthService(MatdoDbContext db, IHttpContextAccessor http)
    {
        _db = db;
        _http = http;
    }

    public record AuthResult(bool Success, string? Error = null);

    public async Task<AuthResult> RegisterAsync(string email, string password, string displayName)
    {
        email = email.Trim().ToLowerInvariant();
        if (await _db.Users.AnyAsync(u => u.Email == email))
            return new AuthResult(false, "Es existiert bereits ein Konto mit dieser E-Mail-Adresse.");

        // Erster Benutzer wird automatisch Admin, alle weiteren normale User.
        var isFirst = !await _db.Users.AnyAsync();
        var roleName = isFirst ? Role.Admin : Role.User;
        var role = await _db.Roles.FirstAsync(r => r.Name == roleName);

        var user = new User
        {
            Email = email,
            DisplayName = string.IsNullOrWhiteSpace(displayName) ? email.Split('@')[0] : displayName.Trim(),
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(password),
            RoleId = role.Id,
            IsActive = true
        };
        _db.Users.Add(user);
        await _db.SaveChangesAsync();

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
    }
}
