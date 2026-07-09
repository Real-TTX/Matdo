using Matdo.Web.Data.Entities;
using Matdo.Web.Services;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace Matdo.Web.Pages.Admin.Groups;

public class GroupsIndexModel : PageModel
{
    private readonly AdminService _admin;
    public GroupsIndexModel(AdminService admin) => _admin = admin;

    public List<UserGroup> Items { get; set; } = new();

    public async Task OnGetAsync()
    {
        Items = await _admin.Groups().Include(g => g.Members).OrderBy(g => g.Name).ToListAsync();
    }
}
