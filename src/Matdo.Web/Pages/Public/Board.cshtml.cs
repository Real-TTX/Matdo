using System.Globalization;
using Matdo.Web.Data.Entities;
using Matdo.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.RateLimiting;

namespace Matdo.Web.Pages.Public;

[AllowAnonymous]
[EnableRateLimiting("anon-board")]
public class BoardModel : PageModel
{
    public const string Cookie = "matdo_anon";
    private readonly AnonymousShareService _anon;
    public BoardModel(AnonymousShareService anon) => _anon = anon;

    public Project Project { get; set; } = default!;
    public List<TaskItem> Tasks { get; set; } = new();
    public List<KanbanColumn> Columns { get; set; } = new();
    public string? AnonName { get; set; }
    public TaskItem? Editing { get; set; }

    /// <summary>"list" | "kanban" | "calendar" – aus dem Ansichtstyp des Projekts.</summary>
    public string ViewMode { get; set; } = "list";
    public int CalYear { get; set; }
    public int CalMonth { get; set; }
    public string? DuePrefill { get; set; }

    private async Task<Project?> ResolveAsync(string token)
        => Guid.TryParse(token, out var g) ? await _anon.GetProjectAsync(g) : null;

    private string? Name()
    {
        var n = Request.Cookies[Cookie];
        if (string.IsNullOrWhiteSpace(n)) return null;
        n = n.Trim();
        return n.Length > 60 ? n[..60] : n;   // Grenze auch beim Lesen des (Client-eigenen) Cookies
    }

    private void StoreName(string name)
    {
        Response.Cookies.Append(Cookie, name.Trim(), new CookieOptions
        {
            Expires = DateTimeOffset.UtcNow.AddYears(1),
            IsEssential = true,
            SameSite = SameSiteMode.Lax,
            Secure = Request.IsHttps,
            Path = "/"
        });
    }

    /// <summary>Datum (yyyy-MM-dd) + optionale Uhrzeit (HH:mm) aus lokalen Eingaben in UTC.</summary>
    private static (DateTime? utc, bool hasTime) ParseDateTime(string? date, string? time)
    {
        if (string.IsNullOrWhiteSpace(date)) return (null, false);
        if (!DateTime.TryParse(date, CultureInfo.InvariantCulture, DateTimeStyles.None, out var d))
            return (null, false);
        d = d.Date;
        var hasTime = false;
        if (!string.IsNullOrWhiteSpace(time) && TimeSpan.TryParse(time, CultureInfo.InvariantCulture, out var ts)
            && ts >= TimeSpan.Zero && ts < TimeSpan.FromDays(1))
        {
            d = d.Add(ts);
            hasTime = true;
        }
        var local = DateTime.SpecifyKind(d, DateTimeKind.Local);
        return (local.ToUniversalTime(), hasTime);
    }

    private static TaskPriority ParsePriority(string? p) =>
        int.TryParse(p, out var n) && n is >= 1 and <= 4 ? (TaskPriority)n : TaskPriority.P4;

    private void SetViewMode()
    {
        ViewMode = Project.ViewType switch
        {
            ProjectViewType.Kanban => "kanban",
            ProjectViewType.Calendar => "calendar",
            _ => "list"
        };
    }

    public async Task<IActionResult> OnGetAsync(string token, long? edit, string? ym, string? due)
    {
        var p = await ResolveAsync(token);
        if (p is null) return NotFound();
        Project = p;
        AnonName = Name();
        if (AnonName != null)
        {
            SetViewMode();
            Columns = await _anon.GetColumnsAsync(p.Id);
            Tasks = await _anon.GetTasksAsync(p.Id);
            if (edit is long eid) Editing = await _anon.GetTaskAsync(p.Id, eid);

            var now = DateTime.Now;
            CalYear = now.Year; CalMonth = now.Month;
            if (!string.IsNullOrWhiteSpace(ym) &&
                DateTime.TryParseExact(ym + "-01", "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var d))
            { CalYear = Math.Clamp(d.Year, 1900, 2999); CalMonth = d.Month; }   // AddMonths(±1) darf nicht überlaufen

            if (!string.IsNullOrWhiteSpace(due) &&
                DateTime.TryParseExact(due, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out _))
                DuePrefill = due;
        }
        return Page();
    }

    public async Task<IActionResult> OnPostNameAsync(string token, string? name)
    {
        if (await ResolveAsync(token) is null) return NotFound();
        if (!string.IsNullOrWhiteSpace(name)) StoreName(name.Length > 60 ? name[..60] : name);
        return Redirect("/s/" + token);
    }

    public IActionResult OnPostForget(string token)
    {
        Response.Cookies.Delete(Cookie);
        return Redirect("/s/" + token);
    }

    public async Task<IActionResult> OnPostAddAsync(string token, string? title, string? due, string? dueTime,
        string? deadline, string? deadlineTime, string? priority, long? columnId, long? parentTaskId)
    {
        var p = await ResolveAsync(token);
        var name = Name();
        if (p is null) return NotFound();
        if (name != null && !string.IsNullOrWhiteSpace(title))
        {
            if (parentTaskId is long parent)
            {
                await _anon.AddSubTaskAsync(p, parent, title, name);
            }
            else
            {
                var (dueUtc, dueHasTime) = ParseDateTime(due, dueTime);
                var (dlUtc, dlHasTime) = ParseDateTime(deadline, deadlineTime);
                await _anon.AddTaskAsync(p, title, dueUtc, dueHasTime, dlUtc, dlHasTime, ParsePriority(priority), columnId, name);
            }
        }
        return Redirect("/s/" + token);
    }

    public async Task<IActionResult> OnPostCompleteAsync(string token, long taskId, bool completed)
    {
        var p = await ResolveAsync(token);
        if (p is null) return NotFound();
        if (Name() != null) await _anon.SetCompletedAsync(p.Id, taskId, completed);
        return Redirect("/s/" + token);
    }

    public async Task<IActionResult> OnPostMoveAsync(string token, long taskId, long? columnId)
    {
        var p = await ResolveAsync(token);
        if (p is null) return NotFound();
        if (Name() != null) await _anon.MoveTaskAsync(p.Id, taskId, columnId);
        return Redirect("/s/" + token);
    }

    public async Task<IActionResult> OnPostEditAsync(string token, long taskId, string? title, string? description,
        string? due, string? dueTime, string? deadline, string? deadlineTime, string? priority, long? columnId)
    {
        var p = await ResolveAsync(token);
        var name = Name();
        if (p is null) return NotFound();
        if (name != null && !string.IsNullOrWhiteSpace(title))
        {
            var (dueUtc, dueHasTime) = ParseDateTime(due, dueTime);
            var (dlUtc, dlHasTime) = ParseDateTime(deadline, deadlineTime);
            await _anon.EditTaskAsync(p.Id, taskId, title, description, dueUtc, dueHasTime, dlUtc, dlHasTime,
                ParsePriority(priority), columnId, name);
        }
        return Redirect("/s/" + token);
    }
}
