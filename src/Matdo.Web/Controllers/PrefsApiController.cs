using Matdo.Web.Data;
using Matdo.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Matdo.Web.Controllers;

/// <summary>Speichert Darstellungs-/Sprach-Einstellungen (Cookie + Benutzerprofil).</summary>
[ApiController]
[Route("api/prefs")]
[Authorize]
[AutoValidateAntiforgeryToken]
public class PrefsApiController : ControllerBase
{
    private readonly UiPreferences _prefs;
    private readonly MatdoDbContext _db;
    private readonly ICurrentUserAccessor _me;
    private readonly LocalizationService _loc;

    public PrefsApiController(UiPreferences prefs, MatdoDbContext db, ICurrentUserAccessor me, LocalizationService loc)
    {
        _prefs = prefs;
        _db = db;
        _me = me;
        _loc = loc;
    }

    public record PrefsDto(string? Scheme, string? Theme, string? Lang);

    [HttpPost]
    public async Task<IActionResult> Save([FromBody] PrefsDto dto)
    {
        _prefs.Write(dto.Scheme, dto.Theme, dto.Lang);

        var user = await _db.Users.FindAsync(_me.UserId);
        if (user is not null)
        {
            if (ThemeCatalog.IsValidMode(dto.Scheme)) user.ColorScheme = dto.Scheme!;
            if (ThemeCatalog.IsValidTheme(dto.Theme)) user.Theme = dto.Theme!;
            if (_loc.IsSupported(dto.Lang)) user.Language = dto.Lang!;
            await _db.SaveChangesAsync();
        }
        return Ok(new { ok = true });
    }
}
