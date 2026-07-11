using System.Text;
using Matdo.Web.Data;
using Microsoft.EntityFrameworkCore;

namespace Matdo.Web.Services.Calendar;

/// <summary>Erzeugt einen abonnierbaren iCal-Feed (VCALENDAR) der Aufgaben eines Benutzers.</summary>
public class IcalExportService
{
    private readonly MatdoDbContext _db;
    public IcalExportService(MatdoDbContext db) => _db = db;

    /// <summary>iCal-Feed der eigenen (+ zugewiesenen) Aufgaben eines Benutzers.</summary>
    public async Task<string?> BuildForTokenAsync(Guid token, CancellationToken ct)
    {
        var user = await _db.Users.FirstOrDefaultAsync(u => u.IcalToken == token && u.IsActive, ct);
        if (user is null) return null;

        var fromUtc = DateTime.UtcNow.AddDays(-60);
        var tasks = await _db.Tasks
            .Where(t => (t.OwnerId == user.Id || t.AssigneeId == user.Id)
                        && t.ParentTaskId == null && t.DueDate != null && t.DueDate >= fromUtc)
            .OrderBy(t => t.DueDate).Take(1000)
            .ToListAsync(ct);

        return Render("Matdo", tasks);
    }

    /// <summary>iCal-Feed aller fälligen Aufgaben eines Projekts (per Projekt-Token, z.B. für Teams).</summary>
    public async Task<string?> BuildForProjectTokenAsync(Guid token, CancellationToken ct)
    {
        var project = await _db.Projects.FirstOrDefaultAsync(p => p.IcalToken == token && !p.IsArchived, ct);
        if (project is null) return null;

        var fromUtc = DateTime.UtcNow.AddDays(-60);
        var tasks = await _db.Tasks
            .Where(t => t.ProjectId == project.Id
                        && t.ParentTaskId == null && t.DueDate != null && t.DueDate >= fromUtc)
            .OrderBy(t => t.DueDate).Take(1000)
            .ToListAsync(ct);

        return Render(project.Name, tasks);
    }

    private static string Render(string calName, List<Data.Entities.TaskItem> tasks)
    {
        var sb = new StringBuilder();
        sb.Append("BEGIN:VCALENDAR\r\n");
        sb.Append("VERSION:2.0\r\n");
        sb.Append("PRODID:-//Matdo//Tasks//EN\r\n");
        sb.Append("CALSCALE:GREGORIAN\r\n");
        sb.Append($"X-WR-CALNAME:{Escape(calName)}\r\n");
        var stamp = DateTime.UtcNow.ToString("yyyyMMdd'T'HHmmss'Z'");

        foreach (var t in tasks)
        {
            var due = t.DueDate!.Value;
            sb.Append("BEGIN:VEVENT\r\n");
            sb.Append($"UID:matdo-{t.Id}@matdo\r\n");
            sb.Append($"DTSTAMP:{stamp}\r\n");
            if (t.DueHasTime)
            {
                sb.Append($"DTSTART:{due.ToUniversalTime():yyyyMMdd'T'HHmmss'Z'}\r\n");
                sb.Append($"DTEND:{due.ToUniversalTime().AddHours(1):yyyyMMdd'T'HHmmss'Z'}\r\n");
            }
            else
            {
                var day = due.ToLocalTime().Date;
                sb.Append($"DTSTART;VALUE=DATE:{day:yyyyMMdd}\r\n");
                sb.Append($"DTEND;VALUE=DATE:{day.AddDays(1):yyyyMMdd}\r\n");
            }
            sb.Append($"SUMMARY:{Escape(t.Title)}\r\n");
            if (!string.IsNullOrWhiteSpace(t.Description))
                sb.Append($"DESCRIPTION:{Escape(t.Description)}\r\n");
            if (t.IsCompleted) sb.Append("STATUS:CONFIRMED\r\n");
            sb.Append("END:VEVENT\r\n");
        }

        sb.Append("END:VCALENDAR\r\n");
        return sb.ToString();
    }

    private static string Escape(string s) =>
        s.Replace("\\", "\\\\").Replace(";", "\\;").Replace(",", "\\,")
         .Replace("\r\n", "\\n").Replace("\n", "\\n");
}
