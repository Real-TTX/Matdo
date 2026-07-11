using System.Text;
using Matdo.Web.Data;
using Matdo.Web.Data.Entities;
using Matdo.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Matdo.Web.Controllers;

/// <summary>Exportiert ein Projekt als CSV im Todoist-„Vorlage"-Format (re-importierbar).</summary>
[Authorize]
public class ProjectExportController : Controller
{
    private readonly ProjectService _projects;
    private readonly MatdoDbContext _db;

    public ProjectExportController(ProjectService projects, MatdoDbContext db)
    {
        _projects = projects;
        _db = db;
    }

    [HttpGet("/projects/{id:long}/export.csv")]
    public async Task<IActionResult> Export(long id, CancellationToken ct)
    {
        var project = await _projects.GetAsync(id);   // nur zugängliche Projekte
        if (project is null) return NotFound();

        var tasks = await _db.Tasks
            .Where(t => t.ProjectId == id && !t.IsCompleted)
            .OrderBy(t => t.Position).ThenBy(t => t.Id)
            .ToListAsync(ct);

        var columns = project.Columns.OrderBy(c => c.Position).ToList();
        var childrenOf = tasks.Where(t => t.ParentTaskId != null)
            .GroupBy(t => t.ParentTaskId!.Value)
            .ToDictionary(g => g.Key, g => g.OrderBy(t => t.Position).ThenBy(t => t.Id).ToList());
        var topLevel = tasks.Where(t => t.ParentTaskId == null).ToList();

        var sb = new StringBuilder();
        sb.Append("﻿");   // BOM (wie Todoist)
        sb.Append("TYPE,CONTENT,PRIORITY,INDENT,AUTHOR,RESPONSIBLE,DATE,DATE_LANG,TIMEZONE\r\n");

        void WriteTask(TaskItem t, int indent)
        {
            var prio = (int)t.Priority;   // unser P1..P4 = 1..4 = CSV-Skala (1 = höchste)
            var date = "";
            if (t.DueDate is DateTime d)
            {
                var local = d.ToLocalTime();
                date = t.DueHasTime ? local.ToString("yyyy-MM-dd HH:mm") : local.ToString("yyyy-MM-dd");
            }
            sb.Append($"task,{Csv(t.Title)},{prio},{indent},,,{Csv(date)},,\r\n");
            if (childrenOf.TryGetValue(t.Id, out var kids))
                foreach (var k in kids) WriteTask(k, indent + 1);
        }

        var emitted = new HashSet<long>();
        if (project.ViewType == ProjectViewType.Kanban && columns.Count > 0)
        {
            foreach (var col in columns)
            {
                sb.Append($"section,{Csv(col.Name)},,,,,,,\r\n");
                foreach (var t in topLevel.Where(t => t.KanbanColumnId == col.Id))
                { WriteTask(t, 1); emitted.Add(t.Id); }
            }
        }
        // Übrige Top-Level-Aufgaben (ohne Spalte bzw. Listenansicht) ohne Sektion.
        foreach (var t in topLevel.Where(t => !emitted.Contains(t.Id)))
            WriteTask(t, 1);

        var fileName = Safe(project.Name) + ".csv";
        return File(Encoding.UTF8.GetBytes(sb.ToString()), "text/csv; charset=utf-8", fileName);
    }

    private static string Csv(string? s)
    {
        s ??= "";
        if (s.IndexOfAny(new[] { ',', '"', '\n', '\r' }) >= 0)
            return "\"" + s.Replace("\"", "\"\"") + "\"";
        return s;
    }

    private static string Safe(string name)
    {
        var cleaned = new string(name.Select(c => char.IsLetterOrDigit(c) || c is '-' or '_' or ' ' ? c : '_').ToArray()).Trim();
        return string.IsNullOrWhiteSpace(cleaned) ? "projekt" : cleaned;
    }
}
