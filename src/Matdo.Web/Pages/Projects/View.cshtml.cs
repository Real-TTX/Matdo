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
    public bool IsKanban { get; set; }

    [BindProperty] public string? QuickTitle { get; set; }

    public async Task<IActionResult> OnGetAsync(long id, string? view)
    {
        var p = await _projects.GetAsync(id);
        if (p is null) return NotFound();
        Project = p;
        Columns = p.Columns.OrderBy(c => c.Position).ToList();
        Tasks = await _tasks.GetByProjectAsync(id);
        IsKanban = view is null ? p.ViewType == ProjectViewType.Kanban : view == "kanban";
        return Page();
    }

    public async Task<IActionResult> OnPostQuickAddAsync(long id)
    {
        if (!string.IsNullOrWhiteSpace(QuickTitle))
        {
            var cols = await _projects.GetColumnsAsync(id);
            await _tasks.CreateAsync(new TaskItem
            {
                Title = QuickTitle.Trim(),
                ProjectId = id,
                KanbanColumnId = cols.FirstOrDefault()?.Id
            });
        }
        return RedirectToPage(new { id });
    }
}
