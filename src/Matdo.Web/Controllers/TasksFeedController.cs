using Matdo.Web.Services.Calendar;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Matdo.Web.Controllers;

/// <summary>Öffentlicher (token-geschützter) iCal-Feed der eigenen Aufgaben zum Abonnieren.</summary>
[AllowAnonymous]
[Route("feed")]
public class TasksFeedController : Controller
{
    private readonly IcalExportService _export;
    public TasksFeedController(IcalExportService export) => _export = export;

    [HttpGet("{token}.ics")]
    public async Task<IActionResult> Tasks(string token, CancellationToken ct)
    {
        if (!Guid.TryParse(token, out var guid)) return NotFound();
        var ics = await _export.BuildForTokenAsync(guid, ct);
        if (ics is null) return NotFound();
        return Content(ics, "text/calendar; charset=utf-8");
    }

    [HttpGet("project/{token}.ics")]
    public async Task<IActionResult> Project(string token, CancellationToken ct)
    {
        if (!Guid.TryParse(token, out var guid)) return NotFound();
        var ics = await _export.BuildForProjectTokenAsync(guid, ct);
        if (ics is null) return NotFound();
        return Content(ics, "text/calendar; charset=utf-8");
    }
}
