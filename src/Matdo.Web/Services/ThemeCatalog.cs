namespace Matdo.Web.Services;

/// <summary>Ein auswählbares Akzent-Design.</summary>
public record ThemeOption(string Key, string NameDe, string NameEn, string Accent, bool Special);

/// <summary>Katalog aller verfügbaren Farbschema-Modi und Akzent-Designs.</summary>
public static class ThemeCatalog
{
    /// <summary>Farbschema-Modi (Regular): System folgt dem Betriebssystem.</summary>
    public static readonly string[] Modes = { "system", "light", "dark" };

    /// <summary>
    /// Akzent-Designs. "Standard" (Special=false) zuerst – Lila (Vorgabe) und das
    /// ursprüngliche Rot; danach die "Speziellen Designs" (Special=true).
    /// </summary>
    public static readonly IReadOnlyList<ThemeOption> Themes = new List<ThemeOption>
    {
        new("purple",   "Lila",        "Purple",     "#8300bc", false),
        new("red",      "Rot",         "Red",        "#dc4c3e", false),
        new("ocean",    "Ozean",       "Ocean",      "#1a73e8", true),
        new("forest",   "Wald",        "Forest",     "#1e8e3e", true),
        new("sunset",   "Sonnenuntergang", "Sunset", "#e8590c", true),
        new("berry",    "Beere",       "Berry",      "#c2185b", true),
        new("teal",     "Türkis",      "Teal",       "#00897b", true),
        new("graphite", "Graphit",     "Graphite",   "#546e7a", true),
        new("gold",     "Gold",        "Gold",       "#b8860b", true),
        new("indigo",   "Indigo",      "Indigo",     "#3f51b5", true),
    };

    public const string DefaultMode = "system";
    public const string DefaultTheme = "purple";

    public static bool IsValidMode(string? m) => m is not null && Modes.Contains(m);
    public static bool IsValidTheme(string? t) => t is not null && Themes.Any(x => x.Key == t);
}
