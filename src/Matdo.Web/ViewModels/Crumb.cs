namespace Matdo.Web.ViewModels;

/// <summary>Ein Segment des Breadcrumb-Pfads in der Topbar. Href = null -> aktuelles (nicht verlinktes) Segment.</summary>
public record Crumb(string Label, string? Href = null);
