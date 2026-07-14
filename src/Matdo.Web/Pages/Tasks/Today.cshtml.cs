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
    public bool ShowCompleted { get; set; }
    public string Sort { get; set; } = "manual";
    public int PrioFilter { get; set; }

    public async Task OnGetAsync(bool done, string? sort, int prio)
    {
        ShowCompleted = done;
        Sort = sort is "due" or "priority" or "name" ? sort : "manual";
        PrioFilter = prio is >= 1 and <= 4 ? prio : 0;

        var all = await _tasks.GetTodayAsync(includeCompleted: done);
        if (PrioFilter > 0) all = all.Where(t => (int)t.Priority == PrioFilter).ToList();
        var today = DateTime.Now.Date;
        Overdue = Order(all.Where(t => t.DueDate!.Value.ToLocalTime().Date < today));
        DueToday = Order(all.Where(t => t.DueDate!.Value.ToLocalTime().Date == today));

        // Externe Termine des heutigen Tages (lokaler Tag -> UTC-Bereich)
        if (_me.UserId is long uid)
        {
            var fromUtc = today.ToUniversalTime();
            var toUtc = today.AddDays(1).ToUniversalTime();
            Events = (await _calendar.GetEventsAsync(uid, fromUtc, toUtc))
                .OrderBy(e => e.AllDay ? 0 : 1).ThenBy(e => e.StartUtc).ToList();
        }
    }

    // Innerhalb einer Datumsgruppe: erledigte ans Ende, dann nach gewählter Sortierung.
    private List<TaskItem> Order(IEnumerable<TaskItem> tasks)
    {
        var ordered = Sort switch
        {
            "due" => tasks.OrderBy(t => t.DueDate).ThenBy(t => t.Priority),
            "priority" => tasks.OrderBy(t => t.Priority).ThenBy(t => t.DueDate),
            "name" => tasks.OrderBy(t => t.Title),
            _ => tasks.OrderBy(t => t.DueDate).ThenBy(t => t.Priority),
        };
        return ordered.OrderBy(t => t.IsCompleted).ToList();
    }
}
