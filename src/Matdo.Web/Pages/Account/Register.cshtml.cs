using System.ComponentModel.DataAnnotations;
using Matdo.Web.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Matdo.Web.Pages.Account;

public class RegisterModel : PageModel
{
    private readonly AuthService _auth;
    public RegisterModel(AuthService auth) => _auth = auth;

    [BindProperty] public InputModel Input { get; set; } = new();
    public string? Error { get; set; }

    public class InputModel
    {
        [Required(ErrorMessage = "Bitte Namen angeben.")]
        public string DisplayName { get; set; } = string.Empty;

        [Required(ErrorMessage = "Bitte E-Mail-Adresse angeben.")]
        [EmailAddress(ErrorMessage = "Ungültige E-Mail-Adresse.")]
        public string Email { get; set; } = string.Empty;

        [Required(ErrorMessage = "Bitte Passwort angeben.")]
        [MinLength(6, ErrorMessage = "Das Passwort muss mindestens 6 Zeichen haben.")]
        public string Password { get; set; } = string.Empty;

        [Compare(nameof(Password), ErrorMessage = "Die Passwörter stimmen nicht überein.")]
        public string PasswordConfirm { get; set; } = string.Empty;
    }

    public IActionResult OnGet()
    {
        if (User.Identity?.IsAuthenticated == true)
            return Redirect("/");
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
