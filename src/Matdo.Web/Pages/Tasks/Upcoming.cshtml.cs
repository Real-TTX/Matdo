using Matdo.Web.Data.Entities;
using Matdo.Web.Services;
using Matdo.Web.Services.Calendar;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Matdo.Web.Pages.Tasks;

public class UpcomingModel : PageModel
{
    private readonly TaskService _tasks;
    private readonly CalendarService _calendar;
    private readonly ICurrentUserAccessor _me;

    public UpcomingModel(TaskService tasks, CalendarService calendar, ICurrentUserAccessor me)
    {
        _tasks = tasks;
        _calendar = calendar;
        _me = me;
    }

    public record DayEntry(DateTime Date, List<TaskItem> Tasks, List<CalendarEventDto> Events);
    public List<DayEntry> Days { get; set; } = new();

    public async Task OnGetAsync()
    {
        const int days = 45;
        var items = await _tasks.GetUpcomingAsync(days);
        var tasksByDay = items
            .GroupBy(t => t.DueDate!.Value.ToLocalTime().Date)
            .ToDictionary(g => g.Key, g => g.ToList());

        var eventsByDay = new Dictionary<DateTime, List<CalendarEventDto>>();
        if (_me.UserId is long uid)
        {
            var start = DateTime.Today.AddDays(1);
            var fromUtc = start.ToUniversalTime();
            var toUtc = start.AddDays(days).ToUniversalTime();
            foreach (var e in await _calendar.GetEventsAsync(uid, fromUtc, toUtc))
            {
                var day = e.StartUtc.ToLocalTime().Date;
                if (!eventsByDay.TryGetValue(day, out var list)) eventsByDay[day] = list = new();
                list.Add(e);
            }
        }

        var allDates = tasksByDay.Keys.Union(eventsByDay.Keys).Distinct().OrderBy(d => d);
        Days = allDates.Select(d => new DayEntry(
            d,
            tasksByDay.GetValueOrDefault(d) ?? new(),
            (eventsByDay.GetValueOrDefault(d) ?? new()).OrderBy(e => e.AllDay ? 0 : 1).ThenBy(e => e.StartUtc).ToList()
        )).ToList();
    }
}
