using System.ComponentModel.DataAnnotations;
using Matdo.Web.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace Matdo.Web.Pages.Admin.Users;

public class UserEditModel : PageModel
{
    private readonly AdminService _admin;
    public UserEditModel(AdminService admin) => _admin = admin;

    [BindProperty] public InputModel Input { get; set; } = new();
    public bool IsNew => Input.Id == 0;
    public List<SelectListItem> RoleOptions { get; set; } = new();
    public string? Error { get; set; }

    public class InputModel
    {
        public long Id { get; set; }
        [Required(ErrorMessage = "Bitte Namen angeben.")]
        public string DisplayName { get; set; } = "";
        [Required(ErrorMessage = "Bitte E-Mail angeben.")]
        [EmailAddress]
        public string Email { get; set; } = "";
        public long RoleId { get; set; }
        public bool IsActive { get; set; } = true;
        public string? Password { get; set; }
    }

    private async Task LoadRolesAsync()
    {
        var roles = await _admin.GetRolesAsync();
        RoleOptions = roles.Select(r => new SelectListItem(r.Name, r.Id.ToString())).ToList();
    }

    public async Task<IActionResult> OnGetAsync(long? id)
    {
        await LoadRolesAsync();
        if (id is > 0)
        {
            var u = await _admin.GetUserAsync(id.Value);
            if (u is null) return NotFound();
            Input = new InputModel { Id = u.Id, DisplayName = u.DisplayName, Email = u.Email, RoleId = u.RoleId, IsActive = u.IsActive };
        }
        else if (RoleOptions.Count > 0)
        {
            Input.RoleId = long.Parse(RoleOptions[0].Value);
        }
        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        await LoadRolesAsync();
        if (!ModelState.IsValid) return Page();

        try
        {
            if (Input.Id == 0)
            {
                if (string.IsNullOrWhiteSpace(Input.Password))
                {
                    Error = "Bitte ein Passwort vergeben.";
                    return Page();
                }
                await _admin.CreateUserAsync(Input.Email, Input.Password, Input.DisplayName, Input.RoleId, Input.IsActive);
            }
            else
            {
                await _admin.UpdateUserAsync(Input.Id, Input.DisplayName, Input.RoleId, Input.IsActive, Input.Password);
            }
        }
        catch (InvalidOperationException ex)
        {
            Error = ex.Message;
            return Page();
        }

        return Redirect("/Admin/Users");
    }

    public async Task<IActionResult> OnPostDeleteAsync(long id)
    {
        await _admin.DeleteUserAsync(id);
        return Redirect("/Admin/Users");
    }
}
