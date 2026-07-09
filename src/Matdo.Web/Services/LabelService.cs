using Matdo.Web.Data;
using Matdo.Web.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace Matdo.Web.Services;

/// <summary>Verwaltung von Etiketten (Tags) des angemeldeten Benutzers.</summary>
public class LabelService
{
    private readonly MatdoDbContext _db;
    private readonly ICurrentUserAccessor _me;

    public LabelService(MatdoDbContext db, ICurrentUserAccessor me)
    {
        _db = db;
        _me = me;
    }

    private long Uid => _me.UserId ?? throw new InvalidOperationException("Kein angemeldeter Benutzer.");

    public Task<List<Label>> GetAllAsync() =>
        _db.Labels.Where(l => l.OwnerId == Uid).OrderByDescending(l => l.IsFavorite).ThenBy(l => l.Name).ToListAsync();

    public Task<List<Label>> GetFavoritesAsync() =>
        _db.Labels.Where(l => l.OwnerId == Uid && l.IsFavorite).OrderBy(l => l.Name).ToListAsync();

    public Task<Label?> GetAsync(long id) =>
        _db.Labels.FirstOrDefaultAsync(l => l.Id == id && l.OwnerId == Uid);

    public async Task<Label> CreateAsync(Label label)
    {
        label.OwnerId = Uid;
        _db.Labels.Add(label);
        await _db.SaveChangesAsync();
        return label;
    }

    public async Task<Label> GetOrCreateByNameAsync(string name)
    {
        name = name.Trim();
        var existing = await _db.Labels.FirstOrDefaultAsync(l => l.OwnerId == Uid && l.Name == name);
        if (existing != null) return existing;
        return await CreateAsync(new Label { Name = name });
    }

    public async Task UpdateAsync(Label updated)
    {
        var l = await GetAsync(updated.Id) ?? throw new InvalidOperationException("Etikett nicht gefunden.");
        l.Name = updated.Name;
        l.Color = updated.Color;
        l.IsFavorite = updated.IsFavorite;
        await _db.SaveChangesAsync();
    }

    public async Task DeleteAsync(long id)
    {
        var l = await GetAsync(id);
        if (l is null) return;
        _db.Labels.Remove(l);
        await _db.SaveChangesAsync();
    }
}
