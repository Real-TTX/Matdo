using Matdo.Web.Data;
using Matdo.Web.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace Matdo.Web.Services;

/// <summary>Verwaltung von Projekten, Kanban-Spalten und Favoriten.</summary>
public class ProjectService
{
    private readonly MatdoDbContext _db;
    private readonly ICurrentUserAccessor _me;

    public ProjectService(MatdoDbContext db, ICurrentUserAccessor me)
    {
        _db = db;
        _me = me;
    }

    private long Uid => _me.UserId ?? throw new InvalidOperationException("Kein angemeldeter Benutzer.");

    public IQueryable<Project> AccessibleProjects()
    {
        var uid = Uid;
        return _db.Projects
            .Where(p => !p.IsArchived && (
                p.OwnerId == uid
                || p.Shares.Any(s => s.SharedWithUserId == uid)
                || (p.TeamId != null && p.Team!.Members.Any(m => m.UserId == uid))));
    }

    /// <summary>Projekte, deren Einstellungen der Benutzer verwalten darf (Eigentümer oder Team-Owner/Admin).</summary>
    private IQueryable<Project> ManageableProjects()
    {
        var uid = Uid;
        return _db.Projects.Where(p =>
            p.OwnerId == uid
            || (p.TeamId != null && p.Team!.Members.Any(m => m.UserId == uid && (m.Role == TeamRole.Owner || m.Role == TeamRole.Admin))));
    }

    private Task<bool> CanManageAsync(long projectId) => ManageableProjects().AnyAsync(p => p.Id == projectId);

    public Task<List<Project>> GetAllAsync() =>
        AccessibleProjects().AsNoTracking()
            .OrderByDescending(p => p.IsFavorite).ThenBy(p => p.Position).ThenBy(p => p.Name).ToListAsync();

    /// <summary>Ids der Projekte, die geteilt sind (für die „geteilt"-Kennzeichnung im Picker).
    /// Nur die für den Nutzer sichtbaren Projekte prüfen – kein globaler Scan aller Freigaben.</summary>
    public async Task<HashSet<long>> GetSharedProjectIdsAsync()
    {
        var accessibleIds = AccessibleProjects().Select(p => p.Id);
        var ids = await _db.ProjectShares
            .Where(s => accessibleIds.Contains(s.ProjectId))
            .Select(s => s.ProjectId).Distinct().ToListAsync();
        return ids.ToHashSet();
    }

    public Task<List<Project>> GetFavoritesAsync() =>
        AccessibleProjects().Where(p => p.IsFavorite).OrderBy(p => p.Name).ToListAsync();

    public Task<Project?> GetAsync(long id) =>
        AccessibleProjects().Include(p => p.Columns.OrderBy(c => c.Position)).FirstOrDefaultAsync(p => p.Id == id);

    /// <summary>Zum Bearbeiten/Konfigurieren – Eigentümer oder Team-Owner/Admin.</summary>
    public Task<Project?> GetOwnedAsync(long id) =>
        ManageableProjects().Include(p => p.Columns.OrderBy(c => c.Position))
            .FirstOrDefaultAsync(p => p.Id == id);

    private async Task<long?> ValidTeamIdAsync(long? teamId)
    {
        if (teamId is not long tid) return null;
        var uid = Uid;
        return await _db.TeamMembers.AnyAsync(m => m.TeamId == tid && m.UserId == uid) ? tid : null;
    }

    private async Task<long?> ValidParentAsync(long? parentId, long excludeId)
    {
        if (parentId is not long pid || pid == excludeId) return null;

        var map = await AccessibleProjects()
            .Select(p => new { p.Id, p.ParentProjectId })
            .ToDictionaryAsync(x => x.Id, x => x.ParentProjectId);
        if (!map.ContainsKey(pid)) return null; // kein Zugriff auf das Eltern-Projekt

        // Zyklus verhindern: das bearbeitete Projekt darf kein Vorfahre des gewählten Elternteils sein.
        long? cur = pid;
        for (var guard = 0; cur is long c && guard < 50; guard++)
        {
            if (c == excludeId) return null;
            cur = map.TryGetValue(c, out var parent) ? parent : null;
        }
        return pid;
    }

    public async Task<Project> CreateAsync(Project project)
    {
        project.OwnerId = Uid;
        project.TeamId = await ValidTeamIdAsync(project.TeamId);
        project.ParentProjectId = await ValidParentAsync(project.ParentProjectId, 0);
        _db.Projects.Add(project);
        await _db.SaveChangesAsync();

        // Für Kanban Standard-Spalten anlegen.
        if (project.ViewType == ProjectViewType.Kanban)
        {
            _db.KanbanColumns.AddRange(
                new KanbanColumn { ProjectId = project.Id, Name = "Zu erledigen", Position = 0 },
                new KanbanColumn { ProjectId = project.Id, Name = "In Arbeit", Position = 1 },
                new KanbanColumn { ProjectId = project.Id, Name = "Erledigt", Position = 2 });
            await _db.SaveChangesAsync();
        }
        return project;
    }

