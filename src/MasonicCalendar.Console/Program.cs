using MasonicCalendar.Core.Services;
using MasonicCalendar.Export.Pdf;

var csvPath = Path.Combine(Directory.GetCurrentDirectory(), "..", "..", "data", "sample-events.csv");
var outputPath = Path.Combine(Directory.GetCurrentDirectory(), "calendar-output.pdf");

Console.WriteLine("🗓️  Masonic Calendar - CSV to PDF Converter");
Console.WriteLine("==========================================\n");

// Read CSV
var ingestor = new CsvIngestorService();
var csvResult = ingestor.ReadEventsFromCsv(csvPath);

if (!csvResult.Success)
{
    Console.WriteLine($"❌ Error reading CSV: {csvResult.Error}");
    return 1;
}

Console.WriteLine($"✅ Loaded {csvResult.Data!.Count} events from CSV");

// Generate PDF
try
{
    var exporter = new EventPdfExporter();
    exporter.ExportEventsToPdf(csvResult.Data, outputPath);
    Console.WriteLine($"✅ PDF generated: {outputPath}");
    Console.WriteLine($"\nEvents included:");
    foreach (var evt in csvResult.Data.OrderBy(e => e.EventDate))
    {
        Console.WriteLine($"  • {evt.EventDate:ddd, MMM d} - {evt.EventName}");
    }
}
catch (Exception ex)
{
    Console.WriteLine($"❌ Error generating PDF: {ex.Message}");
    return 1;
}

return 0;
