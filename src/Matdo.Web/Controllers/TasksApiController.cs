using Matdo.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Matdo.Web.Controllers;

[ApiController]
[Route("api/tasks")]
[Authorize]
[AutoValidateAntiforgeryToken]
public class TasksApiController : ControllerBase
{
    private readonly TaskService _tasks;
    public TasksApiController(TaskService tasks) => _tasks = tasks;

    public record CompleteDto(bool Completed);
    public record MoveDto(long? ColumnId, int Position);

    [HttpPost("{id:long}/complete")]
    public async Task<IActionResult> Complete(long id, [FromBody] CompleteDto dto)
    {
        await _tasks.SetCompletedAsync(id, dto.Completed);
        return Ok(new { ok = true });
    }

    [HttpPost("{id:long}/move")]
    public async Task<IActionResult> Move(long id, [FromBody] MoveDto dto)
    {
        await _tasks.MoveToColumnAsync(id, dto.ColumnId, dto.Position);
        return Ok(new { ok = true });
    }
}
