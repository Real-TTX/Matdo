using System.Globalization;
using System.Text.RegularExpressions;
using Matdo.Web.Data;
using Matdo.Web.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace Matdo.Web.Services;

/// <summary>Ergebnis des Parsens einer Schnell-Eingabe.</summary>
public record ParsedInput(string Title, long? ProjectId, List<long> LabelIds, long? AssigneeId, DateTime? DueUtc, bool DueHasTime);

/// <summary>
/// Erkennt in einem Aufgabentext Tokens (#Projekt, +Etikett, @Person) und natürliche
/// Zeitangaben (heute, morgen, übermorgen, Wochentage, "10 Uhr", "11:30") und liefert die
/// aufgelösten Werte + den bereinigten Titel zurück. Deutsch und Englisch.
/// </summary>
public class SmartInputParser
{
    private readonly MatdoDbContext _db;
    private readonly ICurrentUserAccessor _me;
    private readonly LabelService _labels;

    public SmartInputParser(MatdoDbContext db, ICurrentUserAccessor me, LabelService labels)
    {
        _db = db;
        _me = me;
        _labels = labels;
    }

    public async Task<ParsedInput> ParseAsync(string? raw)
    {
        var text = (raw ?? "").Trim();
        if (text.Length == 0) return new ParsedInput("", null, new(), null, null, false);

        var uid = _me.UserId ?? 0;

        // Nur Projekte, in die auch geschrieben werden darf (Owner oder Edit-Freigabe) –
        // sonst würde der #-Token entfernt, die Zuordnung aber später still verworfen.
        var projects = await _db.Projects
            .Where(p => !p.IsArchived && (
                p.OwnerId == uid
                || p.Shares.Any(s => s.SharedWithUserId == uid && s.Permission == SharePermission.Edit)
                || (p.TeamId != null && p.Team!.Members.Any(m => m.UserId == uid))))
            .Select(p => new NamedId(p.Id, p.Name)).ToListAsync();
        var users = await Collaborators.Query(_db, uid)
            .Select(u => new NamedId(u.Id, u.DisplayName)).ToListAsync();
        var labels = await _db.Labels
            .Where(l => l.OwnerId == uid)
            .Select(l => new NamedId(l.Id, l.Name)).ToListAsync();

        long? projectId = null, assigneeId = null;
        var labelIds = new List<long>();

        (text, projectId) = MatchOne(text, '#', projects);
        (text, assigneeId) = MatchOne(text, '@', users);

        // Bekannte Etiketten (mehrfach möglich)
        while (true)
        {
            (text, var lid) = MatchOne(text, '+', labels);
            if (lid is null) break;
            labelIds.Add(lid.Value);
            labels = labels.Where(l => l.Id != lid.Value).ToList();
        }
        // Neue Etiketten (+wort) anlegen
        foreach (Match m in Regex.Matches(text, @"(?<=^|\s)\+([\p{L}\p{N}_][\p{L}\p{N}_-]*)"))
        {
            var label = await _labels.GetOrCreateByNameAsync(m.Groups[1].Value);
            labelIds.Add(label.Id);
        }
        text = Regex.Replace(text, @"(?<=^|\s)\+[\p{L}\p{N}_][\p{L}\p{N}_-]*", "");

        var (rest, dueUtc, hasTime) = ParseDate(text);
        text = rest;

        var title = Regex.Replace(text, @"\s{2,}", " ").Trim();
        if (title.Length == 0) title = raw!.Trim(); // Titel nie ganz verlieren

        return new ParsedInput(title, projectId, labelIds.Distinct().ToList(), assigneeId, dueUtc, hasTime);
    }

    private record NamedId(long Id, string Name);

    /// <summary>Findet trigger+Name (längster Treffer, case-insensitiv), entfernt ihn aus dem Text.</summary>
    private static (string, long?) MatchOne(string text, char trigger, List<NamedId> candidates)
    {
        foreach (var c in candidates.Where(c => !string.IsNullOrWhiteSpace(c.Name)).OrderByDescending(c => c.Name.Length))
        {
            // Trigger nur am Wortanfang (Start oder nach Leerraum) – nicht mitten im Wort (z.B. E-Mails, "a+b").
            var pattern = @"(?<=^|\s)" + Regex.Escape(trigger.ToString()) + Regex.Escape(c.Name) + @"(?=\s|$|[.,;:!?])";
            var m = Regex.Match(text, pattern, RegexOptions.IgnoreCase);
            if (m.Success)
                return (text.Remove(m.Index, m.Length), c.Id);
        }
        return (text, null);
    }

