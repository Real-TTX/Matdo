using System.ComponentModel.DataAnnotations;
using Matdo.Web.Data.Entities;
using Matdo.Web.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Matdo.Web.Pages.Projects;

public class ProjectEditModel : PageModel
{
    private readonly ProjectService _projects;
    private readonly ShareService _shares;

    public ProjectEditModel(ProjectService projects, ShareService shares)
    {
        _projects = projects;
        _shares = shares;
    }

    [BindProperty] public InputModel Input { get; set; } = new();
    public bool IsNew => Input.Id == 0;
    public List<KanbanColumn> Columns { get; set; } = new();
    public List<ProjectShare> Shares { get; set; } = new();
    public List<User> ShareableUsers { get; set; } = new();

    public static readonly string[] Palette =
        { "#dc4c3e", "#eb8909", "#f9d900", "#af38eb", "#7ecc49", "#158fad", "#4073ff", "#884dff", "#808080" };

    public class InputModel
    {
        public long Id { get; set; }
        [Required(ErrorMessage = "Bitte einen Namen angeben.")]
        public string Name { get; set; } = "";
        public string Color { get; set; } = "#dc4c3e";
        public int ViewType { get; set; }
        public bool IsFavorite { get; set; }
    }

    private async Task LoadAsync(long id)
    {
        Columns = await _projects.GetColumnsAsync(id);
        Shares = await _shares.GetProjectSharesAsync(id);
        ShareableUsers = await _shares.GetShareableUsersAsync();
    }

    public async Task<IActionResult> OnGetAsync(long? id)
    {
        if (id is > 0)
        {
            var p = await _projects.GetAsync(id.Value);
            if (p is null) return NotFound();
            Input = new InputModel { Id = p.Id, Name = p.Name, Color = p.Color, ViewType = (int)p.ViewType, IsFavorite = p.IsFavorite };
            await LoadAsync(p.Id);
        }
        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid)
        {
            if (Input.Id > 0) await LoadAsync(Input.Id);
            return Page();
        }

        var project = new Project
        {
            Id = Input.Id,
            Name = Input.Name.Trim(),
            Color = Input.Color,
            ViewType = (ProjectViewType)Input.ViewType,
            IsFavorite = Input.IsFavorite
        };

        if (Input.Id == 0)
        {
            var created = await _projects.CreateAsync(project);
            return RedirectToPage(new { id = created.Id, saved = true });
        }

        await _projects.UpdateAsync(project);
        return RedirectToPage(new { id = Input.Id, saved = true });
    }

    public async Task<IActionResult> OnPostAddColumnAsync(long id, string columnName)
    {
        if (!string.IsNullOrWhiteSpace(columnName))
            await _projects.AddColumnAsync(id, columnName.Trim());
        return RedirectToPage(new { id });
    }

    public async Task<IActionResult> OnPostRenameColumnAsync(long id, long columnId, string columnName)
    {
        if (!string.IsNullOrWhiteSpace(columnName))
            await _projects.RenameColumnAsync(columnId, columnName.Trim());
        return RedirectToPage(new { id });
    }

    public async Task<IActionResult> OnPostDeleteColumnAsync(long id, long columnId)
    {
        await _projects.DeleteColumnAsync(columnId);
        return RedirectToPage(new { id });
    }

    public async Task<IActionResult> OnPostShareAsync(long id, long targetUserId, int permission)
    {
        await _shares.ShareProjectAsync(id, targetUserId, (SharePermission)permission);
        return RedirectToPage(new { id });
    }

    public async Task<IActionResult> OnPostUnshareAsync(long id, long targetUserId)
    {
        await _shares.UnshareProjectAsync(id, targetUserId);
        return RedirectToPage(new { id });
    }

    public async Task<IActionResult> OnPostDeleteAsync(long id)
    {
        await _projects.DeleteAsync(id);
        return Redirect("/Projects");
    }
}
