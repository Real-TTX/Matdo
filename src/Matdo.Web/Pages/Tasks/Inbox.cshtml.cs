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
    [BindProperty] public string? QuickTitle { get; set; }

    public async Task OnGetAsync() => Items = await _tasks.GetInboxAsync();

    public async Task<IActionResult> OnPostQuickAddAsync()
    {
        if (!string.IsNullOrWhiteSpace(QuickTitle))
            await _tasks.CreateAsync(new TaskItem { Title = QuickTitle.Trim() });
        return RedirectToPage();
    }
}
