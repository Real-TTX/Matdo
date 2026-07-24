using Matdo.Web.Data.Entities;
using Matdo.Web.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Matdo.Web.Pages.Account;

public class InvitationsModel : PageModel
{
    private readonly TeamService _teams;
    public InvitationsModel(TeamService teams) => _teams = teams;

    public List<Invitation> Items { get; set; } = new();

    public async Task OnGetAsync() => Items = await _teams.GetMyInvitationsAsync();

    public async Task<IActionResult> OnPostAcceptAsync(long id)
    {
        await _teams.AcceptInvitationAsync(id);
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostDeclineAsync(long id)
    {
        await _teams.DeclineInvitationAsync(id);
        return RedirectToPage();
    }
}
