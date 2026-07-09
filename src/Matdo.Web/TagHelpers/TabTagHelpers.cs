using Microsoft.AspNetCore.Razor.TagHelpers;

namespace Matdo.Web.TagHelpers;

/// <summary>Tab-Leiste (Bar). Enthält mehrere &lt;tab-item&gt;-Elemente.</summary>
[HtmlTargetElement("tab-bar")]
public class TabBarTagHelper : TagHelper
{
    public override void Process(TagHelperContext context, TagHelperOutput output)
    {
        output.TagName = "div";
        output.Attributes.SetAttribute("class", "tab-bar");
        output.Attributes.SetAttribute("role", "tablist");
    }
}

/// <summary>
/// Einzelner Tab. Als Link (href) für seitenübergreifende Tabs oder als Button
/// (data-tab-target) für In-Page-Tabs verwendbar.
/// </summary>
[HtmlTargetElement("tab-item")]
public class TabItemTagHelper : TagHelper
{
    public string? Href { get; set; }
    public string? Target { get; set; }
    public bool Active { get; set; }
    public string? Icon { get; set; }

    public override async Task ProcessAsync(TagHelperContext context, TagHelperOutput output)
    {
        var inner = (await output.GetChildContentAsync()).GetContent();
        var cls = Active ? "tab active" : "tab";
        var iconHtml = string.IsNullOrEmpty(Icon) ? "" : $"<i class=\"icon\" data-icon=\"{Icon}\"></i>";

        if (!string.IsNullOrEmpty(Href))
        {
            output.TagName = "a";
            output.Attributes.SetAttribute("href", Href);
        }
        else
        {
            output.TagName = "button";
            output.Attributes.SetAttribute("type", "button");
            if (!string.IsNullOrEmpty(Target))
                output.Attributes.SetAttribute("data-tab-target", Target);
        }

        output.Attributes.SetAttribute("class", cls);
        output.Attributes.SetAttribute("role", "tab");
        output.Attributes.SetAttribute("aria-selected", Active ? "true" : "false");
        output.Content.SetHtmlContent(iconHtml + inner);
    }
}
