using System.Security.Claims;
using System.Text.Encodings.Web;
using Matdo.Web.Data;
using Microsoft.AspNetCore.Authentication;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Matdo.Web.Services;

/// <summary>
/// Eigene Authentifizierung auf Basis persistenter Sessions.
/// Der Cookie enthält nur ein opakes Token (UUID); die eigentliche Session liegt
/// in Postgres und übersteht damit Container-Neustarts.
/// </summary>
public class SessionAuthenticationHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    public const string SchemeName = "MatdoSession";
    public const string CookieName = "matdo_session";

    private readonly MatdoDbContext _db;

    public SessionAuthenticationHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder,
        MatdoDbContext db) : base(options, logger, encoder)
    {
        _db = db;
    }

    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Cookies.TryGetValue(CookieName, out var raw) || !Guid.TryParse(raw, out var token))
            return AuthenticateResult.NoResult();

        var session = await _db.UserSessions
            .Include(s => s.User).ThenInclude(u => u!.Role)
            .FirstOrDefaultAsync(s => s.Token == token);

        if (session is null || session.User is null || !session.User.IsActive)
            return AuthenticateResult.NoResult();

        if (session.ExpiresAt < DateTime.UtcNow)
            return AuthenticateResult.Fail("Session abgelaufen.");

        // "Sliding" LastSeen aktualisieren (max. alle 5 Minuten, um Schreiblast zu senken).
        if (session.LastSeenAt < DateTime.UtcNow.AddMinutes(-5))
        {
            session.LastSeenAt = DateTime.UtcNow;
            await _db.SaveChangesAsync();
        }

        var u = session.User;
        var claims = new List<Claim>
        {
            new(MatdoClaims.UserId, u.Id.ToString()),
            new(MatdoClaims.SessionToken, session.Token.ToString()),
            new(MatdoClaims.DisplayName, u.DisplayName),
            new(ClaimTypes.Email, u.Email),
            new(ClaimTypes.Name, u.Email),
            new(ClaimTypes.Role, u.Role?.Name ?? "User")
        };

        var identity = new ClaimsIdentity(claims, SchemeName);
        var ticket = new AuthenticationTicket(new ClaimsPrincipal(identity), SchemeName);
        return AuthenticateResult.Success(ticket);
    }

    protected override Task HandleChallengeAsync(AuthenticationProperties properties)
    {
        var returnUrl = Uri.EscapeDataString(Request.Path + Request.QueryString);
        Response.Redirect($"/Account/Login?returnUrl={returnUrl}");
        return Task.CompletedTask;
    }

    protected override Task HandleForbiddenAsync(AuthenticationProperties properties)
    {
        Response.Redirect("/Account/AccessDenied");
        return Task.CompletedTask;
    }
}
