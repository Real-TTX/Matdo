using Matdo.Web.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.RateLimiting;

namespace Matdo.Web.Pages.Account;

[EnableRateLimiting("auth")]
public class ConfirmEmailModel : PageModel
{
    private readonly AuthService _auth;
    private readonly ICurrentUserAccessor _me;
    public ConfirmEmailModel(AuthService auth, ICurrentUserAccessor me) { _auth = auth; _me = me; }

    public bool Ok { get; set; }
    public bool Resent { get; set; }
    public bool ShowResend { get; set; }

    public async Task<IActionResult> OnGetAsync(string? token)
    {
        if (!string.IsNullOrWhiteSpace(token) && Guid.TryParse(token, out var g))
            Ok = await _auth.ConfirmEmailAsync(g);
        // Angemeldete, noch unbestätigte Nutzer können erneut senden.
        ShowResend = _me.UserId is not null && !Ok;
        return Page();
    }

    public async Task<IActionResult> OnPostResendAsync()
    {
        if (_me.UserId is long uid)
        {
            await _auth.ResendVerificationAsync(uid);
            Resent = true;
        }
        return Page();
    }
}
