using System.ComponentModel.DataAnnotations;
using Matdo.Web.Data.Entities;
using Matdo.Web.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Matdo.Web.Pages.Labels;

public class LabelEditModel : PageModel
{
    private readonly LabelService _labels;
    public LabelEditModel(LabelService labels) => _labels = labels;

    [BindProperty] public InputModel Input { get; set; } = new();
    public bool IsNew => Input.Id == 0;

    public static readonly string[] Palette =
        { "#8300bc", "#884dff", "#af38eb", "#dc4c3e", "#eb8909", "#f9d900", "#7ecc49", "#158fad", "#4073ff", "#808080" };

    public class InputModel
    {
        public long Id { get; set; }
        [Required(ErrorMessage = "Bitte einen Namen angeben.")]
        public string Name { get; set; } = "";
        [RegularExpression("^#[0-9a-fA-F]{6}$", ErrorMessage = "Ungültige Farbe.")]
        public string Color { get; set; } = "#808080";
        public bool IsFavorite { get; set; }
    }

    public async Task<IActionResult> OnGetAsync(long? id)
    {
        if (id is > 0)
        {
            var l = await _labels.GetAsync(id.Value);
            if (l is null) return NotFound();
            Input = new InputModel { Id = l.Id, Name = l.Name, Color = l.Color, IsFavorite = l.IsFavorite };
        }
        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid) return Page();

        var label = new Label { Id = Input.Id, Name = Input.Name.Trim(), Color = Input.Color, IsFavorite = Input.IsFavorite };
        if (Input.Id == 0)
            await _labels.CreateAsync(label);
        else
            await _labels.UpdateAsync(label);

        return Redirect("/Labels");
    }

    public async Task<IActionResult> OnPostDeleteAsync(long id)
    {
        await _labels.DeleteAsync(id);
        return Redirect("/Labels");
    }
}
