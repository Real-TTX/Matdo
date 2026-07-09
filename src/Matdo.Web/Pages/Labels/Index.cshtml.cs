using Matdo.Web.Data.Entities;
using Matdo.Web.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Matdo.Web.Pages.Labels;

public class LabelsIndexModel : PageModel
{
    private readonly LabelService _labels;
    public LabelsIndexModel(LabelService labels) => _labels = labels;

    public List<Label> Items { get; set; } = new();
    [FromQuery(Name = "q")] public string? Search { get; set; }

    public async Task OnGetAsync()
    {
        Items = await _labels.GetAllAsync();
        if (!string.IsNullOrWhiteSpace(Search))
            Items = Items.Where(l => l.Name.Contains(Search, StringComparison.OrdinalIgnoreCase)).ToList();
    }
}
