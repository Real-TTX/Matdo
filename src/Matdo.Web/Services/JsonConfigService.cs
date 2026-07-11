using System.Text.Json;

namespace Matdo.Web.Services;

/// <summary>
/// Anwendungs-Konfiguration die als JSON im gemounteten Daten-Volume liegt
/// (z.B. SMTP-Einstellungen, Web-Push VAPID-Keys). Primäre Verwendung von JSON: Configs.
/// </summary>
public class AppConfig
{
    public SmtpConfig Smtp { get; set; } = new();
    public PushConfig Push { get; set; } = new();
    public OAuthConfig Google { get; set; } = new();
    public OAuthConfig Microsoft { get; set; } = new();
    public string AppName { get; set; } = "Matdo";
    public string PublicBaseUrl { get; set; } = "http://localhost:6006";

    /// <summary>Offene Selbst-Registrierung. Ist sie aus, verschwindet der „Registrieren"-Button
    /// und der Endpunkt lehnt neue Registrierungen ab (bestehende Einladungen greifen weiterhin).</summary>
    public bool AllowRegistration { get; set; } = true;

    /// <summary>OAuth-Client-Zugangsdaten für die Kalender-Anbindung (Google/Microsoft).</summary>
    public class OAuthConfig
    {
        public bool Enabled { get; set; }
        public string ClientId { get; set; } = "";
        public string ClientSecret { get; set; } = "";
    }

    public class SmtpConfig
    {
        public bool Enabled { get; set; }
        public string Host { get; set; } = "";
        public int Port { get; set; } = 587;
        public bool UseStartTls { get; set; } = true;
        public string User { get; set; } = "";
        public string Password { get; set; } = "";
        public string FromAddress { get; set; } = "matdo@localhost";
        public string FromName { get; set; } = "Matdo";
    }

    public class PushConfig
    {
        public bool Enabled { get; set; }
        public string PublicKey { get; set; } = "";
        public string PrivateKey { get; set; } = "";
        public string Subject { get; set; } = "mailto:admin@localhost";
    }
}

/// <summary>Lädt/speichert <see cref="AppConfig"/> als JSON-Datei im Daten-Volume.</summary>
public class JsonConfigService
{
    private readonly string _path;
    private readonly object _lock = new();
    private AppConfig _cache;

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    public JsonConfigService(IConfiguration configuration, IWebHostEnvironment env)
    {
        var configured = configuration["Matdo:ConfigDir"];
        var dir = string.IsNullOrWhiteSpace(configured)
            ? Path.Combine(env.ContentRootPath, "data", "config")
            : configured;
        Directory.CreateDirectory(dir);
        _path = Path.Combine(dir, "appconfig.json");
        _cache = Load();
    }

    public AppConfig Current
    {
        get { lock (_lock) return _cache; }
    }

    public void Save(AppConfig config)
    {
        lock (_lock)
        {
            File.WriteAllText(_path, JsonSerializer.Serialize(config, JsonOpts));
            _cache = config;
        }
    }

    private AppConfig Load()
    {
        try
        {
            if (File.Exists(_path))
                return JsonSerializer.Deserialize<AppConfig>(File.ReadAllText(_path), JsonOpts) ?? new AppConfig();
        }
        catch { /* Bei defekter Datei: Standardwerte verwenden. */ }

        var def = new AppConfig();
        try { File.WriteAllText(_path, JsonSerializer.Serialize(def, JsonOpts)); } catch { }
        return def;
    }
}
