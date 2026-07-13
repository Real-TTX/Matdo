using Matdo.Web.Data.Entities;
using Matdo.Web.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Matdo.Web.Pages.Projects;

/// <summary>Projekt-Aktionen im Dialog: umbenennen, Favorit, duplizieren, Link, CSV, archivieren, löschen.</summary>
public class ActionsModel : PageModel
{
    private readonly ProjectService _projects;
    public ActionsModel(ProjectService projects) => _projects = projects;

    public Project Project { get; set; } = default!;

    public async Task<IActionResult> OnGetAsync(long id)
    {
        var p = await _projects.GetAsync(id);
        if (p is null) return NotFound();
        Project = p;
        return Page();
    }

    public async Task<IActionResult> OnPostRenameAsync(long id, string? name)
    {
        await _projects.RenameAsync(id, name);
        return Redirect($"/Projects/View?id={id}");
    }

    public async Task<IActionResult> OnPostFavoriteAsync(long id, bool favorite)
    {
        await _projects.SetFavoriteAsync(id, favorite);
        return Redirect($"/Projects/View?id={id}");
    }

    public async Task<IActionResult> OnPostDuplicateAsync(long id)
    {
        await _projects.DuplicateAsync(id);
        return Redirect($"/Projects/View?id={id}");
    }

    public async Task<IActionResult> OnPostArchiveAsync(long id)
    {
        await _projects.ArchiveAsync(id);
        return Redirect("/Projects");
    }

    public async Task<IActionResult> OnPostDeleteAsync(long id)
    {
        await _projects.DeleteAsync(id);
        return Redirect("/Projects");
    }
}
