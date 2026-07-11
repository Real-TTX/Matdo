namespace Matdo.Web.Services.Calendar;

/// <summary>Aktualisiert regelmäßig die zwischengespeicherten Kalendertermine und exportiert Aufgaben.</summary>
public class CalendarSyncBackgroundService : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromMinutes(15);

    private readonly IServiceProvider _services;
    private readonly ILogger<CalendarSyncBackgroundService> _logger;

    public CalendarSyncBackgroundService(IServiceProvider services, ILogger<CalendarSyncBackgroundService> logger)
    {
        _services = services;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await Task.Delay(TimeSpan.FromSeconds(20), stoppingToken);
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _services.CreateScope();
                var svc = scope.ServiceProvider.GetRequiredService<CalendarService>();
                await svc.SyncAllAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Kalender-Hintergrundsynchronisation fehlgeschlagen.");
            }
            try { await Task.Delay(Interval, stoppingToken); }
            catch (TaskCanceledException) { break; }
        }
    }
}
