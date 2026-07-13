using System.Globalization;
using Matdo.Web.Data.Entities;
using Matdo.Web.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Matdo.Web.Pages.Projects;

public class ProjectViewModel : PageModel
{
    private readonly ProjectService _projects;
    private readonly TaskService _tasks;

    public ProjectViewModel(ProjectService projects, TaskService tasks)
    {
        _projects = projects;
        _tasks = tasks;
    }

    public Project Project { get; set; } = default!;
    public List<TaskItem> Tasks { get; set; } = new();
    public List<KanbanColumn> Columns { get; set; } = new();

    /// <summary>Aktive Ansicht: "list" | "calendar" | "kanban".</summary>
    public string ViewMode { get; set; } = "list";
    public int CalYear { get; set; }
    public int CalMonth { get; set; }
    public bool ShowCompleted { get; set; }
    /// <summary>Andere Projekte (für „Abschnitt in anderes Projekt verschieben").</summary>
    public List<Project> MoveTargets { get; set; } = new();

    public async Task<IActionResult> OnGetAsync(long id, string? view, string? ym, bool done)
    {
        var p = await _projects.GetAsync(id);
        if (p is null) return NotFound();
        Project = p;
        ShowCompleted = done;
        Columns = p.Columns.OrderBy(c => c.Position).ToList();
        Tasks = await _tasks.GetByProjectAsync(id, includeCompleted: done);
        MoveTargets = (await _projects.GetAllAsync()).Where(x => x.Id != id).ToList();

        ViewMode = view ?? p.ViewType switch
        {
            ProjectViewType.Kanban => "kanban",
            ProjectViewType.Calendar => "calendar",
            _ => "list"
        };
        if (ViewMode is not ("list" or "calendar" or "kanban")) ViewMode = "list";

        // Monat für die Kalenderansicht (Standard: aktueller Monat)
        var now = DateTime.Now;
        CalYear = now.Year;
        CalMonth = now.Month;
        if (!string.IsNullOrWhiteSpace(ym) &&
            DateTime.TryParseExact(ym + "-01", "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var d))
        {
            CalYear = d.Year;
            CalMonth = d.Month;
        }
        return Page();
    }

    // ----- Abschnitte (= Kanban-Spalten) direkt in der Projektansicht verwalten -----
    public async Task<IActionResult> OnPostAddSectionAsync(long id, string? sectionName)
    {
        if (!string.IsNullOrWhiteSpace(sectionName)) await _projects.AddColumnAsync(id, sectionName.Trim());
        return RedirectToPage(new { id });
    }

    public async Task<IActionResult> OnPostRenameSectionAsync(long id, long columnId, string? sectionName)
    {
        if (!string.IsNullOrWhiteSpace(sectionName)) await _projects.RenameColumnAsync(columnId, sectionName.Trim());
        return RedirectToPage(new { id });
    }

    public async Task<IActionResult> OnPostDeleteSectionAsync(long id, long columnId)
    {
        await _projects.DeleteColumnAsync(columnId);
        return RedirectToPage(new { id });
    }

    public async Task<IActionResult> OnPostDuplicateSectionAsync(long id, long columnId)
    {
        await _projects.DuplicateColumnAsync(columnId);
        return RedirectToPage(new { id });
    }

    public async Task<IActionResult> OnPostMoveSectionAsync(long id, long columnId, long targetProjectId)
    {
        await _projects.MoveColumnToProjectAsync(columnId, targetProjectId);
        return RedirectToPage(new { id });
    }

    // ----- Projekt-Aktionen (⋯-Menü) -----
    public async Task<IActionResult> OnPostFavoriteAsync(long id, bool favorite)
    {
        await _projects.SetFavoriteAsync(id, favorite);
        return RedirectToPage(new { id });
    }

    public async Task<IActionResult> OnPostArchiveAsync(long id)
    {
        await _projects.ArchiveAsync(id);
        return Redirect("/Projects");
    }

    public async Task<IActionResult> OnPostDuplicateAsync(long id)
    {
        var newId = await _projects.DuplicateAsync(id);
        return RedirectToPage(new { id = newId ?? id });
    }
}