    public async Task UpdateAsync(Project updated)
    {
        var uid = Uid;
        // Projekteinstellungen darf Eigentümer oder Team-Owner/Admin ändern.
        var p = await ManageableProjects().FirstOrDefaultAsync(x => x.Id == updated.Id)
                ?? throw new InvalidOperationException("Projekt nicht gefunden oder kein Zugriff.");
        p.Name = updated.Name;
        p.Color = updated.Color;
        p.ViewType = updated.ViewType;
        p.IsFavorite = updated.IsFavorite;
        p.Position = updated.Position;
        // Nur der Eigentümer darf ein Projekt einem (anderen) Team zuordnen oder daraus lösen –
        // ein Team-Admin könnte sich sonst über die Zielteam-Zuordnung Löschrechte verschaffen
        // oder das Projekt dem Team des Eigentümers entziehen.
        var wantedTeamId = await ValidTeamIdAsync(updated.TeamId);
        if (wantedTeamId != p.TeamId && p.OwnerId == uid)
            p.TeamId = wantedTeamId;
        p.ParentProjectId = await ValidParentAsync(updated.ParentProjectId, p.Id);
        await _db.SaveChangesAsync();

        // Falls auf Kanban umgestellt und noch keine Spalten existieren.
        if (p.ViewType == ProjectViewType.Kanban && !await _db.KanbanColumns.AnyAsync(c => c.ProjectId == p.Id))
        {
            _db.KanbanColumns.AddRange(
                new KanbanColumn { ProjectId = p.Id, Name = "Zu erledigen", Position = 0 },
                new KanbanColumn { ProjectId = p.Id, Name = "In Arbeit", Position = 1 },
                new KanbanColumn { ProjectId = p.Id, Name = "Erledigt", Position = 2 });
            await _db.SaveChangesAsync();
        }
    }

    /// <summary>Erzeugt/erneuert den iCal-Feed-Token eines Projekts (Eigentümer oder Team-Owner/Admin).</summary>
    public async Task<Guid?> SetIcalTokenAsync(long id)
    {
        var p = await ManageableProjects().FirstOrDefaultAsync(x => x.Id == id);
        if (p is null) return null;
        p.IcalToken = Guid.NewGuid();
        await _db.SaveChangesAsync();
        return p.IcalToken;
    }

    /// <summary>Deaktiviert den iCal-Feed eines Projekts (der bestehende Link wird ungültig).</summary>
    public async Task ClearIcalTokenAsync(long id)
    {
        var p = await ManageableProjects().FirstOrDefaultAsync(x => x.Id == id);
        if (p is null || p.IcalToken is null) return;
        p.IcalToken = null;
        await _db.SaveChangesAsync();
    }

    /// <summary>Erzeugt/erneuert den anonymen Freigabe-Token (Eigentümer oder Team-Owner/Admin).</summary>
    public async Task<Guid?> SetAnonymousTokenAsync(long id)
    {
        var p = await ManageableProjects().FirstOrDefaultAsync(x => x.Id == id);
        if (p is null) return null;
        p.AnonymousToken = Guid.NewGuid();
        await _db.SaveChangesAsync();
        return p.AnonymousToken;
    }

    /// <summary>Deaktiviert die anonyme Freigabe (der bestehende Link wird ungültig).</summary>
    public async Task ClearAnonymousTokenAsync(long id)
    {
        var p = await ManageableProjects().FirstOrDefaultAsync(x => x.Id == id);
        if (p is null || p.AnonymousToken is null) return;
        p.AnonymousToken = null;
        await _db.SaveChangesAsync();
    }

    public async Task DeleteAsync(long id)
    {
        var uid = Uid;
        // Eigentümer oder Team-Owner darf löschen.
        var p = await _db.Projects.FirstOrDefaultAsync(x => x.Id == id &&
            (x.OwnerId == uid || (x.TeamId != null && x.Team!.Members.Any(m => m.UserId == uid && m.Role == TeamRole.Owner))));
        if (p is null) return;
        _db.Projects.Remove(p);
        await _db.SaveChangesAsync();
    }

    public async Task RenameAsync(long id, string? name)
    {
        if (string.IsNullOrWhiteSpace(name)) return;
        var p = await ManageableProjects().FirstOrDefaultAsync(x => x.Id == id);
        if (p is null) return;
        p.Name = name.Trim();
        await _db.SaveChangesAsync();
    }

