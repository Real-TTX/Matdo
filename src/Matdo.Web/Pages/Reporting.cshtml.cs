using Matdo.Web.Data;
using Matdo.Web.Services;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace Matdo.Web.Pages;

public class ReportingModel : PageModel
{
    private readonly MatdoDbContext _db;
    private readonly ICurrentUserAccessor _me;

    public ReportingModel(MatdoDbContext db, ICurrentUserAccessor me)
    {
        _db = db;
        _me = me;
    }

    public int Open { get; set; }
    public int CompletedTotal { get; set; }
    public int CompletedLast7 { get; set; }
    public int Overdue { get; set; }
    public List<(string Name, string Color, int Count)> ByProject { get; set; } = new();
    public int[] Last7Days { get; set; } = new int[7];
    public string[] Last7Labels { get; set; } = new string[7];

    public async Task OnGetAsync()
    {
        var uid = _me.UserId!.Value;
        var mine = _db.Tasks.Where(t => t.OwnerId == uid && t.ParentTaskId == null);

        Open = await mine.CountAsync(t => !t.IsCompleted);
        CompletedTotal = await mine.CountAsync(t => t.IsCompleted);

        var weekAgo = DateTime.UtcNow.AddDays(-7);
        CompletedLast7 = await mine.CountAsync(t => t.IsCompleted && t.CompletedAt >= weekAgo);

        var nowUtc = DateTime.Today.ToUniversalTime();
        Overdue = await mine.CountAsync(t => !t.IsCompleted && t.DueDate != null && t.DueDate < nowUtc);

        var byProject = await _db.Tasks
            .Where(t => t.OwnerId == uid && !t.IsCompleted && t.ParentTaskId == null && t.Project != null)
            .GroupBy(t => new { t.Project!.Name, t.Project.Color })
            .Select(g => new { g.Key.Name, g.Key.Color, Count = g.Count() })
            .OrderByDescending(x => x.Count)
            .Take(8)
            .ToListAsync();
        ByProject = byProject.Select(x => (x.Name, x.Color, x.Count)).ToList();

        // Erledigte Aufgaben je Tag (letzte 7 Tage)
        for (var i = 0; i < 7; i++)
        {
            var day = DateTime.Today.AddDays(-6 + i);
            var startUtc = day.ToUniversalTime();
            var endUtc = day.AddDays(1).ToUniversalTime();
            Last7Days[i] = await mine.CountAsync(t => t.CompletedAt >= startUtc && t.CompletedAt < endUtc);
            Last7Labels[i] = day.ToString("ddd");
        }
    }
}
