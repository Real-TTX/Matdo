using Matdo.Web.Data;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace Matdo.Web.Pages.Admin;

public class AdminIndexModel : PageModel
{
    private readonly MatdoDbContext _db;
    public AdminIndexModel(MatdoDbContext db) => _db = db;

    public int Users { get; set; }
    public int Groups { get; set; }
    public int Roles { get; set; }
    public int Projects { get; set; }
    public int Tasks { get; set; }
    public int Sessions { get; set; }

    public async Task OnGetAsync()
    {
        Users = await _db.Users.CountAsync();
        Groups = await _db.UserGroups.CountAsync();
        Roles = await _db.Roles.CountAsync();
        Projects = await _db.Projects.CountAsync();
        Tasks = await _db.Tasks.CountAsync();
        Sessions = await _db.UserSessions.CountAsync(s => s.ExpiresAt > DateTime.UtcNow);
    }
}
