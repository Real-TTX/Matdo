using Matdo.Web.Data.Entities;
using Matdo.Web.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Matdo.Web.Pages.Projects;

public class ArchiveModel : PageModel
{
    private readonly ProjectService _projects;
    public ArchiveModel(ProjectService projects) => _projects = projects;

    public List<Project> Items { get; set; } = new();

    public async Task OnGetAsync() => Items = await _projects.GetArchivedAsync();

    public async Task<IActionResult> OnPostRestoreAsync(long id)
    {
        await _projects.UnarchiveAsync(id);
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostDeleteAsync(long id)
    {
        await _projects.DeleteAsync(id);
        return RedirectToPage();
    }
}
