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
