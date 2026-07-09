using System.ComponentModel.DataAnnotations;
using Matdo.Web.Data;
using Matdo.Web.Data.Entities;
using Matdo.Web.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace Matdo.Web.Pages.Account;

public class SettingsModel : PageModel
{
    private readonly MatdoDbContext _db;
    private readonly ICurrentUserAccessor _me;
    private readonly JsonConfigService _config;

    public SettingsModel(MatdoDbContext db, ICurrentUserAccessor me, JsonConfigService config)
    {
        _db = db;
        _me = me;
        _config = config;
    }

    [BindProperty] public ProfileInput Profile { get; set; } = new();
    [BindProperty] public PasswordInput Password { get; set; } = new();

    public string? Message { get; set; }
    public string? Error { get; set; }
    public string PushPublicKey => _config.Current.Push.PublicKey;
    public bool PushEnabled => _config.Current.Push.Enabled;
    public string Email { get; set; } = "";

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

    public async Task<IActionResult> OnGetAsync()
    {
        var user = await LoadUserAsync();
        if (user is null) return Redirect("/Account/Login");
        Profile.DisplayName = user.DisplayName;
        Profile.TimeZone = user.TimeZone;
        Email = user.Email;
        return Page();
    }

    public async Task<IActionResult> OnPostProfileAsync()
    {
        var user = await LoadUserAsync();
        if (user is null) return Redirect("/Account/Login");
        Email = user.Email;

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
        Email = user.Email;
        Profile.DisplayName = user.DisplayName;
        Profile.TimeZone = user.TimeZone;

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
}
