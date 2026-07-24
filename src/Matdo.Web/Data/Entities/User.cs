namespace Matdo.Web.Data.Entities;

/// <summary>Rolle für einfache Rechteverwaltung (z.B. Admin, User).</summary>
public class Role : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }

    public ICollection<User> Users { get; set; } = new List<User>();

    public const string Admin = "Admin";
    public const string User = "User";
}

/// <summary>Lokaler Benutzer (Anmeldung per E-Mail / Passwort).</summary>
public class User : BaseEntity
{
    public string Email { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;

    public long RoleId { get; set; }
    public Role? Role { get; set; }

    public bool IsActive { get; set; } = true;

    // ----- Konto-Sicherheit (Public-Platform) -----
    /// <summary>Ob die E-Mail-Adresse bestätigt wurde. Bestandsnutzer gelten als bestätigt (Migration).</summary>
    public bool EmailConfirmed { get; set; }
    /// <summary>Einmal-Token für den E-Mail-Bestätigungslink (null = bestätigt/keiner offen).</summary>
    public Guid? EmailConfirmToken { get; set; }
    /// <summary>Einmal-Token für das Zurücksetzen des Passworts (+ Ablaufzeitpunkt).</summary>
    public Guid? PasswordResetToken { get; set; }
    public DateTime? PasswordResetExpiresUtc { get; set; }
    /// <summary>Fehlversuche in Folge; ab Schwellwert greift eine temporäre Sperre.</summary>
    public int FailedLoginCount { get; set; }
    /// <summary>Gesperrt bis (UTC); solange werden Anmeldungen abgelehnt.</summary>
    public DateTime? LockoutUntilUtc { get; set; }

    /// <summary>Optionale Zeitzone (IANA), für Erinnerungen relevant.</summary>
    public string? TimeZone { get; set; }

    // ----- Darstellungs-/Sprach-Einstellungen -----
    /// <summary>Farbschema-Modus: system | light | dark.</summary>
    public string ColorScheme { get; set; } = "system";
    /// <summary>Akzent-Design (Schlüssel aus dem Theme-Katalog), z.B. "purple", "red".</summary>
    public string Theme { get; set; } = "purple";
    /// <summary>Sprachcode (z.B. "de", "en").</summary>
    public string Language { get; set; } = "de";

    /// <summary>Geheimes Token für den abonnierbaren iCal-Feed der eigenen Aufgaben.</summary>
    public Guid? IcalToken { get; set; }

    public ICollection<UserSession> Sessions { get; set; } = new List<UserSession>();
    public ICollection<UserGroupMember> GroupMemberships { get; set; } = new List<UserGroupMember>();
    public ICollection<Project> Projects { get; set; } = new List<Project>();
    public ICollection<TaskItem> Tasks { get; set; } = new List<TaskItem>();
    public ICollection<Label> Labels { get; set; } = new List<Label>();
}

/// <summary>
/// Persistente Session. Der Primärschlüssel "Id" ist intern, das Token (UUID)
/// agiert als Sicherheitsmerkmal und wird im Cookie gespeichert.
/// Sessions liegen in Postgres und überleben daher Container-Neustarts.
/// </summary>
public class UserSession : BaseEntity
{
    public Guid Token { get; set; } = Guid.NewGuid();

    public long UserId { get; set; }
    public User? User { get; set; }

    public DateTime ExpiresAt { get; set; }
    public DateTime LastSeenAt { get; set; } = DateTime.UtcNow;

    public string? UserAgent { get; set; }
    public string? IpAddress { get; set; }
}

/// <summary>Benutzergruppe (z.B. Familie, Freunde) für einfaches Teilen.</summary>
public class UserGroup : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }

    public ICollection<UserGroupMember> Members { get; set; } = new List<UserGroupMember>();
}

/// <summary>Zuordnung Benutzer &lt;-&gt; Gruppe.</summary>
public class UserGroupMember : BaseEntity
{
    public long UserGroupId { get; set; }
    public UserGroup? UserGroup { get; set; }

    public long UserId { get; set; }
    public User? User { get; set; }
}

/// <summary>Web-Push Abonnement eines Browsers/Geräts für Benachrichtigungen.</summary>
public class PushSubscription : BaseEntity
{
    public long UserId { get; set; }
    public User? User { get; set; }

    public string Endpoint { get; set; } = string.Empty;
    public string P256dh { get; set; } = string.Empty;
    public string Auth { get; set; } = string.Empty;
}
