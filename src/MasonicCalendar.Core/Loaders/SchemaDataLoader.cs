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
    private Dictionary<string, SchemaLocation>? _cachedLocations;
    private readonly Dictionary<string, List<SchemaUnit>> _cachedUnits = [];  // Cache units by section ID

    public async Task<Result<List<SchemaUnit>>> LoadUnitsWithDataAsync(string masterTemplateKey, string? sectionId = null)
    {
        try
        {
            // Check cache first
            string cacheKey = !string.IsNullOrWhiteSpace(sectionId) ? sectionId : "default";
            if (_cachedUnits.TryGetValue(cacheKey, out var cachedData))
            {
                Console.WriteLine($"[SchemaDataLoader] Using cached data for '{cacheKey}'");
                return Result<List<SchemaUnit>>.Ok(cachedData);
            }

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

            // Load location data from unit_locations.csv and join with units
            var locationsFile = Path.Combine(_dataRoot, "unit_locations.csv");
            var locationsResult = await LoadLocationsFromCsvAsync(locationsFile);
            
            if (locationsResult.Success && locationsResult.Data != null)
            {
                var locationsDict = locationsResult.Data;
                foreach (var unit in units)
                {
                    if (!string.IsNullOrWhiteSpace(unit.Hall) && 
                        locationsDict.TryGetValue(unit.Hall, out var location))
                    {
                        unit.Location = location;
                        unit.LocationId = location.Name;        // Keep for backward compatibility
                        unit.What3Words = location.What3Words;   // Keep for backward compatibility
                    }
                }
            }

            // Load membership data (officers, past masters, members, etc.) and attach to units
            var hermesResult = await LoadHermesDataAsync(mapping!, units);
            if (!hermesResult.Success)
                return Result<List<SchemaUnit>>.Fail(hermesResult.Error ?? "Failed to load membership data");

            // Assign column positions (posNo) for splitting officers and members across columns
            AssignColumnPositions(units);

            // Cache the result for future calls
            _cachedUnits[cacheKey] = units;

            return Result<List<SchemaUnit>>.Ok(units);
        }
        catch (Exception ex)
        {
            return Result<List<SchemaUnit>>.Fail($"Error loading data: {ex.Message}");
        }
    }

    /// <summary>
    /// Pre-load all data-driven sections at once to avoid repeated CSV loading during rendering.
    /// </summary>
    public async Task<Result<bool>> PreloadAllDataAsync(string masterTemplateKey)
    {
        try
        {
            var layoutResult = _layoutLoader.LoadMasterLayout(masterTemplateKey);
            if (!layoutResult.Success)
                return Result<bool>.Fail(layoutResult.Error ?? "Failed to load template");

            var layout = layoutResult.Data;
            if (layout?.Sections == null || layout.Sections.Count == 0)
                return Result<bool>.Ok(true);  // No sections to preload

            Console.WriteLine($"  - Pre-loading data for all sections...");
            
            var dataSections = layout.Sections
                .Where(s => (s.Type?.Equals("data-driven", StringComparison.OrdinalIgnoreCase) ?? false) ||
                           (s.Type?.Equals("membership-summary", StringComparison.OrdinalIgnoreCase) ?? false))
                .Where(s => !string.IsNullOrWhiteSpace(s.DataMapping))
                .ToList();

            foreach (var section in dataSections)
            {
                var result = await LoadUnitsWithDataAsync(masterTemplateKey, section.SectionId);
                if (!result.Success)
                {
                    Console.WriteLine($"    ⚠️  Failed to preload {section.SectionId}: {result.Error}");
                    continue;
                }
                Console.WriteLine($"    ✓ Preloaded {section.SectionId} ({result.Data?.Count ?? 0} units)");
            }

            return Result<bool>.Ok(true);
        }
        catch (Exception ex)
        {
            return Result<bool>.Fail($"Error pre-loading data: {ex.Message}");
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
                    LocationId = GetFieldValueWithComposite(csv, fieldMap, "Hall"),  // Mapped to Location column in YAML
                    LastInstallationDate = GetFieldValueWithComposite(csv, fieldMap, "LastInstallationDate"),
                    Warrant = GetFieldValueWithComposite(csv, fieldMap, "Warrant"),
                    MeetingDates = GetFieldValueWithComposite(csv, fieldMap, "MeetingDates"),
                    Hall = GetFieldValueWithComposite(csv, fieldMap, "Hall"),  // Mapped to Location column in YAML
                    What3Words = GetFieldValueWithComposite(csv, fieldMap, "What3Words"),  // Will be overwritten by location join
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
            // v1.7: Use the new property names (unit_ prefix for unit-level data)
            var officersConfig = mapping.UnitOfficers;
            var pastHeadsConfig = mapping.UnitPastHeads;
            var joiningPastConfig = mapping.UnitJoiningPastHeads;
            var membersConfig = mapping.UnitMembers;
            var honoraryConfig = mapping.UnitHonoraryMembers;

            // Load officers
            if (officersConfig != null)
            {
                await LoadPersonTypeAsync(units, officersConfig, "officer", schemaUnit =>
                {
                    return (fieldMap, csv, unitNumber) =>
                    {
                        var reference = GetFieldValue(csv, fieldMap, "Reference");
                        var name = GetFieldValue(csv, fieldMap, "Name");
                        var rawPos = GetFieldValue(csv, fieldMap, "PositionNo");
                        var positionNo = int.TryParse(rawPos, out var pn) ? (int?)pn : null;
                        var memType = csv.GetField("Mem Type")?.Trim() ?? "";
                        var office  = csv.GetField("Office")?.Trim()  ?? "";

                        // v1.6 fields
                        var grandRankStr = GetFieldValue(csv, fieldMap, "GrandRank");
                        var grandRankDateStr = GetFieldValue(csv, fieldMap, "GrandRankDateAccorded");
                        var grandRankDate = int.TryParse(grandRankDateStr?.Trim(), out var grd) ? (int?)grd : null;
                        
                        // v1.7 NEW: Other Province Rank
                        var provRankOtherProv = GetFieldValue(csv, fieldMap, "ProvRankOtherProv");
                        var opDateAccordedStr = GetFieldValue(csv, fieldMap, "OpDateAccorded");
                        var (opDateStart, opDateEnd) = ParseDateRange(opDateAccordedStr);
                        
                        // v1.7 NEW: London Rank
                        var londonRank = GetFieldValue(csv, fieldMap, "LondonRank");
                        var londonRankDateStr = GetFieldValue(csv, fieldMap, "LondonRankDateAccorded");
                        var londonRankDate = int.TryParse(londonRankDateStr?.Trim(), out var lrd) ? (int?)lrd : null;

                        schemaUnit.Officers.Add(new SchemaOfficer
                        {
                            Reference = reference,
                            MemType = memType,
                            Office = office,
                            Name = TextCleaner.CleanName(name),
                            Position = GetFieldValue(csv, fieldMap, "Position"),
                            PosNo = positionNo,
                            // v1.6
                            GrandRank = TextCleaner.CleanProvincialRank(grandRankStr),
                            GrandRankDateAccorded = grandRankDate,
                            // v1.7 NEW
                            ProvRankOtherProv = TextCleaner.CleanProvincialRank(provRankOtherProv),
                            OpDateAccorded = opDateAccordedStr,
                            OpDateStartYear = opDateStart,
                            OpDateEndYear = opDateEnd,
                            LondonRank = TextCleaner.CleanProvincialRank(londonRank),
                            LondonRankDateAccorded = londonRankDate
                        });
                    };
                });
            }

            // Load past masters
            if (pastHeadsConfig != null)
            {
                await LoadPersonTypeAsync(units, pastHeadsConfig, "past master",
                    schemaUnit => (fieldMap, csv, unitNumber) =>
                    {
                        var name = GetFieldValue(csv, fieldMap, "Name");

                        var grandRank = GetFieldValue(csv, fieldMap, "GrandRank");
                        var grandRankDateStr = GetFieldValue(csv, fieldMap, "GrandRankDateAccorded");
                        var grandRankDate = int.TryParse(grandRankDateStr?.Trim(), out var grd) ? (int?)grd : null;
                        var provRank = GetFieldValue(csv, fieldMap, "ProvincialRank");
                        var provRankDateStr = GetFieldValue(csv, fieldMap, "DateRankAccorded");
                        var provRankDate = int.TryParse(provRankDateStr?.Trim(), out var prd) ? (int?)prd : null;
                        // Prefer GrandRank, fallback to ProvincialRank
                        var displayRank = string.IsNullOrWhiteSpace(grandRank) ? provRank : grandRank;
                        var displayRankYear = string.IsNullOrWhiteSpace(grandRankDateStr) ? provRankDateStr : grandRankDateStr;
                        
                        // v1.7 NEW: Other Province Rank
                        var provRankOtherProv = GetFieldValue(csv, fieldMap, "ProvRankOtherProv");
                        var opDateAccordedStr = GetFieldValue(csv, fieldMap, "OpDateAccorded");
                        var (opDateStart, opDateEnd) = ParseDateRange(opDateAccordedStr);
                        
                        // v1.7 NEW: London Rank
                        var londonRank = GetFieldValue(csv, fieldMap, "LondonRank");
                        var londonRankDateStr = GetFieldValue(csv, fieldMap, "LondonRankDateAccorded");
                        var londonRankDate = int.TryParse(londonRankDateStr?.Trim(), out var lrd) ? (int?)lrd : null;

                        schemaUnit.PastMasters.Add(new SchemaPastMaster
                        {
                            Reference = GetFieldValue(csv, fieldMap, "Reference"),
                            MemType = csv.GetField("Mem Type")?.Trim() ?? "",
                            Name = TextCleaner.CleanName(name),
                            YearInstalled = GetFieldValue(csv, fieldMap, "YearInstalled"),
                            Rank = TextCleaner.CleanProvincialRank(displayRank),
                            RankYear = TextCleaner.CleanDateIssued(displayRankYear),
                            IsGrandRank = !string.IsNullOrWhiteSpace(grandRank),
                            // v1.6
                            ProvincialRank = TextCleaner.CleanProvincialRank(provRank),
                            DateRankAccorded = provRankDate,
                            GrandRank = TextCleaner.CleanProvincialRank(grandRank),
                            GrandRankDateAccorded = grandRankDate,
                            // v1.7 NEW
                            ProvRankOtherProv = TextCleaner.CleanProvincialRank(provRankOtherProv),
                            OpDateAccorded = opDateAccordedStr,
                            OpDateStartYear = opDateStart,
                            OpDateEndYear = opDateEnd,
                            LondonRank = TextCleaner.CleanProvincialRank(londonRank),
                            LondonRankDateAccorded = londonRankDate
                        });
                    });
            }

            // Load joining past masters
            if (joiningPastConfig != null)
            {
                await LoadPersonTypeAsync(units, joiningPastConfig, "joining past master",
                    schemaUnit => (fieldMap, csv, unitNumber) =>
                    {
                        var name = GetFieldValue(csv, fieldMap, "Name");
                        var pastUnits = TextCleaner.CleanPastUnits(GetFieldValue(csv, fieldMap, "PastUnits"));
                        
                        var grandRank = GetFieldValue(csv, fieldMap, "GrandRank");
                        var grandRankDateStr = GetFieldValue(csv, fieldMap, "GrandRankDateAccorded");
                        var grandRankDate = int.TryParse(grandRankDateStr?.Trim(), out var grd) ? (int?)grd : null;
                        var provRank = GetFieldValue(csv, fieldMap, "ProvincialRank");
                        var provRankDateStr = GetFieldValue(csv, fieldMap, "DateRankAccorded");
                        var provRankDate = int.TryParse(provRankDateStr?.Trim(), out var prd) ? (int?)prd : null;
                        // Prefer GrandRank, fallback to ProvincialRank
                        var displayRank = string.IsNullOrWhiteSpace(grandRank) ? provRank : grandRank;
                        var displayRankYear = string.IsNullOrWhiteSpace(grandRankDateStr) ? provRankDateStr : grandRankDateStr;
                        
                        // v1.7 NEW: Other Province Rank
                        var provRankOtherProv = GetFieldValue(csv, fieldMap, "ProvRankOtherProv");
                        var opDateAccordedStr = GetFieldValue(csv, fieldMap, "OpDateAccorded");
                        var (opDateStart, opDateEnd) = ParseDateRange(opDateAccordedStr);
                        
                        // v1.7 NEW: London Rank
                        var londonRank = GetFieldValue(csv, fieldMap, "LondonRank");
                        var londonRankDateStr = GetFieldValue(csv, fieldMap, "LondonRankDateAccorded");
                        var londonRankDate = int.TryParse(londonRankDateStr?.Trim(), out var lrd) ? (int?)lrd : null;

                        schemaUnit.JoinPastMasters.Add(new SchemaJoinPastMaster
                        {
                            Reference = GetFieldValue(csv, fieldMap, "Reference"),
                            MemType = csv.GetField("Mem Type")?.Trim() ?? "",
                            Name = TextCleaner.CleanName(name),
                            PastUnits = pastUnits,
                            Rank = TextCleaner.CleanProvincialRank(displayRank),
                            RankYear = TextCleaner.CleanDateIssued(displayRankYear),
                            IsGrandRank = !string.IsNullOrWhiteSpace(grandRank),
                            // v1.6
                            ProvincialRank = TextCleaner.CleanProvincialRank(provRank),
                            DateRankAccorded = provRankDate,
                            GrandRank = TextCleaner.CleanProvincialRank(grandRank),
                            GrandRankDateAccorded = grandRankDate,
                            // v1.7 NEW
                            ProvRankOtherProv = TextCleaner.CleanProvincialRank(provRankOtherProv),
                            OpDateAccorded = opDateAccordedStr,
                            OpDateStartYear = opDateStart,
                            OpDateEndYear = opDateEnd,
                            LondonRank = TextCleaner.CleanProvincialRank(londonRank),
                            LondonRankDateAccorded = londonRankDate
                        });
                    });
            }

            // Load members
            if (membersConfig != null)
            {
                int membersBefore = units.Sum(u => u.Members.Count);
                await LoadPersonTypeAsync(units, membersConfig, "member",
                    schemaUnit => (fieldMap, csv, unitNumber) =>
                    {
                        var name = GetFieldValue(csv, fieldMap, "Name");

                        // v1.6 fields
                        var provRankStr = GetFieldValue(csv, fieldMap, "ProvincialRank");
                        var provRankDateStr = GetFieldValue(csv, fieldMap, "DateRankAccorded");
                        var provRankDate = int.TryParse(provRankDateStr?.Trim(), out var prd) ? (int?)prd : null;
                        var grandRankStr = GetFieldValue(csv, fieldMap, "GrandRank");
                        var grandRankDateStr = GetFieldValue(csv, fieldMap, "GrandRankDateAccorded");
                        var grandRankDate = int.TryParse(grandRankDateStr?.Trim(), out var grd) ? (int?)grd : null;
                        
                        // v1.7 NEW: Other Province Rank
                        var provRankOtherProv = GetFieldValue(csv, fieldMap, "ProvRankOtherProv");
                        var opDateAccordedStr = GetFieldValue(csv, fieldMap, "OpDateAccorded");
                        var (opDateStart, opDateEnd) = ParseDateRange(opDateAccordedStr);
                        
                        // v1.7 NEW: London Rank
                        var londonRank = GetFieldValue(csv, fieldMap, "LondonRank");
                        var londonRankDateStr = GetFieldValue(csv, fieldMap, "LondonRankDateAccorded");
                        var londonRankDate = int.TryParse(londonRankDateStr?.Trim(), out var lrd) ? (int?)lrd : null;

                        schemaUnit.Members.Add(new SchemaMember
                        {
                            Reference = GetFieldValue(csv, fieldMap, "Reference"),
                            MemType = csv.GetField("Mem Type")?.Trim() ?? "",
                            Name = TextCleaner.CleanName(name),
                            YearInitiated = GetFieldValue(csv, fieldMap, "YearInitiated"),
                            // v1.6
                            ProvincialRank = TextCleaner.CleanProvincialRank(provRankStr),
                            DateRankAccorded = provRankDate,
                            GrandRank = TextCleaner.CleanProvincialRank(grandRankStr),
                            GrandRankDateAccorded = grandRankDate,
                            // v1.7 NEW
                            ProvRankOtherProv = TextCleaner.CleanProvincialRank(provRankOtherProv),
                            OpDateAccorded = opDateAccordedStr,
                            OpDateStartYear = opDateStart,
                            OpDateEndYear = opDateEnd,
                            LondonRank = TextCleaner.CleanProvincialRank(londonRank),
                            LondonRankDateAccorded = londonRankDate
                        });
                    });
                int membersAfter = units.Sum(u => u.Members.Count);
                Console.WriteLine($"[SchemaDataLoader Members] Before: {membersBefore}, After: {membersAfter}, Loaded: {membersAfter - membersBefore}");
            }

            // Load honorary members
            if (honoraryConfig != null)
            {
                await LoadPersonTypeAsync(units, honoraryConfig, "honorary member",
                    schemaUnit => (fieldMap, csv, unitNumber) =>
                    {
                        var reference = GetFieldValue(csv, fieldMap, "Reference");
                        var name = GetFieldValue(csv, fieldMap, "Name");
                        var grandRank = GetFieldValue(csv, fieldMap, "GrandRank");
                        var grandRankDateStr = GetFieldValue(csv, fieldMap, "GrandRankDateAccorded");
                        var grandRankDate = int.TryParse(grandRankDateStr?.Trim(), out var grd) ? (int?)grd : null;
                        var provincialRank = GetFieldValue(csv, fieldMap, "ProvincialRank");
                        var provRankDateStr = GetFieldValue(csv, fieldMap, "DateRankAccorded");
                        var provRankDate = int.TryParse(provRankDateStr?.Trim(), out var prd) ? (int?)prd : null;
                        
                        // Prefer GrandRank, fallback to ProvincialRank
                        var displayRank = string.IsNullOrWhiteSpace(grandRank) ? provincialRank : grandRank;
                        
                        // v1.7 NEW: Other Province Rank
                        var provRankOtherProv = GetFieldValue(csv, fieldMap, "ProvRankOtherProv");
                        var opDateAccordedStr = GetFieldValue(csv, fieldMap, "OpDateAccorded");
                        var (opDateStart, opDateEnd) = ParseDateRange(opDateAccordedStr);
                        
                        // v1.7 NEW: London Rank
                        var londonRank = GetFieldValue(csv, fieldMap, "LondonRank");
                        var londonRankDateStr = GetFieldValue(csv, fieldMap, "LondonRankDateAccorded");
                        var londonRankDate = int.TryParse(londonRankDateStr?.Trim(), out var lrd) ? (int?)lrd : null;

                        schemaUnit.HonoraryMembers.Add(new SchemaHonoraryMember
                        {
                            Reference = reference,
                            MemType = csv.GetField("Mem Type")?.Trim() ?? "",
                            Name = TextCleaner.CleanName(name),
                            // v1.6
                            GrandRank = TextCleaner.CleanProvincialRank(grandRank),
                            GrandRankDateAccorded = grandRankDate,
                            ProvincialRank = TextCleaner.CleanProvincialRank(provincialRank),
                            DateRankAccorded = provRankDate,
                            Rank = TextCleaner.CleanProvincialRank(displayRank),
                            IsGrandRank = !string.IsNullOrWhiteSpace(grandRank),
                            // v1.7 NEW
                            ProvRankOtherProv = TextCleaner.CleanProvincialRank(provRankOtherProv),
                            OpDateAccorded = opDateAccordedStr,
                            OpDateStartYear = opDateStart,
                            OpDateEndYear = opDateEnd,
                            LondonRank = TextCleaner.CleanProvincialRank(londonRank),
                            LondonRankDateAccorded = londonRankDate
                        });
                    });
            }

            // Deduplicate all member lists, keeping the last occurrence of each Reference
            foreach (var unit in units)
            {
                DeduplicateMemberLists(unit);
            }

            
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
    /// Parse v1.7 date range format: single year "2021" or range "1993-15" (1993-2015).
    /// Returns tuple: (startYear, endYear).
    /// Examples:
    ///   "2021" -> (2021, null)
    ///   "1993-15" -> (1993, 2015)
    ///   "1993-2015" -> (1993, 2015)
    ///   null/empty -> (null, null)
    /// </summary>
    private static (int? startYear, int? endYear) ParseDateRange(string? dateRangeStr)
    {
        if (string.IsNullOrWhiteSpace(dateRangeStr))
            return (null, null);

        var trimmed = dateRangeStr.Trim();
        
        // Check if it's a range (contains hyphen)
        if (trimmed.Contains('-'))
        {
            var parts = trimmed.Split('-');
            if (parts.Length != 2)
                return (null, null);

            var startStr = parts[0].Trim();
            var endStr = parts[1].Trim();

            if (!int.TryParse(startStr, out var startYear))
                return (null, null);

            // End year might be abbreviated (e.g., "15" for 2015)
            if (!int.TryParse(endStr, out var endYearParsed))
                return (null, null);

            int? endYear = endYearParsed;

            // If end year is 2 digits and less than start year century, it's abbreviated
            if (endStr.Length == 2 && endYearParsed < startYear % 100)
            {
                // Infer century from start year: "1993-15" -> 2015
                int century = (startYear / 100) * 100;
                endYear = century + endYearParsed;
            }

            return (startYear, endYear);
        }
        else
        {
            // Single year
            if (int.TryParse(trimmed, out var year))
                return (year, null);
        }

        return (null, null);
    }

    /// <summary>
    /// Removes duplicate entries from all member lists, keeping only the LAST occurrence of each Reference within the same role type.
    /// This ensures that if the same person appears multiple times with the same MemType, only their most recent entry is retained.
    /// However, a person can legitimately appear in multiple roles (e.g., as both Officer and Member), so those are preserved.
    /// </summary>
    private void DeduplicateMemberLists(SchemaUnit unit)
    {
        // For Officers: deduplicate by Reference + Office (a person can't hold the same office twice)
        unit.Officers = unit.Officers
            .GroupBy(o => (o.Reference, o.Office))
            .SelectMany(g => g.Skip(g.Count() - 1))
            .ToList();

        // For Past Masters: deduplicate by Reference (keep last occurrence)
        // A person appears as PMO, PMEZ, PCO etc - same person different abbreviations are different entries
        // But if the exact same reference appears twice, keep only last
        unit.PastMasters = unit.PastMasters
            .GroupBy(p => p.Reference)
            .SelectMany(g => g.Skip(g.Count() - 1))
            .OrderBy(p => ExtractFirstDate(p.YearInstalled))
            .ToList();

        // For Joining Past Masters: deduplicate by Reference (keep last occurrence)
        unit.JoinPastMasters = unit.JoinPastMasters
            .GroupBy(j => j.Reference)
            .SelectMany(g => g.Skip(g.Count() - 1))
            .ToList();

        // For Members: deduplicate by Reference (keep last occurrence)
        unit.Members = unit.Members
            .GroupBy(m => m.Reference)
            .SelectMany(g => g.Skip(g.Count() - 1))
            .OrderBy(m => ExtractFirstDate(m.YearInitiated))
            .ToList();

        // For Honorary Members: deduplicate by Reference (keep last occurrence)
        unit.HonoraryMembers = unit.HonoraryMembers
            .GroupBy(h => h.Reference)
            .SelectMany(g => g.Skip(g.Count() - 1))
            .ToList();
    }

    /// <summary>
    /// Extract the first date from a potentially comma-separated date string.
    /// Examples: "2021" -> "2021", "2021, 2022, 2023" -> "2021"
    /// </summary>
    private static string? ExtractFirstDate(string? dateString)
    {
        if (string.IsNullOrWhiteSpace(dateString))
            return null;

        // Split on comma and take the first part, trimmed
        var firstDate = dateString.Split(',')[0].Trim();
        return string.IsNullOrWhiteSpace(firstDate) ? null : firstDate;
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

    /// <summary>
    /// Loads unit_locations.csv and creates a lookup by Hall name.
    /// Returns Dict&lt;hallName, SchemaLocation&gt; for joining with units.
    /// </summary>
    private async Task<Result<Dictionary<string, SchemaLocation>>> LoadLocationsFromCsvAsync(string locationsFile)
    {
        try
        {
            // Return cached locations if already loaded
            if (_cachedLocations != null)
                return Result<Dictionary<string, SchemaLocation>>.Ok(_cachedLocations);

            var locations = new Dictionary<string, SchemaLocation>(StringComparer.OrdinalIgnoreCase);
            
            if (!File.Exists(locationsFile))
            {
                // Graceful degradation if file missing
                Console.WriteLine($"⚠️  Locations file not found: {locationsFile}");
                _cachedLocations = locations;  // Cache even if empty
                return Result<Dictionary<string, SchemaLocation>>.Ok(locations);
            }

            using var reader = new StreamReader(locationsFile, Encoding.UTF8);
            using var csv = new CsvReader(reader, CultureInfo.InvariantCulture);
            
            await csv.ReadAsync();
            csv.ReadHeader();
            
            while (await csv.ReadAsync())
            {
                var locationKey = csv.GetField("Location")?.Trim();
                if (string.IsNullOrWhiteSpace(locationKey))
                    continue;
                
                var location = new SchemaLocation
                {
                    ID = locationKey,
                    Name = csv.GetField("Name")?.Trim(),
                    AddressLine1 = csv.GetField("Address")?.Trim(),
                    What3Words = csv.GetField("What3Words")?.Trim(),
                    ImageFile = csv.GetField("ImageFile")?.Trim(),
                    Parking = csv.GetField("Parking")?.Trim(),
                    Exclude = bool.TryParse(csv.GetField("Exclude")?.Trim() ?? "false", out var exclude) && exclude
                };
                
                locations[locationKey] = location;
            }
            
            _cachedLocations = locations;  // Cache the loaded locations
            Console.WriteLine($"✓ Loaded {locations.Count} locations from unit_locations.csv");
            return Result<Dictionary<string, SchemaLocation>>.Ok(locations);
        }
        catch (Exception ex)
        {
            return Result<Dictionary<string, SchemaLocation>>.Fail(
                $"Error loading locations CSV: {ex.Message}");
        }
    }
}



