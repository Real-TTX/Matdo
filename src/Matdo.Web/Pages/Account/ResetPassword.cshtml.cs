using System.ComponentModel.DataAnnotations;
using Matdo.Web.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.RateLimiting;

namespace Matdo.Web.Pages.Account;

[EnableRateLimiting("auth")]
public class ResetPasswordModel : PageModel
{
    private readonly AuthService _auth;
    public ResetPasswordModel(AuthService auth) => _auth = auth;

    [BindProperty] public InputModel Input { get; set; } = new();
    public string? Error { get; set; }
    public bool Done { get; set; }

    public class InputModel
    {
        [Required] public string Token { get; set; } = string.Empty;

        [Required(ErrorMessage = "Bitte Passwort angeben.")]
        [MinLength(8, ErrorMessage = "Das Passwort muss mindestens 8 Zeichen haben.")]
        public string Password { get; set; } = string.Empty;

        [Compare(nameof(Password), ErrorMessage = "Die Passwörter stimmen nicht überein.")]
        public string PasswordConfirm { get; set; } = string.Empty;
    }

    public IActionResult OnGet(string? token)
    {
        if (string.IsNullOrWhiteSpace(token) || !Guid.TryParse(token, out _))
        {
            Error = "invalid";
            return Page();
        }
        Input.Token = token;
        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid) return Page();
        if (!Guid.TryParse(Input.Token, out var token))
        {
            Error = "invalid";
            return Page();
        }
        var result = await _auth.ResetPasswordAsync(token, Input.Password);
        if (!result.Success)
        {
            Error = result.Error;
            return Page();
        }
        Done = true;
        return Page();
    }
}
