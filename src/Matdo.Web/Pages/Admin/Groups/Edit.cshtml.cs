using System.ComponentModel.DataAnnotations;
using Matdo.Web.Data.Entities;
using Matdo.Web.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace Matdo.Web.Pages.Admin.Groups;

public class GroupEditModel : PageModel
{
    private readonly AdminService _admin;
    public GroupEditModel(AdminService admin) => _admin = admin;

    [BindProperty] public InputModel Input { get; set; } = new();
    [BindProperty] public List<long> MemberIds { get; set; } = new();
    public bool IsNew => Input.Id == 0;
    public List<User> AllUsers { get; set; } = new();

    public class InputModel
    {
        public long Id { get; set; }
        [Required(ErrorMessage = "Bitte einen Namen angeben.")]
        public string Name { get; set; } = "";
        public string? Description { get; set; }
    }

    private async Task LoadUsersAsync() => AllUsers = await _admin.Users().OrderBy(u => u.DisplayName).ToListAsync();

    public async Task<IActionResult> OnGetAsync(long? id)
    {
        await LoadUsersAsync();
        if (id is > 0)
        {
            var g = await _admin.GetGroupAsync(id.Value);
            if (g is null) return NotFound();
            Input = new InputModel { Id = g.Id, Name = g.Name, Description = g.Description };
            MemberIds = g.Members.Select(m => m.UserId).ToList();
        }
        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        await LoadUsersAsync();
        if (!ModelState.IsValid) return Page();

        if (Input.Id == 0)
        {
            var g = await _admin.CreateGroupAsync(Input.Name, Input.Description);
            await _admin.UpdateGroupAsync(g.Id, Input.Name, Input.Description, MemberIds);
        }
        else
        {
            await _admin.UpdateGroupAsync(Input.Id, Input.Name, Input.Description, MemberIds);
        }
        return Redirect("/Admin/Groups");
    }

    public async Task<IActionResult> OnPostDeleteAsync(long id)
    {
        await _admin.DeleteGroupAsync(id);
        return Redirect("/Admin/Groups");
    }
}
