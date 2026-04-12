namespace MasonicCalendar.Core.Loaders;

using System.Globalization;
using System.Text;
using CsvHelper;
using MasonicCalendar.Core.Domain;
using MasonicCalendar.Core.Renderers.Utilities;

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

            // Load membership data (officers, past masters, members, etc.) and attach to units
            var hermesResult = await LoadHermesDataAsync(mapping!, units);
            if (!hermesResult.Success)
                return Result<List<SchemaUnit>>.Fail(hermesResult.Error ?? "Failed to load membership data");

            // Assign column positions (posNo) for splitting officers and members across columns
            AssignColumnPositions(units);

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

            var fieldMap = BuildFieldMapWithMetadata(mapping.Units.Fields);

            while (await csv.ReadAsync())
            {
                if (!RowPassesFilters(csv, mapping.Units))
                    continue;

                var unit = new SchemaUnit
                {
                    Number = ParseInt(GetFieldValueWithComposite(csv, fieldMap, "Number")),
                    Name = GetFieldValueWithComposite(csv, fieldMap, "Name") ?? "",
                    ShortName = GetFieldValueWithComposite(csv, fieldMap, "ShortName"),
                    SuperShortName = GetFieldValueWithComposite(csv, fieldMap, "SuperShortName"),
                    Contact = GetFieldValueWithComposite(csv, fieldMap, "Contact"),
                    LocationId = GetFieldValueWithComposite(csv, fieldMap, "Location"),
                    LastInstallationDate = GetFieldValueWithComposite(csv, fieldMap, "LastInstallationDate"),
                    Warrant = GetFieldValueWithComposite(csv, fieldMap, "Warrant"),
                    MeetingDates = GetFieldValueWithComposite(csv, fieldMap, "MeetingDates"),
                    Hall = GetFieldValueWithComposite(csv, fieldMap, "Hall"),
                    UnitType = mapping.Units.FilterField != null ? csv.GetField(mapping.Units.FilterField) : null,
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

    private Dictionary<string, FieldMapping> BuildFieldMapWithMetadata(List<FieldMapping>? fieldMappings)
    {
        var map = new Dictionary<string, FieldMapping>();
        if (fieldMappings == null)
            return map;

        foreach (var field in fieldMappings)
        {
            if (!string.IsNullOrWhiteSpace(field.Name))
            {
                map[field.Name] = field;
            }
        }
        return map;
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

    private string? GetFieldValueWithComposite(CsvReader csv, Dictionary<string, FieldMapping> fieldMap, string propertyName)
    {
        if (!fieldMap.TryGetValue(propertyName, out var fieldMapping))
        {
            // Field not declared in YAML mapping — return null rather than attempting a column lookup
            // that may throw if the column doesn't exist in the CSV.
            return null;
        }

        // Handle composite fields (combine multiple columns)
        if (fieldMapping.IsComposite && !string.IsNullOrWhiteSpace(fieldMapping.CompositeFormat) && 
            fieldMapping.CompositeFields?.Count > 0)
        {
            try
            {
                var compositeValue = fieldMapping.CompositeFormat;
                
                // Replace placeholders with actual column values (trim each field)
                foreach (var columnName in fieldMapping.CompositeFields)
                {
                    var columnValue = (csv.GetField(columnName) ?? "").Trim();
                    compositeValue = compositeValue.Replace($"{{{columnName}}}", columnValue);
                }

                return compositeValue.Trim();
            }
            catch
            {
                // If composite fails, fall back to primary CsvColumn
                return !string.IsNullOrWhiteSpace(fieldMapping.CsvColumn) ? csv.GetField(fieldMapping.CsvColumn) : null;
            }
        }

        // Handle regular single-column fields
        if (!string.IsNullOrWhiteSpace(fieldMapping.CsvColumn))
        {
            return csv.GetField(fieldMapping.CsvColumn);
        }

        return null;
    }

    private string? GetFieldValue(CsvReader csv, Dictionary<string, string> fieldMap, string propertyName)
    {
        if (fieldMap.TryGetValue(propertyName, out var csvColumn))
        {
            return csv.GetField(csvColumn);
        }
        // Fallback to property name if not in map
        return csv.GetField(propertyName);
    }    
    
    
    private async Task<Result<bool>> LoadHermesDataAsync(DataSourceMapping mapping, List<SchemaUnit> units)
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
                        var reference = GetFieldValue(csv, fieldMap, "Reference");
                        var name = GetFieldValue(csv, fieldMap, "Name");
                        var rawPos = GetFieldValue(csv, fieldMap, "PositionNo");
                        var positionNo = int.TryParse(rawPos, out var pn) ? (int?)pn : null;
                        var memType = csv.GetField("MemType")?.Trim() ?? "";
                        var office  = csv.GetField("Office")?.Trim()  ?? "";

                        // Deduplicate only on exact (Reference + PositionNo) match — a person
                        // can legitimately hold multiple offices, so Reference alone is not enough.
                        // Vacant rows share the unit-number as a placeholder ref — never skip those.
                        if (!string.IsNullOrWhiteSpace(reference) && !string.IsNullOrWhiteSpace(name)
                            && positionNo.HasValue
                            && schemaUnit.Officers.Any(o => o.Reference == reference && o.PosNo == positionNo))
                            return; // skip exact duplicate

                        schemaUnit.Officers.Add(new SchemaOfficer
                        {
                            Reference = reference,
                            MemType = memType,
                            Office = office,
                            Name = TextCleaner.CleanName(name),
                            Position = GetFieldValue(csv, fieldMap, "Position"),
                            PosNo = positionNo
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
                        var name = GetFieldValue(csv, fieldMap, "Name");
                        
                        var grandRank = GetFieldValue(csv, fieldMap, "GrandRank");
                        var provRank = GetFieldValue(csv, fieldMap, "ProvincialRank");                                             
                        // Prefer GrandRank, fallback to ProvincialRank
                        var displayRank = string.IsNullOrWhiteSpace(grandRank) ? provRank : grandRank;
                        
                        var grandRankYear = GetFieldValue(csv, fieldMap, "GrandRankYear");
                        var provRankYear = GetFieldValue(csv, fieldMap, "ProvincialRankYear");
                        // Prefer GrandRank, fallback to ProvincialRank
                        var displayRankYear = string.IsNullOrWhiteSpace(grandRank) ? provRankYear : grandRankYear;

                        schemaUnit.PastMasters.Add(new SchemaPastMaster
                        {
                            Reference = GetFieldValue(csv, fieldMap, "Reference"),
                            MemType = csv.GetField("MemType")?.Trim() ?? "",
                            Name = TextCleaner.CleanName(name),
                            YearInstalled = GetFieldValue(csv, fieldMap, "YearInstalled"),
                            Rank = TextCleaner.CleanProvincialRank(displayRank),
                            RankYear = TextCleaner.CleanDateIssued(displayRankYear)
                        });
                    });
            }

            // Load joining past masters
            if (mapping.JoiningPastMasters != null)
            {
                await LoadPersonTypeAsync(units, mapping.JoiningPastMasters, "joining past master",
                    schemaUnit => (fieldMap, csv, unitNumber) =>
                    {
                        var name = GetFieldValue(csv, fieldMap, "Name");
                        var pastUnits = TextCleaner.CleanPastUnits(GetFieldValue(csv, fieldMap, "PastUnits"));
                        var grandRank = GetFieldValue(csv, fieldMap, "GrandRank");
                        var provRank = GetFieldValue(csv, fieldMap, "ProvincialRank");
                        // Prefer GrandRank, fallback to ProvincialRank
                        var displayRank = string.IsNullOrWhiteSpace(grandRank) ? provRank : grandRank;

                        schemaUnit.JoinPastMasters.Add(new SchemaJoinPastMaster
                        {
                            Reference = GetFieldValue(csv, fieldMap, "Reference"),
                            MemType = csv.GetField("MemType")?.Trim() ?? "",
                            Name = TextCleaner.CleanName(name),
                            PastUnits = pastUnits,
                            Rank = TextCleaner.CleanProvincialRank(displayRank),
                            RankYear = TextCleaner.CleanDateIssued(GetFieldValue(csv, fieldMap, "RankYear"))
                        });
                    });
            }

            // Load members
            if (mapping.Members != null)
            {
                await LoadPersonTypeAsync(units, mapping.Members, "member",
                    schemaUnit => (fieldMap, csv, unitNumber) =>
                    {
                        var name = GetFieldValue(csv, fieldMap, "Name");

                        schemaUnit.Members.Add(new SchemaMember
                        {
                            Reference = GetFieldValue(csv, fieldMap, "Reference"),
                            MemType = csv.GetField("MemType")?.Trim() ?? "",
                            Name = TextCleaner.CleanName(name),
                            YearInitiated = GetFieldValue(csv, fieldMap, "YearInitiated")
                        });
                    });
            }

            // Load honorary members
            if (mapping.HonoraryMembers != null)
            {
                await LoadPersonTypeAsync(units, mapping.HonoraryMembers, "honorary member",
                    schemaUnit => (fieldMap, csv, unitNumber) =>
                    {
                        var reference = GetFieldValue(csv, fieldMap, "Reference");
                        if (!string.IsNullOrWhiteSpace(reference) && schemaUnit.HonoraryMembers.Any(h => h.Reference == reference))
                            return; // skip duplicate

                        var name = GetFieldValue(csv, fieldMap, "Name");
                        var grandRank = GetFieldValue(csv, fieldMap, "GrandRank");
                        var provincialRank = GetFieldValue(csv, fieldMap, "ProvincialRank");                        
                        // Prefer GrandRank, fallback to ProvincialRank
                        var displayRank = string.IsNullOrWhiteSpace(grandRank) ? provincialRank : grandRank;

                        schemaUnit.HonoraryMembers.Add(new SchemaHonoraryMember
                        {
                            Reference = reference,
                            MemType = csv.GetField("MemType")?.Trim() ?? "",
                            Name = TextCleaner.CleanName(name),
                            Rank = TextCleaner.CleanProvincialRank(displayRank)
                        });
                    });
            }

            // Data is kept in the order it appears in the CSV file

            return Result<bool>.Ok(true);
        }
        catch (Exception ex)
        {
            return Result<bool>.Fail($"Error loading membership data: {ex.Message}");
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
            var unitIdField = dataSource.UnitIdField ?? "Unit";

            while (await csv.ReadAsync())
            {
                if (!RowPassesFilters(csv, dataSource))
                    continue;

                var unitNumber = ParseInt(csv.GetField(unitIdField));
                if (unitNumber == 0)
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

    /// <summary>
    /// Returns true if the current CSV row passes all filters defined on the data source.
    /// Supports both the legacy single FilterField/FilterValue and the new Filters list (AND logic).
    /// If neither is configured the row is always accepted.
    /// </summary>
    private static bool RowPassesFilters(CsvReader csv, DataSourceDefinition dataSource)
    {
        // New multi-filter list takes precedence when present
        if (dataSource.Filters is { Count: > 0 })
        {
            foreach (var filter in dataSource.Filters)
            {
                if (string.IsNullOrWhiteSpace(filter.FilterField) || string.IsNullOrWhiteSpace(filter.FilterValue))
                    continue;
                var value = csv.GetField(filter.FilterField);
                if (value != filter.FilterValue)
                    return false;
            }
            return true;
        }

        // Legacy single filter
        if (!string.IsNullOrWhiteSpace(dataSource.FilterField) && !string.IsNullOrWhiteSpace(dataSource.FilterValue))
        {
            var value = csv.GetField(dataSource.FilterField);
            return value == dataSource.FilterValue;
        }

        // No filter configured — accept all rows
        return true;
    }

    private int ParseInt(string? value)
    {
        return int.TryParse(value?.Trim(), out var result) ? result : 0;
    }

    /// <summary>
    /// Assign posNo (position number) to officers and members for column splitting in templates.
    /// </summary>
    private void AssignColumnPositions(List<SchemaUnit> units)
    {
        foreach (var unit in units)
        {
            // Sort by OffPos-derived PosNo before reindexing; nulls (no OffPos) go last
            unit.Officers.Sort((a, b) =>
            {
                if (a.PosNo == null && b.PosNo == null) return 0;
                if (a.PosNo == null) return 1;
                if (b.PosNo == null) return -1;
                return a.PosNo.Value.CompareTo(b.PosNo.Value);
            });
            for (int i = 0; i < unit.Officers.Count; i++)
                unit.Officers[i].PosNo = i;

            for (int i = 0; i < unit.Members.Count; i++)
                unit.Members[i].PosNo = i;
        }
    }
}

