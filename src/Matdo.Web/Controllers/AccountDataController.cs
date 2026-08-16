using Matdo.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Matdo.Web.Controllers;

/// <summary>Persönlicher Datenexport (DSGVO Art. 15/20) als herunterladbares JSON.</summary>
[Authorize]
public class AccountDataController : Controller
{
    private readonly AccountDataService _data;

    public AccountDataController(AccountDataService data) => _data = data;

    [HttpGet("/account/export.json")]
    public async Task<IActionResult> Export(CancellationToken ct)
    {
        var bytes = await _data.ExportJsonAsync(DateTime.UtcNow, ct);
        var fileName = $"matdo-export-{DateTime.UtcNow:yyyyMMdd}.json";
        return File(bytes, "application/json; charset=utf-8", fileName);
    }
}
