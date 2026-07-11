using Matdo.Web.Data;
using Matdo.Web.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace Matdo.Web.Services;

/// <summary>
/// Verwaltung von Teams/Workspaces, Mitgliedschaften und Einladungen per E-Mail.
/// Existiert der eingeladene Benutzer bereits, wird er direkt zugeordnet; sonst wird eine
/// „ausstehende" Einladung angelegt, die bei der Registrierung automatisch greift.
/// </summary>
public class TeamService
{
    private readonly MatdoDbContext _db;
    private readonly ICurrentUserAccessor _me;
    private readonly EmailSender _email;
    private readonly JsonConfigService _config;
    private readonly ILogger<TeamService> _logger;

    public TeamService(MatdoDbContext db, ICurrentUserAccessor me, EmailSender email,
        JsonConfigService config, ILogger<TeamService> logger)
    {
        _db = db;
        _me = me;
        _email = email;
        _config = config;
        _logger = logger;
    }

    private long Uid => _me.UserId ?? throw new InvalidOperationException("Kein angemeldeter Benutzer.");

    /// <summary>Ergebnis einer Einladung: entweder direkt zugeordnet oder als ausstehend gespeichert.</summary>
    public enum InviteOutcome { AddedDirectly, PendingInvite, AlreadyMember, AlreadyInvited, Self }

    // ----- Abfragen -----

    /// <summary>Teams, in denen der aktuelle Benutzer Mitglied ist (inkl. eigener Rolle).</summary>
    public async Task<List<(Team Team, TeamRole Role, int MemberCount)>> GetMyTeamsAsync()
    {
        var uid = Uid;
        var rows = await _db.TeamMembers
            .Where(m => m.UserId == uid)
            .Select(m => new { m.Team, m.Role, Count = m.Team!.Members.Count })
            .ToListAsync();
        return rows
            .Where(r => r.Team != null)
            .OrderBy(r => r.Team!.Name)
            .Select(r => (r.Team!, r.Role, r.Count))
            .ToList();
    }

    /// <summary>Team mit Mitgliedern – nur wenn der aktuelle Benutzer Mitglied ist.</summary>
    public async Task<Team?> GetAsync(long teamId)
    {
        var uid = Uid;
        if (!await IsMemberAsync(teamId)) return null;
        return await _db.Teams
            .Include(t => t.Members).ThenInclude(m => m.User)
            .FirstOrDefaultAsync(t => t.Id == teamId);
    }

    public Task<bool> IsMemberAsync(long teamId) =>
        _db.TeamMembers.AnyAsync(m => m.TeamId == teamId && m.UserId == Uid);

    /// <summary>Owner oder Admin des Teams (darf verwalten).</summary>
    public Task<bool> IsManagerAsync(long teamId)
    {
        var uid = Uid;
        return _db.TeamMembers.AnyAsync(m => m.TeamId == teamId && m.UserId == uid
            && (m.Role == TeamRole.Owner || m.Role == TeamRole.Admin));
    }

    public Task<bool> IsOwnerAsync(long teamId)
    {
        var uid = Uid;
        return _db.TeamMembers.AnyAsync(m => m.TeamId == teamId && m.UserId == uid && m.Role == TeamRole.Owner);
    }

    public async Task<List<Invitation>> GetPendingInvitationsAsync(long teamId)
    {
        if (!await IsManagerAsync(teamId)) return new();
        return await _db.Invitations
            .Where(i => i.TeamId == teamId && !i.Accepted)
            .OrderBy(i => i.Email)
            .ToListAsync();
    }

    // ----- Verwaltung -----

    public async Task<Team> CreateAsync(string name, string color)
    {
        var uid = Uid;
        var team = new Team
        {
            Name = string.IsNullOrWhiteSpace(name) ? "Team" : name.Trim(),
            Color = string.IsNullOrWhiteSpace(color) ? "#8300bc" : color.Trim(),
            OwnerId = uid
        };
        _db.Teams.Add(team);
        await _db.SaveChangesAsync();

        _db.TeamMembers.Add(new TeamMember { TeamId = team.Id, UserId = uid, Role = TeamRole.Owner });
        await _db.SaveChangesAsync();
        return team;
    }

