using Matdo.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Matdo.Web.Controllers;

[ApiController]
[Route("api/projects")]
[Authorize]
public class ProjectsApiController : ControllerBase
{
    private readonly ProjectService _projects;
    public ProjectsApiController(ProjectService projects) => _projects = projects;

    /// <summary>Abschnitte (= Kanban-Spalten) eines Projekts – zugriffsgeprüft. Für das Abschnitt-Dropdown im Editor.</summary>
    [HttpGet("{id:long}/columns")]
    public async Task<IActionResult> Columns(long id)
    {
        var cols = await _projects.GetColumnsAsync(id);
        return Ok(cols.Select(c => new { id = c.Id, name = c.Name }));
    }
}
