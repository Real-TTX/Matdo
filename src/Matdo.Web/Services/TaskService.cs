using Matdo.Web.Data;
using Matdo.Web.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace Matdo.Web.Services;

/// <summary>
/// Geschäftslogik rund um Aufgaben: Ansichten (Heute/Demnächst/Eingang),
/// CRUD, Unteraufgaben, Etiketten und Erinnerungen.
///
/// Zugriffsebenen:
///  - <see cref="AccessibleTasks"/>: lesend (Eigentümer ODER geteilt, egal welche Berechtigung).
///  - <see cref="EditableTasks"/>:  schreibend (Eigentümer ODER mit Bearbeiten-Recht geteilt).
///  - Löschen ist ausschließlich dem Eigentümer vorbehalten.
/// </summary>
public class TaskService
{
    private readonly MatdoDbContext _db;
    private readonly ICurrentUserAccessor _me;

    public TaskService(MatdoDbContext db, ICurrentUserAccessor me)
    {
        _db = db;
        _me = me;
    }

    private long Uid => _me.UserId ?? throw new InvalidOperationException("Kein angemeldeter Benutzer.");

    /// <summary>Aufgaben, die der Benutzer sehen darf (eigene + geteilte, jede Berechtigung).</summary>
    public IQueryable<TaskItem> AccessibleTasks()
    {
        var uid = Uid;
        return _db.Tasks
            .Where(t =>
                t.OwnerId == uid
                || t.AssigneeId == uid
                || (t.ProjectId != null && t.Project!.OwnerId == uid)
                || t.Shares.Any(s => s.SharedWithUserId == uid)
                || (t.ProjectId != null && t.Project!.Shares.Any(s => s.SharedWithUserId == uid))
                || (t.ProjectId != null && t.Project!.TeamId != null && t.Project.Team!.Members.Any(m => m.UserId == uid)));
    }

    /// <summary>Aufgaben, die der Benutzer bearbeiten darf (eigene + mit Bearbeiten-Recht geteilt).</summary>
    public IQueryable<TaskItem> EditableTasks()
    {
        var uid = Uid;
        return _db.Tasks
            .Where(t =>
                t.OwnerId == uid
                || t.AssigneeId == uid
                || (t.ProjectId != null && t.Project!.OwnerId == uid)
                || t.Shares.Any(s => s.SharedWithUserId == uid && s.Permission == SharePermission.Edit)
                || (t.ProjectId != null && t.Project!.Shares.Any(s => s.SharedWithUserId == uid && s.Permission == SharePermission.Edit))
                || (t.ProjectId != null && t.Project!.TeamId != null && t.Project.Team!.Members.Any(m => m.UserId == uid)));
    }

    private IQueryable<TaskItem> WithDetails(IQueryable<TaskItem> q) => q
        .Include(t => t.Project)
        .Include(t => t.KanbanColumn)
        .Include(t => t.Assignee)
        .Include(t => t.TaskLabels).ThenInclude(tl => tl.Label)
        .Include(t => t.Reminders)
        .Include(t => t.SubTasks)
        .AsSplitQuery(); // vermeidet kartesische Explosion bei mehreren Collection-Includes

    public Task<TaskItem?> GetAsync(long id) =>
        WithDetails(AccessibleTasks()).FirstOrDefaultAsync(t => t.Id == id);

    // ----- Ansichten (lesend) -----

    /// <summary>Überfällige + heute fällige Aufgaben (Ansicht "Heute").</summary>
    public async Task<List<TaskItem>> GetTodayAsync()
    {
        var endOfTodayUtc = DateTime.Today.AddDays(1).ToUniversalTime();
        return await WithDetails(AccessibleTasks())
            .Where(t => !t.IsCompleted && t.ParentTaskId == null && t.DueDate != null && t.DueDate < endOfTodayUtc)
            .OrderBy(t => t.DueDate).ThenBy(t => t.Priority)
            .ToListAsync();
    }

