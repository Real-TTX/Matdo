using System.ComponentModel.DataAnnotations;
using Matdo.Web.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.RateLimiting;

namespace Matdo.Web.Pages.Account;

[EnableRateLimiting("auth")]
public class RegisterModel : PageModel
{
    private readonly AuthService _auth;
    private readonly JsonConfigService _config;
    public RegisterModel(AuthService auth, JsonConfigService config)
    {
        _auth = auth;
        _config = config;
    }

    [BindProperty] public InputModel Input { get; set; } = new();
    public string? Error { get; set; }

    /// <summary>Offene Registrierung ist aus – Zugang nur über eine Einladung möglich.</summary>
    public bool InviteOnly => !_config.Current.AllowRegistration;

    public class InputModel
    {
        [Required(ErrorMessage = "Bitte Namen angeben.")]
        public string DisplayName { get; set; } = string.Empty;

        [Required(ErrorMessage = "Bitte E-Mail-Adresse angeben.")]
        [EmailAddress(ErrorMessage = "Ungültige E-Mail-Adresse.")]
        public string Email { get; set; } = string.Empty;

        [Required(ErrorMessage = "Bitte Passwort angeben.")]
        [MinLength(8, ErrorMessage = "Das Passwort muss mindestens 8 Zeichen haben.")]
        public string Password { get; set; } = string.Empty;

        [Compare(nameof(Password), ErrorMessage = "Die Passwörter stimmen nicht überein.")]
        public string PasswordConfirm { get; set; } = string.Empty;
    }

    public async Task<IActionResult> OnGetAsync()
    {
        if (User.Identity?.IsAuthenticated == true)
            return Redirect("/");
        // Vor der Ersteinrichtung zur Setup-Seite (erster Admin) leiten.
        if (!await _auth.AnyUsersAsync())
            return Redirect("/Account/Setup");
        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid) return Page();

        var result = await _auth.RegisterAsync(Input.Email, Input.Password, Input.DisplayName);
        if (!result.Success)
        {
            Error = result.Error;
            return Page();
        }

        return Redirect("/");
    }
}
