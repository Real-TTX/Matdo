using System.ComponentModel.DataAnnotations;
using Matdo.Web.Data.Entities;
using Matdo.Web.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace Matdo.Web.Pages.Tasks;

public class TaskEditModel : PageModel
{
    private readonly TaskService _tasks;
    private readonly ProjectService _projects;
    private readonly LabelService _labels;
    private readonly ShareService _shares;

    public TaskEditModel(TaskService tasks, ProjectService projects, LabelService labels, ShareService shares)
    {
        _tasks = tasks;
        _projects = projects;
        _labels = labels;
        _shares = shares;
    }

    [BindProperty] public InputModel Input { get; set; } = new();

    public bool IsNew => Input.Id == 0;
    public TaskItem? Existing { get; set; }
    public List<SelectListItem> ProjectOptions { get; set; } = new();
    public List<SelectListItem> SectionOptions { get; set; } = new();
    public List<Label> AllLabels { get; set; } = new();
    public List<TaskItem> SubTasks { get; set; } = new();
    public List<Reminder> Reminders { get; set; } = new();
    public List<TaskShare> Shares { get; set; } = new();
    public List<User> ShareableUsers { get; set; } = new();
    public List<User> TeamMembers { get; set; } = new();
    public bool CanShare { get; set; }

    public class InputModel
    {
        public long Id { get; set; }

        [Required(ErrorMessage = "Bitte einen Titel angeben.")]
        public string Title { get; set; } = "";
        public string? Description { get; set; }
        public long? ProjectId { get; set; }
        public long? KanbanColumnId { get; set; }   // Abschnitt (= Kanban-Spalte)
        public long? AssigneeId { get; set; }
        public int Priority { get; set; } = 4;
        public int RecurrenceUnit { get; set; }      // 0 = keine Wiederholung
        public int RecurrenceInterval { get; set; } = 1;

        public string? DueDate { get; set; }
        public string? DueTime { get; set; }
        public string? DeadlineDate { get; set; }
        public string? DeadlineTime { get; set; }

        public List<long> LabelIds { get; set; } = new();
    }

    private async Task LoadListsAsync()
    {
        var projects = await _projects.GetAllAsync();
        ProjectOptions = new List<SelectListItem> { new("Eingang", "") };
        ProjectOptions.AddRange(projects.Select(p => new SelectListItem(p.Name, p.Id.ToString())));
        AllLabels = await _labels.GetAllAsync();
        TeamMembers = await _shares.GetCollaboratorsAsync();
    }

    /// <summary>Abschnitte (= Kanban-Spalten) des gewählten Projekts für das Dropdown laden.</summary>
    private async Task LoadSectionsAsync(long? projectId)
    {
        SectionOptions = new List<SelectListItem>();
        if (projectId is long pid)
        {
            var cols = await _projects.GetColumnsAsync(pid);
            SectionOptions = cols.Select(c => new SelectListItem(c.Name, c.Id.ToString())).ToList();
        }
    }

    private async Task LoadDetailsAsync(long id)
    {
        Existing = await _tasks.GetAsync(id);
        if (Existing is null) return;
        SubTasks = Existing.SubTasks?.OrderBy(s => s.Position).ToList() ?? new();
        Reminders = Existing.Reminders?.OrderBy(r => r.RemindAt).ToList() ?? new();
        Shares = await _shares.GetTaskSharesAsync(id);
        ShareableUsers = await _shares.GetShareableUsersAsync();

        // Aktuellen Zugewiesenen als Option sicherstellen, auch wenn er kein Mitstreiter
        // des Bearbeiters ist – sonst würde er beim Speichern still entfernt.
        if (Existing.Assignee is { } asg && TeamMembers.All(u => u.Id != asg.Id))
            TeamMembers.Insert(0, asg);
        CanShare = Existing.OwnerId == (User.FindFirst(MatdoClaims.UserId) is { } c && long.TryParse(c.Value, out var uid) ? uid : -1);
    }

