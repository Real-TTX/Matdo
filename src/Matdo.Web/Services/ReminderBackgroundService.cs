using Matdo.Web.Data;
using Matdo.Web.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace Matdo.Web.Services;

/// <summary>
/// Hintergrunddienst der fällige Erinnerungen prüft und per E-Mail und/oder
/// Web-Push zustellt. Läuft im selben Container – Zustand liegt in Postgres.
/// </summary>
public class ReminderBackgroundService : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromSeconds(60);

    private readonly IServiceProvider _services;
    private readonly ILogger<ReminderBackgroundService> _logger;

    public ReminderBackgroundService(IServiceProvider services, ILogger<ReminderBackgroundService> logger)
    {
        _services = services;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Kurz warten, bis Migrationen/Seed durch sind.
        await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessDueRemindersAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Fehler bei der Verarbeitung von Erinnerungen.");
            }

            try { await Task.Delay(Interval, stoppingToken); }
            catch (TaskCanceledException) { break; }
        }
    }

    private async Task ProcessDueRemindersAsync(CancellationToken ct)
    {
        using var scope = _services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MatdoDbContext>();
        var email = scope.ServiceProvider.GetRequiredService<EmailSender>();
        var push = scope.ServiceProvider.GetRequiredService<PushSender>();

        var now = DateTime.UtcNow;
        var due = await db.Reminders
            .Include(r => r.TaskItem).ThenInclude(t => t!.Owner)
            .Where(r => !r.IsSent && r.RemindAt <= now)
            .OrderBy(r => r.RemindAt)
            .Take(50)
            .ToListAsync(ct);

        foreach (var r in due)
        {
            var task = r.TaskItem;
            if (task is null || task.IsCompleted || task.Owner is null)
            {
                r.IsSent = true;
                r.SentAt = now;
                continue;
            }

            var title = "Erinnerung: " + task.Title;
            var ownerTz = DateHelper.Resolve(task.Owner.TimeZone);
            var body = task.DueDate.HasValue
                ? $"Fällig: {DateHelper.ToLocal(task.DueDate.Value, ownerTz):g}"
                : "Fällige Aufgabe";

            var hasEmail = r.Channel.HasFlag(ReminderChannel.Email);
            var hasPush = r.Channel.HasFlag(ReminderChannel.Push);
            var delivered = false;

            if (hasEmail)
            {
                var html = $"<h2>{System.Net.WebUtility.HtmlEncode(task.Title)}</h2>" +
                           (string.IsNullOrWhiteSpace(task.Description) ? "" : $"<p>{System.Net.WebUtility.HtmlEncode(task.Description)}</p>") +
                           $"<p>{body}</p>";
                if (await email.SendAsync(task.Owner.Email, task.Owner.DisplayName, title, html, ct)) delivered = true;
            }

            if (hasPush)
            {
                await push.SendToUserAsync(task.Owner.Id, title, body, $"/Tasks/Edit?id={task.Id}", ct);
                delivered = true;   // Push ist best-effort (kein Erfolgs-Rückgabewert)
            }

            // Nur als gesendet markieren, wenn tatsächlich zugestellt wurde – sonst beim nächsten
            // Lauf erneut versuchen (kein stiller Verlust z.B. bei SMTP-Ausfall). Ist gar kein Kanal
            // gesetzt, gibt es nichts zu senden -> ebenfalls abschließen.
            if (delivered || (!hasEmail && !hasPush))
            {
                r.IsSent = true;
                r.SentAt = now;
            }
        }

        if (due.Count > 0)
            await db.SaveChangesAsync(ct);
    }
}