    /// <summary>Aufgaben ab morgen (Ansicht "Demnächst").</summary>
    public async Task<List<TaskItem>> GetUpcomingAsync(int days = 30)
    {
        var startUtc = DateTime.Today.AddDays(1).ToUniversalTime();
        var endUtc = DateTime.Today.AddDays(1 + days).ToUniversalTime();
        return await WithDetails(AccessibleTasks())
            .Where(t => !t.IsCompleted && t.ParentTaskId == null && t.DueDate != null && t.DueDate >= startUtc && t.DueDate < endUtc)
            .OrderBy(t => t.DueDate).ThenBy(t => t.Priority)
            .ToListAsync();
    }

    /// <summary>Aufgaben ohne Projekt (Ansicht "Eingang").</summary>
    public async Task<List<TaskItem>> GetInboxAsync()
    {
        var uid = Uid;
        return await WithDetails(AccessibleTasks())
            .Where(t => !t.IsCompleted && t.ParentTaskId == null && t.ProjectId == null && t.OwnerId == uid)
            .OrderBy(t => t.Priority).ThenBy(t => t.Position)
            .ToListAsync();
    }

    public async Task<List<TaskItem>> GetByProjectAsync(long projectId, bool includeCompleted = false)
    {
        return await WithDetails(AccessibleTasks())
            .Where(t => t.ProjectId == projectId && t.ParentTaskId == null && (includeCompleted || !t.IsCompleted))
            .OrderBy(t => t.Position).ThenBy(t => t.Priority)
            .ToListAsync();
    }

    public async Task<List<TaskItem>> GetByLabelAsync(long labelId)
    {
        return await WithDetails(AccessibleTasks())
            .Where(t => !t.IsCompleted && t.TaskLabels.Any(tl => tl.LabelId == labelId))
            .OrderBy(t => t.DueDate).ThenBy(t => t.Priority)
            .ToListAsync();
    }

    public async Task<List<TaskItem>> SearchAsync(string term)
    {
        term = term.Trim();
        if (string.IsNullOrEmpty(term)) return new();
        var like = $"%{term}%";
        return await WithDetails(AccessibleTasks())
            .Where(t => EF.Functions.ILike(t.Title, like) || (t.Description != null && EF.Functions.ILike(t.Description, like)))
            .OrderBy(t => t.IsCompleted).ThenBy(t => t.DueDate)
            .Take(100)
            .ToListAsync();
    }

    // ----- Validierung von Fremdschlüsseln (verhindert Cross-Tenant-Zuordnung) -----

    private async Task<long?> ValidatedProjectIdAsync(long? projectId)
    {
        if (projectId is not long pid) return null;
        var uid = Uid;
        var ok = await _db.Projects.AnyAsync(p => p.Id == pid && (
            p.OwnerId == uid
            || p.Shares.Any(s => s.SharedWithUserId == uid && s.Permission == SharePermission.Edit)
            || (p.TeamId != null && p.Team!.Members.Any(m => m.UserId == uid))));
        return ok ? pid : null;
    }

    private async Task<long?> ValidatedColumnIdAsync(long? columnId, long? projectId)
    {
        if (columnId is not long cid || projectId is not long pid) return null;
        var ok = await _db.KanbanColumns.AnyAsync(c => c.Id == cid && c.ProjectId == pid);
        return ok ? cid : null;
    }

    private async Task<long?> ValidatedParentIdAsync(long? parentId)
    {
        if (parentId is not long pid) return null;
        return await EditableTasks().AnyAsync(t => t.Id == pid) ? pid : null;
    }

    private async Task<long?> ValidatedAssigneeAsync(long? assigneeId, long? current = null)
    {
        if (assigneeId is not long aid) return null;
        // Bestehende Zuweisung beibehalten – ein Bearbeiter ohne gemeinsame Sicht auf den
        // Zugewiesenen darf ihn nicht versehentlich entfernen.
        if (current == aid) return aid;
        var uid = Uid;
        // Sich selbst oder einen Mitstreiter (Team/Freigabe) zuweisen.
        if (aid == uid) return await _db.Users.AnyAsync(u => u.Id == aid && u.IsActive) ? aid : null;
        return await Collaborators.Query(_db, uid).AnyAsync(u => u.Id == aid) ? aid : null;
    }

    private async Task<List<long>> OwnedLabelIdsAsync(IEnumerable<long> labelIds)
    {
        var set = labelIds.Distinct().ToList();
        if (set.Count == 0) return set;
        var uid = Uid;
        return await _db.Labels.Where(l => l.OwnerId == uid && set.Contains(l.Id)).Select(l => l.Id).ToListAsync();
    }