    public async Task UpdateAsync(long teamId, string name, string color)
    {
        if (!await IsManagerAsync(teamId)) throw new InvalidOperationException("Kein Zugriff auf dieses Team.");
        var team = await _db.Teams.FindAsync(teamId);
        if (team is null) return;
        if (!string.IsNullOrWhiteSpace(name)) team.Name = name.Trim();
        if (!string.IsNullOrWhiteSpace(color)) team.Color = color.Trim();
        await _db.SaveChangesAsync();
    }

    public async Task DeleteAsync(long teamId)
    {
        // Nur der Team-Owner darf löschen.
        if (!await IsOwnerAsync(teamId)) return;
        var team = await _db.Teams.FindAsync(teamId);
        if (team is null) return;
        _db.Teams.Remove(team);
        await _db.SaveChangesAsync();
    }

    /// <summary>Team verlassen (der Owner kann nicht einfach austreten – erst Löschen/Übergabe).</summary>
    public async Task LeaveAsync(long teamId)
    {
        var uid = Uid;
        var m = await _db.TeamMembers.FirstOrDefaultAsync(x => x.TeamId == teamId && x.UserId == uid);
        if (m is null || m.Role == TeamRole.Owner) return;
        _db.TeamMembers.Remove(m);
        await _db.SaveChangesAsync();
    }

    // ----- Mitglieder -----

    public async Task RemoveMemberAsync(long teamId, long userId)
    {
        if (!await IsManagerAsync(teamId)) return;
        var m = await _db.TeamMembers.FirstOrDefaultAsync(x => x.TeamId == teamId && x.UserId == userId);
        // Der Owner kann nicht entfernt werden.
        if (m is null || m.Role == TeamRole.Owner) return;
        _db.TeamMembers.Remove(m);
        await _db.SaveChangesAsync();
    }

    public async Task ChangeRoleAsync(long teamId, long userId, TeamRole role)
    {
        // Nur der Owner darf Rollen ändern; die Owner-Rolle selbst bleibt unverändert.
        if (!await IsOwnerAsync(teamId)) return;
        if (role == TeamRole.Owner) return; // Owner-Übergabe ist eine separate Aktion
        var m = await _db.TeamMembers.FirstOrDefaultAsync(x => x.TeamId == teamId && x.UserId == userId);
        if (m is null || m.Role == TeamRole.Owner) return;
        m.Role = role;
        await _db.SaveChangesAsync();
    }

    // ----- Einladungen -----

    /// <summary>
    /// Lädt eine E-Mail-Adresse in ein Team ein. Existiert der Benutzer, wird er sofort Mitglied;
    /// sonst wird eine ausstehende Einladung gespeichert (+ optional E-Mail, wenn SMTP aktiv).
    /// </summary>
    public async Task<InviteOutcome> InviteToTeamAsync(long teamId, string email, TeamRole role)
    {
        if (!await IsManagerAsync(teamId)) throw new InvalidOperationException("Kein Zugriff auf dieses Team.");
        if (role == TeamRole.Owner) role = TeamRole.Admin; // per Einladung kein Owner
        email = email.Trim().ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(email)) throw new InvalidOperationException("E-Mail-Adresse fehlt.");

        var user = await _db.Users.FirstOrDefaultAsync(u => u.Email == email);
        if (user != null)
        {
            if (user.Id == Uid) return InviteOutcome.Self;
            var exists = await _db.TeamMembers.AnyAsync(m => m.TeamId == teamId && m.UserId == user.Id);
            if (exists) return InviteOutcome.AlreadyMember;
            _db.TeamMembers.Add(new TeamMember { TeamId = teamId, UserId = user.Id, Role = role });
            await _db.SaveChangesAsync();
            return InviteOutcome.AddedDirectly;
        }

        var pending = await _db.Invitations.AnyAsync(i => i.Email == email && i.TeamId == teamId && !i.Accepted);
        if (pending) return InviteOutcome.AlreadyInvited;

