namespace MasonicCalendar.Core.Services;

using System.Globalization;
using System.Text;
using CsvHelper;
using MasonicCalendar.Core.Domain;
using MasonicCalendar.Core.Loaders;

/// <summary>
/// Exports expanded meeting dates and unit membership data to CSV files.
/// Produces two files per run:
///   {template}-meetings.csv  — one row per expanded meeting instance
///   {template}-members.csv   — one row per person across all unit member categories
/// </summary>
public class CsvExportService(DocumentLayoutLoader layoutLoader, SchemaDataLoader dataLoader, string documentRoot)
{
    private readonly RecurrenceService _recurrenceService = new();

    public async Task ExportAsync(string templateName, string outputDir)
    {
        // --- Load all units from every data-driven section ---
        var layoutResult = layoutLoader.LoadMasterLayout(templateName);
        if (!layoutResult.Success)
            throw new Exception($"Failed to load layout: {layoutResult.Error}");

        var sections = layoutResult.Data!.Sections ?? [];
        var dataDrivenSections = sections
            .Where(s => s.Type?.Equals("data-driven", StringComparison.OrdinalIgnoreCase) == true)
            .ToList();

        var allUnits = new List<SchemaUnit>();
        foreach (var section in dataDrivenSections)
        {
            Console.WriteLine($"  Loading units for section: {section.SectionId}");
            var result = await dataLoader.LoadUnitsWithDataAsync(templateName, section.SectionId);
            if (result.Success && result.Data != null)
                allUnits.AddRange(result.Data);
        }

        Console.WriteLine($"  ✓ Loaded {allUnits.Count} units total");

        // Build unit name lookup keyed by "unitType:unitNumber" (both as strings)
        var unitNameLookup = allUnits.ToDictionary(
            u => $"{u.UnitType ?? ""}:{u.Number}",
            u => u.Name,
            StringComparer.OrdinalIgnoreCase);

        // --- Load and expand meetings ---
        // Find the first meetings section to get the data mapping path
        var meetingsSection = sections.FirstOrDefault(s =>
            s.Type?.Equals("meetings-calendar", StringComparison.OrdinalIgnoreCase) == true ||
            s.Type?.Equals("meetings-table", StringComparison.OrdinalIgnoreCase) == true);

        var dataMappingPath = meetingsSection?.DataMapping ?? "data_sources/meetings_data_source.yaml";
        var expandedEvents = await LoadAndExpandMeetingsAsync(dataMappingPath);
        Console.WriteLine($"  ✓ Expanded {expandedEvents.Count} meeting instances");

        // --- Write meetings CSV ---
        var meetingsPath = Path.Combine(outputDir, $"{templateName}-meetings.csv");
        WriteMeetingsCsv(meetingsPath, expandedEvents, unitNameLookup);
        Console.WriteLine($"  ✓ Meetings: {meetingsPath}");

        // --- Write members CSV ---
        var membersPath = Path.Combine(outputDir, $"{templateName}-members.csv");
        WriteMembersCsv(membersPath, allUnits);
        Console.WriteLine($"  ✓ Members:  {membersPath}");
    }

    // -------------------------------------------------------------------------

    private async Task<List<EventInstance>> LoadAndExpandMeetingsAsync(string dataMappingPath)
    {
        var mappingResult = layoutLoader.LoadDataSourceMapping(dataMappingPath);
        if (!mappingResult.Success || mappingResult.Data?.Meetings is not { } meetingsDef)
            return [];

        if (string.IsNullOrWhiteSpace(meetingsDef.Source))
            return [];

        var csvFile = Path.Combine(documentRoot, "data", meetingsDef.Source);
        if (!File.Exists(csvFile))
            throw new Exception($"Meetings CSV not found: {csvFile}");

        var fieldMap = BuildFieldMap(meetingsDef.Fields);
        var meetings = new List<CalendarEvent>();

        using var reader = new StreamReader(csvFile, Encoding.UTF8);
        using var csv = new CsvReader(reader, CultureInfo.InvariantCulture);

        await csv.ReadAsync();
        csv.ReadHeader();

        while (await csv.ReadAsync())
        {
            var unitId = GetField(csv, fieldMap, "Number") ?? "";
            if (string.IsNullOrWhiteSpace(unitId))
                continue;

            meetings.Add(new CalendarEvent
            {
                Id = Guid.NewGuid().ToString(),
                UnitId = unitId,
                Title = GetField(csv, fieldMap, "Title") ?? "",
                UnitType = GetField(csv, fieldMap, "UnitType") ?? "",
                RecurrenceType = GetField(csv, fieldMap, "RecurrenceType") ?? "Once",
                RecurrenceStrategy = GetField(csv, fieldMap, "RecurrenceStrategy"),
                DayOfWeek = GetField(csv, fieldMap, "DayOfWeek"),
                WeekNumber = GetField(csv, fieldMap, "WeekNumber"),
                DayNumber = GetField(csv, fieldMap, "DayNumber"),
                InstallationMonth = GetField(csv, fieldMap, "InstallationMonth"),
                StartMonth = GetField(csv, fieldMap, "StartMonth"),
                EndMonth = GetField(csv, fieldMap, "EndMonth"),
                Months = GetField(csv, fieldMap, "Months"),
                Override = GetField(csv, fieldMap, "Override")
            });
        }

        // Determine calendar range from data source config
        var currentYear = DateTime.Now.Year;
        int startMonth = 1, startYear = currentYear;
        int endMonth = 12, endYear = currentYear;

        if (meetingsDef.CalendarStartMonth is int cfgStart && cfgStart >= 1 && cfgStart <= 12)
        {
            startMonth = cfgStart;
            var endDate = new DateTime(currentYear, startMonth, 1).AddMonths(12).AddMonths(-1);
            endYear = endDate.Year;
            endMonth = endDate.Month;
        }

        var expanded = new List<EventInstance>();
        foreach (var m in meetings)
            expanded.AddRange(_recurrenceService.ExpandEvent(m, startYear, startMonth, endYear, endMonth));

        return expanded
            .OrderBy(e => e.UnitType)
            .ThenBy(e => int.TryParse(e.UnitId, out int n) ? n : 0)
            .ThenBy(e => e.Date)
            .ToList();
    }

