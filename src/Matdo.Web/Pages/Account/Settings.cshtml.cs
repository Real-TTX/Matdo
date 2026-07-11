using System.ComponentModel.DataAnnotations;
using System.Text;
using Matdo.Web.Data;
using Matdo.Web.Data.Entities;
using Matdo.Web.Services;
using Matdo.Web.Services.Calendar;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace Matdo.Web.Pages.Account;

public class SettingsModel : PageModel
{
    private readonly MatdoDbContext _db;
    private readonly ICurrentUserAccessor _me;
    private readonly JsonConfigService _config;
    private readonly CalendarService _calendar;
    private readonly TodoistImportService _import;

    public SettingsModel(MatdoDbContext db, ICurrentUserAccessor me, JsonConfigService config, CalendarService calendar, TodoistImportService import)
    {
        _db = db;
        _me = me;
        _config = config;
        _calendar = calendar;
        _import = import;
    }

    [BindProperty] public ProfileInput Profile { get; set; } = new();
    [BindProperty] public PasswordInput Password { get; set; } = new();

    public string? Message { get; set; }
    public string? Error { get; set; }
    public string PushPublicKey => _config.Current.Push.PublicKey;
    public bool PushEnabled => _config.Current.Push.Enabled;
    public string Email { get; set; } = "";
    public string ActiveTab { get; set; } = "appearance";

    // Import
    public string? ImportResult { get; set; }
    public List<string> ImportWarnings { get; set; } = new();

    // Kalender
    public List<CalendarConnection> Connections { get; set; } = new();
    public bool GoogleConfigured { get; set; }
    public bool MicrosoftConfigured { get; set; }
    public string? FeedUrl { get; set; }

    public static readonly string[] CalPalette =
        { "#246fe0", "#8300bc", "#dc4c3e", "#1e8e3e", "#e8590c", "#00897b", "#c2185b" };

    public class ProfileInput
    {
        [Required(ErrorMessage = "Bitte Namen angeben.")]
        public string DisplayName { get; set; } = "";
        public string? TimeZone { get; set; }
    }

    public class PasswordInput
    {
        public string? Current { get; set; }
        [MinLength(6, ErrorMessage = "Mindestens 6 Zeichen.")]
        public string? New { get; set; }
        [Compare(nameof(New), ErrorMessage = "Passwörter stimmen nicht überein.")]
        public string? Confirm { get; set; }
    }

    private Task<User?> LoadUserAsync() =>
        _db.Users.FirstOrDefaultAsync(u => u.Id == _me.UserId);

    /// <summary>Lädt die zur Anzeige nötigen Daten (E-Mail + Kalender-Verbindungen).</summary>
    private async Task PopulateAsync(User user)
    {
        Email = user.Email;
        Connections = await _calendar.GetConnectionsAsync();
        GoogleConfigured = _calendar.ProviderFor(CalendarProvider.Google)?.IsConfigured ?? false;
        MicrosoftConfigured = _calendar.ProviderFor(CalendarProvider.Microsoft)?.IsConfigured ?? false;
        if (user.IcalToken is Guid tok)
            FeedUrl = _config.Current.PublicBaseUrl.TrimEnd('/') + "/feed/" + tok.ToString("N") + ".ics";
    }

    public async Task<IActionResult> OnGetAsync(string? tab)
    {
        var user = await LoadUserAsync();
        if (user is null) return Redirect("/Account/Login");
        Profile.DisplayName = user.DisplayName;
        Profile.TimeZone = user.TimeZone;
        await PopulateAsync(user);
        if (!string.IsNullOrEmpty(tab)) ActiveTab = tab;
        return Page();
    }

    public async Task<IActionResult> OnPostProfileAsync()
    {
        var user = await LoadUserAsync();
        if (user is null) return Redirect("/Account/Login");
        await PopulateAsync(user);
        ActiveTab = "profile";

        if (!ModelState.IsValid) return Page();
        user.DisplayName = Profile.DisplayName.Trim();
        user.TimeZone = string.IsNullOrWhiteSpace(Profile.TimeZone) ? null : Profile.TimeZone.Trim();
        await _db.SaveChangesAsync();
        Message = "Profil gespeichert.";
        return Page();
    }

