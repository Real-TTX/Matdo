using Matdo.Web.Data.Entities;
using Matdo.Web.Services;
using Microsoft.EntityFrameworkCore;

namespace Matdo.Web.Data;

/// <summary>
/// EF-Core Kontext für die Postgres-Datenbank. Tabellen werden PascalCase benannt,
/// Audit-Spalten werden zentral in SaveChanges gepflegt.
/// </summary>
public class MatdoDbContext : DbContext
{
    private readonly ICurrentUserAccessor? _currentUser;

    public MatdoDbContext(DbContextOptions<MatdoDbContext> options, ICurrentUserAccessor? currentUser = null)
        : base(options)
    {
        _currentUser = currentUser;
    }

    public DbSet<Role> Roles => Set<Role>();
    public DbSet<User> Users => Set<User>();
    public DbSet<UserSession> UserSessions => Set<UserSession>();
    public DbSet<UserGroup> UserGroups => Set<UserGroup>();
    public DbSet<UserGroupMember> UserGroupMembers => Set<UserGroupMember>();
    public DbSet<PushSubscription> PushSubscriptions => Set<PushSubscription>();
    public DbSet<Project> Projects => Set<Project>();
    public DbSet<KanbanColumn> KanbanColumns => Set<KanbanColumn>();
    public DbSet<ProjectShare> ProjectShares => Set<ProjectShare>();
    public DbSet<TaskItem> Tasks => Set<TaskItem>();
    public DbSet<Label> Labels => Set<Label>();
    public DbSet<TaskLabel> TaskLabels => Set<TaskLabel>();
    public DbSet<Reminder> Reminders => Set<Reminder>();
    public DbSet<TaskShare> TaskShares => Set<TaskShare>();
    public DbSet<CalendarConnection> CalendarConnections => Set<CalendarConnection>();
    public DbSet<ExternalEvent> ExternalEvents => Set<ExternalEvent>();
    public DbSet<TaskCalendarLink> TaskCalendarLinks => Set<TaskCalendarLink>();
    public DbSet<Team> Teams => Set<Team>();
    public DbSet<TeamMember> TeamMembers => Set<TeamMember>();
    public DbSet<Invitation> Invitations => Set<Invitation>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        base.OnModelCreating(b);

        // Tabellennamen PascalCase (Singular je Entität).
        b.Entity<Role>().ToTable("Role");
        b.Entity<User>().ToTable("User");
        b.Entity<UserSession>().ToTable("UserSession");
        b.Entity<UserGroup>().ToTable("UserGroup");
        b.Entity<UserGroupMember>().ToTable("UserGroupMember");
        b.Entity<PushSubscription>().ToTable("PushSubscription");
        b.Entity<Project>().ToTable("Project");
        b.Entity<KanbanColumn>().ToTable("KanbanColumn");
        b.Entity<ProjectShare>().ToTable("ProjectShare");
        b.Entity<TaskItem>().ToTable("Task");
        b.Entity<Label>().ToTable("Label");
        b.Entity<TaskLabel>().ToTable("TaskLabel");
        b.Entity<Reminder>().ToTable("Reminder");
        b.Entity<TaskShare>().ToTable("TaskShare");
        b.Entity<CalendarConnection>().ToTable("CalendarConnection");
        b.Entity<ExternalEvent>().ToTable("ExternalEvent");
        b.Entity<TaskCalendarLink>().ToTable("TaskCalendarLink");
        b.Entity<Team>().ToTable("Team");
        b.Entity<TeamMember>().ToTable("TeamMember");
        b.Entity<Invitation>().ToTable("Invitation");

        // Eindeutigkeiten & Indizes
        b.Entity<Role>().HasIndex(x => x.Name).IsUnique();
        b.Entity<User>().HasIndex(x => x.Email).IsUnique();
        b.Entity<UserSession>().HasIndex(x => x.Token).IsUnique();
        b.Entity<Label>().HasIndex(x => new { x.OwnerId, x.Name }).IsUnique();

        b.Entity<User>()
            .HasOne(x => x.Role).WithMany(r => r.Users)
            .HasForeignKey(x => x.RoleId).OnDelete(DeleteBehavior.Restrict);

        // Selbstreferenz für Unteraufgaben
        b.Entity<TaskItem>()
            .HasOne(x => x.ParentTask).WithMany(x => x.SubTasks)
            .HasForeignKey(x => x.ParentTaskId).OnDelete(DeleteBehavior.Cascade);

        b.Entity<TaskItem>()
            .HasOne(x => x.Project).WithMany(p => p.Tasks)
            .HasForeignKey(x => x.ProjectId).OnDelete(DeleteBehavior.SetNull);

        b.Entity<TaskItem>()
            .HasOne(x => x.Assignee).WithMany()
            .HasForeignKey(x => x.AssigneeId).OnDelete(DeleteBehavior.SetNull);

