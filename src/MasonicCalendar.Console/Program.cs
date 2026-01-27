using MasonicCalendar.Core.Services;
using MasonicCalendar.Export.Pdf;
using MasonicCalendar.Export.Html;
using QuestPDF.Infrastructure;

// Configure QuestPDF license for community use (non-profit)
QuestPDF.Settings.License = LicenseType.Community;

// Get the project root directory (up three levels from the bin directory)
var projectRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));
var dataPath = Path.Combine(projectRoot, "data");
var eventsPath = Path.Combine(dataPath, "sample-events.csv");
var unitsPath = Path.Combine(dataPath, "sample-units.csv");
var locationsPath = Path.Combine(dataPath, "sample-unit-locations.csv");
var officersPath = Path.Combine(dataPath, "sample-officers.csv");
var unitOfficersPath = Path.Combine(dataPath, "sample-unit-officers.csv");
var unitPastMastersPath = Path.Combine(dataPath, "sample-unit-pmo.csv");

// Output directory (in the project root)
var outputDir = Path.Combine(projectRoot, "output");
if (!Directory.Exists(outputDir))
    Directory.CreateDirectory(outputDir);

// Meetings calendar CLI switch
if (args.Contains("--meetings-calendar"))
{
    // Check for output format (default to PDF)
    var meetingsOutputFormat = "pdf";
    var outputIndex = Array.IndexOf(args, "--output");
    if (outputIndex != -1 && outputIndex + 1 < args.Length)
    {
        var format = args[outputIndex + 1].ToLower();
        if (format == "pdf" || format == "html")
        {
            meetingsOutputFormat = format;
        }
    }
    
    // Check for page size (default to A6)
    var meetingsPageSize = "A6";
    var pageSizeIndex = Array.IndexOf(args, "--pagesize");
    if (pageSizeIndex != -1 && pageSizeIndex + 1 < args.Length)
    {
        var size = args[pageSizeIndex + 1].ToUpper();
        if (size == "A4" || size == "A5" || size == "A6")
        {
            meetingsPageSize = size;
        }
    }
    
    // Check for include Sundays (default to false)
    var includeSundays = args.Contains("--incSunday");
    
    // Check for landscape orientation (default to false for portrait)
    var isLandscape = args.Contains("--landscape");
    
    // Check for from-date parameter (MM-YYYY format, defaults to current month)
    var today = DateOnly.FromDateTime(DateTime.Now);
    DateOnly calendarStartDate = new DateOnly(today.Year, today.Month, 1);
    var fromDateIndex = Array.IndexOf(args, "--from-date");
    if (fromDateIndex != -1 && fromDateIndex + 1 < args.Length)
    {
        var fromDateStr = args[fromDateIndex + 1];
        // Parse MM-YYYY format
        if (fromDateStr.Contains("-") && fromDateStr.Split('-').Length == 2)
        {
            var parts = fromDateStr.Split('-');
            if (int.TryParse(parts[0], out int month) && int.TryParse(parts[1], out int year))
            {
                if (month >= 1 && month <= 12 && year > 1900 && year < 2100)
                {
                    calendarStartDate = new DateOnly(year, month, 1);
                }
            }
        }
    }
    
    var meetingsPath = Path.Combine(dataPath, "sample-unit-meetings.csv");
    var meetingsIngestor = new CsvIngestorService();
    var meetingsResult = meetingsIngestor.ReadUnitMeetingsFromCsv(meetingsPath);
    if (!meetingsResult.Success)
    {
        Console.WriteLine($"❌ Error reading meetings: {meetingsResult.Error}");
        return 1;
    }
    
    // Read units to lookup unit numbers and names
    var meetingsUnitsResult = meetingsIngestor.ReadUnitsFromCsv(unitsPath);
    if (!meetingsUnitsResult.Success)
    {
        Console.WriteLine($"❌ Error reading units: {meetingsUnitsResult.Error}");
        return 1;
    }
    
    Console.WriteLine("🗓️  Masonic Calendar - Meetings Calendar Generator");
    Console.WriteLine($"==========================================");
    Console.WriteLine($"Reading meetings data...");
    Console.WriteLine($"✅ Loaded {meetingsResult.Data!.Count} meeting series");
    Console.WriteLine($"✅ Loaded {meetingsUnitsResult.Data!.Count} units");
    
    // Calculate end date (12 months after start)
    DateOnly calendarEndDate = calendarStartDate.AddMonths(12).AddDays(-1);
    
    // Expand meetings for the range. Need to consider both years involved
    var expandedDates = new List<(MasonicCalendar.Core.Domain.UnitMeeting, DateOnly)>();
    
    // Generate for the start year and the next year (to cover year boundaries)
    foreach (var year in new[] { calendarStartDate.Year, calendarStartDate.AddMonths(12).Year })
    {
        var yearExpanded = MeetingRecurrenceExpander.ExpandMeetings(meetingsResult.Data!, year, calendarStartDate);
        expandedDates.AddRange(yearExpanded);
    }
    
    // Filter to only include dates within the 12-month range and remove duplicates
    var expanded = expandedDates
        .Where(x => x.Item2 >= calendarStartDate && x.Item2 <= calendarEndDate)
        .DistinctBy(x => (x.Item1.Id, x.Item2))
        .ToList();
    
    Console.WriteLine($"✅ Generated {expanded.Count} calendar dates from recurrence rules");
    Console.WriteLine($"📅 Calendar period: {calendarStartDate:MMM yyyy} to {calendarEndDate:MMM yyyy}\n");
    
    // Group meetings by unit and export to CSV
    var unitDict = meetingsUnitsResult.Data!.ToDictionary(u => u.Id);
    var expandedByUnit = expanded
        .GroupBy(x => x.Item1.UnitId)
        .OrderBy(g => SortKey(g))
        .ToList();
    
    int SortKey(IGrouping<Guid, (MasonicCalendar.Core.Domain.UnitMeeting, DateOnly)> g)
    {
        if (unitDict.TryGetValue(g.Key, out var unit))
        {
            try
            {
                return Convert.ToInt32(unit.Number);
            }
            catch { }
        }
        return 999999;
    }
    
    // Export to CSV
    var csvDateLabel = $"{calendarStartDate:MM-yyyy}";
    var csvPath = Path.Combine(outputDir, $"meetings-{csvDateLabel}.csv");
    using (var writer = new System.IO.StreamWriter(csvPath))
    {
        writer.WriteLine("Unit Number,Unit Name,Unit Type,Meeting Date,Meeting Title");
        
        foreach (var group in expandedByUnit)
        {
            if (unitDict.TryGetValue(group.Key, out var unit))
            {
                foreach (var item in group.OrderBy(x => x.Item2))
                {
                    var date = item.Item2.ToString("yyyy-MM-dd");
                    var title = item.Item1.Title.Replace("\"", "\"\""); // Escape quotes
                    writer.WriteLine($"\"{unit.Number}\",\"{unit.Name}\",\"{unit.UnitType}\",\"{date}\",\"{title}\"");
                }
            }
        }
    }
    
    Console.WriteLine($"✅ Exported meetings to CSV: {csvPath}\n");
    
    Console.WriteLine($"\n==========================================");
    var sundayLabel = includeSundays ? "-withsunday" : "";
    if (meetingsOutputFormat == "html")
    {
        Console.WriteLine("Generating meetings calendar HTML...");
        var htmlDateLabel = $"{calendarStartDate:MM-yyyy}";
        var meetingsOutputPath = Path.Combine(outputDir, $"meetings-output-{htmlDateLabel}{sundayLabel}.html");
        var meetingsExporter = new MeetingsCalendarHtmlExporter();
        meetingsExporter.ExportMeetingsToHtml(meetingsResult.Data!, calendarStartDate, meetingsOutputPath, meetingsUnitsResult.Data, includeSundays);
        Console.WriteLine($"✅ Meetings calendar HTML generated: {meetingsOutputPath}");
    }
    else
    {
        Console.WriteLine("Generating meetings calendar PDF...");
        var orientationLabel = isLandscape ? "landscape" : "portrait";
        var pdfDateLabel = $"{calendarStartDate:MM-yyyy}";
        var meetingsOutputPath = Path.Combine(outputDir, $"meetings-output-{pdfDateLabel}-{meetingsPageSize}-{orientationLabel}{sundayLabel}.pdf");
        var meetingsExporter = new MeetingsCalendarExporter();
        meetingsExporter.ExportMeetingsToPdf(meetingsResult.Data!, calendarStartDate, meetingsOutputPath, meetingsUnitsResult.Data, meetingsPageSize, includeSundays, isLandscape);
        Console.WriteLine($"✅ Meetings calendar PDF generated: {meetingsOutputPath}");
    }
    
    Console.WriteLine($"\n✨ Meetings calendar completed successfully!");
    return 0;
}

