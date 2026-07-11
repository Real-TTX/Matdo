using Matdo.Web.Data.Entities;

namespace Matdo.Web.Services.Calendar;

/// <summary>Ein externer Termin in vereinfachter Form.</summary>
public record SimpleEvent(string ExternalId, string Title, DateTime StartUtc, DateTime EndUtc, bool AllDay);

/// <summary>OAuth-Tokens eines Anbieters.</summary>
public record OAuthTokens(string AccessToken, string? RefreshToken, DateTime ExpiresAtUtc);

/// <summary>
/// Abstraktion eines OAuth-Kalenderanbieters (Google, Microsoft). Kapselt den
/// Autorisierungs-Flow, Token-Erneuerung sowie Lesen/Schreiben von Terminen.
/// </summary>
public interface ICalendarProvider
{
    CalendarProvider Provider { get; }
    bool IsConfigured { get; }

    string BuildAuthUrl(string redirectUri, string state);
    Task<OAuthTokens?> ExchangeCodeAsync(string code, string redirectUri, CancellationToken ct);
    Task<OAuthTokens?> RefreshAsync(string refreshToken, CancellationToken ct);

    Task<List<SimpleEvent>> ListEventsAsync(string accessToken, string calendarId, DateTime fromUtc, DateTime toUtc, CancellationToken ct);

    /// <summary>Erstellt/aktualisiert einen Termin für eine Aufgabe; liefert die externe Event-Id.</summary>
    Task<string?> UpsertEventAsync(string accessToken, string calendarId, string? externalId, TaskItem task, CancellationToken ct);

    Task DeleteEventAsync(string accessToken, string calendarId, string externalId, CancellationToken ct);
}
