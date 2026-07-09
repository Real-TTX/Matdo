using Microsoft.AspNetCore.Razor.TagHelpers;

namespace Matdo.Web.TagHelpers;

/// <summary>
/// Wrapper für eine CRUD-Listenseite. Erzwingt die Struktur der UI-Guidelines:
/// Toolbar (Suche/Filter/Sortierung) oben, Liste in der Mitte,
/// Listen-Aktionen darunter, Pagination ganz unten.
/// Mehrere Instanzen pro Seite sind über die (optionale) id möglich.
/// </summary>
[HtmlTargetElement("list-view")]
public class ListViewTagHelper : TagHelper
{
    public string? Id { get; set; }
    public string? Title { get; set; }

    public override void Process(TagHelperContext context, TagHelperOutput output)
    {
        output.TagName = "section";
        output.Attributes.SetAttribute("class", "list-view");
        if (!string.IsNullOrEmpty(Id))
            output.Attributes.SetAttribute("id", Id);
    }
}

/// <summary>Toolbar über der Liste: Suchtext, Filter und Sortierung.</summary>
[HtmlTargetElement("list-toolbar")]
public class ListToolbarTagHelper : TagHelper
{
    public override void Process(TagHelperContext context, TagHelperOutput output)
    {
        output.TagName = "div";
        output.Attributes.SetAttribute("class", "list-toolbar");
    }
}

/// <summary>
/// Tabellen-Container. Der Seiteninhalt liefert thead/tbody selbst,
/// wodurch beliebige Zell-Inhalte möglich sind. Sorgt für horizontales Scrollen
/// auf kleinen Bildschirmen (Mobile-Optimierung).
/// </summary>
[HtmlTargetElement("list-table")]
public class ListTableTagHelper : TagHelper
{
    /// <summary>Zusätzliche CSS-Klassen für das table-Element.</summary>
    public string? TableClass { get; set; }

    public override async Task ProcessAsync(TagHelperContext context, TagHelperOutput output)
    {
        var inner = await output.GetChildContentAsync();
        output.TagName = "div";
        output.Attributes.SetAttribute("class", "table-wrap");
        var cls = string.IsNullOrEmpty(TableClass) ? "table" : $"table {TableClass}";
        output.Content.SetHtmlContent($"<table class=\"{cls}\">{inner.GetContent()}</table>");
    }
}

/// <summary>
/// Aktionsleiste unterhalb der Tabelle. Buttons sind links bündig; Elemente mit
/// der Klasse "danger" (z.B. Löschen) werden automatisch mit Abstand nach rechts geschoben.
/// </summary>
[HtmlTargetElement("list-actions")]
public class ListActionsTagHelper : TagHelper
{
    public override void Process(TagHelperContext context, TagHelperOutput output)
    {
        output.TagName = "div";
        output.Attributes.SetAttribute("class", "list-actions");
    }
}