    private async Task<int> NextPositionAsync(long? projectId)
    {
        var uid = Uid;
        var max = await _db.Tasks
            .Where(t => t.OwnerId == uid && t.ProjectId == projectId && t.ParentTaskId == null)
            .Select(t => (int?)t.Position)
            .MaxAsync();
        return (max ?? -1) + 1;
    }

    // ----- Mutationen -----

    public async Task<TaskItem> CreateAsync(TaskItem task, IEnumerable<long>? labelIds = null)
    {
        task.OwnerId = Uid;
        task.ProjectId = await ValidatedProjectIdAsync(task.ProjectId);
        task.KanbanColumnId = await ValidatedColumnIdAsync(task.KanbanColumnId, task.ProjectId);
        task.ParentTaskId = await ValidatedParentIdAsync(task.ParentTaskId);
        task.AssigneeId = await ValidatedAssigneeAsync(task.AssigneeId);
        task.Position = await NextPositionAsync(task.ProjectId);

        _db.Tasks.Add(task);
        await _db.SaveChangesAsync();

        if (labelIds != null)
            await SetLabelsAsync(task.Id, labelIds);
        return task;
    }

    public async Task UpdateAsync(TaskItem updated, IEnumerable<long>? labelIds = null)
    {
        var task = await EditableTasks().FirstOrDefaultAsync(t => t.Id == updated.Id)
                   ?? throw new InvalidOperationException("Aufgabe nicht gefunden oder keine Bearbeitungsberechtigung.");

        task.Title = updated.Title;
        task.Description = updated.Description;
        task.ProjectId = await ValidatedProjectIdAsync(updated.ProjectId);
        task.KanbanColumnId = await ValidatedColumnIdAsync(updated.KanbanColumnId, task.ProjectId);
        task.DueDate = updated.DueDate;
        task.DueHasTime = updated.DueHasTime;
        task.DeadlineDate = updated.DeadlineDate;
        task.DeadlineHasTime = updated.DeadlineHasTime;
        task.Priority = updated.Priority;
        task.RecurrenceUnit = updated.RecurrenceUnit;
        task.RecurrenceInterval = updated.RecurrenceInterval < 1 ? 1 : updated.RecurrenceInterval;
        task.ParentTaskId = await ValidatedParentIdAsync(updated.ParentTaskId);
        task.AssigneeId = await ValidatedAssigneeAsync(updated.AssigneeId, task.AssigneeId);

        await _db.SaveChangesAsync();
        if (labelIds != null)
            await SetLabelsAsync(task.Id, labelIds);
    }

    public async Task SetCompletedAsync(long id, bool completed)
    {
        var task = await EditableTasks().FirstOrDefaultAsync(t => t.Id == id);
        if (task is null) return;
        // Wiederkehrende Aufgabe mit Fälligkeit: Abhaken erzeugt die nächste Fälligkeit statt Abschluss.
        if (completed && task.RecurrenceUnit != RecurrenceUnit.None && task.DueDate.HasValue)
        {
            task.DueDate = AdvanceDue(task.DueDate.Value, task.RecurrenceUnit, Math.Max(1, task.RecurrenceInterval));
            task.IsCompleted = false;
            task.CompletedAt = null;
        }
        else
        {
            task.IsCompleted = completed;
            task.CompletedAt = completed ? DateTime.UtcNow : null;
        }
        await _db.SaveChangesAsync();
    }

    private static DateTime AdvanceDue(DateTime d, RecurrenceUnit u, int n) => u switch
    {
        RecurrenceUnit.Day => d.AddDays(n),
        RecurrenceUnit.Week => d.AddDays(7 * n),
        RecurrenceUnit.Month => d.AddMonths(n),
        RecurrenceUnit.Year => d.AddYears(n),
        _ => d
    };

    /// <summary>Löschen ist nur dem Eigentümer erlaubt.</summary>
    public async Task DeleteAsync(long id)
    {
        var uid = Uid;
        var task = await _db.Tasks.FirstOrDefaultAsync(t => t.Id == id && t.OwnerId == uid);
        if (task is null) return;
        _db.Tasks.Remove(task);
        await _db.SaveChangesAsync();
    }