        b.Entity<Project>()
            .HasOne(x => x.ParentProject).WithMany(p => p.Children)
            .HasForeignKey(x => x.ParentProjectId).OnDelete(DeleteBehavior.SetNull);

        // ----- Teams -----
        b.Entity<Team>().HasIndex(x => x.OwnerId);
        b.Entity<Team>().Property(x => x.OwnerId);
        b.Entity<TeamMember>().HasIndex(x => new { x.TeamId, x.UserId }).IsUnique();
        b.Entity<TeamMember>().Property(x => x.Role).HasConversion<int>();
        b.Entity<TeamMember>()
            .HasOne(x => x.Team).WithMany(t => t.Members)
            .HasForeignKey(x => x.TeamId).OnDelete(DeleteBehavior.Cascade);
        b.Entity<TeamMember>()
            .HasOne(x => x.User).WithMany()
            .HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);

        b.Entity<Project>()
            .HasOne(x => x.Team).WithMany(t => t.Projects)
            .HasForeignKey(x => x.TeamId).OnDelete(DeleteBehavior.SetNull);

        b.Entity<Invitation>().HasIndex(x => x.Email);
        b.Entity<Invitation>().HasIndex(x => x.Token).IsUnique();
        b.Entity<Invitation>().Property(x => x.TeamRole).HasConversion<int>();
        b.Entity<Invitation>().Property(x => x.Permission).HasConversion<int>();
        b.Entity<Invitation>()
            .HasOne(x => x.Team).WithMany()
            .HasForeignKey(x => x.TeamId).OnDelete(DeleteBehavior.Cascade);
        b.Entity<Invitation>()
            .HasOne(x => x.Project).WithMany()
            .HasForeignKey(x => x.ProjectId).OnDelete(DeleteBehavior.Cascade);

        b.Entity<TaskItem>()
            .HasOne(x => x.KanbanColumn).WithMany(c => c.Tasks)
            .HasForeignKey(x => x.KanbanColumnId).OnDelete(DeleteBehavior.SetNull);

        b.Entity<TaskLabel>().HasIndex(x => new { x.TaskItemId, x.LabelId }).IsUnique();
        b.Entity<TaskShare>().HasIndex(x => new { x.TaskItemId, x.SharedWithUserId }).IsUnique();
        b.Entity<ProjectShare>().HasIndex(x => new { x.ProjectId, x.SharedWithUserId }).IsUnique();
        b.Entity<UserGroupMember>().HasIndex(x => new { x.UserGroupId, x.UserId }).IsUnique();

        b.Entity<CalendarConnection>().HasMany(c => c.Events).WithOne(e => e.Connection)
            .HasForeignKey(e => e.CalendarConnectionId).OnDelete(DeleteBehavior.Cascade);
        b.Entity<ExternalEvent>().HasIndex(x => new { x.CalendarConnectionId, x.ExternalId }).IsUnique();
        b.Entity<TaskCalendarLink>().HasIndex(x => new { x.TaskItemId, x.CalendarConnectionId }).IsUnique();
        b.Entity<TaskCalendarLink>()
            .HasOne(x => x.TaskItem).WithMany().HasForeignKey(x => x.TaskItemId).OnDelete(DeleteBehavior.Cascade);
        b.Entity<CalendarConnection>().Property(x => x.Provider).HasConversion<int>();

        // Enums als int speichern (Standard) - explizit lassen für Klarheit.
        b.Entity<Project>().Property(x => x.ViewType).HasConversion<int>();
        b.Entity<TaskItem>().Property(x => x.Priority).HasConversion<int>();
        b.Entity<Reminder>().Property(x => x.Type).HasConversion<int>();
        b.Entity<Reminder>().Property(x => x.Channel).HasConversion<int>();
    }

    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        StampAudit();
        return base.SaveChanges(acceptAllChangesOnSuccess);
    }

    public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken ct = default)
    {
        StampAudit();
        return base.SaveChangesAsync(acceptAllChangesOnSuccess, ct);
    }

    private void StampAudit()
    {
        var now = DateTime.UtcNow;
        var uid = _currentUser?.UserId;

        foreach (var entry in ChangeTracker.Entries<BaseEntity>())
        {
            if (entry.State == EntityState.Added)
            {
                entry.Entity.CreateDate = now;
                entry.Entity.UpdateDate = now;
                entry.Entity.CreateUserId ??= uid;
                entry.Entity.UpdateUserId ??= uid;
            }
            else if (entry.State == EntityState.Modified)
            {
                entry.Entity.UpdateDate = now;
                entry.Entity.UpdateUserId = uid ?? entry.Entity.UpdateUserId;
                // CreateDate/CreateUserId nicht überschreiben
                entry.Property(nameof(BaseEntity.CreateDate)).IsModified = false;
                entry.Property(nameof(BaseEntity.CreateUserId)).IsModified = false;
            }
        }
    }
}
