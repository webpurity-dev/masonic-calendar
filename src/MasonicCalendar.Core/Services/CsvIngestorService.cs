using System.Globalization;
using CsvHelper;
using MasonicCalendar.Core.Domain;

namespace MasonicCalendar.Core.Services;

/// <summary>
/// Service for ingesting calendar events from CSV files.
/// </summary>
public class CsvIngestorService
{
    public Result<List<CalendarEvent>> ReadEventsFromCsv(string filePath)
    {
        try
        {
            if (!File.Exists(filePath))
                return Result<List<CalendarEvent>>.Fail($"File not found: {filePath}");

            using var reader = new StreamReader(filePath);
            using var csv = new CsvReader(reader, CultureInfo.InvariantCulture);

            var events = csv.GetRecords<CalendarEvent>().ToList();
            return Result<List<CalendarEvent>>.Ok(events);
        }
        catch (Exception ex)
        {
            return Result<List<CalendarEvent>>.Fail($"Error reading CSV: {ex.Message}");
        }
    }
}
