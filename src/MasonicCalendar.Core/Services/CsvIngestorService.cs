

using System.Globalization;
using CsvHelper;
using CsvHelper.Configuration;
using MasonicCalendar.Core.Domain;

namespace MasonicCalendar.Core.Services;

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

    public Result<List<Officer>> ReadOfficersFromCsv(string filePath)
    {
        try
        {
            if (!File.Exists(filePath))
                return Result<List<Officer>>.Fail($"File not found: {filePath}");

            using var reader = new StreamReader(filePath);
            using var csv = new CsvReader(reader, CreateCsvConfig());
            csv.Context.RegisterClassMap<OfficerMap>();

            var officers = csv.GetRecords<Officer>().ToList();
            return Result<List<Officer>>.Ok(officers);
        }
        catch (Exception ex)
        {
            return Result<List<Officer>>.Fail($"Error reading CSV: {ex.Message}");
        }
    }

    public Result<List<UnitOfficer>> ReadUnitOfficersFromCsv(string filePath)
    {
        try
        {
            if (!File.Exists(filePath))
                return Result<List<UnitOfficer>>.Fail($"File not found: {filePath}");

            using var reader = new StreamReader(filePath);
            using var csv = new CsvReader(reader, CreateCsvConfig());
            csv.Context.RegisterClassMap<UnitOfficerMap>();

            var unitOfficers = csv.GetRecords<UnitOfficer>().ToList();
            return Result<List<UnitOfficer>>.Ok(unitOfficers);
        }
        catch (Exception ex)
        {
            return Result<List<UnitOfficer>>.Fail($"Error reading CSV: {ex.Message}");
        }
    }

    public Result<List<UnitPastMaster>> ReadUnitPastMastersFromCsv(string filePath)
    {
        try
        {
            if (!File.Exists(filePath))
                return Result<List<UnitPastMaster>>.Fail($"File not found: {filePath}");

            using var reader = new StreamReader(filePath);
            using var csv = new CsvReader(reader, CreateCsvConfig());
            csv.Context.RegisterClassMap<UnitPastMasterMap>();

            var pastMasters = csv.GetRecords<UnitPastMaster>().ToList();
            return Result<List<UnitPastMaster>>.Ok(pastMasters);
        }
        catch (Exception ex)
        {
            return Result<List<UnitPastMaster>>.Fail($"Error reading CSV: {ex.Message}");
        }
    }

    public Result<List<UnitMeeting>> ReadUnitMeetingsFromCsv(string filePath)
    {
        try
        {
            if (!File.Exists(filePath))
                return Result<List<UnitMeeting>>.Fail($"File not found: {filePath}");

            using var reader = new StreamReader(filePath);
            using var csv = new CsvReader(reader, CreateCsvConfig());
            csv.Context.RegisterClassMap<UnitMeetingMap>();

            var meetings = csv.GetRecords<UnitMeeting>().ToList();
            return Result<List<UnitMeeting>>.Ok(meetings);
        }
        catch (Exception ex)
        {
            return Result<List<UnitMeeting>>.Fail($"Error reading CSV: {ex.Message}");
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
        Map(m => m.LastInstallationDate).Name("LastInstallationDate").TypeConverter<DateOnlyConverter>();
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

/// <summary>
/// ClassMap for Officer CSV parsing.
/// </summary>
public class OfficerMap : ClassMap<Officer>
{
    public OfficerMap()
    {
        Map(m => m.Id).Name("ID");
        Map(m => m.Order).Name("Order");
        Map(m => m.Abbreviation).Name("Abbreviation");
        Map(m => m.Name).Name("Name");
    }
}

/// <summary>
/// ClassMap for UnitOfficer CSV parsing.
/// </summary>
public class UnitOfficerMap : ClassMap<UnitOfficer>
{
    public UnitOfficerMap()
    {
        Map(m => m.Id).Name("ID").Optional();
        Map(m => m.UnitId).Name("UnitID");
        Map(m => m.OfficerId).Name("OfficerID");
        Map(m => m.LastName).Name("LastName");
        Map(m => m.Initials).Name("Initials");
    }
}

/// <summary>
/// ClassMap for UnitPastMaster CSV parsing.
/// </summary>
public class UnitPastMasterMap : ClassMap<UnitPastMaster>
{
    public UnitPastMasterMap()
    {
        Map(m => m.Id).Name("ID").Optional();
        Map(m => m.UnitId).Name("UnitID");
        Map(m => m.LastName).Name("LastName");
        Map(m => m.Initials).Name("Initials");
        Map(m => m.Installed).Name("Installed");
        Map(m => m.ProvRank).Name("ProvRank").Optional();
        Map(m => m.ProvRankIssued).Name("ProvRankIssued").Optional();
    }
}
