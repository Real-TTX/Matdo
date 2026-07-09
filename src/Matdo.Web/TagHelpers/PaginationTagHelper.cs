using System.Text;
using Microsoft.AspNetCore.Razor.TagHelpers;

namespace Matdo.Web.TagHelpers;

/// <summary>
/// Wiederverwendbare Pagination. Das einzige Element das direkt unter der Tabelle steht.
/// Erzeugt Links über eine URL-Vorlage, in der {page} durch die Seitennummer ersetzt wird.
/// </summary>
[HtmlTargetElement("pagination")]
public class PaginationTagHelper : TagHelper
{
    public int Page { get; set; } = 1;
    public int TotalPages { get; set; } = 1;

    /// <summary>URL-Vorlage mit Platzhalter {page}, z.B. "/Admin/Users?p={page}".</summary>
    public string UrlTemplate { get; set; } = "?p={page}";

    /// <summary>Anzahl der Seitenzahlen links/rechts der aktuellen Seite.</summary>
    public int Window { get; set; } = 2;

    public override void Process(TagHelperContext context, TagHelperOutput output)
    {
        output.TagName = "nav";
        output.Attributes.SetAttribute("class", "pagination");
        output.Attributes.SetAttribute("aria-label", "Seitennavigation");

        if (TotalPages <= 1)
        {
            output.SuppressOutput();
            return;
        }

        var sb = new StringBuilder();
        string Url(int p) => UrlTemplate.Replace("{page}", p.ToString());

        // Zurück
        sb.Append(Page > 1
            ? $"<a class=\"page\" href=\"{Url(Page - 1)}\" rel=\"prev\" aria-label=\"Zurück\">‹</a>"
            : "<span class=\"page disabled\">‹</span>");

        var from = Math.Max(1, Page - Window);
        var to = Math.Min(TotalPages, Page + Window);

        if (from > 1)
        {
            sb.Append($"<a class=\"page\" href=\"{Url(1)}\">1</a>");
            if (from > 2) sb.Append("<span class=\"page ellipsis\">…</span>");
        }

        for (var p = from; p <= to; p++)
        {
            sb.Append(p == Page
                ? $"<span class=\"page active\" aria-current=\"page\">{p}</span>"
                : $"<a class=\"page\" href=\"{Url(p)}\">{p}</a>");
        }

        if (to < TotalPages)
        {
            if (to < TotalPages - 1) sb.Append("<span class=\"page ellipsis\">…</span>");
            sb.Append($"<a class=\"page\" href=\"{Url(TotalPages)}\">{TotalPages}</a>");
        }

        // Weiter
        sb.Append(Page < TotalPages
            ? $"<a class=\"page\" href=\"{Url(Page + 1)}\" rel=\"next\" aria-label=\"Weiter\">›</a>"
            : "<span class=\"page disabled\">›</span>");

        output.Content.SetHtmlContent(sb.ToString());
    }
}
