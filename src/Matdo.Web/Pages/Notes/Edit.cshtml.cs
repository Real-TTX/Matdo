using Matdo.Web.Data.Entities;
using Matdo.Web.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Matdo.Web.Pages.Notes;

public class EditModel : PageModel
{
    private readonly NoteService _notes;
    public EditModel(NoteService notes) => _notes = notes;

    [BindProperty] public InputModel Input { get; set; } = new();
    public List<Project> Projects { get; set; } = new();
    public bool IsNew => Input.Id == 0;

    public class InputModel
    {
        public long Id { get; set; }
        public string? Title { get; set; }
        public string? Body { get; set; }
        public long? ProjectId { get; set; }
    }

    public async Task<IActionResult> OnGetAsync(long? id)
    {
        Projects = await _notes.PickerProjectsAsync();
        if (id is long nid)
        {
            var n = await _notes.GetAsync(nid);
            if (n is null) return NotFound();
            Input = new InputModel { Id = n.Id, Title = n.Title, Body = n.Body, ProjectId = n.ProjectId };
        }
        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (string.IsNullOrWhiteSpace(Input.Title) && string.IsNullOrWhiteSpace(Input.Body))
        {
            Projects = await _notes.PickerProjectsAsync();
            ModelState.AddModelError("Input.Title", "Bitte einen Titel oder Text eingeben.");
            return Page();
        }
        if (Input.Id == 0) await _notes.CreateAsync(Input.Title, Input.Body, Input.ProjectId);
        else await _notes.UpdateAsync(Input.Id, Input.Title, Input.Body, Input.ProjectId);
        return RedirectToPage("Index");
    }

    public async Task<IActionResult> OnPostDeleteAsync(long id)
    {
        await _notes.DeleteAsync(id);
        return RedirectToPage("Index");
    }
}
