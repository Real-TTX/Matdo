using Matdo.Web.Data.Entities;
using Matdo.Web.Services;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Matdo.Web.Pages.Tasks;

public class UpcomingModel : PageModel
{
    private readonly TaskService _tasks;
    public UpcomingModel(TaskService tasks) => _tasks = tasks;

    public List<IGrouping<DateTime, TaskItem>> Groups { get; set; } = new();

    public async Task OnGetAsync()
    {
        var items = await _tasks.GetUpcomingAsync(45);
        Groups = items
            .GroupBy(t => t.DueDate!.Value.ToLocalTime().Date)
            .OrderBy(g => g.Key)
            .ToList();
    }
}
