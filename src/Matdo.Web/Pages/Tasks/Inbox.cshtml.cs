using Matdo.Web.Data.Entities;
using Matdo.Web.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Matdo.Web.Pages.Tasks;

public class InboxModel : PageModel
{
    private readonly TaskService _tasks;
    public InboxModel(TaskService tasks) => _tasks = tasks;

    public List<TaskItem> Items { get; set; } = new();
    public bool ShowCompleted { get; set; }
    public string Sort { get; set; } = "manual";
    public int PrioFilter { get; set; }

    public async Task OnGetAsync(bool done, string? sort, int prio)
    {
        ShowCompleted = done;
        Sort = sort is "due" or "priority" or "name" ? sort : "manual";
        PrioFilter = prio is >= 1 and <= 4 ? prio : 0;
        Items = await _tasks.GetInboxAsync(includeCompleted: done);
        if (PrioFilter > 0) Items = Items.Where(t => (int)t.Priority == PrioFilter).ToList();
        Items = Sort switch
        {
            "due" => Items.OrderBy(t => !t.DueDate.HasValue).ThenBy(t => t.DueDate).ThenBy(t => t.Priority).ToList(),
            "priority" => Items.OrderBy(t => t.Priority).ThenBy(t => t.Position).ToList(),
            "name" => Items.OrderBy(t => t.Title).ToList(),
            _ => Items
        };
    }
}
