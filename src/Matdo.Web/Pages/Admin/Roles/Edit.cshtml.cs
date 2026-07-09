using System.ComponentModel.DataAnnotations;
using Matdo.Web.Data.Entities;
using Matdo.Web.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Matdo.Web.Pages.Admin.Roles;

public class RoleEditModel : PageModel
{
    private readonly AdminService _admin;
    public RoleEditModel(AdminService admin) => _admin = admin;

    [BindProperty] public InputModel Input { get; set; } = new();
    public bool IsNew => Input.Id == 0;
    public bool IsSystemRole => Input.Name is Role.Admin or Role.User;
    public string? Error { get; set; }

    public class InputModel
    {
        public long Id { get; set; }
        [Required(ErrorMessage = "Bitte einen Namen angeben.")]
        public string Name { get; set; } = "";
        public string? Description { get; set; }
    }

    public async Task<IActionResult> OnGetAsync(long? id)
    {
        if (id is > 0)
        {
            var r = await _admin.GetRoleAsync(id.Value);
            if (r is null) return NotFound();
            Input = new InputModel { Id = r.Id, Name = r.Name, Description = r.Description };
        }
        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid) return Page();
        try
        {
            if (Input.Id == 0)
                await _admin.CreateRoleAsync(Input.Name, Input.Description);
            else
                await _admin.UpdateRoleAsync(Input.Id, Input.Name, Input.Description);
        }
        catch (InvalidOperationException ex)
        {
            Error = ex.Message;
            return Page();
        }
        return Redirect("/Admin/Roles");
    }

    public async Task<IActionResult> OnPostDeleteAsync(long id)
    {
        try
        {
            await _admin.DeleteRoleAsync(id);
        }
        catch (InvalidOperationException ex)
        {
            Error = ex.Message;
            var r = await _admin.GetRoleAsync(id);
            if (r is not null) Input = new InputModel { Id = r.Id, Name = r.Name, Description = r.Description };
            return Page();
        }
        return Redirect("/Admin/Roles");
    }
}
