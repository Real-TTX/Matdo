using Matdo.Web.Data;
using Matdo.Web.Data.Entities;
using Matdo.Web.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace Matdo.Web.Pages.Projects;

public class ProjectsIndexModel : PageModel
{
    private readonly ProjectService _projects;
    private readonly MatdoDbContext _db;

    public ProjectsIndexModel(ProjectService projects, MatdoDbContext db)
    {
        _projects = projects;
        _db = db;
    }

    public record Row(Project Project, int OpenTasks);

    public List<Row> Rows { get; set; } = new();

    [FromQuery(Name = "q")] public string? Search { get; set; }
    [FromQuery(Name = "sort")] public string Sort { get; set; } = "name";

    public async Task OnGetAsync()
    {
        var projects = await _projects.GetAllAsync();

        if (!string.IsNullOrWhiteSpace(Search))
            projects = projects.Where(p => p.Name.Contains(Search, StringComparison.OrdinalIgnoreCase)).ToList();

        projects = Sort switch
        {
            "name_desc" => projects.OrderByDescending(p => p.Name).ToList(),
            "view" => projects.OrderBy(p => p.ViewType).ThenBy(p => p.Name).ToList(),
            _ => projects.OrderBy(p => p.Name).ToList()
        };

        var counts = await _db.Tasks
            .Where(t => !t.IsCompleted && t.ParentTaskId == null && t.ProjectId != null)
            .GroupBy(t => t.ProjectId!.Value)
            .Select(g => new { g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Key, x => x.Count);

        Rows = projects.Select(p => new Row(p, counts.GetValueOrDefault(p.Id))).ToList();
    }
}
