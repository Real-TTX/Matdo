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
    public string? AnonName { get; set; }
    public TaskItem? Editing { get; set; }

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

    private static (DateTime? utc, bool hasTime) ParseDue(string? s)
    {
        if (string.IsNullOrWhiteSpace(s)) return (null, false);
        if (DateTime.TryParse(s, CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal, out var d))
            return (d.ToUniversalTime(), d.TimeOfDay != TimeSpan.Zero);
        return (null, false);
    }

    public async Task<IActionResult> OnGetAsync(string token, long? edit)
    {
        var p = await ResolveAsync(token);
        if (p is null) return NotFound();
        Project = p;
        AnonName = Name();
        if (AnonName != null)
        {
            Tasks = await _anon.GetTasksAsync(p.Id);
            if (edit is long eid) Editing = await _anon.GetTaskAsync(p.Id, eid);
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

    public async Task<IActionResult> OnPostAddAsync(string token, string? title, string? due)
    {
        var p = await ResolveAsync(token);
        var name = Name();
        if (p is null) return NotFound();
        if (name != null && !string.IsNullOrWhiteSpace(title))
        {
            var (utc, hasTime) = ParseDue(due);
            await _anon.AddTaskAsync(p, title, utc, hasTime, name);
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

    public async Task<IActionResult> OnPostEditAsync(string token, long taskId, string? title, string? description, string? due)
    {
        var p = await ResolveAsync(token);
        var name = Name();
        if (p is null) return NotFound();
        if (name != null && !string.IsNullOrWhiteSpace(title))
        {
            var (utc, hasTime) = ParseDue(due);
            await _anon.EditTaskAsync(p.Id, taskId, title, description, utc, hasTime, name);
        }
        return Redirect("/s/" + token);
    }
}
