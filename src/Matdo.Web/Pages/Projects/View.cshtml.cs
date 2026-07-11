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

    public async Task<IActionResult> OnGetAsync(long id, string? view, string? ym)
    {
        var p = await _projects.GetAsync(id);
        if (p is null) return NotFound();
        Project = p;
        Columns = p.Columns.OrderBy(c => c.Position).ToList();
        Tasks = await _tasks.GetByProjectAsync(id);

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
}
