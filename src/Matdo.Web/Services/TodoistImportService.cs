using System.Globalization;
using System.IO.Compression;
using System.Text;
using System.Text.RegularExpressions;
using Matdo.Web.Data;
using Matdo.Web.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace Matdo.Web.Services;

/// <summary>
/// Importiert einen Todoist-Projektexport („Als Vorlage exportieren" → CSV) als Matdo-Projekt.
/// Kopfzeile z.B.: TYPE, CONTENT, [DESCRIPTION,] PRIORITY, INDENT, …, DATE, DATE_LANG, TIMEZONE.
/// Sektionen → Kanban-Spalten, INDENT → Unteraufgaben, @Tokens im CONTENT → Etiketten.
/// Wichtig: Im CSV ist PRIORITY 1 = höchste (P1) … 4 = niedrigste (P4) – invertiert zur Todoist-API!
/// Der gesamte Import läuft in EINER Transaktion und wird EINMAL gespeichert.
/// </summary>
public class TodoistImportService
{
    private const int MaxTasks = 5000;   // Obergrenze gegen sehr große/bösartige Dateien
    private const int MaxBackupProjects = 300;            // max. CSVs (Projekte) je Backup-ZIP
    private const long MaxEntryBytes = 8L * 1024 * 1024;  // max. entpackte Größe je CSV
    private const long MaxTotalUncompressed = 150L * 1024 * 1024; // Zip-Bomben-Schutz gesamt
    private const int MaxWarnings = 40;                   // Warnungen für die Anzeige begrenzen

    private readonly MatdoDbContext _db;
    private readonly ICurrentUserAccessor _me;
    private readonly LabelService _labels;

    public TodoistImportService(MatdoDbContext db, ICurrentUserAccessor me, LabelService labels)
    {
        _db = db;
        _me = me;
        _labels = labels;
    }

    private long Uid => _me.UserId ?? throw new InvalidOperationException("Kein angemeldeter Benutzer.");

    public record ImportResult(bool Ok, int Sections, int Tasks, int Labels, List<string> Warnings, string? ProjectName = null, long ProjectId = 0);

    /// <summary>Ergebnis eines Backup-Imports (ZIP mit mehreren Projekt-CSVs).</summary>
    public record BackupResult(bool Ok, int Projects, int Tasks, int Sections, List<string> Warnings);

    // @label-Token: muss mit einem Buchstaben beginnen (schließt @30min, @/munich u.ä. aus).
    private static readonly Regex LabelRx = new(@"(?<=^|\s)@([\p{L}][\p{L}\p{N}_-]*)", RegexOptions.Compiled);

    public async Task<ImportResult> ImportTodoistCsvAsync(string fileName, string content, string? projectName)
    {
        var rows = ParseCsv(content);
        if (rows.Count == 0)
            return new ImportResult(false, 0, 0, 0, new() { "Die Datei ist leer." });

        var header = rows[0];
        int Col(string n) { for (var i = 0; i < header.Length; i++) if (string.Equals(header[i].Trim(), n, StringComparison.OrdinalIgnoreCase)) return i; return -1; }
        int cType = Col("TYPE"), cContent = Col("CONTENT"), cDesc = Col("DESCRIPTION"),
            cPrio = Col("PRIORITY"), cIndent = Col("INDENT"), cDate = Col("DATE"), cDateLang = Col("DATE_LANG");

        if (cType < 0 || cContent < 0)
            return new ImportResult(false, 0, 0, 0, new() { "Unerwartetes Format – bitte einen Todoist-CSV-Export (Als Vorlage exportieren) hochladen." });

        static string Get(string[] r, int idx) => (idx >= 0 && idx < r.Length) ? r[idx] : "";

        var warnings = new List<string>();
        var data = rows.Skip(1).ToList();

        var sectionNames = data
            .Where(r => string.Equals(Get(r, cType).Trim(), "section", StringComparison.Ordinal))
            .Select(r => Get(r, cContent).Trim())
            .Where(s => s.Length > 0)
            .Distinct()
            .ToList();
        var hasSections = sectionNames.Count > 0;

        var name = !string.IsNullOrWhiteSpace(projectName) ? projectName.Trim() : DeriveName(fileName);

        // Alles in einer Transaktion – bricht der Import ab, bleibt kein halbes Projekt zurück.
        await using var tx = await _db.Database.BeginTransactionAsync();

        var project = new Project
        {
            OwnerId = Uid,
            Name = name,
            Color = "#8300bc",
            ViewType = hasSections ? ProjectViewType.Kanban : ProjectViewType.List
        };
        _db.Projects.Add(project);
        await _db.SaveChangesAsync();   // Id für Spalten/Aufgaben

        var columnByName = new Dictionary<string, long>(StringComparer.Ordinal);
        if (hasSections)
        {
            var pos = 0;
            foreach (var s in sectionNames)
                _db.KanbanColumns.Add(new KanbanColumn { ProjectId = project.Id, Name = s, Position = pos++ });
            await _db.SaveChangesAsync();
            foreach (var col in await _db.KanbanColumns.Where(c => c.ProjectId == project.Id).ToListAsync())
                columnByName[col.Name] = col.Id;
        }

        // Etiketten vorab auflösen (einmal je Name; nutzt/erweitert die persönlichen Labels).
        var labelCache = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);
        foreach (var r in data)
        {
            if (!string.Equals(Get(r, cType).Trim(), "task", StringComparison.Ordinal)) continue;
            foreach (Match m in LabelRx.Matches(Get(r, cContent)))
            {
                var ln = m.Groups[1].Value;
                if (!labelCache.ContainsKey(ln))
                    labelCache[ln] = (await _labels.GetOrCreateByNameAsync(ln)).Id;
            }
        }

