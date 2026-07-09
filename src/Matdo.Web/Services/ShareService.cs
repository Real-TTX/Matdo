using Matdo.Web.Data;
using Matdo.Web.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace Matdo.Web.Services;

/// <summary>Teilen von Aufgaben und Projekten mit anderen Benutzern (Familie/Freunde).</summary>
public class ShareService
{
    private readonly MatdoDbContext _db;
    private readonly ICurrentUserAccessor _me;

    public ShareService(MatdoDbContext db, ICurrentUserAccessor me)
    {
        _db = db;
        _me = me;
    }

    private long Uid => _me.UserId ?? throw new InvalidOperationException("Kein angemeldeter Benutzer.");

    /// <summary>Benutzer die als Teil-Ziel in Frage kommen (alle außer man selbst).</summary>
    public Task<List<User>> GetShareableUsersAsync() =>
        _db.Users.Where(u => u.IsActive && u.Id != Uid).OrderBy(u => u.DisplayName).ToListAsync();

    // ----- Projekte -----

    public Task<List<ProjectShare>> GetProjectSharesAsync(long projectId) =>
        _db.ProjectShares.Include(s => s.SharedWithUser)
            .Where(s => s.ProjectId == projectId).ToListAsync();

    public async Task ShareProjectAsync(long projectId, long targetUserId, SharePermission permission)
    {
        // Nur der Eigentümer darf teilen.
        var owns = await _db.Projects.AnyAsync(p => p.Id == projectId && p.OwnerId == Uid);
        if (!owns) throw new InvalidOperationException("Nur der Eigentümer kann teilen.");

        var share = await _db.ProjectShares
            .FirstOrDefaultAsync(s => s.ProjectId == projectId && s.SharedWithUserId == targetUserId);
        if (share is null)
        {
            _db.ProjectShares.Add(new ProjectShare { ProjectId = projectId, SharedWithUserId = targetUserId, Permission = permission });
        }
        else
        {
            share.Permission = permission;
        }
        await _db.SaveChangesAsync();
    }

    public async Task UnshareProjectAsync(long projectId, long targetUserId)
    {
        var share = await _db.ProjectShares
            .FirstOrDefaultAsync(s => s.ProjectId == projectId && s.SharedWithUserId == targetUserId);
        if (share is null) return;
        var owns = await _db.Projects.AnyAsync(p => p.Id == projectId && p.OwnerId == Uid);
        if (!owns) return;
        _db.ProjectShares.Remove(share);
        await _db.SaveChangesAsync();
    }

    // ----- Aufgaben -----

    public Task<List<TaskShare>> GetTaskSharesAsync(long taskId) =>
        _db.TaskShares.Include(s => s.SharedWithUser)
            .Where(s => s.TaskItemId == taskId).ToListAsync();

    public async Task ShareTaskAsync(long taskId, long targetUserId, SharePermission permission)
    {
        var owns = await _db.Tasks.AnyAsync(t => t.Id == taskId && t.OwnerId == Uid);
        if (!owns) throw new InvalidOperationException("Nur der Eigentümer kann teilen.");

        var share = await _db.TaskShares
            .FirstOrDefaultAsync(s => s.TaskItemId == taskId && s.SharedWithUserId == targetUserId);
        if (share is null)
        {
            _db.TaskShares.Add(new TaskShare { TaskItemId = taskId, SharedWithUserId = targetUserId, Permission = permission });
        }
        else
        {
            share.Permission = permission;
        }
        await _db.SaveChangesAsync();
    }

    public async Task UnshareTaskAsync(long taskId, long targetUserId)
    {
        var share = await _db.TaskShares
            .FirstOrDefaultAsync(s => s.TaskItemId == taskId && s.SharedWithUserId == targetUserId);
        if (share is null) return;
        var owns = await _db.Tasks.AnyAsync(t => t.Id == taskId && t.OwnerId == Uid);
        if (!owns) return;
        _db.TaskShares.Remove(share);
        await _db.SaveChangesAsync();
    }
}