// Parse command-line arguments
var outputFormat = "pdf"; // default to PDF
int? filterUnitNumber = null; // default to null (all units)
string? filterUnitType = null; // default to null (all unit types)
var pageSize = "A6"; // default to A6 (A4, A5, A6)

if (args.Length > 0)
{
    var outputIndex = Array.IndexOf(args, "--output");
    if (outputIndex != -1 && outputIndex + 1 < args.Length)
    {
        var format = args[outputIndex + 1].ToLower();
        if (format == "pdf" || format == "html")
        {
            outputFormat = format;
        }
        else
        {
            Console.WriteLine($"❌ Invalid output format: {format}. Use 'pdf' or 'html'.");
            return 1;
        }
    }

    var pageSizeIndex = Array.IndexOf(args, "--pagesize");
    if (pageSizeIndex != -1 && pageSizeIndex + 1 < args.Length)
    {
        var size = args[pageSizeIndex + 1].ToUpper();
        if (size == "A4" || size == "A5" || size == "A6")
        {
            pageSize = size;
        }
        else
        {
            Console.WriteLine($"❌ Invalid page size: {size}. Use 'A4', 'A5', or 'A6'.");
            return 1;
        }
    }

    // Check for unit type filter (e.g., --unit-type craft)
    var unitTypeIndex = Array.IndexOf(args, "--unit-type");
    if (unitTypeIndex != -1 && unitTypeIndex + 1 < args.Length)
    {
        var typeArg = args[unitTypeIndex + 1].ToLower();
        if (typeArg == "craft" || typeArg == "royalarch")
        {
            filterUnitType = typeArg == "craft" ? "Craft" : "RoyalArch";
        }
        else
        {
            Console.WriteLine($"❌ Invalid unit type: {typeArg}. Use 'craft' or 'royalarch'.");
            return 1;
        }
    }

    // Check for unit number filter (e.g., --6827)
    var unitFilterArg = args.FirstOrDefault(a => a.StartsWith("--") && int.TryParse(a.Substring(2), out _));
    if (unitFilterArg != null && int.TryParse(unitFilterArg.Substring(2), out var unitNum))
    {
        filterUnitNumber = unitNum;
    }
}