    public async Task SetFavoriteAsync(long id, bool favorite)
    {
        var p = await ManageableProjects().FirstOrDefaultAsync(x => x.Id == id);
        if (p is null) return;
        p.IsFavorite = favorite;
        await _db.SaveChangesAsync();
    }

    public async Task ArchiveAsync(long id)
    {
        var p = await ManageableProjects().FirstOrDefaultAsync(x => x.Id == id);
        if (p is null) return;
        p.IsArchived = true;
        await _db.SaveChangesAsync();
    }

    /// <summary>Archivierte Projekte, die der Benutzer verwalten darf.</summary>
    public Task<List<Project>> GetArchivedAsync() =>
        ManageableProjects().Where(p => p.IsArchived).OrderBy(p => p.Name).ToListAsync();

    public async Task UnarchiveAsync(long id)
    {
        var p = await ManageableProjects().FirstOrDefaultAsync(x => x.Id == id);
        if (p is null) return;
        p.IsArchived = false;
        await _db.SaveChangesAsync();
    }

    /// <summary>Dupliziert ein Projekt inkl. Spalten, Aufgaben (mit Unteraufgaben) und eigenen Etiketten.</summary>
    public async Task<long?> DuplicateAsync(long id)
    {
        var src = await AccessibleProjects().Include(p => p.Columns).FirstOrDefaultAsync(p => p.Id == id);
        if (src is null) return null;
        var uid = Uid;

        var copy = new Project
        {
            Name = src.Name + " (Kopie)",
            Color = src.Color,
            ViewType = src.ViewType,
            TeamId = src.TeamId,
            ParentProjectId = src.ParentProjectId,
            OwnerId = uid,
            IsFavorite = false
        };
        // Spalten als Navigation anhängen – Ids werden beim (einen) SaveChanges gefüllt.
        var colByOld = new Dictionary<long, KanbanColumn>();
        foreach (var c in src.Columns.OrderBy(c => c.Position))
        {
            var nc = new KanbanColumn { Name = c.Name, Position = c.Position };
            copy.Columns.Add(nc);
            colByOld[c.Id] = nc;
        }
        _db.Projects.Add(copy);

        var myLabels = (await _db.Labels.Where(l => l.OwnerId == uid).Select(l => l.Id).ToListAsync()).ToHashSet();
        var tasks = await _db.Tasks
            .Where(t => t.ProjectId == id && t.ParentTaskId == null)
            .Include(t => t.TaskLabels)
            .Include(t => t.SubTasks)
            .OrderBy(t => t.Position)
            .ToListAsync();

        foreach (var t in tasks)
        {
            var nt = Clone(t, uid);
            nt.Project = copy;
            nt.KanbanColumn = t.KanbanColumnId is long oc && colByOld.TryGetValue(oc, out var ncol) ? ncol : null;
            foreach (var tl in t.TaskLabels.Where(x => myLabels.Contains(x.LabelId)))
                nt.TaskLabels.Add(new TaskLabel { LabelId = tl.LabelId });
            foreach (var s in t.SubTasks.OrderBy(x => x.Position))
            {
                var ns = Clone(s, uid);
                ns.Project = copy;
                ns.KanbanColumnId = null;
                nt.SubTasks.Add(ns);
            }
            _db.Tasks.Add(nt);
        }

        // Ein einziger SaveChanges für Kopie, Spalten, Aufgaben, Unteraufgaben und Etiketten
        // (statt einer Schreibrunde pro Zeile). EF füllt die FKs aus den Navigationen.
        await _db.SaveChangesAsync();
        return copy.Id;

        static TaskItem Clone(TaskItem t, long owner) => new()
        {
            Title = t.Title,
            Description = t.Description,
            OwnerId = owner,
            Priority = t.Priority,
            DueDate = t.DueDate,
            DueHasTime = t.DueHasTime,
            DeadlineDate = t.DeadlineDate,
            DeadlineHasTime = t.DeadlineHasTime,
            Position = t.Position,
            IsCompleted = t.IsCompleted,
            CompletedAt = t.CompletedAt
        };
    }

    // ----- Kanban-Spalten -----

    /// <summary>Spalten nur für Projekte, auf die der Benutzer Zugriff hat.</summary>
    public Task<List<KanbanColumn>> GetColumnsAsync(long projectId)
    {
        var uid = Uid;
        return _db.KanbanColumns
            .Where(c => c.ProjectId == projectId && (
                c.Project!.OwnerId == uid
                || c.Project.Shares.Any(s => s.SharedWithUserId == uid)
                || (c.Project.TeamId != null && c.Project.Team!.Members.Any(m => m.UserId == uid))))
            .OrderBy(c => c.Position)
            .ToListAsync();
    }

