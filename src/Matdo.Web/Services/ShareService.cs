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

    /// <summary>Mitstreiter (Team-Kollegen / Freigabe-Partner) – Zuweisungs-Kandidaten für @Person.</summary>
    public Task<List<User>> GetCollaboratorsAsync() =>
        Collaborators.Query(_db, Uid).OrderBy(u => u.DisplayName).ToListAsync();

    // ----- Projekte -----

    public async Task<List<ProjectShare>> GetProjectSharesAsync(long projectId)
    {
        // Freigaben sieht nur der Eigentümer.
        if (!await _db.Projects.AnyAsync(p => p.Id == projectId && p.OwnerId == Uid)) return new();
        return await _db.ProjectShares.Include(s => s.SharedWithUser)
            .Where(s => s.ProjectId == projectId).ToListAsync();
    }

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

    /// <summary>Ergebnis einer Projekt-Freigabe per E-Mail.</summary>
    public enum ShareByEmailOutcome { Shared, PendingInvite, AlreadyShared, Self, NotOwner }

    /// <summary>
    /// Teilt ein Projekt anhand einer E-Mail-Adresse. Existiert der Benutzer, wird direkt
    /// freigegeben; sonst wird eine ausstehende Einladung angelegt (greift bei Registrierung).
    /// </summary>
    public async Task<ShareByEmailOutcome> ShareProjectByEmailAsync(long projectId, string email, SharePermission permission)
    {
        var uid = Uid;
        if (!await _db.Projects.AnyAsync(p => p.Id == projectId && p.OwnerId == uid))
            return ShareByEmailOutcome.NotOwner;

        email = (email ?? "").Trim().ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(email)) throw new InvalidOperationException("E-Mail-Adresse fehlt.");

        var user = await _db.Users.FirstOrDefaultAsync(u => u.Email == email);
        if (user != null)
        {
            if (user.Id == uid) return ShareByEmailOutcome.Self;
            var existing = await _db.ProjectShares
                .FirstOrDefaultAsync(s => s.ProjectId == projectId && s.SharedWithUserId == user.Id);
            if (existing != null)
            {
                existing.Permission = permission;
                await _db.SaveChangesAsync();
                return ShareByEmailOutcome.AlreadyShared;
            }
            _db.ProjectShares.Add(new ProjectShare { ProjectId = projectId, SharedWithUserId = user.Id, Permission = permission });
            await _db.SaveChangesAsync();
            return ShareByEmailOutcome.Shared;
        }

        var pending = await _db.Invitations.AnyAsync(i => i.Email == email && i.ProjectId == projectId && !i.Accepted);
        if (!pending)
        {
            _db.Invitations.Add(new Invitation
            {
                Email = email,
                ProjectId = projectId,
                Permission = permission,
                InvitedByUserId = uid
            });
            await _db.SaveChangesAsync();
        }
        return ShareByEmailOutcome.PendingInvite;
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

    /// <summary>Ausstehende (noch nicht registrierte) E-Mail-Einladungen zu einem Projekt.</summary>
    public async Task<List<Invitation>> GetProjectPendingInvitesAsync(long projectId)
    {
        if (!await _db.Projects.AnyAsync(p => p.Id == projectId && p.OwnerId == Uid)) return new();
        return await _db.Invitations
            .Where(i => i.ProjectId == projectId && !i.Accepted)
            .OrderBy(i => i.Email).ToListAsync();
    }

    public async Task CancelProjectInviteAsync(long invitationId)
    {
        var inv = await _db.Invitations.FirstOrDefaultAsync(i => i.Id == invitationId && i.ProjectId != null);
        if (inv is null) return;
        var owns = await _db.Projects.AnyAsync(p => p.Id == inv.ProjectId && p.OwnerId == Uid);
        if (!owns) return;
        _db.Invitations.Remove(inv);
        await _db.SaveChangesAsync();
    }

    // ----- Aufgaben -----

    public async Task<List<TaskShare>> GetTaskSharesAsync(long taskId)
    {
        if (!await _db.Tasks.AnyAsync(t => t.Id == taskId && t.OwnerId == Uid)) return new();
        return await _db.TaskShares.Include(s => s.SharedWithUser)
            .Where(s => s.TaskItemId == taskId).ToListAsync();
    }

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
