namespace Matdo.Web.Data.Entities;

/// <summary>Ansichtstyp eines Projekts.</summary>
public enum ProjectViewType
{
    List = 0,
    Kanban = 1,
    Calendar = 2
}

/// <summary>Priorität einer Aufgabe (1 = höchste, 4 = keine).</summary>
public enum TaskPriority
{
    P1 = 1,
    P2 = 2,
    P3 = 3,
    P4 = 4
}

/// <summary>Art der Erinnerung.</summary>
public enum ReminderType
{
    /// <summary>Fester Zeitpunkt (Datum &amp; Uhrzeit).</summary>
    DateTime = 0,
    /// <summary>Relativ vor der Fälligkeit (Offset in Minuten).</summary>
    BeforeDue = 1
}

/// <summary>Kanäle über die eine Erinnerung zugestellt wird.</summary>
[Flags]
public enum ReminderChannel
{
    None = 0,
    Email = 1,
    Push = 2,
    Both = Email | Push
}

/// <summary>Berechtigung beim Teilen von Aufgaben/Projekten.</summary>
public enum SharePermission
{
    View = 0,
    Edit = 1
}

/// <summary>Rolle eines Benutzers innerhalb eines Teams.</summary>
public enum TeamRole
{
    Owner = 0,
    Admin = 1,
    Member = 2
}

/// <summary>Anbieter für externe Kalender-Anbindung.</summary>
public enum CalendarProvider
{
    /// <summary>iCal/ICS-Abo (read-only, per URL).</summary>
    Ics = 0,
    Google = 1,
    Microsoft = 2
}
