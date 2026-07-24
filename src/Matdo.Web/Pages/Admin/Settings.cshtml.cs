using Matdo.Web.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using WebPush;

namespace Matdo.Web.Pages.Admin;

public class AdminSettingsModel : PageModel
{
    private readonly JsonConfigService _config;
    public AdminSettingsModel(JsonConfigService config) => _config = config;

    [BindProperty] public AppConfig Config { get; set; } = new();
    public string? Message { get; set; }

    public void OnGet() => Config = _config.Current;

    public IActionResult OnPost()
    {
        // Geheimnis-Felder sind Passwort-Inputs und rendern nie ihren Wert -> ein leeres
        // Feld bedeutet „unverändert lassen", NICHT „löschen" (sonst würden SMTP-Passwort,
        // OAuth-Secrets und der VAPID-Privatschlüssel bei jedem Speichern verschwinden).
        var cur = _config.Current;
        if (string.IsNullOrEmpty(Config.Smtp.Password)) Config.Smtp.Password = cur.Smtp.Password;
        if (string.IsNullOrEmpty(Config.Push.PrivateKey)) Config.Push.PrivateKey = cur.Push.PrivateKey;
        if (string.IsNullOrEmpty(Config.Google.ClientSecret)) Config.Google.ClientSecret = cur.Google.ClientSecret;
        if (string.IsNullOrEmpty(Config.Microsoft.ClientSecret)) Config.Microsoft.ClientSecret = cur.Microsoft.ClientSecret;
        _config.Save(Config);
        Message = "Einstellungen gespeichert.";
        return Page();
    }

    public IActionResult OnPostGenerateVapid()
    {
        Config = _config.Current;
        var keys = VapidHelper.GenerateVapidKeys();
        Config.Push.PublicKey = keys.PublicKey;
        Config.Push.PrivateKey = keys.PrivateKey;
        _config.Save(Config);
        Message = "Neue VAPID-Schlüssel erzeugt und gespeichert.";
        return Page();
    }
}
