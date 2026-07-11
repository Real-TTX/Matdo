using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Matdo.Web.Data.Entities;

namespace Matdo.Web.Services.Calendar;

/// <summary>Google-Kalender via OAuth2 + Calendar API v3.</summary>
public class GoogleCalendarProvider : ICalendarProvider
{
    private const string AuthEndpoint = "https://accounts.google.com/o/oauth2/v2/auth";
    private const string TokenEndpoint = "https://oauth2.googleapis.com/token";
    private const string ApiBase = "https://www.googleapis.com/calendar/v3";
    private const string Scope = "https://www.googleapis.com/auth/calendar.events openid email";

    private readonly JsonConfigService _config;
    private readonly IHttpClientFactory _http;

    public GoogleCalendarProvider(JsonConfigService config, IHttpClientFactory http)
    {
        _config = config;
        _http = http;
    }

    public CalendarProvider Provider => CalendarProvider.Google;
    private AppConfig.OAuthConfig Cfg => _config.Current.Google;
    public bool IsConfigured => Cfg.Enabled && !string.IsNullOrWhiteSpace(Cfg.ClientId) && !string.IsNullOrWhiteSpace(Cfg.ClientSecret);

    public string BuildAuthUrl(string redirectUri, string state)
    {
        var q = new Dictionary<string, string?>
        {
            ["client_id"] = Cfg.ClientId,
            ["redirect_uri"] = redirectUri,
            ["response_type"] = "code",
            ["scope"] = Scope,
            ["access_type"] = "offline",
            ["prompt"] = "consent",
            ["state"] = state
        };
        return AuthEndpoint + "?" + string.Join("&", q.Select(kv => $"{Uri.EscapeDataString(kv.Key)}={Uri.EscapeDataString(kv.Value ?? "")}"));
    }

    public async Task<OAuthTokens?> ExchangeCodeAsync(string code, string redirectUri, CancellationToken ct)
    {
        return await TokenRequestAsync(new Dictionary<string, string>
        {
            ["code"] = code,
            ["client_id"] = Cfg.ClientId,
            ["client_secret"] = Cfg.ClientSecret,
            ["redirect_uri"] = redirectUri,
            ["grant_type"] = "authorization_code"
        }, ct);
    }

    public async Task<OAuthTokens?> RefreshAsync(string refreshToken, CancellationToken ct)
    {
        var t = await TokenRequestAsync(new Dictionary<string, string>
        {
            ["client_id"] = Cfg.ClientId,
            ["client_secret"] = Cfg.ClientSecret,
            ["refresh_token"] = refreshToken,
            ["grant_type"] = "refresh_token"
        }, ct);
        // Google gibt beim Refresh kein neues Refresh-Token zurück -> altes behalten.
        return t is null ? null : t with { RefreshToken = t.RefreshToken ?? refreshToken };
    }

    private async Task<OAuthTokens?> TokenRequestAsync(Dictionary<string, string> form, CancellationToken ct)
    {
        var client = _http.CreateClient("calendar");
        using var resp = await client.PostAsync(TokenEndpoint, new FormUrlEncodedContent(form), ct);
        if (!resp.IsSuccessStatusCode) return null;
        using var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync(ct));
        var root = doc.RootElement;
        var access = root.GetProperty("access_token").GetString()!;
        var expires = root.TryGetProperty("expires_in", out var e) ? e.GetInt32() : 3600;
        var refresh = root.TryGetProperty("refresh_token", out var r) ? r.GetString() : null;
        return new OAuthTokens(access, refresh, DateTime.UtcNow.AddSeconds(expires - 60));
    }

    private HttpClient Client(string accessToken)
    {
        var c = _http.CreateClient("calendar");
        c.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        return c;
    }

    public async Task<List<SimpleEvent>> ListEventsAsync(string accessToken, string calendarId, DateTime fromUtc, DateTime toUtc, CancellationToken ct)
    {
        var cal = string.IsNullOrWhiteSpace(calendarId) ? "primary" : calendarId;
        var url = $"{ApiBase}/calendars/{Uri.EscapeDataString(cal)}/events" +
                  $"?timeMin={fromUtc:yyyy-MM-ddTHH:mm:ssZ}&timeMax={toUtc:yyyy-MM-ddTHH:mm:ssZ}" +
                  "&singleEvents=true&orderBy=startTime&maxResults=500";
        var result = new List<SimpleEvent>();
        using var resp = await Client(accessToken).GetAsync(url, ct);
        if (!resp.IsSuccessStatusCode) return result;
        using var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync(ct));
        if (!doc.RootElement.TryGetProperty("items", out var items)) return result;

        foreach (var it in items.EnumerateArray())
        {
            var id = it.GetProperty("id").GetString() ?? "";
            var title = it.TryGetProperty("summary", out var s) ? (s.GetString() ?? "(Termin)") : "(Termin)";
            var (start, allDay) = ReadPoint(it, "start");
            var (end, _) = ReadPoint(it, "end");
            if (start is null) continue;
            result.Add(new SimpleEvent(id, title, start.Value, end ?? start.Value, allDay));
        }
        return result;
    }

    private static (DateTime?, bool) ReadPoint(JsonElement ev, string prop)
    {
        if (!ev.TryGetProperty(prop, out var p)) return (null, false);
        if (p.TryGetProperty("dateTime", out var dt) && dt.GetString() is { } s && DateTimeOffset.TryParse(s, out var dto))
            return (dto.UtcDateTime, false);
        if (p.TryGetProperty("date", out var d) && d.GetString() is { } ds && DateTime.TryParse(ds, out var day))
            return (DateTime.SpecifyKind(day, DateTimeKind.Utc), true);
        return (null, false);
    }

    public async Task<string?> UpsertEventAsync(string accessToken, string calendarId, string? externalId, TaskItem task, CancellationToken ct)
    {
        var cal = string.IsNullOrWhiteSpace(calendarId) ? "primary" : calendarId;
        object body = BuildBody(task);
        var client = Client(accessToken);

        HttpResponseMessage resp;
        if (string.IsNullOrEmpty(externalId))
            resp = await client.PostAsJsonAsync($"{ApiBase}/calendars/{Uri.EscapeDataString(cal)}/events", body, ct);
        else
            resp = await client.PatchAsJsonAsync($"{ApiBase}/calendars/{Uri.EscapeDataString(cal)}/events/{Uri.EscapeDataString(externalId)}", body, ct);

        if (!resp.IsSuccessStatusCode) return null;
        using var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync(ct));
        return doc.RootElement.GetProperty("id").GetString();
    }

    private static object BuildBody(TaskItem task)
    {
        var due = task.DueDate!.Value;
        if (task.DueHasTime)
            return new
            {
                summary = task.Title,
                description = task.Description,
                start = new { dateTime = due.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ssZ") },
                end = new { dateTime = due.ToUniversalTime().AddHours(1).ToString("yyyy-MM-ddTHH:mm:ssZ") }
            };
        var day = due.ToUniversalTime().Date;
        return new
        {
            summary = task.Title,
            description = task.Description,
            start = new { date = day.ToString("yyyy-MM-dd") },
            end = new { date = day.AddDays(1).ToString("yyyy-MM-dd") } // Google: Enddatum exklusiv
        };
    }

    public async Task DeleteEventAsync(string accessToken, string calendarId, string externalId, CancellationToken ct)
    {
        var cal = string.IsNullOrWhiteSpace(calendarId) ? "primary" : calendarId;
        await Client(accessToken).DeleteAsync($"{ApiBase}/calendars/{Uri.EscapeDataString(cal)}/events/{Uri.EscapeDataString(externalId)}", ct);
    }
}
