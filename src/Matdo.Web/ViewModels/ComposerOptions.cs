namespace Matdo.Web.ViewModels;

/// <summary>Optionen für den Inline-Aufgaben-Composer (<c>_Composer.cshtml</c>).</summary>
public class ComposerOptions
{
    /// <summary>Wohin nach dem Anlegen zurückgeleitet wird.</summary>
    public string ReturnUrl { get; set; } = "/Tasks/Today";

    /// <summary>Vorbelegtes Projekt (z.B. in der Projektansicht).</summary>
    public long? DefaultProjectId { get; set; }

    /// <summary>Vorbelegter Abschnitt (= Kanban-Spalte). Wenn gesetzt, wird das Projekt fixiert
    /// (kein Projekt-Picker) und die Aufgabe landet in diesem Abschnitt.</summary>
    public long? DefaultColumnId { get; set; }

    /// <summary>Fälligkeit standardmäßig auf heute setzen (Ansicht „Heute“).</summary>
    public bool DefaultDueToday { get; set; }

    /// <summary>Eindeutige Instanz-Id, damit mehrere Composer pro Seite kollisionsfrei sind.</summary>
    public string InstanceId { get; set; } = "c" + Guid.NewGuid().ToString("N")[..8];

    /// <summary>Wenn gesetzt: Composer im Bearbeiten-Modus für diese Aufgabe (statt Anlegen).
    /// Erwartet TaskLabels geladen (für vorausgewählte Etiketten).</summary>
    public Matdo.Web.Data.Entities.TaskItem? EditTask { get; set; }
}