    /// <summary>Dupliziert eine Aufgabe (Kernfelder + eigene Etiketten + Unteraufgaben) als neue eigene Aufgabe.</summary>
    public async Task<long?> DuplicateAsync(long id)
    {
        var src = await EditableTasks()
            .Include(t => t.TaskLabels)
            .Include(t => t.SubTasks)
            .FirstOrDefaultAsync(t => t.Id == id);
        if (src is null) return null;

        var labelIds = src.TaskLabels.Select(l => l.LabelId).ToList();
        var copy = await CreateAsync(new TaskItem
        {
            Title = src.Title,
            Description = src.Description,
            ProjectId = src.ProjectId,
            KanbanColumnId = src.KanbanColumnId,
            DueDate = src.DueDate,
            DueHasTime = src.DueHasTime,
            DeadlineDate = src.DeadlineDate,
            DeadlineHasTime = src.DeadlineHasTime,
            Priority = src.Priority,
            AssigneeId = src.AssigneeId
        }, labelIds);

        foreach (var s in src.SubTasks.OrderBy(x => x.Position).ThenBy(x => x.Id))
        {
            await CreateAsync(new TaskItem
            {
                Title = s.Title,
                Description = s.Description,
                ProjectId = copy.ProjectId,
                ParentTaskId = copy.Id,
                Priority = s.Priority,
                DueDate = s.DueDate,
                DueHasTime = s.DueHasTime
            });
        }
        return copy.Id;
    }

    public async Task MoveToColumnAsync(long taskId, long? columnId, int position)
    {
        var task = await EditableTasks().FirstOrDefaultAsync(t => t.Id == taskId);
        if (task is null) return;
        task.KanbanColumnId = await ValidatedColumnIdAsync(columnId, task.ProjectId);
        task.Position = position;
        await _db.SaveChangesAsync();
    }

    // ----- Etiketten -----

    public async Task SetLabelsAsync(long taskId, IEnumerable<long> labelIds)
    {
        if (!await EditableTasks().AnyAsync(t => t.Id == taskId)) return;

        // Persönliche Labels: nur eigene Labels verknüpfen UND nur die EIGENEN Verknüpfungen
        // anfassen – fremde Label-Verknüpfungen an einer geteilten Aufgabe bleiben unberührt.
        var uid = Uid;
        var wanted = (await OwnedLabelIdsAsync(labelIds)).ToHashSet();
        var existing = await _db.TaskLabels
            .Where(tl => tl.TaskItemId == taskId && tl.Label!.OwnerId == uid)
            .ToListAsync();

        foreach (var tl in existing.Where(tl => !wanted.Contains(tl.LabelId)))
            _db.TaskLabels.Remove(tl);

        var have = existing.Select(tl => tl.LabelId).ToHashSet();
        foreach (var lid in wanted.Where(l => !have.Contains(l)))
            _db.TaskLabels.Add(new TaskLabel { TaskItemId = taskId, LabelId = lid });

        await _db.SaveChangesAsync();
    }

    // ----- Erinnerungen -----

    public async Task AddReminderAsync(long taskId, Reminder reminder)
    {
        if (!await EditableTasks().AnyAsync(t => t.Id == taskId)) return;
        reminder.TaskItemId = taskId;
        _db.Reminders.Add(reminder);
        await _db.SaveChangesAsync();
    }

    public async Task RemoveReminderAsync(long reminderId)
    {
        var r = await _db.Reminders.FindAsync(reminderId);
        if (r is null) return;
        if (!await EditableTasks().AnyAsync(t => t.Id == r.TaskItemId)) return;
        _db.Reminders.Remove(r);
        await _db.SaveChangesAsync();
    }

    public async Task<int> CountTodayAsync()
    {
        var endOfTodayUtc = DateTime.Today.AddDays(1).ToUniversalTime();
        return await AccessibleTasks()
            .CountAsync(t => !t.IsCompleted && t.ParentTaskId == null && t.DueDate != null && t.DueDate < endOfTodayUtc);
    }

    public async Task<int> CountInboxAsync()
    {
        var uid = Uid;
        return await AccessibleTasks()
            .CountAsync(t => !t.IsCompleted && t.ParentTaskId == null && t.ProjectId == null && t.OwnerId == uid);
    }
}
