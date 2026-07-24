using System.ComponentModel.DataAnnotations;
using Matdo.Web.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.RateLimiting;

namespace Matdo.Web.Pages.Account;

[EnableRateLimiting("auth")]
public class ForgotPasswordModel : PageModel
{
    private readonly AuthService _auth;
    public ForgotPasswordModel(AuthService auth) => _auth = auth;

    [BindProperty] public InputModel Input { get; set; } = new();
    public bool Sent { get; set; }

    public class InputModel
    {
        [Required(ErrorMessage = "Bitte E-Mail-Adresse angeben.")]
        [EmailAddress(ErrorMessage = "Ungültige E-Mail-Adresse.")]
        public string Email { get; set; } = string.Empty;
    }

    public void OnGet() { }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid) return Page();
        await _auth.RequestPasswordResetAsync(Input.Email);
        // Immer dieselbe neutrale Rückmeldung (kein Hinweis, ob das Konto existiert).
        Sent = true;
        return Page();
    }
}
