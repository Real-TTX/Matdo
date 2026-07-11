using Matdo.Web.Data.Entities;
using Matdo.Web.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Matdo.Web.Pages.Teams;

public class TeamsIndexModel : PageModel
{
    private readonly TeamService _teams;
    public TeamsIndexModel(TeamService teams) => _teams = teams;

    public List<(Team Team, TeamRole Role, int MemberCount)> Teams { get; set; } = new();

    [BindProperty] public string NewName { get; set; } = "";
    [BindProperty] public string NewColor { get; set; } = "#8300bc";

    public async Task OnGetAsync() => Teams = await _teams.GetMyTeamsAsync();

    public async Task<IActionResult> OnPostCreateAsync()
    {
        if (!string.IsNullOrWhiteSpace(NewName))
        {
            var team = await _teams.CreateAsync(NewName, NewColor);
            return RedirectToPage("View", new { id = team.Id });
        }
        return RedirectToPage();
    }
}
