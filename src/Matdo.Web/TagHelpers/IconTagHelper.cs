using Microsoft.AspNetCore.Razor.TagHelpers;

namespace Matdo.Web.TagHelpers;

/// <summary>
/// Inline-SVG-Icons (Lucide-Stil, Strichzeichnung). Keine externe Schrift/Datei,
/// damit die Icons auch offline in der PWA funktionieren.
/// Verwendung: &lt;icon name="check" /&gt;
/// </summary>
[HtmlTargetElement("icon")]
public class IconTagHelper : TagHelper
{
    public string Name { get; set; } = "";
    public int Size { get; set; } = 20;

    // Nur die inneren Pfade je Icon; das svg-Rahmenelement wird gemeinsam erzeugt.
    private static readonly Dictionary<string, string> Icons = new()
    {
        ["inbox"] = "<path d='M22 12h-6l-2 3h-4l-2-3H2'/><path d='M5.45 5.11 2 12v6a2 2 0 0 0 2 2h16a2 2 0 0 0 2-2v-6l-3.45-6.89A2 2 0 0 0 16.76 4H7.24a2 2 0 0 0-1.79 1.11z'/>",
        ["today"] = "<rect width='18' height='18' x='3' y='4' rx='2'/><path d='M3 10h18M8 2v4M16 2v4'/><path d='M8 14h.01M12 14h.01M16 14h.01'/>",
        ["upcoming"] = "<rect width='18' height='18' x='3' y='4' rx='2'/><path d='M3 10h18M8 2v4M16 2v4'/><path d='m9 16 2 2 4-4'/>",
        ["calendar"] = "<rect width='18' height='18' x='3' y='4' rx='2'/><path d='M3 10h18M8 2v4M16 2v4'/>",
        ["tag"] = "<path d='M12.586 2.586A2 2 0 0 0 11.172 2H4a2 2 0 0 0-2 2v7.172a2 2 0 0 0 .586 1.414l8.704 8.704a2.426 2.426 0 0 0 3.42 0l6.58-6.58a2.426 2.426 0 0 0 0-3.42z'/><circle cx='7.5' cy='7.5' r='.5' fill='currentColor'/>",
        ["filter"] = "<polygon points='22 3 2 3 10 12.46 10 19 14 21 14 12.46 22 3'/>",
        ["search"] = "<circle cx='11' cy='11' r='8'/><path d='m21 21-4.3-4.3'/>",
        ["plus"] = "<path d='M5 12h14M12 5v14'/>",
        ["hash"] = "<line x1='4' x2='20' y1='9' y2='9'/><line x1='4' x2='20' y1='15' y2='15'/><line x1='10' x2='8' y1='3' y2='21'/><line x1='16' x2='14' y1='3' y2='21'/>",
        ["settings"] = "<path d='M12.22 2h-.44a2 2 0 0 0-2 2v.18a2 2 0 0 1-1 1.73l-.43.25a2 2 0 0 1-2 0l-.15-.08a2 2 0 0 0-2.73.73l-.22.38a2 2 0 0 0 .73 2.73l.15.1a2 2 0 0 1 1 1.72v.51a2 2 0 0 1-1 1.74l-.15.09a2 2 0 0 0-.73 2.73l.22.38a2 2 0 0 0 2.73.73l.15-.08a2 2 0 0 1 2 0l.43.25a2 2 0 0 1 1 1.73V20a2 2 0 0 0 2 2h.44a2 2 0 0 0 2-2v-.18a2 2 0 0 1 1-1.73l.43-.25a2 2 0 0 1 2 0l.15.08a2 2 0 0 0 2.73-.73l.22-.39a2 2 0 0 0-.73-2.73l-.15-.08a2 2 0 0 1-1-1.74v-.5a2 2 0 0 1 1-1.74l.15-.09a2 2 0 0 0 .73-2.73l-.22-.38a2 2 0 0 0-2.73-.73l-.15.08a2 2 0 0 1-2 0l-.43-.25a2 2 0 0 1-1-1.73V4a2 2 0 0 0-2-2z'/><circle cx='12' cy='12' r='3'/>",
        ["users"] = "<path d='M16 21v-2a4 4 0 0 0-4-4H6a4 4 0 0 0-4 4v2'/><circle cx='9' cy='7' r='4'/><path d='M22 21v-2a4 4 0 0 0-3-3.87M16 3.13a4 4 0 0 1 0 7.75'/>",
        ["user"] = "<circle cx='12' cy='8' r='5'/><path d='M20 21a8 8 0 0 0-16 0'/>",
        ["group"] = "<path d='M18 21a8 8 0 0 0-16 0'/><circle cx='10' cy='8' r='5'/><path d='M22 20c0-3.37-2-6.5-4-8a5 5 0 0 0-.45-8.3'/>",
        ["logout"] = "<path d='M9 21H5a2 2 0 0 1-2-2V5a2 2 0 0 1 2-2h4'/><polyline points='16 17 21 12 16 7'/><line x1='21' x2='9' y1='12' y2='12'/>",
        ["check"] = "<path d='M20 6 9 17l-5-5'/>",
        ["flag"] = "<path d='M4 15s1-1 4-1 5 2 8 2 4-1 4-1V3s-1 1-4 1-5-2-8-2-4 1-4 1z'/><line x1='4' x2='4' y1='22' y2='15'/>",
        ["bell"] = "<path d='M10.268 21a2 2 0 0 0 3.464 0'/><path d='M3.262 15.326A1 1 0 0 0 4 17h16a1 1 0 0 0 .74-1.673C19.41 13.956 18 12.499 18 8A6 6 0 0 0 6 8c0 4.499-1.411 5.956-2.738 7.326z'/>",
        ["trash"] = "<path d='M3 6h18M19 6v14a2 2 0 0 1-2 2H7a2 2 0 0 1-2-2V6m3 0V4a2 2 0 0 1 2-2h4a2 2 0 0 1 2 2v2'/><line x1='10' x2='10' y1='11' y2='17'/><line x1='14' x2='14' y1='11' y2='17'/>",
        ["edit"] = "<path d='M11 4H4a2 2 0 0 0-2 2v14a2 2 0 0 0 2 2h14a2 2 0 0 0 2-2v-7'/><path d='M18.5 2.5a2.12 2.12 0 0 1 3 3L12 15l-4 1 1-4z'/>",
        ["back"] = "<path d='m12 19-7-7 7-7'/><path d='M19 12H5'/>",
        ["save"] = "<path d='M15.2 3a2 2 0 0 1 1.4.6l3.8 3.8a2 2 0 0 1 .6 1.4V19a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2V5a2 2 0 0 1 2-2z'/><path d='M17 21v-7a1 1 0 0 0-1-1H8a1 1 0 0 0-1 1v7M7 3v4a1 1 0 0 0 1 1h7'/>",
        ["upload"] = "<path d='M21 15v4a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2v-4'/><polyline points='17 8 12 3 7 8'/><line x1='12' x2='12' y1='3' y2='15'/>",
        ["kanban"] = "<path d='M5 3v18M19 3v18M12 3v10'/><rect width='18' height='18' x='3' y='3' rx='2'/>",
        ["list"] = "<line x1='8' x2='21' y1='6' y2='6'/><line x1='8' x2='21' y1='12' y2='12'/><line x1='8' x2='21' y1='18' y2='18'/><line x1='3' x2='3.01' y1='6' y2='6'/><line x1='3' x2='3.01' y1='12' y2='12'/><line x1='3' x2='3.01' y1='18' y2='18'/>",
        ["star"] = "<polygon points='12 2 15.09 8.26 22 9.27 17 14.14 18.18 21.02 12 17.77 5.82 21.02 7 14.14 2 9.27 8.91 8.26 12 2'/>",
        ["clock"] = "<circle cx='12' cy='12' r='10'/><polyline points='12 6 12 12 16 14'/>",
        ["share"] = "<circle cx='18' cy='5' r='3'/><circle cx='6' cy='12' r='3'/><circle cx='18' cy='19' r='3'/><line x1='8.59' x2='15.42' y1='13.51' y2='17.49'/><line x1='15.41' x2='8.59' y1='6.51' y2='10.49'/>",
        ["chart"] = "<path d='M3 3v16a2 2 0 0 0 2 2h16'/><path d='M18 17V9M13 17V5M8 17v-3'/>",
        ["menu"] = "<line x1='4' x2='20' y1='12' y2='12'/><line x1='4' x2='20' y1='6' y2='6'/><line x1='4' x2='20' y1='18' y2='18'/>",
        ["x"] = "<path d='M18 6 6 18M6 6l12 12'/>",
        ["chevron-down"] = "<path d='m6 9 6 6 6-6'/>",
        ["chevron-right"] = "<path d='m9 18 6-6-6-6'/>",
        ["dots"] = "<circle cx='12' cy='12' r='1'/><circle cx='19' cy='12' r='1'/><circle cx='5' cy='12' r='1'/>",
        ["copy"] = "<rect width='14' height='14' x='8' y='8' rx='2' ry='2'/><path d='M4 16c-1.1 0-2-.9-2-2V4c0-1.1.9-2 2-2h10c1.1 0 2 .9 2 2'/>",
        ["star"] = "<polygon points='12 2 15.09 8.26 22 9.27 17 14.14 18.18 21.02 12 17.77 5.82 21.02 7 14.14 2 9.27 8.91 8.26 12 2'/>",
        ["link"] = "<path d='M10 13a5 5 0 0 0 7.54.54l3-3a5 5 0 0 0-7.07-7.07l-1.72 1.71'/><path d='M14 11a5 5 0 0 0-7.54-.54l-3 3a5 5 0 0 0 7.07 7.07l1.71-1.71'/>",
        ["archive"] = "<rect width='20' height='5' x='2' y='3' rx='1'/><path d='M4 8v11a2 2 0 0 0 2 2h12a2 2 0 0 0 2-2V8'/><path d='M10 12h4'/>",
    };

    public override void Process(TagHelperContext context, TagHelperOutput output)
    {
        output.TagName = "svg";
        output.TagMode = TagMode.StartTagAndEndTag;
        output.Attributes.SetAttribute("width", Size);
        output.Attributes.SetAttribute("height", Size);
        output.Attributes.SetAttribute("viewBox", "0 0 24 24");
        output.Attributes.SetAttribute("fill", "none");
        output.Attributes.SetAttribute("stroke", "currentColor");
        output.Attributes.SetAttribute("stroke-width", "2");
        output.Attributes.SetAttribute("stroke-linecap", "round");
        output.Attributes.SetAttribute("stroke-linejoin", "round");
        output.Attributes.SetAttribute("class", "svg-icon");
        output.Attributes.SetAttribute("aria-hidden", "true");
        output.Content.SetHtmlContent(Icons.TryGetValue(Name, out var path) ? path : "");
    }
}