        long? currentColumn = null;
        int taskCount = 0, position = 0;
        var truncated = false;
        var parents = new List<(int indent, TaskItem task)>();   // Eltern-Stapel (Entitäten)

        foreach (var r in data)
        {
            var type = Get(r, cType).Trim();

            if (string.Equals(type, "section", StringComparison.Ordinal))
            {
                var sname = Get(r, cContent).Trim();
                currentColumn = (hasSections && columnByName.TryGetValue(sname, out var cid)) ? cid : null;
                parents.Clear();
                continue;
            }
            if (!string.Equals(type, "task", StringComparison.Ordinal)) continue;

            if (taskCount >= MaxTasks) { truncated = true; break; }

            var rawTitle = Get(r, cContent).Trim();
            if (rawTitle.Length == 0) continue;

            var labelNames = new List<string>();
            var title = LabelRx.Replace(rawTitle, m => { labelNames.Add(m.Groups[1].Value); return " "; });
            title = Regex.Replace(title, @"\s{2,}", " ").Trim();
            if (title.Length == 0) title = rawTitle;

            var indent = 1;
            int.TryParse(Get(r, cIndent).Trim(), out indent);
            if (indent < 1) indent = 1;

            while (parents.Count > 0 && parents[^1].indent >= indent) parents.RemoveAt(parents.Count - 1);
            var parent = parents.Count > 0 ? parents[^1].task : null;

            var (dueUtc, hasTime, dateWarn) = ParseDate(Get(r, cDate).Trim(), Get(r, cDateLang).Trim());
            if (dateWarn != null) warnings.Add($"„{Trunc(title)}“: {dateWarn}");

            var task = new TaskItem
            {
                OwnerId = Uid,
                ProjectId = project.Id,
                Title = title,
                Description = string.IsNullOrWhiteSpace(Get(r, cDesc)) ? null : Get(r, cDesc).Trim(),
                Priority = MapPriority(Get(r, cPrio).Trim()),
                DueDate = dueUtc,
                DueHasTime = hasTime,
                // Nur Top-Level-Aufgaben landen in einer Spalte; Unteraufgaben hängen am Elternteil.
                KanbanColumnId = parent == null ? currentColumn : null,
                ParentTask = parent,          // Navigation: EF setzt ParentTaskId beim Speichern
                Position = position++
            };
            foreach (var ln in labelNames.Distinct(StringComparer.OrdinalIgnoreCase))
                if (labelCache.TryGetValue(ln, out var lid))
                    task.TaskLabels.Add(new TaskLabel { LabelId = lid });

            _db.Tasks.Add(task);
            taskCount++;
            parents.Add((indent, task));
        }

        await _db.SaveChangesAsync();     // ein einziger Insert-Batch
        await tx.CommitAsync();

