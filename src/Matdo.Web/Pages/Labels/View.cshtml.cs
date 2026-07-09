using Matdo.Web.Data.Entities;
using Matdo.Web.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Matdo.Web.Pages.Labels;

public class LabelViewModel : PageModel
{
    private readonly LabelService _labels;
    private readonly TaskService _tasks;

    public LabelViewModel(LabelService labels, TaskService tasks)
    {
        _labels = labels;
        _tasks = tasks;
    }

    public Label Label { get; set; } = default!;
    public List<TaskItem> Tasks { get; set; } = new();

    public async Task<IActionResult> OnGetAsync(long id)
    {
        var l = await _labels.GetAsync(id);
        if (l is null) return NotFound();
        Label = l;
        Tasks = await _tasks.GetByLabelAsync(id);
        return Page();
    }
}
