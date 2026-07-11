namespace Matdo.Web.Data.Entities;

/// <summary>
/// Verbindung eines Benutzers zu einem externen Kalender: entweder ein ICS-Abo (read-only)
/// oder ein per OAuth verbundenes Google-/Microsoft-Konto (lesen und optional schreiben).
/// OAuth-Tokens werden verschlüsselt gespeichert.
/// </summary>
public class CalendarConnection : BaseEntity
{
    public long UserId { get; set; }
    public User? User { get; set; }

    public CalendarProvider Provider { get; set; }
    public string DisplayName { get; set; } = string.Empty;

    /// <summary>Anzeigefarbe der Termine im Matdo-Kalender.</summary>
    public string Color { get; set; } = "#246fe0";

    /// <summary>Termine dieser Verbindung in Matdo anzeigen.</summary>
    public bool IsEnabled { get; set; } = true;

    // ----- ICS -----
    public string? IcsUrl { get; set; }

    // ----- OAuth (Google/Microsoft) -----
    /// <summary>Verschlüsseltes Access-Token.</summary>
    public string? AccessTokenEnc { get; set; }
    /// <summary>Verschlüsseltes Refresh-Token.</summary>
    public string? RefreshTokenEnc { get; set; }
    public DateTime? TokenExpiresAt { get; set; }
    /// <summary>Externe Kalender-Id (z.B. "primary").</summary>
    public string? ExternalCalendarId { get; set; }

    /// <summary>Zwei-Wege: Matdo-Aufgaben mit Fälligkeit in diesen Kalender exportieren.</summary>
    public bool ExportTasks { get; set; }

    public DateTime? LastSyncAt { get; set; }
    public string? LastError { get; set; }

    public ICollection<ExternalEvent> Events { get; set; } = new List<ExternalEvent>();

    public bool IsOAuth => Provider is CalendarProvider.Google or CalendarProvider.Microsoft;
}

/// <summary>Zwischengespeicherter externer Termin (für die Anzeige ohne API-Aufruf pro Request).</summary>
public class ExternalEvent : BaseEntity
{
    public long CalendarConnectionId { get; set; }
    public CalendarConnection? Connection { get; set; }

    public long UserId { get; set; }

    /// <summary>Id des Termins beim Anbieter (für Upsert/Dedup).</summary>
    public string ExternalId { get; set; } = string.Empty;

    public string Title { get; set; } = string.Empty;
    public DateTime StartUtc { get; set; }
    public DateTime EndUtc { get; set; }
    public bool AllDay { get; set; }
}

/// <summary>Verknüpfung einer Matdo-Aufgabe mit einem exportierten Kalendertermin (2-Wege).</summary>
public class TaskCalendarLink : BaseEntity
{
    public long TaskItemId { get; set; }
    public TaskItem? TaskItem { get; set; }

    public long CalendarConnectionId { get; set; }
    public CalendarConnection? Connection { get; set; }

    /// <summary>Id des in den externen Kalender geschriebenen Termins.</summary>
    public string ExternalEventId { get; set; } = string.Empty;
}
