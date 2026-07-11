using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Matdo.Web.Data.Entities;

namespace Matdo.Web.Services.Calendar;

/// <summary>Microsoft-/Outlook-Kalender via OAuth2 + Microsoft Graph.</summary>
public class MicrosoftCalendarProvider : ICalendarProvider
{
    private const string AuthEndpoint = "https://login.microsoftonline.com/common/oauth2/v2.0/authorize";
    private const string TokenEndpoint = "https://login.microsoftonline.com/common/oauth2/v2.0/token";
    private const string Graph = "https://graph.microsoft.com/v1.0";
    private const string Scope = "offline_access openid email Calendars.ReadWrite";

    private readonly JsonConfigService _config;
    private readonly IHttpClientFactory _http;

    public MicrosoftCalendarProvider(JsonConfigService config, IHttpClientFactory http)
    {
        _config = config;
        _http = http;
    }

    public CalendarProvider Provider => CalendarProvider.Microsoft;
    private AppConfig.OAuthConfig Cfg => _config.Current.Microsoft;
    public bool IsConfigured => Cfg.Enabled && !string.IsNullOrWhiteSpace(Cfg.ClientId) && !string.IsNullOrWhiteSpace(Cfg.ClientSecret);

    public string BuildAuthUrl(string redirectUri, string state)
    {
        var q = new Dictionary<string, string?>
        {
            ["client_id"] = Cfg.ClientId,
            ["redirect_uri"] = redirectUri,
            ["response_type"] = "code",
            ["scope"] = Scope,
            ["response_mode"] = "query",
            ["state"] = state
        };
        return AuthEndpoint + "?" + string.Join("&", q.Select(kv => $"{Uri.EscapeDataString(kv.Key)}={Uri.EscapeDataString(kv.Value ?? "")}"));
    }

    public Task<OAuthTokens?> ExchangeCodeAsync(string code, string redirectUri, CancellationToken ct) =>
        TokenRequestAsync(new Dictionary<string, string>
        {
            ["code"] = code,
            ["client_id"] = Cfg.ClientId,
            ["client_secret"] = Cfg.ClientSecret,
            ["redirect_uri"] = redirectUri,
            ["grant_type"] = "authorization_code",
            ["scope"] = Scope
        }, ct);

    public Task<OAuthTokens?> RefreshAsync(string refreshToken, CancellationToken ct) =>
        TokenRequestAsync(new Dictionary<string, string>
        {
            ["client_id"] = Cfg.ClientId,
            ["client_secret"] = Cfg.ClientSecret,
            ["refresh_token"] = refreshToken,
            ["grant_type"] = "refresh_token",
            ["scope"] = Scope
        }, ct);

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
        c.DefaultRequestHeaders.Add("Prefer", "outlook.timezone=\"UTC\"");
        return c;
    }

    public async Task<List<SimpleEvent>> ListEventsAsync(string accessToken, string calendarId, DateTime fromUtc, DateTime toUtc, CancellationToken ct)
    {
        // calendarView expandiert Serientermine im Zeitraum.
        var url = $"{Graph}/me/calendarView?startDateTime={fromUtc:yyyy-MM-ddTHH:mm:ssZ}&endDateTime={toUtc:yyyy-MM-ddTHH:mm:ssZ}&$top=500&$select=subject,start,end,isAllDay";
        var result = new List<SimpleEvent>();
        using var resp = await Client(accessToken).GetAsync(url, ct);
        if (!resp.IsSuccessStatusCode) return result;
        using var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync(ct));
        if (!doc.RootElement.TryGetProperty("value", out var items)) return result;

        foreach (var it in items.EnumerateArray())
        {
            var id = it.GetProperty("id").GetString() ?? "";
            var title = it.TryGetProperty("subject", out var s) ? (s.GetString() ?? "(Termin)") : "(Termin)";
            var allDay = it.TryGetProperty("isAllDay", out var a) && a.GetBoolean();
            var start = ReadGraphDate(it, "start");
            var end = ReadGraphDate(it, "end");
            if (start is null) continue;
            result.Add(new SimpleEvent(id, title, start.Value, end ?? start.Value, allDay));
        }
        return result;
    }

    private static DateTime? ReadGraphDate(JsonElement ev, string prop)
    {
        if (ev.TryGetProperty(prop, out var p) && p.TryGetProperty("dateTime", out var dt) && dt.GetString() is { } s
            && DateTime.TryParse(s, out var d))
            return DateTime.SpecifyKind(d, DateTimeKind.Utc);
        return null;
    }

    public async Task<string?> UpsertEventAsync(string accessToken, string calendarId, string? externalId, TaskItem task, CancellationToken ct)
    {
        var body = BuildBody(task);
        var client = Client(accessToken);
        HttpResponseMessage resp;
        if (string.IsNullOrEmpty(externalId))
            resp = await client.PostAsJsonAsync($"{Graph}/me/events", body, ct);
        else
            resp = await client.PatchAsJsonAsync($"{Graph}/me/events/{Uri.EscapeDataString(externalId)}", body, ct);

        if (!resp.IsSuccessStatusCode) return null;
        using var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync(ct));
        return doc.RootElement.GetProperty("id").GetString();
    }

    private static object BuildBody(TaskItem task)
    {
        var due = task.DueDate!.Value.ToUniversalTime();
        if (task.DueHasTime)
            return new
            {
                subject = task.Title,
                isAllDay = false,
                start = new { dateTime = due.ToString("yyyy-MM-ddTHH:mm:ss"), timeZone = "UTC" },
                end = new { dateTime = due.AddHours(1).ToString("yyyy-MM-ddTHH:mm:ss"), timeZone = "UTC" }
            };
        var day = due.Date;
        return new
        {
            subject = task.Title,
            isAllDay = true,
            start = new { dateTime = day.ToString("yyyy-MM-ddT00:00:00"), timeZone = "UTC" },
            end = new { dateTime = day.AddDays(1).ToString("yyyy-MM-ddT00:00:00"), timeZone = "UTC" }
        };
    }

    public async Task DeleteEventAsync(string accessToken, string calendarId, string externalId, CancellationToken ct)
    {
        await Client(accessToken).DeleteAsync($"{Graph}/me/events/{Uri.EscapeDataString(externalId)}", ct);
    }
}