        if (truncated) warnings.Insert(0, $"Es wurden nur die ersten {MaxTasks} Aufgaben importiert.");
        return new ImportResult(true, columnByName.Count, taskCount, labelCache.Count, warnings, project.Name, project.Id);
    }

    /// <summary>
    /// Importiert ein Todoist-Backup-ZIP (eine CSV je Projekt) als mehrere Matdo-Projekte.
    /// Jede CSV wird eigenständig (eigene Transaktion) importiert; scheitert eine, laufen die
    /// übrigen trotzdem durch (der ChangeTracker wird zwischen den CSVs geleert).
    /// </summary>
    public async Task<BackupResult> ImportTodoistBackupZipAsync(Stream zipStream)
    {
        var warnings = new List<string>();
        int projects = 0, tasks = 0, sections = 0;

        ZipArchive archive;
        try { archive = new ZipArchive(zipStream, ZipArchiveMode.Read, leaveOpen: true); }
        catch (InvalidDataException)
        {
            return new BackupResult(false, 0, 0, 0, new() { "Die Datei ist kein gültiges ZIP-Archiv." });
        }

        using (archive)
        {
            // Nur echte CSV-Dateien; macOS-Beiwerk (__MACOSX/…, ._*) ignorieren; stabile Reihenfolge.
            var entries = archive.Entries
                .Where(e => e.Name.Length > 0
                    && e.FullName.EndsWith(".csv", StringComparison.OrdinalIgnoreCase)
                    && !e.FullName.StartsWith("__MACOSX", StringComparison.OrdinalIgnoreCase)
                    && !e.Name.StartsWith("._", StringComparison.Ordinal))
                .OrderBy(e => e.FullName, StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (entries.Count == 0)
                return new BackupResult(false, 0, 0, 0, new() { "Das ZIP enthält keine CSV-Dateien. Bitte ein Todoist-Backup hochladen." });

            long totalBytes = 0;
            var processed = 0;
            foreach (var entry in entries)
            {
                if (processed >= MaxBackupProjects)
                { warnings.Add($"Es wurden nur die ersten {MaxBackupProjects} Projekte importiert."); break; }
                totalBytes += entry.Length;
                if (totalBytes > MaxTotalUncompressed)
                { warnings.Add("Das Backup ist zu groß – die restlichen Projekte wurden übersprungen."); break; }

                var name = DeriveName(entry.Name);
                string content;
                try { content = await ReadEntryTextAsync(entry, MaxEntryBytes); }
                catch (Exception)
                { warnings.Add($"„{name}“: konnte nicht gelesen werden – übersprungen."); processed++; continue; }

                try
                {
                    var r = await ImportTodoistCsvAsync(entry.Name, content, name);
                    if (r.Ok)
                    {
                        projects++; tasks += r.Tasks; sections += r.Sections;
                        foreach (var w in r.Warnings) warnings.Add($"„{name}“: {w}");
                    }
                    else warnings.Add($"„{name}“ übersprungen: {r.Warnings.FirstOrDefault() ?? "unbekanntes Format"}");
                }
                catch (Exception)
                {
                    warnings.Add($"„{name}“ konnte nicht importiert werden – übersprungen.");
                }
                finally
                {
                    // Wichtig: Reste (auch zurückgerollte Entitäten) verwerfen, damit das nächste
                    // Projekt sauberen Zustand hat und nichts doppelt eingefügt wird.
                    _db.ChangeTracker.Clear();
                }
                processed++;
            }
        }

        if (warnings.Count > MaxWarnings)
        {
            var extra = warnings.Count - MaxWarnings;
            warnings = warnings.Take(MaxWarnings).ToList();
            warnings.Add($"… und {extra} weitere Hinweise.");
        }
        return new BackupResult(projects > 0, projects, tasks, sections, warnings);
    }

    /// <summary>Liest einen ZIP-Eintrag als Text (UTF-8, BOM-aware) mit harter Byte-Obergrenze.</summary>
    private static async Task<string> ReadEntryTextAsync(ZipArchiveEntry entry, long maxBytes)
    {
        await using var es = entry.Open();
        using var ms = new MemoryStream();
        var buffer = new byte[81920];
        long total = 0;
        int read;
        while ((read = await es.ReadAsync(buffer)) > 0)
        {
            total += read;
            if (total > maxBytes) throw new InvalidOperationException("Eintrag zu groß.");
            ms.Write(buffer, 0, read);
        }
        ms.Position = 0;
        using var reader = new StreamReader(ms, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
        return await reader.ReadToEndAsync();
    }

    /// <summary>Todoist-CSV-Priorität (1 = höchste/P1 … 4 = niedrigste/P4) auf unser Enum (P1=1…P4=4) abbilden.</summary>
    private static TaskPriority MapPriority(string csv)
    {
        if (int.TryParse(csv, out var p) && p is >= 1 and <= 4)
            return (TaskPriority)p;      // 1→P1, 2→P2, 3→P3, 4→P4
        return TaskPriority.P4;          // leer / unbekannt → keine Priorität
    }

    private static (DateTime? utc, bool hasTime, string? warning) ParseDate(string raw, string lang)
    {
        if (string.IsNullOrWhiteSpace(raw)) return (null, false, null);
        var s = raw.Trim();
        try
        {
            // Führendes ISO-Datum übernehmen – auch bei wiederkehrenden wie „2017-09-21 Monthly".
            var iso = Regex.Match(s, @"^(\d{4})-(\d{2})-(\d{2})(?:[ T](\d{2}):(\d{2}))?");
            if (iso.Success)
            {
                var y = int.Parse(iso.Groups[1].Value); var mo = int.Parse(iso.Groups[2].Value); var da = int.Parse(iso.Groups[3].Value);
                var hasT = iso.Groups[4].Success;
                var hh = hasT ? int.Parse(iso.Groups[4].Value) : 0; var mi = hasT ? int.Parse(iso.Groups[5].Value) : 0;
                if (y is < 1 or > 9999 || mo is < 1 or > 12 || da < 1 || da > DateTime.DaysInMonth(y, mo) || hh > 23 || mi > 59)
                    return (null, false, $"Datum „{s}“ nicht erkannt");
                var d = new DateTime(y, mo, da, hh, mi, 0, DateTimeKind.Local);
                return (d.ToUniversalTime(), hasT, null);
            }

            var low = s.ToLowerInvariant();
            var today = DateTime.Today;
            if (low is "today" or "heute") return (today.ToUniversalTime(), false, null);
            if (low is "tomorrow" or "morgen") return (today.AddDays(1).ToUniversalTime(), false, null);
            if (low is "yesterday" or "gestern") return (today.AddDays(-1).ToUniversalTime(), false, null);
            var inDays = Regex.Match(low, @"^in (\d{1,5}) (days?|tagen?)$");
            if (inDays.Success && int.TryParse(inDays.Groups[1].Value, out var n) && n <= 3650)
                return (today.AddDays(n).ToUniversalTime(), false, null);

            // Wiederkehrend ohne festen Startpunkt – nicht übernehmbar.
            if (low.StartsWith("every") || low.StartsWith("jede") || low.StartsWith("jeden") || low.Contains('!')
                || low is "daily" or "täglich" or "weekly" or "wöchentlich" or "monthly" or "monatlich" or "yearly" or "jährlich")
                return (null, false, $"wiederkehrendes Datum „{s}“ übersprungen");

            // Natürlichsprachliches Datum in DATE_LANG, sonst en/de/invariant.
            var cultures = new List<CultureInfo>();
            void Add(string c) { try { cultures.Add(new CultureInfo(c)); } catch { } }
            if (!string.IsNullOrWhiteSpace(lang)) Add(lang);
            Add("en-GB"); Add("en-US"); Add("de-DE");
            cultures.Add(CultureInfo.InvariantCulture);
            foreach (var ci in cultures)
                if (DateTime.TryParse(s, ci, DateTimeStyles.AssumeLocal, out var dt))
                    return (dt.ToUniversalTime(), dt.TimeOfDay != TimeSpan.Zero || s.Contains(':'), null);
        }
        catch { /* fällt unten auf „nicht erkannt" */ }
        return (null, false, $"Datum „{s}“ nicht erkannt");
    }

    private static string DeriveName(string fileName)
    {
        var n = Path.GetFileNameWithoutExtension(fileName ?? "").Trim();
        return string.IsNullOrWhiteSpace(n) ? "Import" : n;
    }

    private static string Trunc(string s) => s.Length > 40 ? s[..40] + "…" : s;

    /// <summary>RFC-4180-CSV-Parser: Anführungszeichen, eingebettete Kommas/Zeilenumbrüche, ""-Escapes.
    /// Unterstützt LF, CRLF und alleinstehendes CR als Zeilenende.</summary>
    private static List<string[]> ParseCsv(string text)
    {
        if (text.Length > 0 && text[0] == '﻿') text = text[1..];   // BOM entfernen
        var rows = new List<string[]>();
        var field = new StringBuilder();
        var row = new List<string>();
        var inQuotes = false;

        void EndRow() { row.Add(field.ToString()); field.Clear(); rows.Add(row.ToArray()); row = new List<string>(); }

        for (var i = 0; i < text.Length; i++)
        {
            var c = text[i];
            if (inQuotes)
            {
                if (c == '"')
                {
                    if (i + 1 < text.Length && text[i + 1] == '"') { field.Append('"'); i++; }
                    else inQuotes = false;
                }
                else field.Append(c);
            }
            else if (c == '"') inQuotes = true;
            else if (c == ',') { row.Add(field.ToString()); field.Clear(); }
            else if (c == '\n') EndRow();
            else if (c == '\r') { if (i + 1 < text.Length && text[i + 1] == '\n') i++; EndRow(); }  // CRLF oder alleinstehendes CR
            else field.Append(c);
        }
        if (field.Length > 0 || row.Count > 0) EndRow();

        // Vollständig leere Zeilen (Todoist-Trennzeilen ",,,,,") verwerfen.
        return rows.Where(r => r.Any(f => f.Trim().Length > 0)).ToList();
    }
}
