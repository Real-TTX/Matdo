using System.Security.Claims;
using Microsoft.AspNetCore.Http;

namespace Matdo.Web.Services;

/// <summary>Claim-Typen die vom Session-Auth-Handler gesetzt werden.</summary>
public static class MatdoClaims
{
    public const string UserId = "matdo:uid";
    public const string SessionToken = "matdo:sid";
    public const string DisplayName = "matdo:name";
}

/// <summary>Zugriff auf den aktuell angemeldeten Benutzer (aus dem HTTP-Kontext).</summary>
public interface ICurrentUserAccessor
{
    long? UserId { get; }
    string? Email { get; }
    string? DisplayName { get; }
    string? Role { get; }
    bool IsAuthenticated { get; }
    bool IsAdmin { get; }
}

public class CurrentUserAccessor : ICurrentUserAccessor
{
    private readonly IHttpContextAccessor _http;

    public CurrentUserAccessor(IHttpContextAccessor http) => _http = http;

    private ClaimsPrincipal? Principal => _http.HttpContext?.User;

    public bool IsAuthenticated => Principal?.Identity?.IsAuthenticated ?? false;

    public long? UserId
    {
        get
        {
            var raw = Principal?.FindFirstValue(MatdoClaims.UserId);
            return long.TryParse(raw, out var id) ? id : null;
        }
    }

    public string? Email => Principal?.FindFirstValue(ClaimTypes.Email);
    public string? DisplayName => Principal?.FindFirstValue(MatdoClaims.DisplayName);
    public string? Role => Principal?.FindFirstValue(ClaimTypes.Role);
    public bool IsAdmin => string.Equals(Role, Entities_Role_Admin, StringComparison.OrdinalIgnoreCase);

    private const string Entities_Role_Admin = "Admin";
}
