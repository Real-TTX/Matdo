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
        AccessibleProjects().OrderByDescending(p => p.IsFavorite).ThenBy(p => p.Position).ThenBy(p => p.Name).ToListAsync();

    /// <summary>Ids der Projekte, die geteilt sind (für die „geteilt"-Kennzeichnung im Picker).</summary>
    public async Task<HashSet<long>> GetSharedProjectIdsAsync()
    {
        var ids = await _db.ProjectShares.Select(s => s.ProjectId).Distinct().ToListAsync();
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
}
