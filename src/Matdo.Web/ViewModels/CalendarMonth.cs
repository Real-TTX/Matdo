using Matdo.Web.Data.Entities;

namespace Matdo.Web.ViewModels;

/// <summary>Modell für die Monats-Kalenderansicht (<c>_Calendar.cshtml</c>).</summary>
public class CalendarMonth
{
    public int Year { get; set; }
    public int Month { get; set; }

    /// <summary>Aufgaben (mit Fälligkeit) im sichtbaren Zeitraum.</summary>
    public List<TaskItem> Tasks { get; set; } = new();

    /// <summary>Externe Kalendertermine (aus verbundenen Kalendern), optional.</summary>
    public List<CalendarEventView> Events { get; set; } = new();

    /// <summary>Basis-URL für die Monatsnavigation; {ym} wird durch yyyy-MM ersetzt.</summary>
    public string NavUrlTemplate { get; set; } = "?ym={ym}";

    /// <summary>Optionale URL-Vorlage für "Aufgabe an Tag hinzufügen"; {date} = yyyy-MM-dd.</summary>
    public string? AddUrlTemplate { get; set; }

    public DateTime FirstOfMonth => new(Year, Month, 1);
}

/// <summary>Vereinfachter externer Termin zur Anzeige.</summary>
public record CalendarEventView(string Title, DateTime Start, bool AllDay, string Color);