    private static readonly Dictionary<string, DayOfWeek> Weekdays = new(StringComparer.OrdinalIgnoreCase)
    {
        ["montag"] = DayOfWeek.Monday, ["monday"] = DayOfWeek.Monday,
        ["dienstag"] = DayOfWeek.Tuesday, ["tuesday"] = DayOfWeek.Tuesday,
        ["mittwoch"] = DayOfWeek.Wednesday, ["wednesday"] = DayOfWeek.Wednesday,
        ["donnerstag"] = DayOfWeek.Thursday, ["thursday"] = DayOfWeek.Thursday,
        ["freitag"] = DayOfWeek.Friday, ["friday"] = DayOfWeek.Friday,
        ["samstag"] = DayOfWeek.Saturday, ["sonnabend"] = DayOfWeek.Saturday, ["saturday"] = DayOfWeek.Saturday,
        ["sonntag"] = DayOfWeek.Sunday, ["sunday"] = DayOfWeek.Sunday,
    };

    private static (string text, DateTime? dueUtc, bool hasTime) ParseDate(string text)
    {
        var now = DateTime.Now;
        DateTime? date = null;
        TimeSpan? time = null;

        // Uhrzeit: "11:30", "10 Uhr", "11:30 Uhr", "10am/pm"
        var tm = Regex.Match(text, @"(?<=^|\s)(\d{1,2}):(\d{2})(?:\s*uhr)?(?=\s|$|[.,;!?])", RegexOptions.IgnoreCase);
        if (!tm.Success)
            tm = Regex.Match(text, @"(?<=^|\s)(\d{1,2})\s*uhr(?=\s|$|[.,;!?])", RegexOptions.IgnoreCase);
        if (tm.Success)
        {
            var h = int.Parse(tm.Groups[1].Value);
            var min = tm.Groups.Count > 2 && tm.Groups[2].Success ? int.Parse(tm.Groups[2].Value) : 0;
            if (h is >= 0 and < 24 && min is >= 0 and < 60)
            {
                time = new TimeSpan(h, min, 0);
                text = text.Remove(tm.Index, tm.Length);
            }
        }

        // Relative Tagesangaben
        (string term, int offset)[] rel =
        {
            ("übermorgen", 2), ("uebermorgen", 2), ("day after tomorrow", 2),
            ("morgen", 1), ("tomorrow", 1),
            ("heute", 0), ("today", 0),
            ("gestern", -1), ("yesterday", -1),
        };
        foreach (var (term, offset) in rel)
        {
            var m = Regex.Match(text, $@"(?<=^|\s){Regex.Escape(term)}(?=\s|$|[.,;!?])", RegexOptions.IgnoreCase);
            if (m.Success) { date = now.Date.AddDays(offset); text = text.Remove(m.Index, m.Length); break; }
        }

        // "in X Tagen" / "in X days"
        if (date is null)
        {
            var m = Regex.Match(text, @"(?<=^|\s)in\s+(\d{1,3})\s+(tag(?:en)?|days?)(?=\s|$|[.,;!?])", RegexOptions.IgnoreCase);
            if (m.Success) { date = now.Date.AddDays(int.Parse(m.Groups[1].Value)); text = text.Remove(m.Index, m.Length); }
        }

        // Wochentage (optional "nächsten/next")
        if (date is null)
        {
            foreach (var kv in Weekdays)
            {
                var m = Regex.Match(text, $@"(?<=^|\s)(n(?:ä|ae)chste[rn]?\s+|next\s+)?{kv.Key}(?=\s|$|[.,;!?])", RegexOptions.IgnoreCase);
                if (!m.Success) continue;
                var days = (((int)kv.Value - (int)now.DayOfWeek) + 7) % 7;
                if (days == 0) days = 7;                 // Wochentagsname = nächstes Vorkommen
                if (m.Groups[1].Success) days += 7;       // "nächsten"
                date = now.Date.AddDays(days);
                text = text.Remove(m.Index, m.Length);
                break;
            }
        }

        if (date is null && time is null) return (text, null, false);
        if (date is null) date = now.Date; // nur Uhrzeit -> heute

        var local = date.Value.Date + (time ?? TimeSpan.Zero);
        var utc = DateTime.SpecifyKind(local, DateTimeKind.Local).ToUniversalTime();
        return (text, utc, time.HasValue);
    }
}
