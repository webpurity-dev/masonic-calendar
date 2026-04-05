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

            // Build unit mapping from data source if configured (e.g., CraftData.csv S01 rows)
            var unitMapping = await BuildUnitMappingAsync(mapping!);

            // Load hermes export data and attach to units
            var hermesResult = await LoadHermesDataAsync(mapping!, units, unitMapping);
            if (!hermesResult.Success)
                return Result<List<SchemaUnit>>.Fail(hermesResult.Error ?? "Failed to load hermes export");

            // Load composite properties from data source and attach to units
            if (mapping!.InstallationDates != null)
            {
                var compositeResult = await LoadCompositePropertyAsync(units, mapping.InstallationDates, "LastInstallationDate", unitMapping);
                if (!compositeResult.Success)
                    return Result<List<SchemaUnit>>.Fail(compositeResult.Error ?? "Failed to load composite properties");
            }

            // Load locations and attach to units
            var locationsResult = await LoadLocationsAsync(mapping!);
            if (locationsResult.Success && locationsResult.Data != null)
            {
                var locationsByID = locationsResult.Data.ToDictionary(l => l.ID ?? "", l => l);
                foreach (var unit in units)
                {
                    // Match location by LocationId from CSV (e.g., "Weymouth")
                    if (!string.IsNullOrWhiteSpace(unit.LocationId) && 
                        locationsByID.TryGetValue(unit.LocationId, out var location))
                    {
                        unit.Location = location;
                    }
                }
            }

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
                    Email = GetFieldValueWithComposite(csv, fieldMap, "Email"),
                    LocationId = GetFieldValueWithComposite(csv, fieldMap, "Location"),
                    LastInstallationDate = GetFieldValueWithComposite(csv, fieldMap, "LastInstallationDate"),
                    Warrant = GetFieldValueWithComposite(csv, fieldMap, "Warrant"),
                    MeetingDates = GetFieldValueWithComposite(csv, fieldMap, "MeetingDates"),
                    Hall = GetFieldValueWithComposite(csv, fieldMap, "Hall"),
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
    
    
    private async Task<Result<bool>> LoadHermesDataAsync(DataSourceMapping mapping, List<SchemaUnit> units, Dictionary<int, int>? unitMapping = null)
    {
        try
        {
            // Load officers
            if (mapping.Officers != null)
            {
                await LoadPersonTypeAsync(units, mapping.Officers, "officer", unitMapping, schemaUnit =>
                {
                    return (fieldMap, csv, unitNumber) =>
                    {
                        var surname = GetFieldValue(csv, fieldMap, "Surname");
                        var firstName = GetFieldValue(csv, fieldMap, "FirstName");
                        var initials = GetFieldValue(csv, fieldMap, "Initials");
                        var displayName = TextCleaner.CombineNameInitialsAndFirstName(surname, initials, firstName);
                        
                        schemaUnit.Officers.Add(new SchemaOfficer
                        {
                            Reference = GetFieldValue(csv, fieldMap, "Reference"),
                            Surname = surname,
                            Initials = initials,
                            Name = displayName,
                            Position = GetFieldValue(csv, fieldMap, "Position")
                        });
                    };
                });
            }

            // Load past masters
            if (mapping.PastMasters != null)
            {
                await LoadPersonTypeAsync(units, mapping.PastMasters, "past master", unitMapping,
                    schemaUnit => (fieldMap, csv, unitNumber) =>
                    {
                        var surname = GetFieldValue(csv, fieldMap, "Surname");
                        var initials = GetFieldValue(csv, fieldMap, "Initials");
                        var displayName = TextCleaner.CombineNameInitialsAndFirstName(surname, initials, null);
                        var pastRank = GetFieldValue(csv, fieldMap, "ProvincialRank");
                        var pastRankYear = GetFieldValue(csv, fieldMap, "RankYear");
                        var activeRank = GetFieldValue(csv, fieldMap, "ActiveProvincialRank");
                        var activeRankYear = GetFieldValue(csv, fieldMap, "ActiveRankYear");
                        
                        var displayRank = string.IsNullOrWhiteSpace(activeRank) ? pastRank : activeRank;
                        var displayRankYear = string.IsNullOrWhiteSpace(activeRankYear) ? pastRankYear : activeRankYear;

                        schemaUnit.PastMasters.Add(new SchemaPastMaster
                        {
                            Reference = GetFieldValue(csv, fieldMap, "Reference"),
                            Surname = surname,
                            Initials = initials,
                            Name = displayName,
                            YearInstalled = GetFieldValue(csv, fieldMap, "YearInstalled"),
                            ProvincialRank = displayRank,
                            RankYear = displayRankYear
                        });
                    });
            }

            // Load joining past masters
            if (mapping.JoiningPastMasters != null)
            {
                await LoadPersonTypeAsync(units, mapping.JoiningPastMasters, "joining past master", unitMapping,
                    schemaUnit => (fieldMap, csv, unitNumber) =>
                    {
                        var surname = GetFieldValue(csv, fieldMap, "Surname");
                        var initials = GetFieldValue(csv, fieldMap, "Initials");
                        var displayName = TextCleaner.CombineNameInitialsAndFirstName(surname, initials, null);
                        var pastUnits = GetFieldValue(csv, fieldMap, "PastUnits");
                        var displayPastUnits = TextCleaner.CleanPastUnits(pastUnits);
                        
                        schemaUnit.JoinPastMasters.Add(new SchemaJoinPastMaster
                        {
                            Reference = GetFieldValue(csv, fieldMap, "Reference"),
                            Surname = surname,
                            Initials = initials,
                            Name = displayName,
                            PastUnits = displayPastUnits,
                            ProvincialRank = GetFieldValue(csv, fieldMap, "ProvincialRank"),
                            RankYear = GetFieldValue(csv, fieldMap, "RankYear")
                        });
                    });
            }

            // Load members
            if (mapping.Members != null)
            {
                await LoadPersonTypeAsync(units, mapping.Members, "member", unitMapping,
                    schemaUnit => (fieldMap, csv, unitNumber) =>
                    {
                        var surname = GetFieldValue(csv, fieldMap, "Surname");
                        var initials = GetFieldValue(csv, fieldMap, "Initials");
                        var displayName = TextCleaner.CombineNameInitialsAndFirstName(surname, null, initials);
                        
                        schemaUnit.Members.Add(new SchemaMember
                        {
                            Reference = GetFieldValue(csv, fieldMap, "Reference"),
                            Surname = surname,
                            Initials = initials,
                            Name = displayName,
                            YearInitiated = GetFieldValue(csv, fieldMap, "YearInitiated")
                        });
                    });
            }

            // Load honorary members
            if (mapping.HonoraryMembers != null)
            {
                await LoadPersonTypeAsync(units, mapping.HonoraryMembers, "honorary member", unitMapping,
                    schemaUnit => (fieldMap, csv, unitNumber) =>
                    {
                        var surname = GetFieldValue(csv, fieldMap, "Surname");
                        var initials = GetFieldValue(csv, fieldMap, "Initials");
                        var displayName = TextCleaner.CombineNameInitialsAndFirstName(surname, null, initials);
                        var grandRank = GetFieldValue(csv, fieldMap, "GrandRank");
                        var provincialRank = GetFieldValue(csv, fieldMap, "ProvincialRank");
                        var displayRank = TextCleaner.CombineRanks(grandRank, provincialRank);
                        
                        schemaUnit.HonoraryMembers.Add(new SchemaHonoraryMember
                        {
                            Reference = GetFieldValue(csv, fieldMap, "Reference"),
                            Surname = surname,
                            Initials = initials,
                            Name = displayName,
                            Rank = displayRank
                        });
                    });
            }

            // Data is kept in the order it appears in the CSV file

            return Result<bool>.Ok(true);
        }
        catch (Exception ex)
        {
            return Result<bool>.Fail($"Error loading hermes data: {ex.Message}");
        }
    }

    private async Task<Result<bool>> LoadCompositePropertyAsync(
        List<SchemaUnit> units,
        DataSourceDefinition dataSource,
        string propertyName,
        Dictionary<int, int>? unitMapping = null)
    {
        try
        {
            // If no data source configured, skip
            if (string.IsNullOrWhiteSpace(dataSource.Source))
                return Result<bool>.Ok(true);

            var file = Path.Combine(_dataRoot, dataSource.Source);
            if (!File.Exists(file))
                return Result<bool>.Ok(true);

            Console.WriteLine($"  Loading composite property '{propertyName}' from {dataSource.Source}");

            using var reader = new StreamReader(file, Encoding.UTF8);
            using var csv = new CsvReader(reader, CultureInfo.InvariantCulture);

            await csv.ReadAsync();
            csv.ReadHeader();

            var fieldMapWithMetadata = BuildFieldMapWithMetadata(dataSource.Fields);
            int rowIndex = 1;
            int matchedRows = 0;
            int updatedUnits = 0;

            while (await csv.ReadAsync())
            {
                // Determine unit number: use mapping if available, otherwise use UnitIdField
                int unitNumber = 0;
                
                if (unitMapping != null && unitMapping.TryGetValue(rowIndex, out var mappedUnitNumber))
                {
                    unitNumber = mappedUnitNumber;
                }
                else
                {
                    var unitIdField = dataSource.UnitIdField ?? "Unit";
                    unitNumber = ParseInt(csv.GetField(unitIdField));
                }

                // Check filter
                if (!RowPassesFilters(csv, dataSource))
                {
                    rowIndex++;
                    continue;
                }
                matchedRows++;

                // Skip invalid records
                if (unitNumber == 0)
                {
                    rowIndex++;
                    continue;
                }

                // Find the unit and update its property
                var unit = units.FirstOrDefault(u => u.Number == unitNumber);
                if (unit != null)
                {
                    var valueString = GetFieldValueWithComposite(csv, fieldMapWithMetadata, propertyName);
                    if (!string.IsNullOrWhiteSpace(valueString))
                    {
                        // Handle property assignment based on property name
                        if (propertyName == "LastInstallationDate")
                        {
                            unit.LastInstallationDate = valueString;
                            updatedUnits++;
                            Console.WriteLine($"    Unit {unitNumber}: {propertyName} = {valueString}");
                        }
                        // Add other properties here as needed
                    }
                }

                rowIndex++;
            }

            Console.WriteLine($"  Composite property '{propertyName}': {matchedRows} filter matches, {updatedUnits} units updated");
            return Result<bool>.Ok(true);
        }
        catch (Exception ex)
        {
            return Result<bool>.Fail($"Error loading composite property {propertyName}: {ex.Message}");
        }
    }

    private async Task<Result<List<SchemaLocation>>> LoadLocationsAsync(DataSourceMapping mapping)
    {
        try
        {
            var locations = new List<SchemaLocation>();

            if (mapping.Locations?.Source == null)
                return Result<List<SchemaLocation>>.Ok(locations);

            var locationsFile = Path.Combine(_dataRoot, mapping.Locations.Source);

            if (!File.Exists(locationsFile))
                return Result<List<SchemaLocation>>.Ok(locations);

            using var reader = new StreamReader(locationsFile, Encoding.UTF8);
            using var csv = new CsvReader(reader, CultureInfo.InvariantCulture);

            await csv.ReadAsync();
            csv.ReadHeader();

            var fieldMap = BuildFieldMap(mapping.Locations.Fields);

            while (await csv.ReadAsync())
            {
                var location = new SchemaLocation
                {
                    ID = GetFieldValue(csv, fieldMap, "ID"),
                    Name = GetFieldValue(csv, fieldMap, "Name"),
                    AddressLine1 = GetFieldValue(csv, fieldMap, "AddressLine1"),
                    Town = GetFieldValue(csv, fieldMap, "Town"),
                    Postcode = GetFieldValue(csv, fieldMap, "Postcode"),
                    What3Words = GetFieldValue(csv, fieldMap, "What3Words")
                };

                if (!string.IsNullOrWhiteSpace(location.ID))
                {
                    locations.Add(location);
                }
            }

            return Result<List<SchemaLocation>>.Ok(locations);
        }
        catch (Exception ex)
        {
            return Result<List<SchemaLocation>>.Fail($"Error loading locations: {ex.Message}");
        }
    }

    private async Task LoadPersonTypeAsync(
        List<SchemaUnit> units,
        DataSourceDefinition dataSource,
        string personTypeName,
        Dictionary<int, int>? unitMapping,
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
            int rowIndex = 1;  // Start at 1 (header is row 0)

            while (await csv.ReadAsync())
            {
                // Determine unit number: use mapping if available, otherwise use UnitIdField
                int unitNumber = 0;
                
                if (unitMapping != null && unitMapping.TryGetValue(rowIndex, out var mappedUnitNumber))
                {
                    // Use row-based unit mapping
                    unitNumber = mappedUnitNumber;
                }
                else
                {
                    // Fall back to UnitIdField from CSV
                    var unitIdField = dataSource.UnitIdField ?? "Unit";
                    unitNumber = ParseInt(csv.GetField(unitIdField));
                }

                // Check filter
                if (!RowPassesFilters(csv, dataSource))
                {
                    rowIndex++;
                    continue;
                }

                // Skip invalid records
                if (unitNumber == 0)
                {
                    rowIndex++;
                    continue;
                }

                var unit = units.FirstOrDefault(u => u.Number == unitNumber);
                if (unit == null)
                {
                    rowIndex++;
                    continue;
                }

                var addPerson = addPersonDelegate(unit);
                addPerson(fieldMap, csv, unitNumber);
                
                rowIndex++;
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

    /// <summary>
    /// Assign posNo (position number) to officers and members for column splitting in templates.
    /// Officers and members with posNo <= 11 go in left column, > 11 go in right column.
    /// </summary>
    private void AssignColumnPositions(List<SchemaUnit> units)
    {
        foreach (var unit in units)
        {
            // Assign posNo to officers for left/right column splitting
            for (int i = 0; i < unit.Officers.Count; i++)
            {
                unit.Officers[i].PosNo = i;
            }

            // Assign posNo to members for column splitting
            for (int i = 0; i < unit.Members.Count; i++)
            {
                unit.Members[i].PosNo = i;
            }
        }
    }

    /// <summary>
    /// Builds a unit mapping from a CSV file by reading sequentially and tracking unit identifiers.
    /// When a unit definition row (e.g., S01) is encountered, extracts the unit number and applies 
    /// it to all subsequent rows until the next unit definition row.
    /// Returns Dictionary<int, int> mapping row index to unit number.
    /// </summary>
    private async Task<Dictionary<int, int>?> BuildUnitMappingAsync(DataSourceMapping mapping)
    {
        if (mapping.UnitMapping == null)
            return null;

        try
        {
            var unitMappingConfig = mapping.UnitMapping;
            if (string.IsNullOrWhiteSpace(unitMappingConfig.Source) ||
                string.IsNullOrWhiteSpace(unitMappingConfig.RowIdentifierField) ||
                string.IsNullOrWhiteSpace(unitMappingConfig.RowIdentifierValue) ||
                string.IsNullOrWhiteSpace(unitMappingConfig.UnitNumberField))
                return null;

            var file = Path.Combine(_dataRoot, unitMappingConfig.Source);
            if (!File.Exists(file))
                return null;

            var unitMapping = new Dictionary<int, int>();
            int rowIndex = 0;
            int currentUnitNumber = 0;

            using (var reader = new StreamReader(file, Encoding.UTF8))
            using (var csv = new CsvReader(reader, CultureInfo.InvariantCulture))
            {
                await csv.ReadAsync();
                csv.ReadHeader();
                rowIndex++;  // Header row

                while (await csv.ReadAsync())
                {
                    var rowIdentifier = csv.GetField(unitMappingConfig.RowIdentifierField);

                    // When we see a unit definition row, extract and update the current unit number
                    if (rowIdentifier == unitMappingConfig.RowIdentifierValue)
                    {
                        var unitNumberStr = csv.GetField(unitMappingConfig.UnitNumberField);
                        currentUnitNumber = ParseInt(unitNumberStr);
                    }

                    // Map this row index to the current unit number
                    if (currentUnitNumber > 0)
                    {
                        unitMapping[rowIndex] = currentUnitNumber;
                    }

                    rowIndex++;
                }
            }

            return unitMapping.Count > 0 ? unitMapping : null;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error building unit mapping: {ex.Message}");
            return null;
        }
    }
}

