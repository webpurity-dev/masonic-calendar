using MasonicCalendar.Core.Services;
using MasonicCalendar.Export.Pdf;
using QuestPDF.Infrastructure;

// Configure QuestPDF license for community use (non-profit)
QuestPDF.Settings.License = LicenseType.Community;

var dataPath = Path.Combine(Directory.GetCurrentDirectory(), "..", "..", "data");
var eventsPath = Path.Combine(dataPath, "sample-events.csv");
var unitsPath = Path.Combine(dataPath, "sample-units.csv");
var locationsPath = Path.Combine(dataPath, "sample-unit-locations.csv");
var officersPath = Path.Combine(dataPath, "sample-officers.csv");
var unitOfficersPath = Path.Combine(dataPath, "sample-unit-officers.csv");
var unitPastMastersPath = Path.Combine(dataPath, "sample-unit-pmo.csv");

// Output directory
var outputDir = Path.Combine(Directory.GetCurrentDirectory(), "output");
if (!Directory.Exists(outputDir))
    Directory.CreateDirectory(outputDir);

// Meetings calendar CLI switch
if (args.Contains("--meetings-calendar"))
{
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
    
    // Expand meetings to see how many dates are generated
    var expanded = MeetingRecurrenceExpander.ExpandMeetings(meetingsResult.Data!, 2026, new DateOnly(2026, 1, 1));
    Console.WriteLine($"✅ Generated {expanded.Count} calendar dates from recurrence rules\n");
    
    Console.WriteLine("Generating meetings calendar PDF...");
    var meetingsOutputPath = Path.Combine(outputDir, $"meetings-output-2026.pdf");
    var meetingsExporter = new MeetingsCalendarExporter();
    meetingsExporter.ExportMeetingsToPdf(meetingsResult.Data!, 2026, meetingsOutputPath, meetingsUnitsResult.Data);
    Console.WriteLine($"✅ Meetings calendar PDF generated: {meetingsOutputPath}");
    Console.WriteLine($"\n✨ Meetings calendar completed successfully!");
    return 0;
}

// Parse command-line arguments
var outputFormat = "pdf"; // default to PDF
int? filterUnitNumber = 6827; // default to unit 6827
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

    // Check for unit number filter (e.g., --6827)
    var unitFilterArg = args.FirstOrDefault(a => a.StartsWith("--") && int.TryParse(a.Substring(2), out _));
    if (unitFilterArg != null && int.TryParse(unitFilterArg.Substring(2), out var unitNum))
    {
        filterUnitNumber = unitNum;
    }
}

// Clear output directory before running
{
    // Only remove files matching the current output type
    var ext = outputFormat == "html" ? ".html" : ".pdf";
    foreach (var file in Directory.GetFiles(outputDir, "*" + ext))
        File.Delete(file);
}

// Generate filename based on unit filter and page size
var filenameIdentifier = filterUnitNumber.Value.ToString();
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
