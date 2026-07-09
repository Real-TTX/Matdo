using Matdo.Web.Data;
using Matdo.Web.Data.Entities;
using Matdo.Web.Services;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace Matdo.Web.Pages.Admin.Roles;

public class RolesIndexModel : PageModel
{
    private readonly AdminService _admin;
    private readonly MatdoDbContext _db;

    public RolesIndexModel(AdminService admin, MatdoDbContext db)
    {
        _admin = admin;
        _db = db;
    }

    public record Row(Role Role, int UserCount);
    public List<Row> Rows { get; set; } = new();

    public async Task OnGetAsync()
    {
        var roles = await _admin.GetRolesAsync();
        var counts = await _db.Users.GroupBy(u => u.RoleId)
            .Select(g => new { g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Key, x => x.Count);
        Rows = roles.Select(r => new Row(r, counts.GetValueOrDefault(r.Id))).ToList();
    }
}
