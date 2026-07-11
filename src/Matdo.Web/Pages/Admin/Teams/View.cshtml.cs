using Matdo.Web.Data.Entities;
using Matdo.Web.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Matdo.Web.Pages.Admin.Teams;

public class AdminTeamViewModel : PageModel
{
    private readonly TeamService _teams;
    public AdminTeamViewModel(TeamService teams) => _teams = teams;

    public Team Team { get; set; } = null!;
    public string? Message { get; set; }

    private async Task<bool> LoadAsync(long id)
    {
        var t = await _teams.AdminGetAsync(id);
        if (t is null) return false;
        Team = t;
        Message = TempData["Msg"] as string;
        return true;
    }

    public async Task<IActionResult> OnGetAsync(long id)
        => await LoadAsync(id) ? Page() : NotFound();

    public async Task<IActionResult> OnPostRoleAsync(long id, long userId, int role)
    {
        await _teams.AdminChangeRoleAsync(id, userId, (TeamRole)role);
        return RedirectToPage(new { id });
    }

    public async Task<IActionResult> OnPostRemoveAsync(long id, long userId)
    {
        await _teams.AdminRemoveMemberAsync(id, userId);
        return RedirectToPage(new { id });
    }

    public async Task<IActionResult> OnPostDeleteAsync(long id)
    {
        await _teams.AdminDeleteAsync(id);
        return Redirect("/Admin/Teams");
    }
}
