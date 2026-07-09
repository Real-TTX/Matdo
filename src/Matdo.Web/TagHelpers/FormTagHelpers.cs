using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.Razor.TagHelpers;

namespace Matdo.Web.TagHelpers;

/// <summary>
/// Button-Leiste eines Formulars. Buttons stehen in einer Zeile, von positiv nach
/// negativ (z.B. Speichern, Zurück, &lt;Abstand&gt; Löschen). Elemente mit Klasse "danger"
/// werden per CSS automatisch nach rechts abgesetzt.
/// </summary>
[HtmlTargetElement("form-buttons")]
public class FormButtonsTagHelper : TagHelper
{
    public override void Process(TagHelperContext context, TagHelperOutput output)
    {
        output.TagName = "div";
        output.Attributes.SetAttribute("class", "form-buttons");
    }
}

/// <summary>
/// Ein Formularfeld: Label + Inhalt (Input) + optionaler Hinweis.
/// Vereinheitlicht Abstände und Darstellung von Formularen.
/// </summary>
[HtmlTargetElement("form-field")]
public class FormFieldTagHelper : TagHelper
{
    public string? Label { get; set; }
    public string? For { get; set; }
    public string? Hint { get; set; }
    public bool Required { get; set; }

    public override async Task ProcessAsync(TagHelperContext context, TagHelperOutput output)
    {
        var inner = (await output.GetChildContentAsync()).GetContent();
        output.TagName = "div";
        output.Attributes.SetAttribute("class", "form-field");

        var req = Required ? " <span class=\"req\">*</span>" : "";
        var labelHtml = string.IsNullOrEmpty(Label)
            ? ""
            : $"<label{(string.IsNullOrEmpty(For) ? "" : $" for=\"{For}\"")}>{System.Net.WebUtility.HtmlEncode(Label)}{req}</label>";
        var hintHtml = string.IsNullOrEmpty(Hint) ? "" : $"<div class=\"hint\">{System.Net.WebUtility.HtmlEncode(Hint)}</div>";

        output.Content.SetHtmlContent($"{labelHtml}{inner}{hintHtml}");
    }
}
