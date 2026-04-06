namespace MasonicCalendar.Core.Services.Renderers.SectionRenderers;

using MasonicCalendar.Core.Domain;
using MasonicCalendar.Core.Loaders;
using MasonicCalendar.Core.Services;
using Scriban;
using System.Globalization;
using System.Text;
using CsvHelper;

/// <summary>
/// Renders meetings calendar as a calendar grid view (like Google Calendar).
/// Creates one complete HTML document with all months, grouped by date.
/// </summary>
public class MeetingsCalendarSectionRenderer(string templateRoot, SchemaDataLoader? dataLoader, bool debugMode)
    : SectionRenderer(templateRoot, dataLoader, debugMode)
{
    private readonly RecurrenceService _recurrenceService = new();

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
            {
                if (DebugMode)
                    Console.WriteLine($"  ⚠️  No template specified for section '{section.SectionId}'");
                return;
            }

            var template = LoadTemplate(section.Template);
            if (template == null)
                return;

            // Load meetings from CSV
            var meetings = await LoadMeetingsAsync(section);
            if (meetings.Count == 0)
            {
                if (DebugMode)
                    Console.WriteLine($"  ⚠️  No meetings found for section '{section.SectionId}'");
                return;
            }

            // Determine calendar range: honour calendar_start_month from the data source YAML
            var currentYear = DateTime.Now.Year;
            int startMonth = 1, startYear = currentYear;
            int endMonth = 12, endYear = currentYear;

            var calLayoutLoader = new DocumentLayoutLoader(Path.Combine(TemplateRoot, ".."));
            var calMappingResult = section.DataMapping is not null
                ? calLayoutLoader.LoadDataSourceMapping(section.DataMapping)
                : null;
            if (calMappingResult?.Success == true && calMappingResult.Data?.Meetings?.CalendarStartMonth is int cfgStart
                && cfgStart >= 1 && cfgStart <= 12)
            {
                startMonth = cfgStart;
                startYear = currentYear;
                // Run for exactly 12 months; if start > Jan the window crosses into the next year
                var endDate = new DateTime(currentYear, startMonth, 1).AddMonths(12).AddMonths(-1);
                endYear = endDate.Year;
                endMonth = endDate.Month;
            }

            var expandedEvents = ExpandMeetings(meetings, startYear, startMonth, endYear, endMonth);

            // Add section anchor for TOC links, wrapped in section-divider for consistent page break behaviour
            output.AppendLine("<div class='section-divider'>");
            output.AppendLine($"<div id=\"section_{section.SectionId}\"></div>");

            // Build unit lookup for determining unit type
            var unitMap = units.ToDictionary(u => u.Number.ToString());

            // Group events by month
            var eventsByMonth = expandedEvents
                .GroupBy(e => new { e.Year, e.Month })
                .OrderBy(g => g.Key.Year)
                .ThenBy(g => g.Key.Month)
                .ToList();

            // Render each month using the template
            var isFirstMonth = true;
            foreach (var monthGroup in eventsByMonth)
            {
                var monthDate = new DateOnly(monthGroup.Key.Year, monthGroup.Key.Month, 1);
                var monthName = monthDate.ToString("MMMM yyyy");

                // Build calendar weeks for this month, stripping padding weeks that fall entirely outside the month
                var weeks = BuildCalendarWeeks(monthDate, monthGroup.ToList(), unitMap)
                    .Where(w => w.Any(d => (int)(d["day_number"] ?? 0) > 0))
                    .ToList();

                // Create model for template
                var model = new Dictionary<string, object?>
                {
                    { "month_name", monthName },
                    { "weeks", weeks },
                    { "section_title", section.SectionTitle },
                    { "is_first_month", isFirstMonth }
                };
                isFirstMonth = false;

                // Render month using template
                var monthHtml = template.Render(model);
                output.Append(monthHtml);
            }

            output.AppendLine("</div>");

            if (DebugMode)
                Console.WriteLine($"  ✓ Generated calendar with {expandedEvents.Count} total events");
        }
        catch (Exception ex)
        {
            if (DebugMode)
                Console.WriteLine($"  ❌ Error rendering meetings calendar: {ex.Message}");
            throw;
        }
    }

    /// <summary>
    /// Build calendar grid weeks for a month, with meetings assigned to dates.
    /// Returns a structure suitable for Scriban template rendering.
    /// </summary>
    private List<List<Dictionary<string, object?>>> BuildCalendarWeeks(DateOnly monthDate, List<EventInstance> monthEvents, Dictionary<string, SchemaUnit> unitMap)
    {
        var weeks = new List<List<Dictionary<string, object?>>>();
        var eventsByDate = monthEvents.GroupBy(e => e.Date).ToDictionary(g => g.Key);

        var firstDay = monthDate;
        var lastDayOfMonth = DateTime.DaysInMonth(monthDate.Year, monthDate.Month);

        // Start from Monday of the week containing the 1st
        var dayOfWeek = (int)firstDay.DayOfWeek;
        var daysToSubtract = dayOfWeek == 0 ? 6 : dayOfWeek - 1;  // Monday = 1, Sunday = 0 maps to 6 days back
        var currentWeekStart = firstDay.AddDays(-daysToSubtract);

        var currentDate = currentWeekStart;
        var endDate = firstDay.AddDays(lastDayOfMonth - 1).AddDays(7);  // Go through end of last week

        while (currentDate < endDate)
        {
            var week = new List<Dictionary<string, object?>>();

            for (int i = 0; i < 6; i++)  // Mon-Sat only
            {
                var isCurrentMonth = currentDate.Year == monthDate.Year && currentDate.Month == monthDate.Month;
                var dayNumber = isCurrentMonth ? currentDate.Day : 0;

                // Build meetings list for this day
                var dayMeetings = new List<Dictionary<string, object?>>();
                if (isCurrentMonth && eventsByDate.TryGetValue(currentDate, out var dayMeetingsList))
                {
                    foreach (var meeting in dayMeetingsList.OrderBy(m => m.Title))
                    {
                        dayMeetings.Add(new Dictionary<string, object?>
                        {
                            { "unit_id", meeting.UnitId },
                            { "title", meeting.Title },
                            { "is_installation", meeting.IsInstallation },
                            { "is_royal_arch", meeting.UnitType.Equals("RoyalArch", StringComparison.OrdinalIgnoreCase) },
                            { "display_unit_id", meeting.UnitType.Equals("RoyalArch", StringComparison.OrdinalIgnoreCase) ? $"RA{meeting.UnitId}" : meeting.UnitId }
                        });
                    }
                }

                // Create day dictionary for template
                var day = new Dictionary<string, object?>
                {
                    { "day_number", dayNumber },
                    { "meetings", dayMeetings }
                };

                week.Add(day);
                currentDate = currentDate.AddDays(1);
            }

            // After Sat (6 days), currentDate is now Sunday — skip it to land on Monday for the next week
            currentDate = currentDate.AddDays(1);

            weeks.Add(week);
        }

        return weeks;
    }

    /// <summary>
    /// Load meetings from CSV file based on data mapping.
    /// </summary>
    private async Task<List<CalendarEvent>> LoadMeetingsAsync(SectionConfig section)
    {
        var meetings = new List<CalendarEvent>();

        if (string.IsNullOrWhiteSpace(section.DataMapping))
            return meetings;

        // Load the data source mapping
        var layoutLoader = new DocumentLayoutLoader(Path.Combine(TemplateRoot, ".."));
        var mappingResult = layoutLoader.LoadDataSourceMapping(section.DataMapping);
        if (!mappingResult.Success)
            return meetings;

        var mapping = mappingResult.Data;
        if (mapping?.Meetings == null || string.IsNullOrWhiteSpace(mapping.Meetings.Source))
            return meetings;

        // Construct the CSV file path
        var dataRoot = Path.Combine(TemplateRoot, "..", "data");
        var csvFile = Path.Combine(dataRoot, mapping.Meetings.Source);

        if (!File.Exists(csvFile))
            throw new Exception($"Meetings CSV file not found: {csvFile}");

        // Parse CSV and build CalendarEvent objects
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

            var evt = new CalendarEvent
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
            };

            meetings.Add(evt);
        }

        return meetings;
    }

    /// <summary>
    /// Expand calendar events into concrete instances for the current year.
    /// </summary>
    private List<EventInstance> ExpandMeetings(List<CalendarEvent> meetings, int startYear, int startMonth, int endYear, int endMonth)
    {
        var expanded = new List<EventInstance>();

        foreach (var meeting in meetings)
        {
            var instances = _recurrenceService.ExpandEvent(meeting, startYear, startMonth, endYear, endMonth);
            expanded.AddRange(instances);
        }

        return expanded;
    }

    private Dictionary<string, string> BuildFieldMap(List<FieldMapping>? fieldMappings)
    {
        var map = new Dictionary<string, string>();
        if (fieldMappings == null)
            return map;

        foreach (var field in fieldMappings)
        {
            if (!string.IsNullOrWhiteSpace(field.Name) && !string.IsNullOrWhiteSpace(field.CsvColumn))
                map[field.Name] = field.CsvColumn;
        }
        return map;
    }

    private string? GetFieldValue(CsvReader csv, Dictionary<string, string> fieldMap, string propertyName)
    {
        if (fieldMap.TryGetValue(propertyName, out var csvColumn))
            return csv.GetField(csvColumn);
        return csv.GetField(propertyName);
    }
}
