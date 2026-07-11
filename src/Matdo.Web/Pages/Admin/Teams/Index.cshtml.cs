using Matdo.Web.Data.Entities;
using Matdo.Web.Services;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Matdo.Web.Pages.Admin.Teams;

public class AdminTeamsIndexModel : PageModel
{
    private readonly TeamService _teams;
    public AdminTeamsIndexModel(TeamService teams) => _teams = teams;

    public List<(Team Team, int MemberCount)> Teams { get; set; } = new();

    public async Task OnGetAsync() => Teams = await _teams.AdminGetAllAsync();
}
