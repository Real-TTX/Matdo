using Matdo.Web.Data;
using Matdo.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Matdo.Web.Controllers;

/// <summary>Autocomplete-Vorschläge für die Schnell-Eingabe (#Projekt, +Etikett, @Person).</summary>
[ApiController]
[Route("api/suggest")]
[Authorize]
public class SuggestApiController : ControllerBase
{
    private readonly MatdoDbContext _db;
    private readonly ICurrentUserAccessor _me;

    public SuggestApiController(MatdoDbContext db, ICurrentUserAccessor me)
    {
        _db = db;
        _me = me;
    }

    public record Suggestion(long Id, string Name, string? Color);

    [HttpGet]
    public async Task<IActionResult> Get([FromQuery] string type, [FromQuery] string? q)
    {
        var uid = _me.UserId ?? 0;
        q = (q ?? "").Trim();
        var like = $"%{q}%";

        List<Suggestion> items = type switch
        {
            "project" => await _db.Projects
                .Where(p => !p.IsArchived
                            && (p.OwnerId == uid
                                || p.Shares.Any(s => s.SharedWithUserId == uid && s.Permission == Matdo.Web.Data.Entities.SharePermission.Edit)
                                || (p.TeamId != null && p.Team!.Members.Any(m => m.UserId == uid)))
                            && (q == "" || EF.Functions.ILike(p.Name, like)))
                .OrderBy(p => p.Name).Take(8)
                .Select(p => new Suggestion(p.Id, p.Name, p.Color)).ToListAsync(),

            "label" => await _db.Labels
                .Where(l => l.OwnerId == uid && (q == "" || EF.Functions.ILike(l.Name, like)))
                .OrderBy(l => l.Name).Take(8)
                .Select(l => new Suggestion(l.Id, l.Name, l.Color)).ToListAsync(),

            "person" => await Collaborators.Query(_db, uid)
                .Where(u => q == "" || EF.Functions.ILike(u.DisplayName, like))
                .OrderBy(u => u.DisplayName).Take(8)
                .Select(u => new Suggestion(u.Id, u.DisplayName, null)).ToListAsync(),

            _ => new List<Suggestion>()
        };
        return Ok(items);
    }
}
