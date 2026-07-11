using Matdo.Web.Data.Entities;
using Matdo.Web.Services;
using Matdo.Web.Services.Calendar;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Matdo.Web.Controllers;

/// <summary>OAuth-Flow (Redirect) zum Verbinden von Google-/Microsoft-Kalendern.</summary>
[Authorize]
[Route("calendar")]
public class CalendarOAuthController : Controller
{
    private const string StateCookie = "matdo_cal_oauth";

    private readonly CalendarService _calendar;
    private readonly ICurrentUserAccessor _me;

    public CalendarOAuthController(CalendarService calendar, ICurrentUserAccessor me)
    {
        _calendar = calendar;
        _me = me;
    }

    [HttpGet("connect/{provider}")]
    public IActionResult Connect(string provider)
    {
        if (!TryParse(provider, out var p)) return Redirect("/Account/Settings?tab=calendar&error=provider");
        var prov = _calendar.ProviderFor(p);
        if (prov is null || !prov.IsConfigured) return Redirect("/Account/Settings?tab=calendar&error=notconfigured");

        var state = Guid.NewGuid().ToString("N");
        Response.Cookies.Append(StateCookie, $"{p}:{state}", new CookieOptions
        {
            HttpOnly = true,
            IsEssential = true,
            SameSite = SameSiteMode.Lax,
            Secure = Request.IsHttps,
            Expires = DateTimeOffset.UtcNow.AddMinutes(10),
            Path = "/"
        });

        var url = prov.BuildAuthUrl(_calendar.BuildRedirectUri(p), state);
        return Redirect(url);
    }

    [HttpGet("callback/{provider}")]
    public async Task<IActionResult> Callback(string provider, string? code, string? state, string? error)
    {
        if (!string.IsNullOrEmpty(error) || string.IsNullOrEmpty(code) || string.IsNullOrEmpty(state))
            return Redirect("/Account/Settings?tab=calendar&error=denied");
        if (!TryParse(provider, out var p)) return Redirect("/Account/Settings?tab=calendar&error=provider");

        // State prüfen (CSRF).
        if (!Request.Cookies.TryGetValue(StateCookie, out var raw) || raw != $"{p}:{state}")
            return Redirect("/Account/Settings?tab=calendar&error=state");
        Response.Cookies.Delete(StateCookie);

        var prov = _calendar.ProviderFor(p);
        if (prov is null || !prov.IsConfigured) return Redirect("/Account/Settings?tab=calendar&error=notconfigured");

        var tokens = await prov.ExchangeCodeAsync(code, _calendar.BuildRedirectUri(p), HttpContext.RequestAborted);
        if (tokens is null) return Redirect("/Account/Settings?tab=calendar&error=token");

        var name = p == CalendarProvider.Google ? "Google-Kalender" : "Microsoft-Kalender";
        await _calendar.StoreOAuthConnectionAsync(_me.UserId!.Value, p, tokens, name);
        return Redirect("/Account/Settings?tab=calendar&connected=1");
    }

    private static bool TryParse(string provider, out CalendarProvider p)
    {
        switch (provider?.ToLowerInvariant())
        {
            case "google": p = CalendarProvider.Google; return true;
            case "microsoft": p = CalendarProvider.Microsoft; return true;
            default: p = CalendarProvider.Ics; return false;
        }
    }
}
