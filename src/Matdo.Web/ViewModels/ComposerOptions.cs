namespace Matdo.Web.ViewModels;

/// <summary>Optionen für den Inline-Aufgaben-Composer (<c>_Composer.cshtml</c>).</summary>
public class ComposerOptions
{
    /// <summary>Wohin nach dem Anlegen zurückgeleitet wird.</summary>
    public string ReturnUrl { get; set; } = "/Tasks/Today";

    /// <summary>Vorbelegtes Projekt (z.B. in der Projektansicht).</summary>
    public long? DefaultProjectId { get; set; }

    /// <summary>Fälligkeit standardmäßig auf heute setzen (Ansicht „Heute“).</summary>
    public bool DefaultDueToday { get; set; }

    /// <summary>Eindeutige Instanz-Id, damit mehrere Composer pro Seite kollisionsfrei sind.</summary>
    public string InstanceId { get; set; } = "c" + Guid.NewGuid().ToString("N")[..8];
}