        var inv = new Invitation
        {
            Email = email,
            TeamId = teamId,
            TeamRole = role,
            InvitedByUserId = Uid
        };
        _db.Invitations.Add(inv);
        await _db.SaveChangesAsync();

        await TrySendInviteEmailAsync(inv);
        return InviteOutcome.PendingInvite;
    }

    /// <summary>Aktive Nutzer, die noch nicht Mitglied des Teams sind (für die Direkt-Auswahl beim Einladen).</summary>
    public async Task<List<User>> GetInvitableUsersAsync(long teamId)
    {
        if (!await IsManagerAsync(teamId)) return new();
        var memberIds = await _db.TeamMembers.Where(m => m.TeamId == teamId).Select(m => m.UserId).ToListAsync();
        return await _db.Users.Where(u => u.IsActive && !memberIds.Contains(u.Id))
            .OrderBy(u => u.DisplayName).ToListAsync();
    }

    /// <summary>Einen bestehenden Plattform-Nutzer direkt ins Team aufnehmen (per Auswahl).</summary>
    public async Task<InviteOutcome> InviteUserAsync(long teamId, long userId, TeamRole role)
    {
        if (!await IsManagerAsync(teamId)) throw new InvalidOperationException("Kein Zugriff auf dieses Team.");
        if (role == TeamRole.Owner) role = TeamRole.Admin;
        if (userId == Uid) return InviteOutcome.Self;
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == userId && u.IsActive);
        if (user is null) return InviteOutcome.AlreadyInvited;
        if (await _db.TeamMembers.AnyAsync(m => m.TeamId == teamId && m.UserId == userId)) return InviteOutcome.AlreadyMember;
        _db.TeamMembers.Add(new TeamMember { TeamId = teamId, UserId = userId, Role = role });
        await _db.SaveChangesAsync();
        return InviteOutcome.AddedDirectly;
    }

    // ----- Plattform-Administration (alle Teams, nur Admin) -----

    private void EnsureAdmin() { if (!_me.IsAdmin) throw new InvalidOperationException("Nur für Administratoren."); }

    public async Task<List<(Team Team, int MemberCount)>> AdminGetAllAsync()
    {
        EnsureAdmin();
        var rows = await _db.Teams.Include(t => t.Owner)
            .Select(t => new { t, c = t.Members.Count })
            .OrderBy(x => x.t.Name).ToListAsync();
        return rows.Select(x => (x.t, x.c)).ToList();
    }

    public async Task<Team?> AdminGetAsync(long teamId)
    {
        EnsureAdmin();
        return await _db.Teams.Include(t => t.Members).ThenInclude(m => m.User)
            .FirstOrDefaultAsync(t => t.Id == teamId);
    }

    public async Task AdminRemoveMemberAsync(long teamId, long userId)
    {
        EnsureAdmin();
        var m = await _db.TeamMembers.FirstOrDefaultAsync(x => x.TeamId == teamId && x.UserId == userId);
        if (m is null || m.Role == TeamRole.Owner) return; // Owner erst wechseln oder Team löschen
        _db.TeamMembers.Remove(m);
        await _db.SaveChangesAsync();
    }

    public async Task AdminChangeRoleAsync(long teamId, long userId, TeamRole role)
    {
        EnsureAdmin();
        if (role == TeamRole.Owner) { await AdminSetOwnerAsync(teamId, userId); return; }
        var m = await _db.TeamMembers.FirstOrDefaultAsync(x => x.TeamId == teamId && x.UserId == userId);
        if (m is null || m.Role == TeamRole.Owner) return;
        m.Role = role;
        await _db.SaveChangesAsync();
    }

    /// <summary>Eigentümerschaft übertragen: bisheriger Owner wird Admin, Ziel wird Owner.</summary>
    public async Task AdminSetOwnerAsync(long teamId, long userId)
    {
        EnsureAdmin();
        var team = await _db.Teams.Include(t => t.Members).FirstOrDefaultAsync(t => t.Id == teamId);
        var target = team?.Members.FirstOrDefault(m => m.UserId == userId);
        if (team is null || target is null) return; // Ziel muss bereits Mitglied sein
        foreach (var m in team.Members.Where(m => m.Role == TeamRole.Owner)) m.Role = TeamRole.Admin;
        target.Role = TeamRole.Owner;
        team.OwnerId = userId;
        await _db.SaveChangesAsync();
    }

    public async Task AdminDeleteAsync(long teamId)
    {
        EnsureAdmin();
        var team = await _db.Teams.FindAsync(teamId);
        if (team is null) return;
        _db.Teams.Remove(team);
        await _db.SaveChangesAsync();
    }

    public async Task CancelInvitationAsync(long invitationId)
    {
        var inv = await _db.Invitations.FindAsync(invitationId);
        if (inv is null) return;
        // Team- oder Projekt-basierte Berechtigung prüfen.
        var allowed = (inv.TeamId is long tid && await IsManagerAsync(tid))
                      || (inv.ProjectId is long pid && await _db.Projects.AnyAsync(p => p.Id == pid && p.OwnerId == Uid));
        if (!allowed) return;
        _db.Invitations.Remove(inv);
        await _db.SaveChangesAsync();
    }

    private async Task TrySendInviteEmailAsync(Invitation inv)
    {
        var cfg = _config.Current;
        if (!cfg.Smtp.Enabled) return;
        try
        {
            var team = inv.TeamId is long tid ? await _db.Teams.FindAsync(tid) : null;
            var inviter = await _db.Users.FindAsync(inv.InvitedByUserId);
            var appName = string.IsNullOrWhiteSpace(cfg.AppName) ? "Matdo" : cfg.AppName;
            var baseUrl = cfg.PublicBaseUrl.TrimEnd('/');
            var teamName = team?.Name ?? appName;
            var by = inviter?.DisplayName ?? "Ein Benutzer";
            var subject = $"{by} hat dich zu „{teamName}\" bei {appName} eingeladen";
            var body =
                $"<p>Hallo,</p>" +
                $"<p><b>{System.Net.WebUtility.HtmlEncode(by)}</b> hat dich zum Team " +
                $"<b>{System.Net.WebUtility.HtmlEncode(teamName)}</b> bei {appName} eingeladen.</p>" +
                $"<p>Registriere dich einfach mit dieser E-Mail-Adresse – die Einladung wird dann " +
                $"automatisch übernommen:</p>" +
                $"<p><a href=\"{baseUrl}/Account/Register\">{baseUrl}/Account/Register</a></p>";
            await _email.SendAsync(inv.Email, inv.Email, subject, body);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Einladungs-E-Mail an {Email} konnte nicht gesendet werden.", inv.Email);
        }
    }

    // ----- Auflösung bei Registrierung -----

    /// <summary>
    /// Übernimmt alle ausstehenden Einladungen für die E-Mail-Adresse des neu registrierten Benutzers.
    /// Wird ohne angemeldeten Kontext aus <see cref="AuthService"/> aufgerufen – nutzt daher kein Uid.
    /// </summary>
    public static async Task ApplyPendingInvitationsAsync(MatdoDbContext db, User user)
    {
        var email = user.Email.Trim().ToLowerInvariant();
        var invites = await db.Invitations.Where(i => i.Email == email && !i.Accepted).ToListAsync();
        if (invites.Count == 0) return;

        foreach (var inv in invites)
        {
            if (inv.TeamId is long tid)
            {
                var teamExists = await db.Teams.AnyAsync(t => t.Id == tid);
                var already = await db.TeamMembers.AnyAsync(m => m.TeamId == tid && m.UserId == user.Id);
                if (teamExists && !already)
                    db.TeamMembers.Add(new TeamMember { TeamId = tid, UserId = user.Id, Role = inv.TeamRole });
            }
            else if (inv.ProjectId is long pid)
            {
                var projExists = await db.Projects.AnyAsync(p => p.Id == pid);
                var already = await db.ProjectShares.AnyAsync(s => s.ProjectId == pid && s.SharedWithUserId == user.Id);
                if (projExists && !already)
                    db.ProjectShares.Add(new ProjectShare { ProjectId = pid, SharedWithUserId = user.Id, Permission = inv.Permission });
            }
            inv.Accepted = true;
        }
        await db.SaveChangesAsync();
    }
}
