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

    private static string Clamp(string s, int max) => s.Length > max ? s[..max] : s;

    public Task<Project?> GetProjectAsync(Guid token) =>
        _db.Projects.FirstOrDefaultAsync(p => p.AnonymousToken == token && !p.IsArchived);

    public Task<List<TaskItem>> GetTasksAsync(long projectId, bool includeCompleted = false) =>
        _db.Tasks
            .Where(t => t.ProjectId == projectId && t.ParentTaskId == null && (includeCompleted || !t.IsCompleted))
            .OrderBy(t => t.IsCompleted).ThenBy(t => t.Position).ThenBy(t => t.Id)
            .ToListAsync();

    public Task<TaskItem?> GetTaskAsync(long projectId, long taskId) =>
        _db.Tasks.FirstOrDefaultAsync(t => t.Id == taskId && t.ProjectId == projectId && t.ParentTaskId == null);

    public async Task AddTaskAsync(Project project, string title, DateTime? dueUtc, bool dueHasTime, string anonName)
    {
        title = Clamp(title.Trim(), MaxTitle);
        if (title.Length == 0) return;
        var pos = await _db.Tasks.CountAsync(t => t.ProjectId == project.Id && t.ParentTaskId == null);
        if (pos >= MaxTasksPerProject) return;   // Obergrenze je Projekt gegen Flut über den Link
        _db.Tasks.Add(new TaskItem
        {
            OwnerId = project.OwnerId,          // die Aufgabe gehört dem Projekt-Eigentümer
            ProjectId = project.Id,
            Title = title,
            DueDate = dueUtc,
            DueHasTime = dueHasTime,
            AnonymousAuthor = Clamp(anonName, MaxName),
            Position = pos
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

    public async Task EditTaskAsync(long projectId, long taskId, string title, string? description, DateTime? dueUtc, bool dueHasTime, string anonName)
    {
        var t = await GetTaskAsync(projectId, taskId);
        if (t is null || string.IsNullOrWhiteSpace(title)) return;
        t.Title = Clamp(title.Trim(), MaxTitle);
        t.Description = string.IsNullOrWhiteSpace(description) ? null : Clamp(description.Trim(), MaxDescription);
        t.DueDate = dueUtc;
        t.DueHasTime = dueHasTime;
        t.AnonymousAuthor = Clamp(anonName, MaxName);
        await _db.SaveChangesAsync();
    }
}
