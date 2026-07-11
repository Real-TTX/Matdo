using Microsoft.AspNetCore.Http;

namespace Matdo.Web.Services;

/// <summary>
/// Liest/schreibt die Darstellungs- und Sprach-Einstellungen aus Cookies.
/// Cookies werden serverseitig beim Rendern ausgewertet (kein Flackern) und funktionieren
/// auch auf anonymen Seiten (Login). Für angemeldete Benutzer werden die Werte zusätzlich
/// im Benutzerprofil gespeichert und beim Login in die Cookies übernommen.
/// </summary>
public class UiPreferences
{
    public const string SchemeCookie = "matdo_scheme";
    public const string ThemeCookie = "matdo_theme";
    public const string LangCookie = "matdo_lang";

    private readonly IHttpContextAccessor _http;
    private readonly LocalizationService _loc;

    public UiPreferences(IHttpContextAccessor http, LocalizationService loc)
    {
        _http = http;
        _loc = loc;
    }

    private string? Cookie(string name) => _http.HttpContext?.Request.Cookies[name];

    public string Scheme
    {
        get { var v = Cookie(SchemeCookie); return ThemeCatalog.IsValidMode(v) ? v! : ThemeCatalog.DefaultMode; }
    }

    public string Theme
    {
        get { var v = Cookie(ThemeCookie); return ThemeCatalog.IsValidTheme(v) ? v! : ThemeCatalog.DefaultTheme; }
    }

    public string Language
    {
        get { var v = Cookie(LangCookie); return _loc.IsSupported(v) ? v! : _loc.DefaultLanguage; }
    }

    /// <summary>Setzt die Cookies (langlebig, 1 Jahr). Ungültige Werte werden ignoriert.</summary>
    public void Write(string? scheme, string? theme, string? lang)
    {
        var ctx = _http.HttpContext;
        if (ctx is null) return;

        var opts = new CookieOptions
        {
            Expires = DateTimeOffset.UtcNow.AddYears(1),
            IsEssential = true,
            SameSite = SameSiteMode.Lax,
            Secure = ctx.Request.IsHttps,
            Path = "/"
        };

        if (ThemeCatalog.IsValidMode(scheme)) ctx.Response.Cookies.Append(SchemeCookie, scheme!, opts);
        if (ThemeCatalog.IsValidTheme(theme)) ctx.Response.Cookies.Append(ThemeCookie, theme!, opts);
        if (_loc.IsSupported(lang)) ctx.Response.Cookies.Append(LangCookie, lang!, opts);
    }
}
