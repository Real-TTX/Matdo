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
            .Where(p => !p.IsArchived && (p.OwnerId == uid || p.Shares.Any(s => s.SharedWithUserId == uid)));
    }

    public Task<List<Project>> GetAllAsync() =>
        AccessibleProjects().OrderByDescending(p => p.IsFavorite).ThenBy(p => p.Position).ThenBy(p => p.Name).ToListAsync();

    public Task<List<Project>> GetFavoritesAsync() =>
        AccessibleProjects().Where(p => p.IsFavorite).OrderBy(p => p.Name).ToListAsync();

    public Task<Project?> GetAsync(long id) =>
        AccessibleProjects().Include(p => p.Columns.OrderBy(c => c.Position)).FirstOrDefaultAsync(p => p.Id == id);

    public async Task<Project> CreateAsync(Project project)
    {
        project.OwnerId = Uid;
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
        var p = await AccessibleProjects().FirstOrDefaultAsync(x => x.Id == updated.Id)
                ?? throw new InvalidOperationException("Projekt nicht gefunden.");
        p.Name = updated.Name;
        p.Color = updated.Color;
        p.ViewType = updated.ViewType;
        p.IsFavorite = updated.IsFavorite;
        p.Position = updated.Position;
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

    public async Task DeleteAsync(long id)
    {
        var p = await _db.Projects.FirstOrDefaultAsync(x => x.Id == id && x.OwnerId == Uid);
        if (p is null) return;
        _db.Projects.Remove(p);
        await _db.SaveChangesAsync();
    }

    // ----- Kanban-Spalten -----

    public Task<List<KanbanColumn>> GetColumnsAsync(long projectId) =>
        _db.KanbanColumns.Where(c => c.ProjectId == projectId).OrderBy(c => c.Position).ToListAsync();

    public async Task<KanbanColumn> AddColumnAsync(long projectId, string name)
    {
        var pos = await _db.KanbanColumns.Where(c => c.ProjectId == projectId).CountAsync();
        var col = new KanbanColumn { ProjectId = projectId, Name = name, Position = pos };
        _db.KanbanColumns.Add(col);
        await _db.SaveChangesAsync();
        return col;
    }

    public async Task RenameColumnAsync(long columnId, string name)
    {
        var col = await _db.KanbanColumns.FindAsync(columnId);
        if (col is null) return;
        col.Name = name;
        await _db.SaveChangesAsync();
    }

    public async Task DeleteColumnAsync(long columnId)
    {
        var col = await _db.KanbanColumns.FindAsync(columnId);
        if (col is null) return;
        _db.KanbanColumns.Remove(col);
        await _db.SaveChangesAsync();
    }
}
