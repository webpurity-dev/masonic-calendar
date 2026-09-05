namespace MasonicCalendar.Core.Services.Renderers.SectionRenderers;

using MasonicCalendar.Core.Domain;
using MasonicCalendar.Core.Loaders;
using MasonicCalendar.Core.Services;
using CsvHelper;
using System.Globalization;
using System.Text;

/// <summary>
/// Renders meetings as a compact grid: one row per unit, one column per month.
/// Uses SuperShortName for unit display. Installation months are bolded with *.
/// </summary>
public class MeetingsTableSectionRenderer(string templateRoot, SchemaDataLoader? dataLoader, bool debugMode, TablePagination? tablePagination = null)
    : SectionRenderer(templateRoot, dataLoader, debugMode)
{
    private readonly RecurrenceService _recurrenceService = new();
    private readonly TablePagination? _tablePagination = tablePagination;

    public override async Task RenderAsync(
        SectionConfig section,
        int sectionIndex,
        List<SectionConfig> allSections,
        string masterTemplateKey,
        List<SchemaUnit> units,
        StringBuilder output)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(section.Template))
                return;

            var template = LoadTemplate(section.Template);
            if (template == null)
                return;

            var meetings = await LoadMeetingsAsync(section);
            if (meetings.Count == 0)
                return;

            // Determine 12-month calendar window from data source config
            var currentYear = DateTime.Now.Year;
            int startMonth = 1, startYear = currentYear;
            int endMonth = 12, endYear = currentYear;

            var layoutLoader = new DocumentLayoutLoader(Path.Combine(TemplateRoot, ".."));
            var mappingResult = section.DataMapping is not null
                ? layoutLoader.LoadDataSourceMapping(section.DataMapping)
                : null;
            if (mappingResult?.Success == true
                && mappingResult.Data?.Meetings?.CalendarStartMonth is int cfgStart
                && cfgStart >= 1 && cfgStart <= 12)
            {
                startMonth = cfgStart;
                startYear = currentYear;
                var endDate = new DateTime(currentYear, startMonth, 1).AddMonths(12).AddMonths(-1);
                endYear = endDate.Year;
                endMonth = endDate.Month;
            }

            // Build ordered month column descriptors
            var monthColumns = new List<(int Year, int Month, string Label)>();
            var cur = new DateOnly(startYear, startMonth, 1);
            for (int i = 0; i < 12; i++)
            {
                monthColumns.Add((cur.Year, cur.Month, cur.ToString("MMM")));
                cur = cur.AddMonths(1);
            }

            // e.g. "2026-27"
            var yearLabel = $"{startYear}-{endYear % 100:D2}";

            // Expand recurrence rules into concrete dates
            var expanded = ExpandMeetings(meetings, startYear, startMonth, endYear, endMonth);

            // If single unit is pre-filtered, filter to just that unit; otherwise apply section unit types filter
            if (units.Count == 1)
            {
                var singleUnit = units[0];
                expanded = expanded
                    .Where(e => e.UnitType.Equals(singleUnit.UnitType, StringComparison.OrdinalIgnoreCase) &&
                                e.UnitId.Equals(singleUnit.Number.ToString(), StringComparison.OrdinalIgnoreCase))
                    .ToList();
                if (DebugMode)
                    Console.WriteLine($"  - Filtered meetings to unit: {singleUnit.UnitType} {singleUnit.Number}");
            }
            else if (section.UnitTypes?.Count > 0)
            {
                expanded = expanded
                    .Where(e => section.UnitTypes.Any(t => t.Equals(e.UnitType, StringComparison.OrdinalIgnoreCase)))
                    .ToList();
            }

            var orderedUnitTypes = section.UnitTypes?
                .Where(t => !string.IsNullOrWhiteSpace(t))
                .Select(t => t.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList() ?? [];
            var shouldGroupByUnitType = orderedUnitTypes.Count > 1;

            var unitTypeTitles = BuildUnitTypeTitleLookup(allSections, orderedUnitTypes);

            // Resolve unit display names (SuperShortName preferred) from data-driven sections
            var unitNameLookup = await BuildUnitNameLookupAsync(allSections, masterTemplateKey);
            var unitPostfixLookup = await BuildUnitPostfixLookupAsync(allSections, masterTemplateKey);

            // One row per (UnitType, UnitId), sorted numerically by unit number.
            // When multiple unit types are provided, group rows by the explicit unit_types order.
            var unitGroups = expanded
                .GroupBy(e => (UnitType: e.UnitType ?? string.Empty, UnitId: e.UnitId ?? string.Empty))
                .ToList();

            int GetUnitSortValue(string unitId)
            {
                return int.TryParse(unitId, out int n) ? n : int.MaxValue;
            }

            var orderedGroups = new List<IGrouping<(string UnitType, string UnitId), EventInstance>>();
            if (shouldGroupByUnitType)
            {
                foreach (var unitType in orderedUnitTypes)
                {
                    orderedGroups.AddRange(unitGroups
                        .Where(g => g.Key.UnitType.Equals(unitType, StringComparison.OrdinalIgnoreCase))
                        .OrderBy(g => GetUnitSortValue(g.Key.UnitId))
                        .ThenBy(g => g.Key.UnitId, StringComparer.OrdinalIgnoreCase));
                }
            }
            else
            {
                orderedGroups.AddRange(unitGroups
                    .OrderBy(g => GetUnitSortValue(g.Key.UnitId))
                    .ThenBy(g => g.Key.UnitType, StringComparer.OrdinalIgnoreCase));
            }

            var rows = new List<object?>();
            string? currentUnitType = null;
            foreach (var group in orderedGroups)
            {
                var unitId = group.Key.UnitId;
                var unitType = group.Key.UnitType;

                if (shouldGroupByUnitType && !unitType.Equals(currentUnitType, StringComparison.OrdinalIgnoreCase))
                {
                    rows.Add(new Dictionary<string, object?>
                    {
                        { "is_group_header", true },
                        { "group_title", unitTypeTitles.TryGetValue(unitType, out var groupTitle) ? groupTitle : unitType },
                        { "group_row_span", monthColumns.Count + 2 }
                    });
                    currentUnitType = unitType;
                }

                var lookupKey = $"{unitType}:{unitId}";
                unitNameLookup.TryGetValue(lookupKey, out var unitName);
                unitName ??= unitId;
                unitPostfixLookup.TryGetValue(lookupKey, out var unitPostfixDisplay);
                unitPostfixDisplay ??= string.Empty;

                var byMonthYear = group
                    .GroupBy(e => (e.Year, e.Month))
                    .ToDictionary(
                        g => g.Key,
                        g =>
                        {
                            // Sort: installation first, then by date
                            return g.OrderByDescending(e => e.IsInstallation)
                                    .ThenBy(e => e.Date)
                                    .ToList();
                        });

                // How many rows does this unit need? (max events in any single month)
                int rowCount = byMonthYear.Values.Count > 0
                    ? byMonthYear.Values.Max(v => v.Count)
                    : 1;

                for (int rowIndex = 0; rowIndex < rowCount; rowIndex++)
                {
                    var cells = monthColumns.Select(col =>
                    {
                            if (!byMonthYear.TryGetValue((col.Year, col.Month), out var dayEvents)
                                || rowIndex >= dayEvents.Count())
                        {
                            return (object?)new Dictionary<string, object?>
                            {
                                { "day", null },
                                { "is_installation", false },
                                { "has_meeting", false }
                            };
                        }

                        var evt = dayEvents[rowIndex];
                        return (object?)new Dictionary<string, object?>
                        {
                            { "day", evt.Date.Day },
                            { "is_installation", evt.IsInstallation },
                            { "has_meeting", true }
                        };
                    }).ToList();

                    rows.Add(new Dictionary<string, object?>
                    {
                        { "is_group_header", false },
                        { "unit_number", unitId },
                        { "unit_postfix_display", unitPostfixDisplay },
                        { "unit_name",   unitName },
                        { "unit_type",   group.Key.UnitType },
                        { "row_span",    rowIndex == 0 ? rowCount : 0 },
                        { "cells", cells }
                    });
                }
            }

            var monthsModel = monthColumns
                .Select(m => (object?)new Dictionary<string, object?> { { "label", m.Label } })
                .ToList();

            // Determine font size and line-height based on style
            var (fontSize, lineHeight) = section.Style?.ToLowerInvariant() switch
            {
                "medium" => ("var(--font-dense-table)", "1.2"),
                "large" => ("var(--font-body)", "1.3"),
                _ => ("var(--font-dense-table)", "1")  // Default to "small"
            };

            var model = new Dictionary<string, object?>
            {
                { "section_title", section.SectionTitle },
                { "year_label", yearLabel },
                { "months", monthsModel },
                { "table_colspan", monthColumns.Count + 2 },
                { "rows", rows },
                { "row_pages", rows.Chunk(_tablePagination?.MeetingsTableRowsPerPage > 0 ? _tablePagination.MeetingsTableRowsPerPage : 25).Select(page => page.ToList()).ToList() },
                { "font_size", fontSize },
                { "line_height", lineHeight },
                { "heading_spacing", _tablePagination?.MeetingsTableHeadingSpacing ?? "12px" },
                { "override_break_before", section.OverrideBreakBefore }
            };

            var html = template.Render(model);
            WrapWithPageBreakAndAnchor(output, $"section_{section.SectionId}", html, sectionIndex, section.ResetPageCounter, section.OverrideBreakBefore);

            if (DebugMode)
                Console.WriteLine($"  ✓ Generated meetings table with {rows.Count} unit rows");
        }
        catch (Exception ex)
        {
            if (DebugMode)
                Console.WriteLine($"  ❌ Error rendering meetings table: {ex.Message}");
            throw;
        }
    }

    /// <summary>
    /// Builds a unit number -> display name lookup from every data-driven section,
    /// preferring SuperShortName then ShortName then Name.
    /// </summary>
    private async Task<Dictionary<string, string>> BuildUnitNameLookupAsync(
        List<SectionConfig> allSections, string masterTemplateKey)
    {
        var lookup = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (DataLoader == null)
            return lookup;

        foreach (var s in allSections.Where(s =>
            s.Type?.Equals("data-driven", StringComparison.OrdinalIgnoreCase) == true &&
            !string.IsNullOrWhiteSpace(s.DataMapping)))
        {
            var result = await DataLoader.LoadUnitsWithDataAsync(masterTemplateKey, s.SectionId);
            if (!result.Success || result.Data == null)
                continue;
            foreach (var unit in result.Data)
            {
                var key = $"{unit.UnitType}:{unit.Number}";
                if (!lookup.ContainsKey(key))
                    lookup[key] = unit.SuperShortName ?? unit.ShortName ?? unit.Name;
            }
        }
        return lookup;
    }

    /// <summary>
    /// Builds a unit number -> display postfix lookup from every data-driven section,
    /// preserving blank postfix values when the source data leaves them empty.
    /// </summary>
    private async Task<Dictionary<string, string>> BuildUnitPostfixLookupAsync(
        List<SectionConfig> allSections, string masterTemplateKey)
    {
        var lookup = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (DataLoader == null)
            return lookup;

        foreach (var s in allSections.Where(s =>
            s.Type?.Equals("data-driven", StringComparison.OrdinalIgnoreCase) == true &&
            !string.IsNullOrWhiteSpace(s.DataMapping)))
        {
            var result = await DataLoader.LoadUnitsWithDataAsync(masterTemplateKey, s.SectionId);
            if (!result.Success || result.Data == null)
                continue;

            foreach (var unit in result.Data)
            {
                var key = $"{unit.UnitType}:{unit.Number}";
                if (!lookup.ContainsKey(key))
                    lookup[key] = unit.UnitPostfix ?? string.Empty;
            }
        }

        return lookup;
    }

    /// <summary>
    /// Build a lookup from unit type codes (e.g. AMD, RC) to human-readable section titles.
    /// Uses the first matching section title for the unit type, falling back to the code.
    /// </summary>
    private static Dictionary<string, string> BuildUnitTypeTitleLookup(List<SectionConfig> allSections, List<string> orderedUnitTypes)
    {
        var titles = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var unitType in orderedUnitTypes)
        {
            var normalizedUnitType = unitType.Trim();
            var matchingSection = allSections.FirstOrDefault(s =>
                !string.IsNullOrWhiteSpace(s.SectionTitle) &&
                s.SectionId?.StartsWith(normalizedUnitType, StringComparison.OrdinalIgnoreCase) == true &&
                !s.SectionId.EndsWith("_meetings_table", StringComparison.OrdinalIgnoreCase));

            titles[normalizedUnitType] = matchingSection?.SectionTitle ?? normalizedUnitType;
        }

        return titles;
    }

    private List<EventInstance> ExpandMeetings(
        List<CalendarEvent> meetings, int startYear, int startMonth, int endYear, int endMonth)
    {
        var result = new List<EventInstance>();
        foreach (var m in meetings)
            result.AddRange(_recurrenceService.ExpandEvent(m, startYear, startMonth, endYear, endMonth));
        return result;
    }

    private async Task<List<CalendarEvent>> LoadMeetingsAsync(SectionConfig section)
    {
        var meetings = new List<CalendarEvent>();
        if (string.IsNullOrWhiteSpace(section.DataMapping))
            return meetings;

        var layoutLoader = new DocumentLayoutLoader(Path.Combine(TemplateRoot, ".."));
        var mappingResult = layoutLoader.LoadDataSourceMapping(section.DataMapping);
        if (!mappingResult.Success)
            return meetings;

        var mapping = mappingResult.Data;
        if (mapping?.Meetings == null || string.IsNullOrWhiteSpace(mapping.Meetings.Source))
            return meetings;

        var csvFile = Path.Combine(TemplateRoot, "..", "data", mapping.Meetings.Source);
        if (!File.Exists(csvFile))
            throw new Exception($"Meetings CSV file not found: {csvFile}");

        using var reader = new StreamReader(csvFile, Encoding.UTF8);
        using var csv = new CsvReader(reader, CultureInfo.InvariantCulture);
        await csv.ReadAsync();
        csv.ReadHeader();

        var fieldMap = BuildFieldMap(mapping.Meetings.Fields);

        while (await csv.ReadAsync())
        {
            var unitId = GetFieldValue(csv, fieldMap, "Number") ?? GetFieldValue(csv, fieldMap, "UnitID") ?? "";
            var title = GetFieldValue(csv, fieldMap, "Title") ?? "";
            if (string.IsNullOrWhiteSpace(unitId) || string.IsNullOrWhiteSpace(title))
                continue;

            meetings.Add(new CalendarEvent
            {
                Id = Guid.NewGuid().ToString(),
                UnitId = unitId,
                Title = title,
                UnitType = GetFieldValue(csv, fieldMap, "UnitType") ?? "",
                RecurrenceType = GetFieldValue(csv, fieldMap, "RecurrenceType") ?? "Once",
                RecurrenceStrategy = GetFieldValue(csv, fieldMap, "RecurrenceStrategy"),
                DayOfWeek = GetFieldValue(csv, fieldMap, "DayOfWeek"),
                WeekNumber = GetFieldValue(csv, fieldMap, "WeekNumber"),
                DayNumber = GetFieldValue(csv, fieldMap, "DayNumber"),
                InstallationMonth = GetFieldValue(csv, fieldMap, "InstallationMonth"),
                StartMonth = GetFieldValue(csv, fieldMap, "StartMonth"),
                EndMonth = GetFieldValue(csv, fieldMap, "EndMonth"),
                Months = GetFieldValue(csv, fieldMap, "Months"),
                Override = GetFieldValue(csv, fieldMap, "Override")
            });
        }

        return meetings;
    }

    private static Dictionary<string, string> BuildFieldMap(List<FieldMapping>? fieldMappings)
    {
        var map = new Dictionary<string, string>();
        if (fieldMappings == null) return map;
        foreach (var f in fieldMappings)
            if (!string.IsNullOrWhiteSpace(f.Name) && !string.IsNullOrWhiteSpace(f.CsvColumn))
                map[f.Name] = f.CsvColumn;
        return map;
    }

    private static string? GetFieldValue(CsvReader csv, Dictionary<string, string> fieldMap, string propertyName)
    {
        if (fieldMap.TryGetValue(propertyName, out var csvColumn))
            return csv.GetField(csvColumn);
        return csv.GetField(propertyName);
    }
}