    private static void WriteMeetingsCsv(
        string path,
        List<EventInstance> events,
        Dictionary<string, string> unitNameLookup)
    {
        using var writer = new StreamWriter(path, false, new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));

        // Header
        writer.WriteLine("Unit Type,Unit Number,Unit Name,Meeting Date,Is Installation,Meeting Title");

        foreach (var e in events)
        {
            var name = unitNameLookup.TryGetValue($"{e.UnitType}:{e.UnitId}", out var n) ? n : "";
            writer.WriteLine(
                $"{Q(e.UnitType)},{Q(e.UnitId)},{Q(name)}," +
                $"{Q(e.Date.ToString("yyyy-MM-dd"))}," +
                $"{Q(e.IsInstallation ? "TRUE" : "FALSE")}," +
                $"{Q(e.Title)}");
        }
    }

    private static void WriteMembersCsv(string path, List<SchemaUnit> units)
    {
        using var writer = new StreamWriter(path, false, new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));

        // Header
        writer.WriteLine("Unit Type,Unit Number,Unit Name,Category,Name,Office / Role,Year,Provincial Rank");

        foreach (var unit in units.OrderBy(u => u.UnitType).ThenBy(u => u.Number))
        {
            var t = Q(unit.UnitType ?? "");
            var num = Q(unit.Number.ToString());
            var name = Q(unit.Name);

            foreach (var o in unit.Officers)
                writer.WriteLine($"{t},{num},{name},Officer,{Q(o.Name)},{Q(o.Position ?? o.Office ?? "")},,");

            foreach (var pm in unit.PastMasters)
                writer.WriteLine($"{t},{num},{name},PastMaster,{Q(pm.Name)},,{Q(pm.YearInstalled ?? "")},{Q(pm.Rank ?? "")},{Q(pm.RankYear ?? "")}");

            foreach (var jp in unit.JoinPastMasters)
                writer.WriteLine($"{t},{num},{name},JoinPastMaster,{Q(jp.Name)},{Q(jp.PastUnits ?? "")},,{Q(jp.Rank ?? "")},{Q(jp.RankYear ?? "")}");

            foreach (var m in unit.Members)
                writer.WriteLine($"{t},{num},{name},Member,{Q(m.Name)},,{Q(m.YearInitiated ?? "")},");

            foreach (var h in unit.HonoraryMembers)
                writer.WriteLine($"{t},{num},{name},HonoraryMember,{Q(h.Name)},,,{Q(h.Rank ?? "")}");
        }
    }

    // Wrap a value in CSV double-quotes, escaping any embedded quotes.
    private static string Q(string? value) => $"\"{(value ?? "").Replace("\"", "\"\"")}\"";

    private static Dictionary<string, string> BuildFieldMap(List<FieldMapping>? fields)
    {
        var map = new Dictionary<string, string>();
        if (fields == null) return map;
        foreach (var f in fields)
            if (!string.IsNullOrWhiteSpace(f.Name) && !string.IsNullOrWhiteSpace(f.CsvColumn))
                map[f.Name] = f.CsvColumn;
        return map;
    }

    private static string? GetField(CsvReader csv, Dictionary<string, string> fieldMap, string property)
    {
        if (fieldMap.TryGetValue(property, out var col))
            return csv.GetField(col);
        return csv.GetField(property);
    }
}
