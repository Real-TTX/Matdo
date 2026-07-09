using Matdo.Web.Data.Entities;
using Matdo.Web.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace Matdo.Web.Pages.Admin.Users;

public class UsersIndexModel : PageModel
{
    private readonly AdminService _admin;
    public UsersIndexModel(AdminService admin) => _admin = admin;

    public const int PageSize = 10;

    public List<User> Items { get; set; } = new();
    public int TotalPages { get; set; }

    [FromQuery(Name = "q")] public string? Search { get; set; }
    [FromQuery(Name = "sort")] public string Sort { get; set; } = "name";
    [FromQuery(Name = "p")] public int PageNo { get; set; } = 1;

    public async Task OnGetAsync()
    {
        var query = _admin.Users();

        if (!string.IsNullOrWhiteSpace(Search))
        {
            var like = $"%{Search}%";
            query = query.Where(u => EF.Functions.ILike(u.DisplayName, like) || EF.Functions.ILike(u.Email, like));
        }

        query = Sort switch
        {
            "name_desc" => query.OrderByDescending(u => u.DisplayName),
            "email" => query.OrderBy(u => u.Email),
            "created" => query.OrderByDescending(u => u.CreateDate),
            _ => query.OrderBy(u => u.DisplayName)
        };

        var total = await query.CountAsync();
        TotalPages = (int)Math.Ceiling(total / (double)PageSize);
        PageNo = Math.Clamp(PageNo, 1, Math.Max(1, TotalPages));

        Items = await query.Skip((PageNo - 1) * PageSize).Take(PageSize).ToListAsync();
    }
}
