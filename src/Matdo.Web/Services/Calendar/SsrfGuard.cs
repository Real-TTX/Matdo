using System.Net;
using System.Net.Http;
using System.Net.Sockets;

namespace Matdo.Web.Services.Calendar;

/// <summary>
/// SSRF-Schutz für ausgehende Abrufe (ICS-Abos). Kernstück ist <see cref="SafeConnectAsync"/>:
/// als ConnectCallback eines SocketsHttpHandler löst es den Host EINMAL auf, prüft die IP und
/// verbindet sich dann zu genau dieser IP. Dadurch gibt es kein Zeitfenster für DNS-Rebinding
/// (Prüf-IP == Verbindungs-IP) – auch über Redirects hinweg, da jeder Hop hier durchläuft.
/// </summary>
public static class SsrfGuard
{
    /// <summary>Nicht-öffentliche/interne Adressbereiche (Loopback, private Netze, Link-Local/Metadaten, CGNAT, ULA).</summary>
    public static bool IsPrivate(IPAddress ip)
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
            if (ip.IsIPv4MappedToIPv6) return IsPrivate(ip.MapToIPv4());  // IPv4-mapped -> v4 prüfen
        }
        return false;
    }

    /// <summary>ConnectCallback: Host einmal auflösen, alle Adressen müssen öffentlich sein,
    /// dann zu genau diesen Adressen verbinden (keine erneute Auflösung → kein Rebinding).</summary>
    public static async ValueTask<Stream> SafeConnectAsync(SocketsHttpConnectionContext context, CancellationToken ct)
    {
        var ep = context.DnsEndPoint;
        var addresses = IPAddress.TryParse(ep.Host, out var literal)
            ? new[] { literal }
            : await Dns.GetHostAddressesAsync(ep.Host, ct);

        if (addresses.Length == 0 || addresses.Any(IsPrivate))
            throw new IOException("Zieladresse ist nicht erlaubt (SSRF-Schutz).");

        var socket = new Socket(SocketType.Stream, ProtocolType.Tcp) { NoDelay = true };
        try
        {
            await socket.ConnectAsync(addresses, ep.Port, ct);
            return new NetworkStream(socket, ownsSocket: true);
        }
        catch
        {
            socket.Dispose();
            throw;
        }
    }
}
