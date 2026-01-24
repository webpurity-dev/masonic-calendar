using System.Globalization;
using CsvHelper;
using CsvHelper.Configuration;
using MasonicCalendar.Core.Domain;

namespace MasonicCalendar.Core.Services;

/// <summary>
/// Service for ingesting entities from CSV files using CsvHelper.
/// </summary>
public class CsvIngestorService
{
    private static CsvConfiguration CreateCsvConfig()
    {
        return new CsvConfiguration(CultureInfo.InvariantCulture)
        {
            HeaderValidated = null,
            MissingFieldFound = null
        };
    }

    public Result<List<CalendarEvent>> ReadEventsFromCsv(string filePath)
    {
        try
        {
            if (!File.Exists(filePath))
                return Result<List<CalendarEvent>>.Fail($"File not found: {filePath}");

            using var reader = new StreamReader(filePath);
            using var csv = new CsvReader(reader, CreateCsvConfig());
            csv.Context.RegisterClassMap<CalendarEventMap>();

            var events = csv.GetRecords<CalendarEvent>().ToList();
            return Result<List<CalendarEvent>>.Ok(events);
        }
        catch (Exception ex)
        {
            return Result<List<CalendarEvent>>.Fail($"Error reading CSV: {ex.Message}");
        }
    }

    public Result<List<UnitLocation>> ReadLocationsFromCsv(string filePath)
    {
        try
        {
            if (!File.Exists(filePath))
                return Result<List<UnitLocation>>.Fail($"File not found: {filePath}");

            using var reader = new StreamReader(filePath);
            using var csv = new CsvReader(reader, CreateCsvConfig());
            csv.Context.RegisterClassMap<UnitLocationMap>();

            var locations = csv.GetRecords<UnitLocation>().ToList();
            return Result<List<UnitLocation>>.Ok(locations);
        }
        catch (Exception ex)
        {
            return Result<List<UnitLocation>>.Fail($"Error reading CSV: {ex.Message}");
        }
    }

    public Result<List<Unit>> ReadUnitsFromCsv(string filePath)
    {
        try
        {
            if (!File.Exists(filePath))
                return Result<List<Unit>>.Fail($"File not found: {filePath}");

            using var reader = new StreamReader(filePath);
            using var csv = new CsvReader(reader, CreateCsvConfig());
            csv.Context.RegisterClassMap<UnitMap>();

            var units = csv.GetRecords<Unit>().ToList();
            return Result<List<Unit>>.Ok(units);
        }
        catch (Exception ex)
        {
            return Result<List<Unit>>.Fail($"Error reading CSV: {ex.Message}");
        }
    }
}

/// <summary>
/// ClassMap for CalendarEvent CSV parsing.
/// </summary>
public class CalendarEventMap : ClassMap<CalendarEvent>
{
    public CalendarEventMap()
    {
        Map(m => m.EventId).Name("EventId");
        Map(m => m.EventName).Name("EventName");
        Map(m => m.EventDate).Name("EventDate");
        Map(m => m.Description).Name("Description");
        Map(m => m.Location).Name("Location");
    }
}

/// <summary>
/// Custom TypeConverter for What3Words addresses.
/// Extracts the actual what3words address (e.g., "///rounds.solicitor.received") from CSV text.
/// </summary>
public class What3WordsConverter : CsvHelper.TypeConversion.DefaultTypeConverter
{
    public override object? ConvertFromString(string? text, IReaderRow row, MemberMapData memberMapData)
    {
        if (string.IsNullOrWhiteSpace(text))
            return string.Empty;

        text = text.Trim();

        // If it starts with "what3words address: " extract just the address part
        if (text.StartsWith("what3words address: ", StringComparison.OrdinalIgnoreCase))
        {
            return text.Substring("what3words address: ".Length).Trim();
        }

        // Otherwise return as-is (in case it's already just the address)
        return text;
    }
}

/// <summary>
/// ClassMap for UnitLocation CSV parsing.
/// </summary>
public class UnitLocationMap : ClassMap<UnitLocation>
{
    public UnitLocationMap()
    {
        Map(m => m.Id).Name("ID");
        Map(m => m.Name).Name("Name");
        Map(m => m.AddressLine1).Name("AddressLine1");
        Map(m => m.Town).Name("Town");
        Map(m => m.Postcode).Name("Postcode");
        Map(m => m.What3Words).Name("What3Words").TypeConverter<What3WordsConverter>();
    }
}

/// <summary>
/// ClassMap for Unit CSV parsing.
/// </summary>
public class UnitMap : ClassMap<Unit>
{
    public UnitMap()
    {
        Map(m => m.Id).Name("ID");
        Map(m => m.Number).Name("Number");
        Map(m => m.Name).Name("Name");
        Map(m => m.Location).Name("Location");
        Map(m => m.LocationId).Name("LocationID");
        Map(m => m.Email).Name("Email");
        Map(m => m.InstallationMonth).Name("InstallationMonth");
        Map(m => m.MeetingSummary).Name("MeetingSummary");
        Map(m => m.WarrantIssued).Name("WarrantIssued").TypeConverter<DateOnlyConverter>();
    }
}

/// <summary>
/// Custom converter for DateOnly to handle multiple date formats.
/// </summary>
public class DateOnlyConverter : CsvHelper.TypeConversion.DefaultTypeConverter
{
    public override object? ConvertFromString(string? text, CsvHelper.IReaderRow row, CsvHelper.Configuration.MemberMapData memberMapData)
    {
        if (string.IsNullOrWhiteSpace(text))
            return null;

        text = text.Trim();

        // Try DD/MM/YYYY format (26/03/1847)
        if (DateOnly.TryParseExact(text, "dd/MM/yyyy", System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out var dateOnly))
            return dateOnly;

        // Try YYYY-MM-DD format
        if (DateOnly.TryParseExact(text, "yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out dateOnly))
            return dateOnly;

        return null;
    }
}
