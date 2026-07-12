namespace Matdo.Web.Data.Entities;

/// <summary>
/// Eine Aufgabe. Unteraufgaben werden über die Selbstreferenz ParentTaskId abgebildet
/// (einzelne Schritte einer Aufgabe).
/// </summary>
public class TaskItem : BaseEntity
{
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }

    public long OwnerId { get; set; }
    public User? Owner { get; set; }

    /// <summary>Zugewiesene Person (Teammitglied). Erhält Zugriff auf die Aufgabe.</summary>
    public long? AssigneeId { get; set; }
    public User? Assignee { get; set; }

    /// <summary>Name eines anonymen Bearbeiters (aus der öffentlichen Projekt-Freigabe), sonst null.</summary>
    public string? AnonymousAuthor { get; set; }

    public long? ProjectId { get; set; }
    public Project? Project { get; set; }

    public long? KanbanColumnId { get; set; }
    public KanbanColumn? KanbanColumn { get; set; }

    /// <summary>Übergeordnete Aufgabe (für Unteraufgaben / Schritte).</summary>
    public long? ParentTaskId { get; set; }
    public TaskItem? ParentTask { get; set; }
    public ICollection<TaskItem> SubTasks { get; set; } = new List<TaskItem>();

    public DateTime? DueDate { get; set; }
    /// <summary>True, wenn DueDate auch eine relevante Uhrzeit enthält.</summary>
    public bool DueHasTime { get; set; }

    public DateTime? DeadlineDate { get; set; }
    public bool DeadlineHasTime { get; set; }

    public TaskPriority Priority { get; set; } = TaskPriority.P4;

    /// <summary>Wiederholung: None = einmalig. Beim Abhaken rückt die Fälligkeit um Interval×Unit vor.</summary>
    public RecurrenceUnit RecurrenceUnit { get; set; } = RecurrenceUnit.None;
    public int RecurrenceInterval { get; set; } = 1;

    public bool IsCompleted { get; set; }
    public DateTime? CompletedAt { get; set; }

    public int Position { get; set; }

    public ICollection<TaskLabel> TaskLabels { get; set; } = new List<TaskLabel>();
    public ICollection<Reminder> Reminders { get; set; } = new List<Reminder>();
    public ICollection<TaskShare> Shares { get; set; } = new List<TaskShare>();
}

/// <summary>Etikett / Tag zum Kategorisieren von Aufgaben.</summary>
public class Label : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public string Color { get; set; } = "#808080";

    public long OwnerId { get; set; }
    public User? Owner { get; set; }

    public bool IsFavorite { get; set; }

    public ICollection<TaskLabel> TaskLabels { get; set; } = new List<TaskLabel>();
}

/// <summary>Verknüpfung Aufgabe &lt;-&gt; Etikett (n:m).</summary>
public class TaskLabel : BaseEntity
{
    public long TaskItemId { get; set; }
    public TaskItem? TaskItem { get; set; }

    public long LabelId { get; set; }
    public Label? Label { get; set; }
}

/// <summary>Erinnerung an eine Aufgabe (fester Zeitpunkt oder vor Fälligkeit).</summary>
public class Reminder : BaseEntity
{
    public long TaskItemId { get; set; }
    public TaskItem? TaskItem { get; set; }

    public ReminderType Type { get; set; }

    /// <summary>Absoluter Auslösezeitpunkt (bei Type = DateTime bzw. berechnet bei BeforeDue).</summary>
    public DateTime RemindAt { get; set; }

    /// <summary>Vorlaufzeit in Minuten (bei Type = BeforeDue).</summary>
    public int OffsetMinutes { get; set; }

    public ReminderChannel Channel { get; set; } = ReminderChannel.Both;

    public bool IsSent { get; set; }
    public DateTime? SentAt { get; set; }
}

/// <summary>Teilen einer einzelnen Aufgabe mit einem anderen Benutzer.</summary>
public class TaskShare : BaseEntity
{
    public long TaskItemId { get; set; }
    public TaskItem? TaskItem { get; set; }

    public long SharedWithUserId { get; set; }
    public User? SharedWithUser { get; set; }

    public SharePermission Permission { get; set; } = SharePermission.Edit;
}
