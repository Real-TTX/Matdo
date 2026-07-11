namespace Matdo.Web.Services;

/// <summary>
/// Bequemer Zugriff auf Übersetzungen in Views und Seiten für die aktuelle Sprache.
/// Verwendung: <c>@T["nav.today"]</c> oder mit Platzhaltern <c>@T["x.count", n]</c>.
/// </summary>
public class Translator
{
    private readonly LocalizationService _loc;
    private readonly UiPreferences _prefs;

    public Translator(LocalizationService loc, UiPreferences prefs)
    {
        _loc = loc;
        _prefs = prefs;
    }

    public string Language => _prefs.Language;

    public string this[string key] => _loc.Get(_prefs.Language, key);

    public string this[string key, params object[] args]
    {
        get
        {
            var s = _loc.Get(_prefs.Language, key);
            try { return string.Format(s, args); } catch { return s; }
        }
    }
}
