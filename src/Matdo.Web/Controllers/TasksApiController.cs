using Matdo.Web.Data.Entities;
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
    private readonly SmartInputParser _parser;
    public TasksApiController(TaskService tasks, SmartInputParser parser)
    {
        _tasks = tasks;
        _parser = parser;
    }

    public record CompleteDto(bool Completed);
    public record MoveDto(long? ColumnId, int Position);
    public record CreateDto(string? Title, string? Description);

    /// <summary>Aufgabe per Text anlegen (z.B. offline erfasste Aufgaben beim Sync).
    /// Smart-Tokens (#Projekt, +Etikett, @Person, Datum) werden geparst; ohne Treffer → Eingang.</summary>
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateDto dto)
    {
        if (dto is null || string.IsNullOrWhiteSpace(dto.Title)) return BadRequest();
        var parsed = await _parser.ParseAsync(dto.Title);
        var task = new TaskItem
        {
            Title = parsed.Title,
            Description = string.IsNullOrWhiteSpace(dto.Description) ? null : dto.Description.Trim(),
            ProjectId = parsed.ProjectId,
            AssigneeId = parsed.AssigneeId,
            DueDate = parsed.DueUtc,
            DueHasTime = parsed.DueHasTime
        };
        var created = await _tasks.CreateAsync(task, parsed.LabelIds);
        return Ok(new { id = created.Id });
    }

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
