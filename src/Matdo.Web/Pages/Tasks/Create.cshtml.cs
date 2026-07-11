using System.ComponentModel.DataAnnotations;
using Matdo.Web.Data.Entities;
using Matdo.Web.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Matdo.Web.Pages.Tasks;

/// <summary>
/// Gemeinsamer Endpunkt für den Inline-Composer. Legt eine Aufgabe inkl. inline
/// gesetzter Eigenschaften (Fälligkeit, Priorität, Etiketten, Deadline, Erinnerung) an
/// und leitet auf die Ursprungsseite zurück.
/// </summary>
public class TaskCreateModel : PageModel
{
    private readonly TaskService _tasks;
    private readonly SmartInputParser _parser;
    public TaskCreateModel(TaskService tasks, SmartInputParser parser)
    {
        _tasks = tasks;
        _parser = parser;
    }

    [BindProperty] public InputModel Input { get; set; } = new();

    public class InputModel
    {
        /// <summary>Gesetzt beim Inline-Bearbeiten (sonst wird angelegt).</summary>
        public long? Id { get; set; }
        [Required] public string Title { get; set; } = "";
        public string? Description { get; set; }
        public long? ProjectId { get; set; }
        public long? KanbanColumnId { get; set; }   // Abschnitt (= Spalte), z.B. Schnell-Anlegen je Abschnitt
        public int Priority { get; set; } = 4;

        public string? DueDate { get; set; }
        public string? DueTime { get; set; }
        public string? DeadlineDate { get; set; }
        public string? DeadlineTime { get; set; }

        public List<long> LabelIds { get; set; } = new();

        // Erinnerung (optional)
        public int? ReminderType { get; set; }   // 0 = Datum/Uhrzeit, 1 = vor Fälligkeit
        public string? RemindDate { get; set; }
        public string? RemindTime { get; set; }
        public int ReminderOffsetMinutes { get; set; } = 60;
        public int ReminderChannel { get; set; } = 3;

        public string ReturnUrl { get; set; } = "/Tasks/Today";
    }

    public IActionResult OnGet() => Redirect(SafeReturn(Input.ReturnUrl));

    public async Task<IActionResult> OnPostAsync()
    {
        var target = SafeReturn(Input.ReturnUrl);
        if (string.IsNullOrWhiteSpace(Input.Title))
            return Redirect(target);

        // ----- Inline-Bearbeiten: explizite Felder, kein Smart-Parsing (Titel bleibt wörtlich). -----
        if (Input.Id is long editId && editId > 0)
        {
            var existing = await _tasks.GetAsync(editId);
            if (existing is null) return Redirect(target);   // kein Zugriff / nicht gefunden
            var upd = new TaskItem
            {
                Id = editId,
                Title = Input.Title.Trim(),
                Description = string.IsNullOrWhiteSpace(Input.Description) ? null : Input.Description.Trim(),
                ProjectId = Input.ProjectId,
                Priority = (TaskPriority)Math.Clamp(Input.Priority, 1, 4),
                DueDate = DateHelper.ToUtc(Input.DueDate, Input.DueTime),
                DueHasTime = !string.IsNullOrWhiteSpace(Input.DueTime),
                DeadlineDate = DateHelper.ToUtc(Input.DeadlineDate, Input.DeadlineTime),
                DeadlineHasTime = !string.IsNullOrWhiteSpace(Input.DeadlineTime),
                // Nicht im Composer bearbeitbare Felder erhalten:
                KanbanColumnId = existing.KanbanColumnId,
                ParentTaskId = existing.ParentTaskId,
                AssigneeId = existing.AssigneeId
            };
            await _tasks.UpdateAsync(upd, Input.LabelIds);
            return Redirect(target);
        }

        // Schnell-Eingabe parsen: #Projekt, +Etikett, @Person und Zeitangaben aus dem Titel.
        var parsed = await _parser.ParseAsync(Input.Title);

        // Explizite Felder aus dem Composer haben Vorrang; Lücken werden aus dem Text gefüllt.
        var explicitDue = DateHelper.ToUtc(Input.DueDate, Input.DueTime);
        var due = explicitDue ?? parsed.DueUtc;
        var dueHasTime = explicitDue != null ? !string.IsNullOrWhiteSpace(Input.DueTime) : parsed.DueHasTime;

        var labelIds = Input.LabelIds.Concat(parsed.LabelIds).Distinct().ToList();

        var task = new TaskItem
        {
            Title = parsed.Title,
            Description = string.IsNullOrWhiteSpace(Input.Description) ? null : Input.Description.Trim(),
            ProjectId = Input.ProjectId ?? parsed.ProjectId,
            KanbanColumnId = Input.KanbanColumnId,
            AssigneeId = parsed.AssigneeId,
            Priority = (TaskPriority)Math.Clamp(Input.Priority, 1, 4),
            DueDate = due,
            DueHasTime = dueHasTime,
            DeadlineDate = DateHelper.ToUtc(Input.DeadlineDate, Input.DeadlineTime),
            DeadlineHasTime = !string.IsNullOrWhiteSpace(Input.DeadlineTime)
        };

        var created = await _tasks.CreateAsync(task, labelIds);

        // Optionale Erinnerung anlegen
        if (Input.ReminderType is int rt)
        {
            var ch = Input.ReminderChannel is 1 or 2 or 3 ? Input.ReminderChannel : 3;
            var reminder = new Reminder
            {
                Type = (ReminderType)rt,
                Channel = (ReminderChannel)ch,
                OffsetMinutes = Math.Max(0, Input.ReminderOffsetMinutes)
            };
            if (reminder.Type == Data.Entities.ReminderType.DateTime)
            {
                var at = DateHelper.ToUtc(Input.RemindDate, Input.RemindTime);
                if (at.HasValue)
                {
                    reminder.RemindAt = at.Value;
                    await _tasks.AddReminderAsync(created.Id, reminder);
                }
            }
            else if (due.HasValue)
            {
                reminder.RemindAt = due.Value.AddMinutes(-Input.ReminderOffsetMinutes);
                await _tasks.AddReminderAsync(created.Id, reminder);
            }
        }

        return Redirect(target);
    }

    /// <summary>Nur lokale Rücksprung-URLs zulassen (kein Open-Redirect).</summary>
    private string SafeReturn(string? url) =>
        !string.IsNullOrWhiteSpace(url) && Url.IsLocalUrl(url) ? url : "/Tasks/Today";
}
