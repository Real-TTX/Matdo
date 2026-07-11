using Matdo.Web.Data;
using Matdo.Web.Data.Entities;

namespace Matdo.Web.Services;

/// <summary>
/// Ermittelt die „Mitstreiter" eines Benutzers auf der Plattform: Personen, mit denen er
/// tatsächlich zusammenarbeitet – gemeinsame Team-Mitgliedschaft oder eine Projekt-/Aufgaben-
/// Freigabe (in beliebiger Richtung). Damit werden @-Zuweisung und Freigabe-Auswahl bei
/// hunderten Nutzern nicht mit fremden Personen geflutet.
/// </summary>
public static class Collaborators
{
    /// <summary>Aktive Mitstreiter des Benutzers (ohne ihn selbst) als abfragbare Menge.</summary>
    public static IQueryable<User> Query(MatdoDbContext db, long uid) =>
        db.Users.Where(u => u.IsActive && u.Id != uid && (
            // Gemeinsame Team-Mitgliedschaft
            db.TeamMembers.Any(m1 => m1.UserId == uid
                && db.TeamMembers.Any(m2 => m2.TeamId == m1.TeamId && m2.UserId == u.Id))
            // Projekt-Freigaben (beide Richtungen)
            || db.ProjectShares.Any(s => s.SharedWithUserId == u.Id && s.Project!.OwnerId == uid)
            || db.ProjectShares.Any(s => s.SharedWithUserId == uid && s.Project!.OwnerId == u.Id)
            // Aufgaben-Freigaben (beide Richtungen)
            || db.TaskShares.Any(s => s.SharedWithUserId == u.Id && s.TaskItem!.OwnerId == uid)
            || db.TaskShares.Any(s => s.SharedWithUserId == uid && s.TaskItem!.OwnerId == u.Id)
        ));
}
