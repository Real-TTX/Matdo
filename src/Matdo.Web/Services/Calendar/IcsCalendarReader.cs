using System.Net;
using System.Net.Sockets;
using Ical.Net.CalendarComponents;
using Ical.Net.DataTypes;

namespace Matdo.Web.Services.Calendar;

/// <summary>Lädt ein ICS/iCal-Abo per URL und liefert die Termine im gewünschten Zeitraum.</summary>
public class IcsCalendarReader
{
    private readonly IHttpClientFactory _httpFactory;
    private readonly ILogger<IcsCalendarReader> _logger;

    public IcsCalendarReader(IHttpClientFactory httpFactory, ILogger<IcsCalendarReader> logger)
    {
        _httpFactory = httpFactory;
        _logger = logger;
    }

    public async Task<List<SimpleEvent>> ReadAsync(string url, DateTime fromUtc, DateTime toUtc, CancellationToken ct)
    {
        var text = await FetchAsync(url, ct);
        var calendar = Ical.Net.Calendar.Load(text);

        var events = new List<SimpleEvent>();
        var occurrences = calendar.GetOccurrences(new CalDateTime(fromUtc, "UTC"))
            .TakeWhile(o => o.Period.StartTime.AsUtc <= toUtc)
            .Take(5000);

        foreach (var occ in occurrences)
        {
            if (occ.Source is not CalendarEvent ev) continue;
            var start = occ.Period.StartTime;
            var startUtc = start.AsUtc;
            var endUtc = occ.Period.EndTime?.AsUtc ?? startUtc;
            var allDay = !start.HasTime;
            var title = string.IsNullOrWhiteSpace(ev.Summary) ? "(Termin)" : ev.Summary;
            var extId = $"{ev.Uid}@{startUtc:yyyyMMddTHHmmssZ}";
            events.Add(new SimpleEvent(extId, title, startUtc, endUtc, allDay));
        }
        return events;
    }

    /// <summary>
    /// Holt die ICS-Datei mit SSRF-Schutz: nur http(s), Ziel-IP muss öffentlich sein,
    /// Redirects werden manuell (max. 3) verfolgt und jeweils erneut geprüft.
    /// </summary>
    private async Task<string> FetchAsync(string url, CancellationToken ct)
    {
        if (url.StartsWith("webcal://", StringComparison.OrdinalIgnoreCase))
            url = "https://" + url.Substring("webcal://".Length);

        var client = _httpFactory.CreateClient("ics");
        var current = new Uri(url, UriKind.Absolute);

        for (var hop = 0; hop < 4; hop++)
        {
            await EnsurePublicHostAsync(current, ct);
            using var resp = await client.GetAsync(current, HttpCompletionOption.ResponseHeadersRead, ct);

            if ((int)resp.StatusCode is >= 300 and < 400 && resp.Headers.Location is { } loc)
            {
                current = loc.IsAbsoluteUri ? loc : new Uri(current, loc);
                continue;
            }
            resp.EnsureSuccessStatusCode();
            return await resp.Content.ReadAsStringAsync(ct);
        }
        throw new InvalidOperationException("Zu viele Weiterleitungen beim Abruf des Kalenders.");
    }

    private static async Task EnsurePublicHostAsync(Uri uri, CancellationToken ct)
    {
        if (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps)
            throw new InvalidOperationException("Nur http(s)-Adressen sind erlaubt.");

        var addresses = IPAddress.TryParse(uri.Host, out var literal)
            ? new[] { literal }
            : await Dns.GetHostAddressesAsync(uri.Host, ct);

        if (addresses.Length == 0 || addresses.Any(IsPrivate))
            throw new InvalidOperationException("Diese Zieladresse ist nicht erlaubt.");
    }

    private static bool IsPrivate(IPAddress ip)
    {
        if (IPAddress.IsLoopback(ip)) return true;

        if (ip.AddressFamily == AddressFamily.InterNetwork)
        {
            var b = ip.GetAddressBytes();
            return b[0] == 10                                  // 10.0.0.0/8
                || (b[0] == 172 && b[1] >= 16 && b[1] <= 31)   // 172.16.0.0/12
                || (b[0] == 192 && b[1] == 168)                // 192.168.0.0/16
                || (b[0] == 169 && b[1] == 254)                // 169.254.0.0/16 (Link-Local / Metadaten)
                || (b[0] == 100 && b[1] >= 64 && b[1] <= 127)  // 100.64.0.0/10 (CGNAT)
                || b[0] == 0;
        }
        if (ip.AddressFamily == AddressFamily.InterNetworkV6)
        {
            if (ip.IsIPv6LinkLocal || ip.IsIPv6SiteLocal) return true;
            var b = ip.GetAddressBytes();
            if ((b[0] & 0xFE) == 0xFC) return true;            // fc00::/7 (ULA)
            // IPv4-mapped IPv6 -> auf zugrunde liegende v4-Adresse prüfen
            if (ip.IsIPv4MappedToIPv6) return IsPrivate(ip.MapToIPv4());
        }
        return false;
    }
}
