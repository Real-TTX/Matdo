using Matdo.Web.Data.Entities;
using Matdo.Web.Services;
using Matdo.Web.Services.Calendar;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Matdo.Web.Pages.Tasks;

public class TodayModel : PageModel
{
    private readonly TaskService _tasks;
    private readonly CalendarService _calendar;
    private readonly ICurrentUserAccessor _me;

    public TodayModel(TaskService tasks, CalendarService calendar, ICurrentUserAccessor me)
    {
        _tasks = tasks;
        _calendar = calendar;
        _me = me;
    }

    public List<TaskItem> Overdue { get; set; } = new();
    public List<TaskItem> DueToday { get; set; } = new();
    public List<CalendarEventDto> Events { get; set; } = new();

    public async Task OnGetAsync()
    {
        var all = await _tasks.GetTodayAsync();
        var today = DateTime.Now.Date;
        Overdue = all.Where(t => t.DueDate!.Value.ToLocalTime().Date < today).ToList();
        DueToday = all.Where(t => t.DueDate!.Value.ToLocalTime().Date == today).ToList();

        // Externe Termine des heutigen Tages (lokaler Tag -> UTC-Bereich)
        if (_me.UserId is long uid)
        {
            var fromUtc = today.ToUniversalTime();
            var toUtc = today.AddDays(1).ToUniversalTime();
            Events = (await _calendar.GetEventsAsync(uid, fromUtc, toUtc))
                .OrderBy(e => e.AllDay ? 0 : 1).ThenBy(e => e.StartUtc).ToList();
        }
    }
}
