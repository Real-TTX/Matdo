namespace Matdo.Web.Data.Entities;

/// <summary>Projekt zum Gruppieren von Aufgaben. Besitzt eine Ansicht (Liste/Kanban).</summary>
public class Project : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public string Color { get; set; } = "#8300bc";

    public long OwnerId { get; set; }
    public User? Owner { get; set; }

    /// <summary>Optionales Team, dem das Projekt gehört (null = persönliches Projekt).</summary>
    public long? TeamId { get; set; }
    public Team? Team { get; set; }

    /// <summary>Optionales Eltern-Projekt (nur zur Baum-Darstellung; keine Vererbung).</summary>
    public long? ParentProjectId { get; set; }
    public Project? ParentProject { get; set; }
    public ICollection<Project> Children { get; set; } = new List<Project>();

    public ProjectViewType ViewType { get; set; } = ProjectViewType.List;
    public bool IsFavorite { get; set; }
    public bool IsArchived { get; set; }
    public int Position { get; set; }

    /// <summary>Token für den abonnierbaren iCal-Feed dieses Projekts (null = kein Feed).</summary>
    public Guid? IcalToken { get; set; }

    /// <summary>Token für anonyme Freigabe per Link (null = keine anonyme Freigabe).
    /// Jeder mit dem Link kann nach Namenseingabe die Aufgaben dieses Projekts pflegen.</summary>
    public Guid? AnonymousToken { get; set; }

    public ICollection<KanbanColumn> Columns { get; set; } = new List<KanbanColumn>();
    public ICollection<TaskItem> Tasks { get; set; } = new List<TaskItem>();
    public ICollection<ProjectShare> Shares { get; set; } = new List<ProjectShare>();
}

/// <summary>Vom Benutzer definierbare Kanban-Spalte innerhalb eines Projekts.</summary>
public class KanbanColumn : BaseEntity
{
    public long ProjectId { get; set; }
    public Project? Project { get; set; }

    public string Name { get; set; } = string.Empty;
    public int Position { get; set; }

    public ICollection<TaskItem> Tasks { get; set; } = new List<TaskItem>();
}

/// <summary>Teilen eines ganzen Projekts mit einem anderen Benutzer.</summary>
public class ProjectShare : BaseEntity
{
    public long ProjectId { get; set; }
    public Project? Project { get; set; }

    public long SharedWithUserId { get; set; }
    public User? SharedWithUser { get; set; }

    public SharePermission Permission { get; set; } = SharePermission.Edit;
}
