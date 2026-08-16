using System.Globalization;

namespace Matdo.Web.Services;

/// <summary>
/// Umrechnung zwischen Formular-Eingaben (in der Zeitzone des Nutzers) und in der Datenbank
/// gespeicherter UTC-Zeit. Alle Methoden bekommen die Zeitzone explizit übergeben
/// (aus <see cref="ICurrentUserAccessor.TimeZone"/> bzw. <see cref="Resolve"/>).
/// </summary>
public static class DateHelper
{
    /// <summary>IANA/Windows-Zeitzonen-Id auflösen; leer/ungültig -> Server-Zeitzone.</summary>
    public static TimeZoneInfo Resolve(string? tzId)
    {
        if (!string.IsNullOrWhiteSpace(tzId))
        {
            try { return TimeZoneInfo.FindSystemTimeZoneById(tzId.Trim()); }
            catch { /* unbekannte Id -> Fallback */ }
        }
        return TimeZoneInfo.Local;
    }

    /// <summary>UTC -> lokale Zeit des Nutzers.</summary>
    public static DateTime ToLocal(DateTime utc, TimeZoneInfo tz) =>
        TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utc, DateTimeKind.Utc), tz);

    /// <summary>Lokale Zeit des Nutzers -> UTC. Ungültige (DST-Lücke) Zeiten werden toleriert.</summary>
    public static DateTime LocalToUtc(DateTime local, TimeZoneInfo tz)
    {
        var unspec = DateTime.SpecifyKind(local, DateTimeKind.Unspecified);
        try { return TimeZoneInfo.ConvertTimeToUtc(unspec, tz); }
        // Seltene Sommerzeit-Lücke (diese lokale Zeit existiert nicht): 1 Stunde vorschieben.
        catch (ArgumentException) { return TimeZoneInfo.ConvertTimeToUtc(unspec.AddHours(1), tz); }
    }

    /// <summary>Heutiges Datum in der Zeitzone des Nutzers.</summary>
    public static DateTime TodayLocal(TimeZoneInfo tz) => ToLocal(DateTime.UtcNow, tz).Date;

    /// <summary>Jetzt in der Zeitzone des Nutzers.</summary>
    public static DateTime NowLocal(TimeZoneInfo tz) => ToLocal(DateTime.UtcNow, tz);

    /// <summary>UTC-Zeitpunkt des lokalen Mitternacht-Beginns von heute.</summary>
    public static DateTime StartOfTodayUtc(TimeZoneInfo tz) => LocalToUtc(TodayLocal(tz), tz);

    /// <summary>Kombiniert Datum (Pflicht) und optionale Uhrzeit zu einem UTC-Zeitpunkt (Eingabe in Nutzer-Zeit).</summary>
    public static DateTime? ToUtc(string? date, string? time, TimeZoneInfo tz)
    {
        if (string.IsNullOrWhiteSpace(date)) return null;
        if (!DateTime.TryParse(date, CultureInfo.InvariantCulture, DateTimeStyles.None, out var d))
            return null;

        d = (!string.IsNullOrWhiteSpace(time) && TimeSpan.TryParse(time, CultureInfo.InvariantCulture, out var t))
            ? d.Date + t
            : d.Date;

        return LocalToUtc(d, tz);
    }

    /// <summary>UTC -> lokaler Datums-String (yyyy-MM-dd) für &lt;input type=date&gt;.</summary>
    public static string ToDateInput(DateTime? utc, TimeZoneInfo tz) =>
        utc.HasValue ? ToLocal(utc.Value, tz).ToString("yyyy-MM-dd") : "";

    /// <summary>UTC -> lokaler Uhrzeit-String (HH:mm) für &lt;input type=time&gt;.</summary>
    public static string ToTimeInput(DateTime? utc, bool hasTime, TimeZoneInfo tz) =>
        (utc.HasValue && hasTime) ? ToLocal(utc.Value, tz).ToString("HH:mm") : "";
}
