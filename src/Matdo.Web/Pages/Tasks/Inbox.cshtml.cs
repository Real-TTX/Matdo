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

    public async Task OnGetAsync() => Items = await _tasks.GetInboxAsync();
}
