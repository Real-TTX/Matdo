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
        var tz = _me.TimeZone;
        var items = await _tasks.GetUpcomingAsync(days);
        var tasksByDay = items
            .GroupBy(t => DateHelper.ToLocal(t.DueDate!.Value, tz).Date)
            .ToDictionary(g => g.Key, g => g.ToList());

        var eventsByDay = new Dictionary<DateTime, List<CalendarEventDto>>();
        if (_me.UserId is long uid)
        {
            var start = DateHelper.TodayLocal(tz).AddDays(1);
            var fromUtc = DateHelper.LocalToUtc(start, tz);
            var toUtc = DateHelper.LocalToUtc(start.AddDays(days), tz);
            foreach (var e in await _calendar.GetEventsAsync(uid, fromUtc, toUtc))
            {
                var day = DateHelper.ToLocal(e.StartUtc, tz).Date;
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
