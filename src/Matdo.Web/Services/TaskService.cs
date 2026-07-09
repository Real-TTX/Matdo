using Matdo.Web.Data;
using Matdo.Web.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace Matdo.Web.Services;

/// <summary>
/// Geschäftslogik rund um Aufgaben: Ansichten (Heute/Demnächst/Eingang),
/// CRUD, Unteraufgaben, Etiketten und Erinnerungen.
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

    /// <summary>Alle Aufgaben, auf die der aktuelle Benutzer Zugriff hat (eigene + geteilte).</summary>
    public IQueryable<TaskItem> AccessibleTasks()
    {
        var uid = Uid;
        return _db.Tasks
            .Where(t =>
                t.OwnerId == uid
                || t.Shares.Any(s => s.SharedWithUserId == uid)
                || (t.ProjectId != null && t.Project!.Shares.Any(s => s.SharedWithUserId == uid)));
    }

    private IQueryable<TaskItem> WithDetails(IQueryable<TaskItem> q) => q
        .Include(t => t.Project)
        .Include(t => t.TaskLabels).ThenInclude(tl => tl.Label)
        .Include(t => t.Reminders)
        .Include(t => t.SubTasks);

    public Task<TaskItem?> GetAsync(long id) =>
        WithDetails(AccessibleTasks()).FirstOrDefaultAsync(t => t.Id == id);

    // ----- Ansichten -----

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

    // ----- Mutationen -----

    public async Task<TaskItem> CreateAsync(TaskItem task, IEnumerable<long>? labelIds = null)
    {
        task.OwnerId = Uid;
        _db.Tasks.Add(task);
        await _db.SaveChangesAsync();
        if (labelIds != null)
            await SetLabelsAsync(task.Id, labelIds);
        return task;
    }

    public async Task UpdateAsync(TaskItem updated, IEnumerable<long>? labelIds = null)
    {
        var task = await AccessibleTasks().FirstOrDefaultAsync(t => t.Id == updated.Id)
                   ?? throw new InvalidOperationException("Aufgabe nicht gefunden oder kein Zugriff.");

        task.Title = updated.Title;
        task.Description = updated.Description;
        task.ProjectId = updated.ProjectId;
        task.KanbanColumnId = updated.KanbanColumnId;
        task.DueDate = updated.DueDate;
        task.DueHasTime = updated.DueHasTime;
        task.DeadlineDate = updated.DeadlineDate;
        task.DeadlineHasTime = updated.DeadlineHasTime;
        task.Priority = updated.Priority;
        task.ParentTaskId = updated.ParentTaskId;

        await _db.SaveChangesAsync();
        if (labelIds != null)
            await SetLabelsAsync(task.Id, labelIds);
    }

    public async Task SetCompletedAsync(long id, bool completed)
    {
        var task = await AccessibleTasks().FirstOrDefaultAsync(t => t.Id == id);
        if (task is null) return;
        task.IsCompleted = completed;
        task.CompletedAt = completed ? DateTime.UtcNow : null;
        await _db.SaveChangesAsync();
    }

    public async Task DeleteAsync(long id)
    {
        var task = await AccessibleTasks().FirstOrDefaultAsync(t => t.Id == id);
        if (task is null) return;
        _db.Tasks.Remove(task);
        await _db.SaveChangesAsync();
    }

    public async Task MoveToColumnAsync(long taskId, long? columnId, int position)
    {
        var task = await AccessibleTasks().FirstOrDefaultAsync(t => t.Id == taskId);
        if (task is null) return;
        task.KanbanColumnId = columnId;
        task.Position = position;
        await _db.SaveChangesAsync();
    }

    // ----- Etiketten -----

    public async Task SetLabelsAsync(long taskId, IEnumerable<long> labelIds)
    {
        var wanted = labelIds.Distinct().ToHashSet();
        var existing = await _db.TaskLabels.Where(tl => tl.TaskItemId == taskId).ToListAsync();

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
        reminder.TaskItemId = taskId;
        _db.Reminders.Add(reminder);
        await _db.SaveChangesAsync();
    }

    public async Task RemoveReminderAsync(long reminderId)
    {
        var r = await _db.Reminders.FindAsync(reminderId);
        if (r is null) return;
        var owns = await AccessibleTasks().AnyAsync(t => t.Id == r.TaskItemId);
        if (!owns) return;
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
