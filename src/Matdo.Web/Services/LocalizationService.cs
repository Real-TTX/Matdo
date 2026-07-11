using System.Collections.Concurrent;
using System.Text.Json;

namespace Matdo.Web.Services;

/// <summary>Beschreibt eine verfügbare Sprache (aus einem Sprachpaket).</summary>
public record LanguageInfo(string Code, string Name);

/// <summary>
/// Lädt Sprachpakete (JSON) und liefert Übersetzungen. Pakete liegen als
/// &lt;code&gt;.json im Ordner "Localization" der App und können zusätzlich über
/// ein gemountetes Verzeichnis (Matdo:LangDir bzw. /data/lang) ergänzt/überschrieben werden.
/// Damit sind Sprachpakete auch nachträglich ohne Neu-Build hinzufügbar.
/// </summary>
public class LocalizationService
{
    public const string FallbackLanguage = "de";

    private readonly ILogger<LocalizationService> _logger;
    private readonly ConcurrentDictionary<string, Dictionary<string, string>> _packs = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<LanguageInfo> _languages = new();

    public LocalizationService(IWebHostEnvironment env, IConfiguration config, ILogger<LocalizationService> logger)
    {
        _logger = logger;

        var baseDir = Path.Combine(env.ContentRootPath, "Localization");
        var overrideDir = config["Matdo:LangDir"];
        if (string.IsNullOrWhiteSpace(overrideDir))
            overrideDir = Path.Combine(env.ContentRootPath, "data", "lang");

        LoadFrom(baseDir);
        LoadFrom(overrideDir); // Overrides gewinnen

        // Anzeigenamen ermitteln (aus "_name", sonst Code groß)
        foreach (var code in _packs.Keys.OrderBy(c => c))
        {
            var name = _packs[code].TryGetValue("_name", out var n) && !string.IsNullOrWhiteSpace(n)
                ? n : code.ToUpperInvariant();
            _languages.Add(new LanguageInfo(code, name));
        }

        if (_languages.Count == 0)
            _logger.LogWarning("Keine Sprachpakete gefunden (Ordner: {Base}).", baseDir);
    }

    private void LoadFrom(string dir)
    {
        if (!Directory.Exists(dir)) return;
        foreach (var file in Directory.EnumerateFiles(dir, "*.json"))
        {
            var code = Path.GetFileNameWithoutExtension(file).ToLowerInvariant();
            try
            {
                var dict = JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(file))
                           ?? new Dictionary<string, string>();
                var target = _packs.GetOrAdd(code, _ => new Dictionary<string, string>(StringComparer.Ordinal));
                foreach (var kv in dict) target[kv.Key] = kv.Value;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Sprachpaket {File} konnte nicht geladen werden.", file);
            }
        }
    }

    public IReadOnlyList<LanguageInfo> Languages => _languages;

    public string DefaultLanguage =>
        _languages.Any(l => l.Code == FallbackLanguage) ? FallbackLanguage
        : (_languages.FirstOrDefault()?.Code ?? FallbackLanguage);

    public bool IsSupported(string? code) =>
        code is not null && _packs.ContainsKey(code);

    /// <summary>Übersetzung für den Schlüssel; Fallback: Standardsprache, dann der Schlüssel selbst.</summary>
    public string Get(string language, string key)
    {
        if (_packs.TryGetValue(language, out var pack) && pack.TryGetValue(key, out var v))
            return v;
        if (language != FallbackLanguage && _packs.TryGetValue(FallbackLanguage, out var fb) && fb.TryGetValue(key, out var fv))
            return fv;
        return key;
    }
}
