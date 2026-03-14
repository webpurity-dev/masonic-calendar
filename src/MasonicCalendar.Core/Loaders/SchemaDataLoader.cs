namespace MasonicCalendar.Core.Loaders;

using System.Globalization;
using System.Text;
using CsvHelper;
using MasonicCalendar.Core.Domain;

/// <summary>
/// Schema-driven data loader that reads master_v1.yaml to dynamically load and parse CSV files.
/// Handles type coercion, field mapping, and creates strongly-typed domain objects.
/// </summary>
public class SchemaDataLoader(DocumentLayoutLoader layoutLoader, string? dataRoot = null)
{
    private readonly DocumentLayoutLoader _layoutLoader = layoutLoader;
    private readonly string _dataRoot = dataRoot ?? Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "data");

    public async Task<Result<List<SchemaUnit>>> LoadUnitsWithDataAsync(string masterTemplateKey, string? sectionId = null)
    {
        try
        {
            var layoutResult = _layoutLoader.LoadMasterLayout(masterTemplateKey);
            if (!layoutResult.Success)
                return Result<List<SchemaUnit>>.Fail(layoutResult.Error ?? "Failed to load template");

            var layout = layoutResult.Data;
            
            // Determine which data mapping to load
            string? dataMappingFile = null;
            
            if (!string.IsNullOrWhiteSpace(sectionId) && layout?.Sections != null)
            {
                // Load specific section's data mapping
                var section = layout.Sections.FirstOrDefault(s => 
                    s.SectionId?.Equals(sectionId, StringComparison.OrdinalIgnoreCase) ?? false);
                dataMappingFile = section?.DataMapping;
            }
            else if (layout?.Sections?.Count > 0)
            {
                // Use first data-driven section's mapping
                var firstDataSection = layout.Sections.FirstOrDefault(s => 
                    s.Type?.Equals("data-driven", StringComparison.OrdinalIgnoreCase) ?? false);
                dataMappingFile = firstDataSection?.DataMapping;
            }

            if (string.IsNullOrWhiteSpace(dataMappingFile))
                // Fallback to default craft mapping
                dataMappingFile = "craft_data_source.yaml";

            // Load the data source mapping
            var mappingResult = _layoutLoader.LoadDataSourceMapping(dataMappingFile);
            if (!mappingResult.Success)
                return Result<List<SchemaUnit>>.Fail(mappingResult.Error ?? "Failed to load data source mapping");

            var mapping = mappingResult.Data;
            var units = new List<SchemaUnit>();

            // Load units from CSV using the mapping
            var unitsResult = await LoadUnitsFromCsvAsync(mapping!);
            if (!unitsResult.Success)
                return Result<List<SchemaUnit>>.Fail(unitsResult.Error ?? "Failed to load units CSV");

            units = unitsResult.Data ?? [];

            // Load hermes export data and attach to units
            var hermesResult = await LoadHermesDataAsync(mapping!, units);
            if (!hermesResult.Success)
                return Result<List<SchemaUnit>>.Fail(hermesResult.Error ?? "Failed to load hermes export");

            return Result<List<SchemaUnit>>.Ok(units);
        }
        catch (Exception ex)
        {
            return Result<List<SchemaUnit>>.Fail($"Error loading data: {ex.Message}");
        }
    }

    private async Task<Result<List<SchemaUnit>>> LoadUnitsFromCsvAsync(DataSourceMapping mapping)
    {
        try
        {
            var units = new List<SchemaUnit>();
            
            if (mapping.Units?.Source == null)
                return Result<List<SchemaUnit>>.Fail("No units source defined in data mapping");

            var unitsFile = Path.Combine(_dataRoot, mapping.Units.Source);

            if (!File.Exists(unitsFile))
                return Result<List<SchemaUnit>>.Fail($"Units file not found: {unitsFile}");

            using var reader = new StreamReader(unitsFile, Encoding.UTF8);
            using var csv = new CsvReader(reader, CultureInfo.InvariantCulture);

            await csv.ReadAsync();
            csv.ReadHeader();

            var fieldMap = BuildFieldMap(mapping.Units.Fields);

            while (await csv.ReadAsync())
            {
                var unit = new SchemaUnit
                {
                    Number = ParseInt(GetFieldValue(csv, fieldMap, "Number")),
                    Name = GetFieldValue(csv, fieldMap, "Name") ?? "",
                    ShortName = GetFieldValue(csv, fieldMap, "ShortName"),
                    Email = GetFieldValue(csv, fieldMap, "Email"),
                    Established = ParseDate(GetFieldValue(csv, fieldMap, "Established")),
                    LastInstallationDate = ParseDate(GetFieldValue(csv, fieldMap, "LastInstallationDate"))
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

    private Dictionary<string, string> BuildFieldMap(List<FieldMapping>? fieldMappings)
    {
        var map = new Dictionary<string, string>();
        if (fieldMappings == null)
            return map;

        foreach (var field in fieldMappings)
        {
            if (!string.IsNullOrWhiteSpace(field.Name) && !string.IsNullOrWhiteSpace(field.CsvColumn))
            {
                map[field.Name] = field.CsvColumn;
            }
        }
        return map;
    }

    private string? GetFieldValue(CsvReader csv, Dictionary<string, string> fieldMap, string propertyName)
    {
        if (fieldMap.TryGetValue(propertyName, out var csvColumn))
        {
            return csv.GetField(csvColumn);
        }
        // Fallback to property name if not in map
        return csv.GetField(propertyName);
    }    private async Task<Result<bool>> LoadHermesDataAsync(DataSourceMapping mapping, List<SchemaUnit> units)
    {
        try
        {
            // Load officers
            if (mapping.Officers != null)
            {
                await LoadPersonTypeAsync(units, mapping.Officers, "officer", schemaUnit =>
                {
                    return (fieldMap, csv, unitNumber) =>
                    {
                        schemaUnit.Officers.Add(new SchemaOfficer
                        {
                            Reference = GetFieldValue(csv, fieldMap, "Reference"),
                            Name = GetFieldValue(csv, fieldMap, "Name") ?? "",
                            Position = GetFieldValue(csv, fieldMap, "Position"),
                            DisplayOrder = ParseInt(GetFieldValue(csv, fieldMap, "DisplayOrder"))
                        });
                    };
                });
            }

            // Load past masters
            if (mapping.PastMasters != null)
            {
                await LoadPersonTypeAsync(units, mapping.PastMasters, "past master",
                    schemaUnit => (fieldMap, csv, unitNumber) =>
                    {
                        schemaUnit.PastMasters.Add(new SchemaPastMaster
                        {
                            Reference = GetFieldValue(csv, fieldMap, "Reference"),
                            Name = GetFieldValue(csv, fieldMap, "Name") ?? "",
                            YearInstalled = GetFieldValue(csv, fieldMap, "YearInstalled"),
                            ProvincialRank = GetFieldValue(csv, fieldMap, "ProvincialRank"),
                            RankYear = GetFieldValue(csv, fieldMap, "RankYear"),
                            DisplayOrder = ParseInt(GetFieldValue(csv, fieldMap, "DisplayOrder"))
                        });
                    });
            }

            // Load joining past masters
            if (mapping.JoiningPastMasters != null)
            {
                await LoadPersonTypeAsync(units, mapping.JoiningPastMasters, "joining past master",
                    schemaUnit => (fieldMap, csv, unitNumber) =>
                    {
                        schemaUnit.JoinPastMasters.Add(new SchemaJoinPastMaster
                        {Reference = GetFieldValue(csv, fieldMap, "Reference"),
                            
                            Name = GetFieldValue(csv, fieldMap, "Name") ?? "",
                            YearInstalled = GetFieldValue(csv, fieldMap, "YearInstalled"),
                            ProvincialRank = GetFieldValue(csv, fieldMap, "ProvincialRank"),
                            RankYear = GetFieldValue(csv, fieldMap, "RankYear"),
                            DisplayOrder = ParseInt(GetFieldValue(csv, fieldMap, "DisplayOrder"))
                        });
                    });
            }

            // Load members
            if (mapping.Members != null)
            {
                await LoadPersonTypeAsync(units, mapping.Members, "member",
                    schemaUnit => (fieldMap, csv, unitNumber) =>
                    {
                        schemaUnit.Members.Add(new SchemaMember
                        {
                            Reference = GetFieldValue(csv, fieldMap, "Reference"),
                            Name = GetFieldValue(csv, fieldMap, "Name") ?? "",
                            YearInitiated = GetFieldValue(csv, fieldMap, "YearInitiated"),
                            DisplayOrder = ParseInt(GetFieldValue(csv, fieldMap, "DisplayOrder"))
                        });
                    });
            }

            // Load honorary members
            if (mapping.HonoraryMembers != null)
            {
                await LoadPersonTypeAsync(units, mapping.HonoraryMembers, "honorary member",
                    schemaUnit => (fieldMap, csv, unitNumber) =>
                    {
                        schemaUnit.HonoraryMembers.Add(new SchemaHonoraryMember
                        {
                            Reference = GetFieldValue(csv, fieldMap, "Reference"),
                            Name = GetFieldValue(csv, fieldMap, "Name") ?? "",
                            DisplayOrder = ParseInt(GetFieldValue(csv, fieldMap, "DisplayOrder"))
                        });
                    });
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

    private async Task LoadPersonTypeAsync(
        List<SchemaUnit> units,
        DataSourceDefinition dataSource,
        string personTypeName,
        Func<SchemaUnit, Action<Dictionary<string, string>, CsvReader, int>> addPersonDelegate)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(dataSource.Source))
                return;

            var file = Path.Combine(_dataRoot, dataSource.Source);
            if (!File.Exists(file))
                return;

            using var reader = new StreamReader(file, Encoding.UTF8);
            using var csv = new CsvReader(reader, CultureInfo.InvariantCulture);

            await csv.ReadAsync();
            csv.ReadHeader();

            var fieldMap = BuildFieldMap(dataSource.Fields);

            while (await csv.ReadAsync())
            {
                var unitNumber = ParseInt(csv.GetField("Unit"));
                var rawName = csv.GetField("Name");
                var name = CleanName(rawName);

                // Check filter
                if (!string.IsNullOrWhiteSpace(dataSource.FilterField) &&
                    !string.IsNullOrWhiteSpace(dataSource.FilterValue))
                {
                    var filterValue = csv.GetField(dataSource.FilterField);
                    if (filterValue != dataSource.FilterValue)
                        continue;
                }

                // Skip invalid records
                if (unitNumber == 0 || string.IsNullOrWhiteSpace(name))
                    continue;

                var unit = units.FirstOrDefault(u => u.Number == unitNumber);
                if (unit == null)
                    continue;

                var addPerson = addPersonDelegate(unit);
                addPerson(fieldMap, csv, unitNumber);
            }
        }
        catch (Exception ex)
        {
            // Log but don't fail - missing person types are not fatal
            System.Diagnostics.Debug.WriteLine($"Error loading {personTypeName}: {ex.Message}");
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

        // Standard date formats for parsing
        var formatStrings = new[] 
        { 
            "yyyy-MM-dd", 
            "dd/MM/yyyy", 
            "d MMMM yyyy",
            "d'st' MMMM yyyy",
            "d'nd' MMMM yyyy",
            "d'rd' MMMM yyyy",
            "d'th' MMMM yyyy"
        };

        if (DateOnly.TryParseExact(value.Trim(), formatStrings, CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out var result))
            return result;

        return null;
    }

    private string? CleanName(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return name;
        
        // Clean name: remove newlines from quoted CSV fields, trim, replace corruption chars with space
        var cleaned = name.Replace("\r", "").Replace("\n", "").Trim();
        cleaned = cleaned.Replace("•", " ");  // Replace bullet char with space
        cleaned = cleaned.Replace("\ufffd", " ");  // Replace Unicode Replacement Character with space
        
        // Collapse multiple spaces to single space
        while (cleaned.Contains("  "))
            cleaned = cleaned.Replace("  ", " ");
        
        return cleaned;
    }
}
