using Matdo.Web.Data;
using Matdo.Web.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace Matdo.Web.Services;

/// <summary>Administrative Verwaltung: Benutzer, Rollen und Benutzergruppen.</summary>
public class AdminService
{
    private readonly MatdoDbContext _db;

    public AdminService(MatdoDbContext db) => _db = db;

    // ----- Benutzer -----

    public IQueryable<User> Users() => _db.Users.Include(u => u.Role);

    public Task<User?> GetUserAsync(long id) =>
        _db.Users.Include(u => u.Role).FirstOrDefaultAsync(u => u.Id == id);

    public async Task<User> CreateUserAsync(string email, string password, string displayName, long roleId, bool isActive)
    {
        email = email.Trim().ToLowerInvariant();
        if (await _db.Users.AnyAsync(u => u.Email == email))
            throw new InvalidOperationException("E-Mail-Adresse bereits vergeben.");

        var user = new User
        {
            Email = email,
            DisplayName = string.IsNullOrWhiteSpace(displayName) ? email.Split('@')[0] : displayName.Trim(),
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(password),
            RoleId = roleId,
            IsActive = isActive
        };
        _db.Users.Add(user);
        await _db.SaveChangesAsync();
        return user;
    }

    public async Task UpdateUserAsync(long id, string displayName, long roleId, bool isActive, string? newPassword)
    {
        var user = await _db.Users.FindAsync(id) ?? throw new InvalidOperationException("Benutzer nicht gefunden.");
        user.DisplayName = displayName.Trim();
        user.RoleId = roleId;
        user.IsActive = isActive;
        if (!string.IsNullOrWhiteSpace(newPassword))
            user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(newPassword);
        await _db.SaveChangesAsync();
    }

    public async Task DeleteUserAsync(long id)
    {
        var user = await _db.Users.FindAsync(id);
        if (user is null) return;
        _db.Users.Remove(user);
        await _db.SaveChangesAsync();
    }

    // ----- Rollen -----

    public Task<List<Role>> GetRolesAsync() => _db.Roles.OrderBy(r => r.Name).ToListAsync();

    public Task<Role?> GetRoleAsync(long id) => _db.Roles.FirstOrDefaultAsync(r => r.Id == id);

    public async Task<Role> CreateRoleAsync(string name, string? description)
    {
        if (await _db.Roles.AnyAsync(r => r.Name == name))
            throw new InvalidOperationException("Rolle existiert bereits.");
        var role = new Role { Name = name.Trim(), Description = description };
        _db.Roles.Add(role);
        await _db.SaveChangesAsync();
        return role;
    }

    public async Task UpdateRoleAsync(long id, string name, string? description)
    {
        var role = await _db.Roles.FindAsync(id) ?? throw new InvalidOperationException("Rolle nicht gefunden.");
        role.Name = name.Trim();
        role.Description = description;
        await _db.SaveChangesAsync();
    }

    public async Task DeleteRoleAsync(long id)
    {
        var role = await _db.Roles.FindAsync(id);
        if (role is null) return;
        if (role.Name is Role.Admin or Role.User)
            throw new InvalidOperationException("Standardrollen können nicht gelöscht werden.");
        if (await _db.Users.AnyAsync(u => u.RoleId == id))
            throw new InvalidOperationException("Rolle ist noch Benutzern zugewiesen.");
        _db.Roles.Remove(role);
        await _db.SaveChangesAsync();
    }

    // ----- Benutzergruppen -----

    public IQueryable<UserGroup> Groups() => _db.UserGroups;

    public Task<UserGroup?> GetGroupAsync(long id) =>
        _db.UserGroups.Include(g => g.Members).ThenInclude(m => m.User).FirstOrDefaultAsync(g => g.Id == id);

    public async Task<UserGroup> CreateGroupAsync(string name, string? description)
    {
        var group = new UserGroup { Name = name.Trim(), Description = description };
        _db.UserGroups.Add(group);
        await _db.SaveChangesAsync();
        return group;
    }

    public async Task UpdateGroupAsync(long id, string name, string? description, IEnumerable<long> memberUserIds)
    {
        var group = await _db.UserGroups.Include(g => g.Members).FirstOrDefaultAsync(g => g.Id == id)
                    ?? throw new InvalidOperationException("Gruppe nicht gefunden.");
        group.Name = name.Trim();
        group.Description = description;

        var wanted = memberUserIds.Distinct().ToHashSet();
        foreach (var m in group.Members.Where(m => !wanted.Contains(m.UserId)).ToList())
            _db.UserGroupMembers.Remove(m);
        var have = group.Members.Select(m => m.UserId).ToHashSet();
        foreach (var uid in wanted.Where(u => !have.Contains(u)))
            _db.UserGroupMembers.Add(new UserGroupMember { UserGroupId = id, UserId = uid });

        await _db.SaveChangesAsync();
    }

    public async Task DeleteGroupAsync(long id)
    {
        var group = await _db.UserGroups.FindAsync(id);
        if (group is null) return;
        _db.UserGroups.Remove(group);
        await _db.SaveChangesAsync();
    }
}
