namespace Matdo.Web.ViewModels;

/// <summary>Optionen für den Projekt-Picker (<c>_ProjectPicker.cshtml</c>).</summary>
public class ProjectPickerOptions
{
    /// <summary>Name des versteckten Felds, das die gewählte ProjectId trägt.</summary>
    public string FieldName { get; set; } = "Input.ProjectId";

    /// <summary>Aktuell gewähltes Projekt (null = Eingang).</summary>
    public long? SelectedId { get; set; }
}
