using MasonicCalendar.Core.Services;
using MasonicCalendar.Export.Pdf;

// Parse command-line arguments
var outputFormat = "pdf"; // default to PDF
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
}

var dataPath = Path.Combine(Directory.GetCurrentDirectory(), "..", "..", "data");
var eventsPath = Path.Combine(dataPath, "sample-events.csv");
var unitsPath = Path.Combine(dataPath, "sample-units.csv");
var locationsPath = Path.Combine(dataPath, "sample-unit-locations.csv");

// Generate timestamp for unique filenames
var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
var unitsOutputPath = Path.Combine(Directory.GetCurrentDirectory(), $"units-output_{timestamp}.{outputFormat}");

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
Console.WriteLine($"✅ Loaded {unitsResult.Data!.Count} units\n");

// Generate output
try
{
    if (outputFormat == "pdf")
    {
        Console.WriteLine("Generating unit pages PDF...");
        var unitExporter = new UnitPdfExporter();
        unitExporter.ExportUnitsToPdf(unitsResult.Data, locationDict, unitsOutputPath);
        Console.WriteLine($"✅ Units PDF generated: {unitsOutputPath}");
    }
    else if (outputFormat == "html")
    {
        Console.WriteLine("Generating unit pages HTML...");
        var unitExporter = new UnitPdfExporter();
        unitExporter.ExportUnitsToHtml(unitsResult.Data, locationDict, unitsOutputPath);
        Console.WriteLine($"✅ Units HTML generated: {unitsOutputPath}");
    }
    
    Console.WriteLine($"   Units included: {unitsResult.Data.Count}");
}
catch (Exception ex)
{
    Console.WriteLine($"❌ Error generating output: {ex.Message}");
    return 1;
}

Console.WriteLine("\n✨ Output generation completed successfully!");
return 0;
