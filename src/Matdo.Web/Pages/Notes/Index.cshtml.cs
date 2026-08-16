using Matdo.Web.Data.Entities;
using Matdo.Web.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Matdo.Web.Pages.Notes;

public class IndexModel : PageModel
{
    private readonly NoteService _notes;
    public IndexModel(NoteService notes) => _notes = notes;

    public List<Note> Items { get; set; } = new();
    [FromQuery] public string? Q { get; set; }

    public async Task OnGetAsync() => Items = await _notes.GetAllAsync(Q);

    public async Task<IActionResult> OnPostPinAsync(long id)
    {
        await _notes.TogglePinAsync(id);
        return RedirectToPage(new { q = Q });
    }

    public async Task<IActionResult> OnPostDeleteAsync(long id)
    {
        await _notes.DeleteAsync(id);
        return RedirectToPage(new { q = Q });
    }
}
