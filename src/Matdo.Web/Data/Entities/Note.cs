namespace Matdo.Web.Data.Entities;

/// <summary>Eine persönliche Notiz. Bewusst schlank: Titel + Freitext-Body, optional einem
/// Projekt zugeordnet, anheftbar. Zugriff ist rein persönlich (OwnerId) – Team-/Freigabe-
/// Funktionen können später ergänzt werden.</summary>
public class Note : BaseEntity
{
    public string Title { get; set; } = string.Empty;
    /// <summary>Freitext (Markdown-Quelle); wird sicher als Text mit Zeilenumbrüchen dargestellt.</summary>
    public string Body { get; set; } = string.Empty;

    public long OwnerId { get; set; }
    public User? Owner { get; set; }

    /// <summary>Optionale Zuordnung zu einem Projekt (nur zur Organisation; ändert den Zugriff nicht).</summary>
    public long? ProjectId { get; set; }
    public Project? Project { get; set; }

    public bool IsPinned { get; set; }
}