    /// <summary>Eine Spalte laden, wenn der Benutzer sie verwalten darf – sonst null.</summary>
    public async Task<KanbanColumn?> GetManagedColumnAsync(long columnId)
    {
        var col = await _db.KanbanColumns.FindAsync(columnId);
        if (col is null || !await CanManageAsync(col.ProjectId)) return null;
        return col;
    }

    public async Task<KanbanColumn?> AddColumnAsync(long projectId, string name)
    {
        if (!await CanManageAsync(projectId)) return null;
        var pos = await _db.KanbanColumns.Where(c => c.ProjectId == projectId).CountAsync();
        var col = new KanbanColumn { ProjectId = projectId, Name = name, Position = pos };
        _db.KanbanColumns.Add(col);
        await _db.SaveChangesAsync();
        return col;
    }

    public async Task RenameColumnAsync(long columnId, string name)
    {
        var col = await _db.KanbanColumns.FindAsync(columnId);
        if (col is null || !await CanManageAsync(col.ProjectId)) return;
        col.Name = name;
        await _db.SaveChangesAsync();
    }

    public async Task DeleteColumnAsync(long columnId)
    {
        var col = await _db.KanbanColumns.FindAsync(columnId);
        if (col is null || !await CanManageAsync(col.ProjectId)) return;
        _db.KanbanColumns.Remove(col);
        await _db.SaveChangesAsync();
    }

    /// <summary>Dupliziert einen Abschnitt (Spalte) samt seiner Aufgaben + Unteraufgaben.</summary>
    public async Task<long?> DuplicateColumnAsync(long columnId)
    {
        var col = await _db.KanbanColumns.FindAsync(columnId);
        if (col is null || !await CanManageAsync(col.ProjectId)) return null;
        var uid = Uid;
        var pos = await _db.KanbanColumns.CountAsync(c => c.ProjectId == col.ProjectId);
        var nc = new KanbanColumn { ProjectId = col.ProjectId, Name = col.Name + " (Kopie)", Position = pos };
        _db.KanbanColumns.Add(nc);

        var tasks = await _db.Tasks
            .Where(t => t.KanbanColumnId == columnId && t.ParentTaskId == null)
            .Include(t => t.SubTasks)
            .OrderBy(t => t.Position)
            .ToListAsync();
        foreach (var t in tasks)
        {
            var copy = new TaskItem
            {
                Title = t.Title, Description = t.Description, OwnerId = uid, ProjectId = col.ProjectId,
                KanbanColumn = nc, Priority = t.Priority, DueDate = t.DueDate, DueHasTime = t.DueHasTime,
                DeadlineDate = t.DeadlineDate, DeadlineHasTime = t.DeadlineHasTime, Position = t.Position
            };
            foreach (var s in t.SubTasks.OrderBy(x => x.Position))
                copy.SubTasks.Add(new TaskItem
                {
                    Title = s.Title, Description = s.Description, OwnerId = uid, ProjectId = col.ProjectId,
                    Priority = s.Priority, Position = s.Position
                });
            _db.Tasks.Add(copy);
        }
        // Ein SaveChanges für neue Spalte + Aufgaben + Unteraufgaben (FKs aus Navigationen).
        await _db.SaveChangesAsync();
        return nc.Id;
    }

    /// <summary>Verschiebt einen Abschnitt (Spalte) inkl. Aufgaben in ein anderes verwaltbares Projekt.</summary>
    public async Task MoveColumnToProjectAsync(long columnId, long targetProjectId)
    {
        var col = await _db.KanbanColumns.FindAsync(columnId);
        if (col is null || col.ProjectId == targetProjectId) return;
        if (!await CanManageAsync(col.ProjectId) || !await CanManageAsync(targetProjectId)) return;

        var pos = await _db.KanbanColumns.CountAsync(c => c.ProjectId == targetProjectId);
        // Aufgaben dieser Spalte (Top-Level) + deren Unteraufgaben mitnehmen.
        var topIds = await _db.Tasks.Where(t => t.KanbanColumnId == columnId).Select(t => t.Id).ToListAsync();
        var affected = await _db.Tasks
            .Where(t => t.KanbanColumnId == columnId || (t.ParentTaskId != null && topIds.Contains(t.ParentTaskId.Value)))
            .ToListAsync();
        foreach (var t in affected) t.ProjectId = targetProjectId;
        col.ProjectId = targetProjectId;
        col.Position = pos;
        await _db.SaveChangesAsync();
    }
}
