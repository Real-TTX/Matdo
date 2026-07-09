using Matdo.Web.Data.Entities;
using Matdo.Web.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Matdo.Web.Pages.Tasks;

public class TodayModel : PageModel
{
    private readonly TaskService _tasks;
    public TodayModel(TaskService tasks) => _tasks = tasks;

    public List<TaskItem> Overdue { get; set; } = new();
    public List<TaskItem> DueToday { get; set; } = new();

    [BindProperty] public string? QuickTitle { get; set; }

    public async Task OnGetAsync()
    {
        var all = await _tasks.GetTodayAsync();
        var today = DateTime.Now.Date;
        Overdue = all.Where(t => t.DueDate!.Value.ToLocalTime().Date < today).ToList();
        DueToday = all.Where(t => t.DueDate!.Value.ToLocalTime().Date == today).ToList();
    }

    public async Task<IActionResult> OnPostQuickAddAsync()
    {
        if (!string.IsNullOrWhiteSpace(QuickTitle))
        {
            await _tasks.CreateAsync(new TaskItem
            {
                Title = QuickTitle.Trim(),
                DueDate = DateTime.Today.ToUniversalTime(),
                DueHasTime = false
            });
        }
        return RedirectToPage();
    }
}
