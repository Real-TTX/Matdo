using System.ComponentModel.DataAnnotations;
using Matdo.Web.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Matdo.Web.Pages.Account;

/// <summary>Ersteinrichtung: legt beim ersten Start das Administrator-Konto an.</summary>
public class SetupModel : PageModel
{
    private readonly AuthService _auth;
    public SetupModel(AuthService auth) => _auth = auth;

    [BindProperty] public InputModel Input { get; set; } = new();
    public string? Error { get; set; }

    public class InputModel
    {
        public string? DisplayName { get; set; }

        [Required(ErrorMessage = "Bitte E-Mail-Adresse angeben.")]
        [EmailAddress(ErrorMessage = "Ungültige E-Mail-Adresse.")]
        public string Email { get; set; } = string.Empty;

        [Required(ErrorMessage = "Bitte Passwort angeben.")]
        [MinLength(6, ErrorMessage = "Das Passwort muss mindestens 6 Zeichen haben.")]
        public string Password { get; set; } = string.Empty;

        [Compare(nameof(Password), ErrorMessage = "Die Passwörter stimmen nicht überein.")]
        public string PasswordConfirm { get; set; } = string.Empty;
    }

    public async Task<IActionResult> OnGetAsync()
    {
        // Nach der Einrichtung nicht mehr erreichbar.
        if (await _auth.AnyUsersAsync()) return Redirect("/Account/Login");
        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (await _auth.AnyUsersAsync()) return Redirect("/Account/Login");
        if (!ModelState.IsValid) return Page();

        var result = await _auth.RegisterAsync(Input.Email, Input.Password, Input.DisplayName ?? "");
        if (!result.Success)
        {
            Error = result.Error;
            return Page();
        }
        return Redirect("/");   // erster Benutzer wird automatisch Admin + eingeloggt
    }
}