// Generate filename based on unit filter and page size
var filenameIdentifier = filterUnitNumber.HasValue ? filterUnitNumber.Value.ToString() : (filterUnitType != null ? filterUnitType.ToLower() : "all-units");
var unitsOutputPath = Path.Combine(outputDir, $"units-output-{filenameIdentifier}-{pageSize}.{outputFormat}");

Console.WriteLine("🗓️  Masonic Calendar - CSV to Output Converter");
Console.WriteLine($"==========================================");
Console.WriteLine($"Output Format: {outputFormat.ToUpper()}\n");

var ingestor = new CsvIngestorService();

// Read Calendar Events
Console.WriteLine("Reading calendar events...");
var eventsResult = ingestor.ReadEventsFromCsv(eventsPath);
if (!eventsResult.Success)
{
    Console.WriteLine($"❌ Error reading events: {eventsResult.Error}");
    return 1;
}
Console.WriteLine($"✅ Loaded {eventsResult.Data!.Count} events");

// Read Locations
Console.WriteLine("Reading unit locations...");
var locationsResult = ingestor.ReadLocationsFromCsv(locationsPath);
if (!locationsResult.Success)
{
    Console.WriteLine($"❌ Error reading locations: {locationsResult.Error}");
    return 1;
}
var locationDict = locationsResult.Data!.ToDictionary(l => l.Id);
Console.WriteLine($"✅ Loaded {locationDict.Count} locations");

