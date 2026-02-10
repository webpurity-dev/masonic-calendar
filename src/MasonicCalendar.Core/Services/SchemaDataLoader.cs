namespace MasonicCalendar.Core.Services;

using System.Globalization;
using System.Text;
using CsvHelper;
using CsvHelper.Configuration;
using MasonicCalendar.Core.Domain;
using YamlDotNet.Serialization;

/// <summary>
/// Schema-driven data loader that reads master_v1.yaml to dynamically load and parse CSV files.
/// Handles type coercion, field mapping, and creates strongly-typed domain objects.
/// </summary>
public class SchemaDataLoader(DocumentLayoutLoader layoutLoader, ISerializer yamlDeserializer, string? dataRoot = null)
{
    private readonly DocumentLayoutLoader _layoutLoader = layoutLoader;
    private readonly ISerializer _yamlDeserializer = yamlDeserializer;
    private readonly string _dataRoot = dataRoot ?? Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "data");
    private Dictionary<string, object>? _csvMappings;
    private Dictionary<string, object>? _typeCoercion;

    public async Task<Result<List<SchemaUnit>>> LoadUnitsWithDataAsync(string masterTemplateKey)
    {
        try
        {
            var layoutResult = _layoutLoader.LoadMasterLayout(masterTemplateKey);
            if (!layoutResult.Success)
                return Result<List<SchemaUnit>>.Fail(layoutResult.Error ?? "Failed to load template");

            var layout = layoutResult.Data;
            if (layout?.DataSources == null)
                return Result<List<SchemaUnit>>.Fail("No data_sources defined in template");

            // Extract CSV mappings and type coercion rules
            if (layout.CsvColumnMappings != null)
                _csvMappings = layout.CsvColumnMappings;
            if (layout.TypeCoercion != null)
                _typeCoercion = layout.TypeCoercion;

            var units = new List<SchemaUnit>();

            // Load units from sample-units.csv
            var unitsResult = await LoadUnitsFromCsvAsync(layout);
            if (!unitsResult.Success)
                return Result<List<SchemaUnit>>.Fail(unitsResult.Error ?? "Failed to load units CSV");

            units = unitsResult.Data ?? [];

            // Load hermes export data and attach to units
            var hermesResult = await LoadHermesDataAsync(layout, units);
            if (!hermesResult.Success)
                return Result<List<SchemaUnit>>.Fail(hermesResult.Error ?? "Failed to load hermes export");

            return Result<List<SchemaUnit>>.Ok(units);
        }
        catch (Exception ex)
        {
            return Result<List<SchemaUnit>>.Fail($"Error loading data: {ex.Message}");
        }
    }

    private async Task<Result<List<SchemaUnit>>> LoadUnitsFromCsvAsync(DocumentLayout layout)
    {
        try
        {
            var units = new List<SchemaUnit>();
            var unitsFile = Path.Combine(_dataRoot, "sample-units.csv");

            if (!File.Exists(unitsFile))
                return Result<List<SchemaUnit>>.Fail($"Units file not found: {unitsFile}");

            using var reader = new StreamReader(unitsFile, Encoding.UTF8);
            using var csv = new CsvReader(reader, CultureInfo.InvariantCulture);

            await csv.ReadAsync();
            csv.ReadHeader();

            while (await csv.ReadAsync())
            {
                var unit = new SchemaUnit
                {
                    Number = ParseInt(csv.GetField("Number")),
                    Name = csv.GetField("Name") ?? "",
                    Email = csv.GetField("Email"),
                    Established = ParseDate(csv.GetField("Established")),
                    LastInstallationDate = ParseDate(csv.GetField("LastInstallationDate")),
                    UnitType = csv.GetField("UnitType")
                };

                units.Add(unit);
            }

            return Result<List<SchemaUnit>>.Ok(units);
        }
        catch (Exception ex)
        {
            return Result<List<SchemaUnit>>.Fail($"Error loading units CSV: {ex.Message}");
        }
    }

    private async Task<Result<bool>> LoadHermesDataAsync(DocumentLayout layout, List<SchemaUnit> units)
    {
        try
        {
            var hermesFile = Path.Combine(_dataRoot, "hermes-export.csv");

            if (!File.Exists(hermesFile))
                return Result<bool>.Fail($"Hermes file not found: {hermesFile}");

            using var reader = new StreamReader(hermesFile, Encoding.UTF8);
            using var csv = new CsvReader(reader, CultureInfo.InvariantCulture);

            await csv.ReadAsync();
            csv.ReadHeader();

            while (await csv.ReadAsync())
            {
                var unitNumber = ParseInt(csv.GetField("Unit"));
                var recordType = csv.GetField("Type");
                var name = csv.GetField("Name");

                // Skip invalid records
                if (unitNumber == 0 || string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(recordType))
                    continue;

                var unit = units.FirstOrDefault(u => u.Number == unitNumber);
                if (unit == null)
                    continue;

                var posNo = ParseInt(csv.GetField("PosNo"));

                switch (recordType.Trim())
                {
                    case "Off":
                        unit.Officers.Add(new SchemaOfficer
                        {
                            Name = name,
                            Position = csv.GetField("FN01"),
                            DisplayOrder = posNo
                        });
                        break;

                    case "PMO":
                        unit.PastMasters.Add(new SchemaPastMaster
                        {
                            Name = name,
                            YearInstalled = csv.GetField("FN01"),
                            ProvincialRank = csv.GetField("FN13"),
                            RankYear = csv.GetField("FN14"),
                            DisplayOrder = posNo
                        });
                        break;

                    case "PMI":
                        unit.JoinPastMasters.Add(new SchemaJoinPastMaster
                        {
                            Name = name,
                            YearInstalled = csv.GetField("FN01"),
                            ProvincialRank = csv.GetField("FN12"),
                            RankYear = csv.GetField("FN13"),
                            DisplayOrder = posNo
                        });
                        break;

                    case "Mem":
                        unit.Members.Add(new SchemaMember
                        {
                            Name = name,
                            YearInitiated = csv.GetField("FN01"),
                            DisplayOrder = posNo
                        });
                        break;

                    case "Hon":
                        unit.HonoraryMembers.Add(new SchemaHonoraryMember
                        {
                            Name = name,
                            DisplayOrder = posNo
                        });
                        break;
                }
            }

            // Sort all collections by DisplayOrder
            foreach (var unit in units)
            {
                unit.Officers = [..unit.Officers.OrderBy(o => o.DisplayOrder ?? 0)];
                unit.PastMasters = [..unit.PastMasters.OrderBy(o => o.DisplayOrder ?? 0)];
                unit.JoinPastMasters = [..unit.JoinPastMasters.OrderBy(o => o.DisplayOrder ?? 0)];
                unit.Members = [..unit.Members.OrderBy(o => o.DisplayOrder ?? 0)];
                unit.HonoraryMembers = [..unit.HonoraryMembers.OrderBy(o => o.DisplayOrder ?? 0)];
            }

            return Result<bool>.Ok(true);
        }
        catch (Exception ex)
        {
            return Result<bool>.Fail($"Error loading hermes data: {ex.Message}");
        }
    }

    private int ParseInt(string? value)
    {
        return int.TryParse(value?.Trim(), out var result) ? result : 0;
    }

    private DateOnly? ParseDate(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        var formats = _typeCoercion?["date_formats"] as List<object>
            ?? ["yyyy-MM-dd", "dd/MM/yyyy", "d MMMM yyyy"];

        var formatStrings = formats.Cast<string>().ToArray();

        if (DateOnly.TryParseExact(value.Trim(), formatStrings, CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out var result))
            return result;

        return null;
    }
}

/// <summary>
/// Extension to DocumentLayout to support new schema-driven properties
/// </summary>
public static class DocumentLayoutExtensions
{
    public static Dictionary<string, object>? GetCsvColumnMappings(this DocumentLayout layout)
        => layout.CsvColumnMappings;

    public static Dictionary<string, object>? GetTypeCoercion(this DocumentLayout layout)
        => layout.TypeCoercion;
}
