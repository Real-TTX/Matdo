namespace Matdo.Web.Data.Entities;

/// <summary>Ein Team / Workspace. Besitzt Mitglieder und kann Projekte besitzen.</summary>
public class Team : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public string Color { get; set; } = "#8300bc";

    /// <summary>Ersteller/Eigentümer des Teams.</summary>
    public long OwnerId { get; set; }
    public User? Owner { get; set; }

    public ICollection<TeamMember> Members { get; set; } = new List<TeamMember>();
    public ICollection<Project> Projects { get; set; } = new List<Project>();
}

/// <summary>Mitgliedschaft eines Benutzers in einem Team, mit Rolle.</summary>
public class TeamMember : BaseEntity
{
    public long TeamId { get; set; }
    public Team? Team { get; set; }

    public long UserId { get; set; }
    public User? User { get; set; }

    public TeamRole Role { get; set; } = TeamRole.Member;
}

/// <summary>
/// Einladung per E-Mail in ein Team oder zu einem Projekt. Existiert der Benutzer bereits,
/// wird direkt zugeordnet; sonst greift die Einladung automatisch bei der Registrierung.
/// </summary>
public class Invitation : BaseEntity
{
    public string Email { get; set; } = string.Empty;   // klein geschrieben
    public Guid Token { get; set; } = Guid.NewGuid();

    // Team-Einladung
    public long? TeamId { get; set; }
    public Team? Team { get; set; }
    public TeamRole TeamRole { get; set; } = TeamRole.Member;

    // Projekt-Einladung (Einzel-Freigabe)
    public long? ProjectId { get; set; }
    public Project? Project { get; set; }
    public SharePermission Permission { get; set; } = SharePermission.Edit;

    public long InvitedByUserId { get; set; }
    public bool Accepted { get; set; }
}
