using Matdo.Web.Data.Entities;
using Matdo.Web.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Matdo.Web.Pages.Projects;

/// <summary>Abschnitt (= Kanban-Spalte) im Dialog bearbeiten: umbenennen / duplizieren / verschieben / löschen.</summary>
public class SectionModel : PageModel
{
    private readonly ProjectService _projects;
    public SectionModel(ProjectService projects) => _projects = projects;

    public KanbanColumn Column { get; set; } = default!;
    public long ProjectId { get; set; }
    public List<Project> MoveTargets { get; set; } = new();

    public async Task<IActionResult> OnGetAsync(long id)
    {
        var col = await _projects.GetManagedColumnAsync(id);
        if (col is null) return NotFound();
        Column = col;
        ProjectId = col.ProjectId;
        MoveTargets = (await _projects.GetAllAsync()).Where(p => p.Id != col.ProjectId).ToList();
        return Page();
    }

    public async Task<IActionResult> OnPostRenameAsync(long id, long pid, string? name)
    {
        if (!string.IsNullOrWhiteSpace(name)) await _projects.RenameColumnAsync(id, name.Trim());
        return Redirect($"/Projects/View?id={pid}");
    }

    public async Task<IActionResult> OnPostDuplicateAsync(long id, long pid)
    {
        await _projects.DuplicateColumnAsync(id);
        return Redirect($"/Projects/View?id={pid}");
    }

    public async Task<IActionResult> OnPostMoveAsync(long id, long pid, long targetProjectId)
    {
        await _projects.MoveColumnToProjectAsync(id, targetProjectId);
        return Redirect($"/Projects/View?id={pid}");
    }

    public async Task<IActionResult> OnPostDeleteAsync(long id, long pid)
    {
        await _projects.DeleteColumnAsync(id);
        return Redirect($"/Projects/View?id={pid}");
    }
}