// Read Units
Console.WriteLine("Reading units...");
var unitsResult = ingestor.ReadUnitsFromCsv(unitsPath);
if (!unitsResult.Success)
{
    Console.WriteLine($"❌ Error reading units: {unitsResult.Error}");
    return 1;
}
Console.WriteLine($"✅ Loaded {unitsResult.Data!.Count} units");

// Read Officers
Console.WriteLine("Reading officers...");
var officersResult = ingestor.ReadOfficersFromCsv(officersPath);
if (!officersResult.Success)
{
    Console.WriteLine($"❌ Error reading officers: {officersResult.Error}");
    return 1;
}
var officerDict = officersResult.Data!.ToDictionary(o => o.Id);
Console.WriteLine($"✅ Loaded {officerDict.Count} officer positions");

// Read Unit Officers
Console.WriteLine("Reading unit officers...");
var unitOfficersResult = ingestor.ReadUnitOfficersFromCsv(unitOfficersPath);
if (!unitOfficersResult.Success)
{
    Console.WriteLine($"❌ Error reading unit officers: {unitOfficersResult.Error}");
    return 1;
}
Console.WriteLine($"✅ Loaded {unitOfficersResult.Data!.Count} unit officer assignments");

// Read Unit Past Masters
Console.WriteLine("Reading unit past masters...");
var unitPastMastersResult = ingestor.ReadUnitPastMastersFromCsv(unitPastMastersPath);
if (!unitPastMastersResult.Success)
{
    Console.WriteLine($"❌ Error reading unit past masters: {unitPastMastersResult.Error}");
    return 1;
}
Console.WriteLine($"✅ Loaded {unitPastMastersResult.Data!.Count} unit past master records\n");

// Filter units if a specific unit number was requested
var unitsToExport = unitsResult.Data;
if (filterUnitNumber.HasValue)
{
    unitsToExport = unitsResult.Data.Where(u => u.Number == filterUnitNumber.Value).ToList();
    if (unitsToExport.Count == 0)
    {
        Console.WriteLine($"❌ No unit found with number {filterUnitNumber}");
        return 1;
    }
    Console.WriteLine($"Filtering to unit number: {filterUnitNumber}");
    Console.WriteLine($"Units to export: {unitsToExport.Count}\n");
}
else if (filterUnitType != null)
{
    unitsToExport = unitsResult.Data.Where(u => u.UnitType == filterUnitType).ToList();
    if (unitsToExport.Count == 0)
    {
        Console.WriteLine($"❌ No units found with type {filterUnitType}");
        return 1;
    }
    Console.WriteLine($"Filtering to unit type: {filterUnitType}");
    Console.WriteLine($"Units to export: {unitsToExport.Count}\n");
}
else
{
    Console.WriteLine($"Generating for all units");
    Console.WriteLine($"Units to export: {unitsToExport.Count}\n");
}

// Generate output
try
{
    if (outputFormat == "pdf")
    {
        Console.WriteLine("Generating unit pages PDF...");
        var unitExporter = new UnitPdfExporter(pageSize: pageSize);
        unitExporter.ExportUnitsToPdf(unitsToExport, locationDict, unitOfficersResult.Data, officerDict, unitPastMastersResult.Data, unitsOutputPath);
        Console.WriteLine($"✅ Units PDF generated: {unitsOutputPath} ({pageSize})");
    }
    else if (outputFormat == "html")
    {
        Console.WriteLine("Generating unit pages HTML...");
        var unitExporter = new UnitPdfExporter();
        unitExporter.ExportUnitsToHtml(unitsToExport, locationDict, unitOfficersResult.Data, officerDict, unitPastMastersResult.Data, unitsOutputPath);
        Console.WriteLine($"✅ Units HTML generated: {unitsOutputPath}");
    }
    
    Console.WriteLine($"   Units included: {unitsToExport.Count}");
}
catch (Exception ex)
{
    Console.WriteLine($"❌ Error generating output: {ex.Message}");
    return 1;
}

Console.WriteLine("\n✨ Output generation completed successfully!");
return 0;
