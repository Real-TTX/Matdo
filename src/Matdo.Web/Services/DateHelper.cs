using System.Globalization;

namespace Matdo.Web.Services;

/// <summary>
/// Hilfsfunktionen zur Umrechnung zwischen Formular-Eingaben (lokale Zeit)
/// und in der Datenbank gespeicherter UTC-Zeit.
/// </summary>
public static class DateHelper
{
    /// <summary>Kombiniert Datum (Pflicht) und optionale Uhrzeit zu einem UTC-Zeitpunkt.</summary>
    public static DateTime? ToUtc(string? date, string? time)
    {
        if (string.IsNullOrWhiteSpace(date)) return null;
        if (!DateTime.TryParse(date, CultureInfo.InvariantCulture, DateTimeStyles.None, out var d))
            return null;

        if (!string.IsNullOrWhiteSpace(time) && TimeSpan.TryParse(time, CultureInfo.InvariantCulture, out var t))
            d = d.Date + t;
        else
            d = d.Date;

        // Eingabe als lokale Zeit interpretieren und nach UTC umrechnen.
        return DateTime.SpecifyKind(d, DateTimeKind.Local).ToUniversalTime();
    }

    /// <summary>UTC-Zeit -> lokaler Datums-String (yyyy-MM-dd) für &lt;input type=date&gt;.</summary>
    public static string ToDateInput(DateTime? utc) =>
        utc?.ToLocalTime().ToString("yyyy-MM-dd") ?? "";

    /// <summary>UTC-Zeit -> lokaler Uhrzeit-String (HH:mm) für &lt;input type=time&gt;.</summary>
    public static string ToTimeInput(DateTime? utc, bool hasTime) =>
        (utc.HasValue && hasTime) ? utc.Value.ToLocalTime().ToString("HH:mm") : "";
}