    public async Task<IActionResult> OnGetAsync(long? id, string? due, long? projectId)
    {
        await LoadListsAsync();

        if (id is > 0)
        {
            var t = await _tasks.GetAsync(id.Value);
            if (t is null) return NotFound();
            Input = new InputModel
            {
                Id = t.Id,
                Title = t.Title,
                Description = t.Description,
                ProjectId = t.ProjectId,
                KanbanColumnId = t.KanbanColumnId,
                AssigneeId = t.AssigneeId,
                Priority = (int)t.Priority,
                RecurrenceUnit = (int)t.RecurrenceUnit,
                RecurrenceInterval = t.RecurrenceInterval,
                DueDate = DateHelper.ToDateInput(t.DueDate),
                DueTime = DateHelper.ToTimeInput(t.DueDate, t.DueHasTime),
                DeadlineDate = DateHelper.ToDateInput(t.DeadlineDate),
                DeadlineTime = DateHelper.ToTimeInput(t.DeadlineDate, t.DeadlineHasTime),
                LabelIds = t.TaskLabels?.Select(tl => tl.LabelId).ToList() ?? new()
            };
            await LoadDetailsAsync(t.Id);
            await LoadSectionsAsync(t.ProjectId);
        }
        else
        {
            if (string.Equals(due, "today", StringComparison.OrdinalIgnoreCase))
                Input.DueDate = DateTime.Today.ToString("yyyy-MM-dd");
            else if (!string.IsNullOrWhiteSpace(due) &&
                     DateTime.TryParseExact(due, "yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture,
                         System.Globalization.DateTimeStyles.None, out var d))
                Input.DueDate = d.ToString("yyyy-MM-dd");
            Input.ProjectId = projectId;
            await LoadSectionsAsync(projectId);
        }
        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        await LoadListsAsync();
        if (!ModelState.IsValid)
        {
            if (Input.Id > 0) await LoadDetailsAsync(Input.Id);
            await LoadSectionsAsync(Input.ProjectId);
            return Page();
        }

        var task = new TaskItem
        {
            Id = Input.Id,
            Title = Input.Title.Trim(),
            Description = string.IsNullOrWhiteSpace(Input.Description) ? null : Input.Description.Trim(),
            ProjectId = Input.ProjectId,
            KanbanColumnId = Input.KanbanColumnId,
            AssigneeId = Input.AssigneeId,
            Priority = (TaskPriority)Math.Clamp(Input.Priority, 1, 4),
            RecurrenceUnit = (RecurrenceUnit)Math.Clamp(Input.RecurrenceUnit, 0, 4),
            RecurrenceInterval = Input.RecurrenceInterval < 1 ? 1 : Input.RecurrenceInterval,
            DueDate = DateHelper.ToUtc(Input.DueDate, Input.DueTime),
            DueHasTime = !string.IsNullOrWhiteSpace(Input.DueTime),
            DeadlineDate = DateHelper.ToUtc(Input.DeadlineDate, Input.DeadlineTime),
            DeadlineHasTime = !string.IsNullOrWhiteSpace(Input.DeadlineTime)
        };

        if (Input.Id == 0)
        {
            var created = await _tasks.CreateAsync(task, Input.LabelIds);
            return RedirectToPage(new { id = created.Id, saved = true });
        }

        await _tasks.UpdateAsync(task, Input.LabelIds);
        return RedirectToPage(new { id = Input.Id, saved = true });
    }

    public async Task<IActionResult> OnPostAddSubtaskAsync(long id, string subtaskTitle)
    {
        if (!string.IsNullOrWhiteSpace(subtaskTitle))
            await _tasks.CreateAsync(new TaskItem { Title = subtaskTitle.Trim(), ParentTaskId = id });
        return RedirectToPage(new { id });
    }

    public async Task<IActionResult> OnPostAddReminderAsync(long id, int reminderType, string? remindDate, string? remindTime, int offsetMinutes, int channel)
    {
        var ch = channel is 1 or 2 or 3 ? channel : 3; // gültigen Kanal erzwingen (kein stummes "None")
        var reminder = new Reminder
        {
            Type = (ReminderType)reminderType,
            Channel = (ReminderChannel)ch,
            OffsetMinutes = Math.Max(0, offsetMinutes)
        };

        if (reminder.Type == ReminderType.DateTime)
        {
            reminder.RemindAt = DateHelper.ToUtc(remindDate, remindTime) ?? DateTime.UtcNow.AddHours(1);
        }
        else
        {
            // Vor der Fälligkeit: relativ zur DueDate der Aufgabe berechnen.
            var t = await _tasks.GetAsync(id);
            var baseUtc = t?.DueDate ?? DateTime.UtcNow;
            reminder.RemindAt = baseUtc.AddMinutes(-offsetMinutes);
        }

        await _tasks.AddReminderAsync(id, reminder);
        return RedirectToPage(new { id });
    }

    public async Task<IActionResult> OnPostRemoveReminderAsync(long id, long reminderId)
    {
        await _tasks.RemoveReminderAsync(reminderId);
        return RedirectToPage(new { id });
    }

    public async Task<IActionResult> OnPostShareAsync(long id, long targetUserId, int permission)
    {
        await _shares.ShareTaskAsync(id, targetUserId, (SharePermission)permission);
        return RedirectToPage(new { id });
    }

    public async Task<IActionResult> OnPostUnshareAsync(long id, long targetUserId)
    {
        await _shares.UnshareTaskAsync(id, targetUserId);
        return RedirectToPage(new { id });
    }

    public async Task<IActionResult> OnPostDeleteAsync(long id)
    {
        await _tasks.DeleteAsync(id);
        return Redirect("/Tasks/Today");
    }
}
