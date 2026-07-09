namespace Matdo.Web.Services;

/// <summary>Versionsinformationen, gesetzt beim Docker-Build über die Umgebungsvariable MATDO_VERSION.</summary>
public static class BuildInfo
{
    public static string Version { get; } =
        Environment.GetEnvironmentVariable("MATDO_VERSION") is { Length: > 0 } v ? v : "local-dev";
}
