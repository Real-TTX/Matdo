using Matdo.Web.Data;
using Matdo.Web.Data.Entities;
using Matdo.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Matdo.Web.Controllers;

[ApiController]
[Route("api/push")]
[Authorize]
[AutoValidateAntiforgeryToken]
public class PushApiController : ControllerBase
{
    private readonly MatdoDbContext _db;
    private readonly ICurrentUserAccessor _me;

    public PushApiController(MatdoDbContext db, ICurrentUserAccessor me)
    {
        _db = db;
        _me = me;
    }

    public record SubscribeDto(string Endpoint, string P256dh, string Auth);

    [HttpPost("subscribe")]
    public async Task<IActionResult> Subscribe([FromBody] SubscribeDto dto)
    {
        var uid = _me.UserId!.Value;
        var existing = await _db.PushSubscriptions.FirstOrDefaultAsync(s => s.Endpoint == dto.Endpoint);
        if (existing is null)
        {
            _db.PushSubscriptions.Add(new PushSubscription
            {
                UserId = uid,
                Endpoint = dto.Endpoint,
                P256dh = dto.P256dh,
                Auth = dto.Auth
            });
        }
        else
        {
            existing.UserId = uid;
            existing.P256dh = dto.P256dh;
            existing.Auth = dto.Auth;
        }
        await _db.SaveChangesAsync();
        return Ok(new { ok = true });
    }

    [HttpPost("unsubscribe")]
    public async Task<IActionResult> Unsubscribe([FromBody] SubscribeDto dto)
    {
        var subs = await _db.PushSubscriptions.Where(s => s.Endpoint == dto.Endpoint).ToListAsync();
        _db.PushSubscriptions.RemoveRange(subs);
        await _db.SaveChangesAsync();
        return Ok(new { ok = true });
    }
}
