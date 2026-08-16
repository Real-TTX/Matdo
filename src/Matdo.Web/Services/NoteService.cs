using Matdo.Web.Data;
using Matdo.Web.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace Matdo.Web.Services;

/// <summary>Persönliche Notizen (rein OwnerId-basiert). Optional einem eigenen Projekt zugeordnet.</summary>
public class NoteService
{
    private readonly MatdoDbContext _db;
    private readonly ICurrentUserAccessor _me;

    public NoteService(MatdoDbContext db, ICurrentUserAccessor me)
    {
        _db = db;
        _me = me;
    }

    private long Uid => _me.UserId ?? throw new InvalidOperationException("Kein angemeldeter Benutzer.");

    public Task<List<Note>> GetAllAsync(string? search = null)
    {
        var q = _db.Notes.Include(n => n.Project).Where(n => n.OwnerId == Uid);
        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.Trim();
            q = q.Where(n => EF.Functions.ILike(n.Title, "%" + s + "%") || EF.Functions.ILike(n.Body, "%" + s + "%"));
        }
        return q.OrderByDescending(n => n.IsPinned).ThenByDescending(n => n.UpdateDate).ToListAsync();
    }

    public Task<Note?> GetAsync(long id) =>
        _db.Notes.Include(n => n.Project).FirstOrDefaultAsync(n => n.Id == id && n.OwnerId == Uid);

    public async Task<Note> CreateAsync(string? title, string? body, long? projectId)
    {
        var note = new Note
        {
            OwnerId = Uid,
            Title = (title ?? "").Trim(),
            Body = body ?? "",
            ProjectId = await ValidProjectAsync(projectId)
        };
        _db.Notes.Add(note);
        await _db.SaveChangesAsync();
        return note;
    }

    public async Task UpdateAsync(long id, string? title, string? body, long? projectId)
    {
        var note = await _db.Notes.FirstOrDefaultAsync(n => n.Id == id && n.OwnerId == Uid);
        if (note is null) return;
        note.Title = (title ?? "").Trim();
        note.Body = body ?? "";
        note.ProjectId = await ValidProjectAsync(projectId);
        await _db.SaveChangesAsync();
    }

    public async Task TogglePinAsync(long id)
    {
        var note = await _db.Notes.FirstOrDefaultAsync(n => n.Id == id && n.OwnerId == Uid);
        if (note is null) return;
        note.IsPinned = !note.IsPinned;
        await _db.SaveChangesAsync();
    }

    public async Task DeleteAsync(long id)
    {
        var note = await _db.Notes.FirstOrDefaultAsync(n => n.Id == id && n.OwnerId == Uid);
        if (note is null) return;
        _db.Notes.Remove(note);
        await _db.SaveChangesAsync();
    }

    /// <summary>Projekte, denen der Benutzer eine Notiz zuordnen darf (eigene/zugängliche, nicht archiviert).</summary>
    public Task<List<Project>> PickerProjectsAsync() =>
        _db.Projects.Where(p => !p.IsArchived && (
                p.OwnerId == Uid
                || p.Shares.Any(s => s.SharedWithUserId == Uid)
                || (p.TeamId != null && p.Team!.Members.Any(m => m.UserId == Uid))))
            .OrderBy(p => p.Name).ToListAsync();

    // Zuordnung nur zu einem tatsächlich zugänglichen Projekt zulassen (kein IDOR über ProjectId).
    private async Task<long?> ValidProjectAsync(long? projectId)
    {
        if (projectId is not long pid) return null;
        var uid = Uid;
        var ok = await _db.Projects.AnyAsync(p => p.Id == pid && !p.IsArchived && (
            p.OwnerId == uid
            || p.Shares.Any(s => s.SharedWithUserId == uid)
            || (p.TeamId != null && p.Team!.Members.Any(m => m.UserId == uid))));
        return ok ? pid : null;
    }
}
