namespace Matdo.Web.ViewModels;

/// <summary>Aktiver Sortier-/Filterzustand einer Listen-/Projektansicht + URLs zum Entfernen
/// des jeweiligen Filters (vom aufrufenden Page über seinen DUrl-Helfer erzeugt).</summary>
public record ActiveFilters(
    string Sort,
    int Prio,
    bool Done,
    string ClearSortUrl,
    string ClearPrioUrl,
    string ClearDoneUrl);