    public async Task<IActionResult> OnPostPasswordAsync()
    {
        var user = await LoadUserAsync();
        if (user is null) return Redirect("/Account/Login");
        await PopulateAsync(user);
        Profile.DisplayName = user.DisplayName;
        Profile.TimeZone = user.TimeZone;
        ActiveTab = "password";

        if (string.IsNullOrWhiteSpace(Password.New))
        {
            Error = "Bitte ein neues Passwort angeben.";
            return Page();
        }
        if (!BCrypt.Net.BCrypt.Verify(Password.Current ?? "", user.PasswordHash))
        {
            Error = "Das aktuelle Passwort ist falsch.";
            return Page();
        }
        if (Password.New != Password.Confirm)
        {
            Error = "Die neuen Passwörter stimmen nicht überein.";
            return Page();
        }

        user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(Password.New);
        await _db.SaveChangesAsync();
        Message = "Passwort geändert.";
        return Page();
    }

    // ----- Kalender-Verbindungen -----

    private IActionResult BackToCalendar() => RedirectToPage(new { tab = "calendar" });

    public async Task<IActionResult> OnPostAddIcsAsync(string icsUrl, string displayName, string color)
    {
        if (!string.IsNullOrWhiteSpace(icsUrl) &&
            (icsUrl.StartsWith("http", StringComparison.OrdinalIgnoreCase) || icsUrl.StartsWith("webcal", StringComparison.OrdinalIgnoreCase)))
        {
            await _calendar.AddIcsAsync(icsUrl, displayName, string.IsNullOrWhiteSpace(color) ? "#246fe0" : color);
            var latest = (await _calendar.GetConnectionsAsync()).LastOrDefault();
            if (latest is not null) await _calendar.SyncNowAsync(latest.Id);
        }
        return BackToCalendar();
    }

    public async Task<IActionResult> OnPostCalToggleAsync(long id, bool isEnabled, bool exportTasks)
    {
        await _calendar.SetOptionsAsync(id, isEnabled, exportTasks);
        return BackToCalendar();
    }

    public async Task<IActionResult> OnPostCalSyncAsync(long id)
    {
        await _calendar.SyncNowAsync(id);
        return BackToCalendar();
    }

    public async Task<IActionResult> OnPostCalDisconnectAsync(long id)
    {
        await _calendar.DisconnectAsync(id);
        return BackToCalendar();
    }

    public async Task<IActionResult> OnPostFeedAsync()
    {
        var user = await LoadUserAsync();
        if (user is not null) { user.IcalToken = Guid.NewGuid(); await _db.SaveChangesAsync(); }
        return BackToCalendar();
    }

    public async Task<IActionResult> OnPostFeedDisableAsync()
    {
        var user = await LoadUserAsync();
        if (user is not null && user.IcalToken != null) { user.IcalToken = null; await _db.SaveChangesAsync(); }
        return BackToCalendar();
    }

    // ----- Import (Todoist-CSV) -----

    public async Task<IActionResult> OnPostImportAsync(IFormFile? file, string? projectName)
    {
        var user = await LoadUserAsync();
        if (user is null) return Redirect("/Account/Login");
        await PopulateAsync(user);
        Profile.DisplayName = user.DisplayName;
        Profile.TimeZone = user.TimeZone;
        ActiveTab = "import";

        if (file is null || file.Length == 0)
        {
            Error = "Bitte eine CSV-Datei auswählen.";
            return Page();
        }
        if (file.Length > 5 * 1024 * 1024)
        {
            Error = "Die Datei ist zu groß (max. 5 MB).";
            return Page();
        }

        string content;
        using (var reader = new StreamReader(file.OpenReadStream(), Encoding.UTF8, detectEncodingFromByteOrderMarks: true))
            content = await reader.ReadToEndAsync();

        try
        {
            var result = await _import.ImportTodoistCsvAsync(file.FileName, content, projectName);
            if (!result.Ok)
            {
                Error = result.Warnings.FirstOrDefault() ?? "Import fehlgeschlagen.";
                return Page();
            }
            var sectionPart = result.Sections > 0 ? $", {result.Sections} Spalten" : "";
            ImportResult = $"„{result.ProjectName}“ importiert: {result.Tasks} Aufgaben{sectionPart}.";
            ImportWarnings = result.Warnings;
        }
        catch (Exception)
        {
            // Keine internen Details nach außen geben.
            Error = "Import fehlgeschlagen. Bitte prüfe, ob es sich um einen gültigen Todoist-CSV-Export handelt.";
        }
        return Page();
    }
}
