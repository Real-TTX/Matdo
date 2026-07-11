using Matdo.Web.Data;
using Matdo.Web.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace Matdo.Web.Services;

/// <summary>
/// Token-gesteuerter Zugriff auf EIN Projekt für anonyme Nutzer (öffentlicher Freigabe-Link).
/// Nutzt bewusst KEIN ICurrentUserAccessor – jede Operation ist streng auf das Token-Projekt begrenzt.
/// </summary>
public class AnonymousShareService
{
    private readonly MatdoDbContext _db;
    public AnonymousShareService(MatdoDbContext db) => _db = db;

    // Grenzen für den öffentlichen (anonymen) Schreibpfad. Der Link-Inhaber ist zwar berechtigt,
    // darf aber weder das Projekt fluten noch übergroße Einträge speichern (Ressourcen-Schutz).
    private const int MaxTitle = 500;
    private const int MaxDescription = 10_000;
    private const int MaxName = 60;
    private const int MaxTasksPerProject = 2_000;
    private const int MaxSubTasksPerTask = 200;

    private static string Clamp(string s, int max) => s.Length > max ? s[..max] : s;

    public Task<Project?> GetProjectAsync(Guid token) =>
        _db.Projects.FirstOrDefaultAsync(p => p.AnonymousToken == token && !p.IsArchived);

    public Task<List<KanbanColumn>> GetColumnsAsync(long projectId) =>
        _db.KanbanColumns.Where(c => c.ProjectId == projectId).OrderBy(c => c.Position).ThenBy(c => c.Id).ToListAsync();

    /// <summary>Top-Level-Aufgaben des Projekts inklusive ihrer Unteraufgaben.</summary>
    public Task<List<TaskItem>> GetTasksAsync(long projectId, bool includeCompleted = false) =>
        _db.Tasks
            .Where(t => t.ProjectId == projectId && t.ParentTaskId == null && (includeCompleted || !t.IsCompleted))
            .Include(t => t.SubTasks.OrderBy(s => s.Position).ThenBy(s => s.Id))
            .OrderBy(t => t.Position).ThenBy(t => t.Id)
            .ToListAsync();

    /// <summary>Beliebige Aufgabe (Top-Level ODER Unteraufgabe) – streng auf das Token-Projekt begrenzt.</summary>
    public Task<TaskItem?> GetTaskAsync(long projectId, long taskId) =>
        _db.Tasks.FirstOrDefaultAsync(t => t.Id == taskId && t.ProjectId == projectId);

    private Task<bool> ColumnInProjectAsync(long projectId, long columnId) =>
        _db.KanbanColumns.AnyAsync(c => c.Id == columnId && c.ProjectId == projectId);

    public async Task<long?> AddTaskAsync(Project project, string title, DateTime? dueUtc, bool dueHasTime,
        DateTime? deadlineUtc, bool deadlineHasTime, TaskPriority priority, long? columnId, string anonName)
    {
        title = Clamp(title.Trim(), MaxTitle);
        if (title.Length == 0) return null;
        var pos = await _db.Tasks.CountAsync(t => t.ProjectId == project.Id && t.ParentTaskId == null);
        if (pos >= MaxTasksPerProject) return null;   // Obergrenze je Projekt gegen Flut über den Link

        long? col = null;
        if (columnId is long cid && await ColumnInProjectAsync(project.Id, cid)) col = cid;

        var task = new TaskItem
        {
            OwnerId = project.OwnerId,          // die Aufgabe gehört dem Projekt-Eigentümer
            ProjectId = project.Id,
            Title = title,
            DueDate = dueUtc,
            DueHasTime = dueHasTime,
            DeadlineDate = deadlineUtc,
            DeadlineHasTime = deadlineHasTime,
            Priority = priority,
            KanbanColumnId = col,
            AnonymousAuthor = Clamp(anonName, MaxName),
            Position = pos
        };
        _db.Tasks.Add(task);
        await _db.SaveChangesAsync();
        return task.Id;
    }

    public async Task AddSubTaskAsync(Project project, long parentTaskId, string title, string anonName)
    {
        title = Clamp(title.Trim(), MaxTitle);
        if (title.Length == 0) return;
        // Elternaufgabe muss eine Top-Level-Aufgabe DES Projekts sein.
        var parentOk = await _db.Tasks.AnyAsync(t => t.Id == parentTaskId && t.ProjectId == project.Id && t.ParentTaskId == null);
        if (!parentOk) return;
        var sib = await _db.Tasks.CountAsync(t => t.ParentTaskId == parentTaskId);
        if (sib >= MaxSubTasksPerTask) return;
        _db.Tasks.Add(new TaskItem
        {
            OwnerId = project.OwnerId,
            ProjectId = project.Id,
            ParentTaskId = parentTaskId,
            Title = title,
            Priority = TaskPriority.P4,
            AnonymousAuthor = Clamp(anonName, MaxName),
            Position = sib
        });
        await _db.SaveChangesAsync();
    }

    public async Task SetCompletedAsync(long projectId, long taskId, bool completed)
    {
        var t = await GetTaskAsync(projectId, taskId);
        if (t is null) return;
        t.IsCompleted = completed;
        t.CompletedAt = completed ? DateTime.UtcNow : null;
        await _db.SaveChangesAsync();
    }

    /// <summary>Verschiebt eine Top-Level-Aufgabe in eine (andere) Kanban-Spalte.</summary>
    public async Task MoveTaskAsync(long projectId, long taskId, long? columnId)
    {
        var t = await _db.Tasks.FirstOrDefaultAsync(x => x.Id == taskId && x.ProjectId == projectId && x.ParentTaskId == null);
        if (t is null) return;
        if (columnId is long cid)
        {
            if (!await ColumnInProjectAsync(projectId, cid)) return;
            t.KanbanColumnId = cid;
        }
        else t.KanbanColumnId = null;
        await _db.SaveChangesAsync();
    }

    public async Task EditTaskAsync(long projectId, long taskId, string title, string? description,
        DateTime? dueUtc, bool dueHasTime, DateTime? deadlineUtc, bool deadlineHasTime,
        TaskPriority priority, long? columnId, string anonName)
    {
        var t = await GetTaskAsync(projectId, taskId);
        if (t is null || string.IsNullOrWhiteSpace(title)) return;
        t.Title = Clamp(title.Trim(), MaxTitle);
        t.Description = string.IsNullOrWhiteSpace(description) ? null : Clamp(description.Trim(), MaxDescription);
        t.DueDate = dueUtc;
        t.DueHasTime = dueHasTime;
        t.DeadlineDate = deadlineUtc;
        t.DeadlineHasTime = deadlineHasTime;
        t.Priority = priority;
        // Spaltenzuordnung nur für Top-Level-Aufgaben.
        if (t.ParentTaskId == null)
        {
            if (columnId is long cid && await ColumnInProjectAsync(projectId, cid)) t.KanbanColumnId = cid;
            else if (columnId is null) t.KanbanColumnId = null;
        }
        t.AnonymousAuthor = Clamp(anonName, MaxName);
        await _db.SaveChangesAsync();
    }
}
