using System.Text.Json;
using Matdo.Web.Data;
using Microsoft.EntityFrameworkCore;
using WebPush;

namespace Matdo.Web.Services;

/// <summary>Versendet Web-Push-Benachrichtigungen an abonnierte Browser/Geräte.</summary>
public class PushSender
{
    private readonly MatdoDbContext _db;
    private readonly JsonConfigService _config;
    private readonly ILogger<PushSender> _logger;

    public PushSender(MatdoDbContext db, JsonConfigService config, ILogger<PushSender> logger)
    {
        _db = db;
        _config = config;
        _logger = logger;
    }

    public bool IsConfigured =>
        _config.Current.Push.Enabled
        && !string.IsNullOrWhiteSpace(_config.Current.Push.PublicKey)
        && !string.IsNullOrWhiteSpace(_config.Current.Push.PrivateKey);

    public async Task SendToUserAsync(long userId, string title, string body, string? url = null, CancellationToken ct = default)
    {
        if (!IsConfigured) return;

        var cfg = _config.Current.Push;
        var vapid = new VapidDetails(cfg.Subject, cfg.PublicKey, cfg.PrivateKey);
        var client = new WebPushClient();

        var payload = JsonSerializer.Serialize(new { title, body, url = url ?? "/" });
        var subs = await _db.PushSubscriptions.Where(s => s.UserId == userId).ToListAsync(ct);

        foreach (var s in subs)
        {
            var sub = new WebPush.PushSubscription(s.Endpoint, s.P256dh, s.Auth);
            try
            {
                await client.SendNotificationAsync(sub, payload, vapid, ct);
            }
            catch (WebPushException ex) when ((int)ex.StatusCode is 404 or 410)
            {
                // Abo ist ungültig -> entfernen.
                _db.PushSubscriptions.Remove(s);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Push-Versand an Abo {Id} fehlgeschlagen.", s.Id);
            }
        }
        await _db.SaveChangesAsync(ct);
    }
}
