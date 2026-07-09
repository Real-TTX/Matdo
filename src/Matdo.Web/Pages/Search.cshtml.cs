using Matdo.Web.Data.Entities;
using Matdo.Web.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Matdo.Web.Pages;

public class SearchModel : PageModel
{
    private readonly TaskService _tasks;
    public SearchModel(TaskService tasks) => _tasks = tasks;

    [FromQuery(Name = "q")] public string? Query { get; set; }
    public List<TaskItem> Results { get; set; } = new();

    public async Task OnGetAsync()
    {
        if (!string.IsNullOrWhiteSpace(Query))
            Results = await _tasks.SearchAsync(Query);
    }
}
