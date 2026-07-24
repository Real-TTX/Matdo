using System.ComponentModel.DataAnnotations;
using Matdo.Web.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.RateLimiting;

namespace Matdo.Web.Pages.Account;

[EnableRateLimiting("auth")]
public class LoginModel : PageModel
{
    private readonly AuthService _auth;
    public LoginModel(AuthService auth) => _auth = auth;

    [BindProperty] public InputModel Input { get; set; } = new();
    public string? Error { get; set; }
    [FromQuery] public string? ReturnUrl { get; set; }

    public class InputModel
    {
        [Required(ErrorMessage = "Bitte E-Mail-Adresse angeben.")]
        [EmailAddress(ErrorMessage = "Ungültige E-Mail-Adresse.")]
        public string Email { get; set; } = string.Empty;

        [Required(ErrorMessage = "Bitte Passwort angeben.")]
        public string Password { get; set; } = string.Empty;
    }

    public async Task<IActionResult> OnGetAsync()
    {
        if (User.Identity?.IsAuthenticated == true)
            return Redirect("/");
        // Vor der Ersteinrichtung (noch kein Benutzer) zur Setup-Seite leiten.
        if (!await _auth.AnyUsersAsync())
            return Redirect("/Account/Setup");
        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid) return Page();

        var result = await _auth.LoginAsync(Input.Email, Input.Password);
        if (!result.Success)
        {
            Error = result.Error;
            return Page();
        }

        return LocalRedirect(string.IsNullOrEmpty(ReturnUrl) ? "/" : ReturnUrl);
    }
}
