namespace MasonicCalendar.Core.Services.Renderers.SectionRenderers;

using System.Globalization;
using CsvHelper;
using MasonicCalendar.Core.Domain;
using MasonicCalendar.Core.Loaders;
using Scriban;
using System.Text;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

/// <summary>
/// Renders membership statistics sections (displays membership change data from CSV as a single table).
/// </summary>
public class MembershipStatisticsSectionRenderer(string templateRoot, SchemaDataLoader? dataLoader, bool debugMode, TablePagination? tablePagination = null)
    : SectionRenderer(templateRoot, dataLoader, debugMode)
{
    private readonly TablePagination? _tablePagination = tablePagination;
    /// <summary>
    /// Represents a single row of membership statistics.
    /// </summary>
    private class MemberStatRow
    {
        public string? LodgeCode { get; set; }
        public string? SuperShortName { get; set; }
        public int MembershipsStart { get; set; }
        public int MembershipsEnd { get; set; }
        public int Difference { get; set; }
        public int Resigned { get; set; }
        public int Honorary { get; set; }
        public int Cessation { get; set; }
        public int Exclusion { get; set; }
        public int Deceased { get; set; }
        public int InitiateExaltee { get; set; }
        public int Joiner { get; set; }
        public int Rejoiner { get; set; }
    }

    public override async Task RenderAsync(
        SectionConfig section,
        int sectionIndex,
        List<SectionConfig> allSections,
        string masterTemplateKey,
        List<SchemaUnit> units,
        StringBuilder output)
    {
        if (string.IsNullOrWhiteSpace(section.Template))
            return;

        var template = LoadTemplate(section.Template);
        if (template == null)
            return;

        // Load membership statistics from CSV via data mapping
        var memberStatsResult = await LoadMembershipStatsAsync(section.DataMapping);
        if (!memberStatsResult.Success || memberStatsResult.Data == null || memberStatsResult.Data.Count == 0)
        {
            Console.WriteLine($"      ✗ No membership statistics data found");
            return;
        }

        var memberStats = memberStatsResult.Data;
        Console.WriteLine($"      ✓ Rendering membership statistics table for {memberStats.Count} lodges");

        // Filter to exclude units not found in units CSV
        var memberStatsFiltered = memberStats.Where(r => !string.IsNullOrWhiteSpace(r.SuperShortName)).ToList();
        var excludedCount = memberStats.Count - memberStatsFiltered.Count;
        if (excludedCount > 0)
        {
            Console.WriteLine($"      ℹ Excluded {excludedCount} units not found in units CSV");
        }

        // Calculate totals from filtered data
        var totalMembershipsStart = memberStatsFiltered.Sum(r => r.MembershipsStart);
        var totalMembershipsEnd = memberStatsFiltered.Sum(r => r.MembershipsEnd);
        var totalDifference = memberStatsFiltered.Sum(r => r.Difference);
        var totalResigned = memberStatsFiltered.Sum(r => r.Resigned);
        var totalHonorary = memberStatsFiltered.Sum(r => r.Honorary);
        var totalCessation = memberStatsFiltered.Sum(r => r.Cessation);
        var totalExclusion = memberStatsFiltered.Sum(r => r.Exclusion);
        var totalDeceased = memberStatsFiltered.Sum(r => r.Deceased);
        var totalInitiateExaltee = memberStatsFiltered.Sum(r => r.InitiateExaltee);
        var totalJoiner = memberStatsFiltered.Sum(r => r.Joiner);
        var totalRejoiner = memberStatsFiltered.Sum(r => r.Rejoiner);

        // Convert to dictionary list and split for two-page layout (28 per page)
        var memberStatsDicts = memberStatsFiltered
            .Select(r => new Dictionary<string, object?>
            {
                { "lodgeCode", r.LodgeCode },
                { "superShortName", r.SuperShortName },
                { "membershipsStart", r.MembershipsStart },
                { "membershipsEnd", r.MembershipsEnd },
                { "difference", r.Difference },
                { "resigned", r.Resigned },
                { "honorary", r.Honorary },
                { "cessation", r.Cessation },
                { "exclusion", r.Exclusion },
                { "deceased", r.Deceased },
                { "initiateExaltee", r.InitiateExaltee },
                // Sum joiners and rejoiners into a single "joiner" field for display
                { "joiner", r.Joiner + r.Rejoiner }
            })
            .ToList();

        var rowsPerPage = _tablePagination?.MembershipStatisticsRowsPerPage > 0
            ? _tablePagination.MembershipStatisticsRowsPerPage
            : 25;
        var memberStatsPage1 = memberStatsDicts.Take(rowsPerPage).ToList();
        var memberStatsPage2 = memberStatsDicts.Skip(rowsPerPage).ToList();

        // Build the model for the template
        var statsModel = new Dictionary<string, object?>
        {
            { "section_title", section.SectionTitle },
            { "member_stats", memberStatsDicts },
            { "member_stats_page1", memberStatsPage1 },
            { "member_stats_page2", memberStatsPage2 },
            { "heading_spacing", _tablePagination?.MeetingsTableHeadingSpacing ?? "4px" },
            { "total_memberships_start", totalMembershipsStart },
            { "total_memberships_end", totalMembershipsEnd },
            { "total_difference", totalDifference },
            { "total_resigned", totalResigned },
            { "total_honorary", totalHonorary },
            { "total_cessation", totalCessation },
            { "total_exclusion", totalExclusion },
            { "total_deceased", totalDeceased },
            { "total_initiate_exaltee", totalInitiateExaltee },
            // Sum joiners and rejoiners into a single "joiner" field for display
            { "total_joiner", totalJoiner + totalRejoiner }
        };

        // Render the table
        var statsHtml = template.Render(statsModel);
        WrapWithPageBreakAndAnchor(output, $"section_{section.SectionId}", statsHtml, sectionIndex, section.ResetPageCounter, section.OverrideBreakBefore);
    }

    /// <summary>
    /// Load membership statistics data from CSV file specified in the data mapping YAML.
    /// Reads the data mapping file, extracts the member_stats section, and loads the CSV.
    /// Also loads units data to enrich statistics with unit names.
    /// </summary>
    private async Task<Result<List<MemberStatRow>>> LoadMembershipStatsAsync(string? dataMappingPath)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(dataMappingPath))
                return Result<List<MemberStatRow>>.Fail("No data mapping path specified in section config");

            // Construct path to the data mapping YAML file
            var documentRoot = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "document");
            documentRoot = Path.GetFullPath(documentRoot);  // Normalize the path
            var mappingFile = Path.Combine(documentRoot, dataMappingPath);

            if (!File.Exists(mappingFile))
                return Result<List<MemberStatRow>>.Fail($"Data mapping file not found: {mappingFile}");

            // Load and parse the YAML file
            var yaml = File.ReadAllText(mappingFile);
            var deserializer = new DeserializerBuilder()
                .WithNamingConvention(UnderscoredNamingConvention.Instance)
                .Build();

            var mapping = deserializer.Deserialize<DataSourceMapping>(yaml);
            if (mapping == null)
                return Result<List<MemberStatRow>>.Fail("Failed to deserialize data mapping YAML");
            
            // v1.7: Use OrderMemberStats (order_ prefix for order-level data)
            var memberStatsConfig = mapping.OrderMemberStats;
            if (memberStatsConfig == null)
                return Result<List<MemberStatRow>>.Fail("order_member_stats section not found in data mapping");

            if (string.IsNullOrWhiteSpace(memberStatsConfig.Source))
                return Result<List<MemberStatRow>>.Fail("order_member_stats.source not defined in data mapping");

            // Load the CSV file
            var dataRoot = Path.Combine(documentRoot, "data");
            var csvFile = Path.Combine(dataRoot, memberStatsConfig.Source);

            if (!File.Exists(csvFile))
                return Result<List<MemberStatRow>>.Fail($"Member stats CSV file not found: {csvFile}");

            // Load units data for enrichment (to get super_short_name)
            var unitsResult = await LoadUnitsForEnrichmentAsync(mapping, documentRoot, dataRoot);
            var unitsByNumber = unitsResult.Success && unitsResult.Data != null ? unitsResult.Data : new Dictionary<int, string>();

            var rows = new List<MemberStatRow>();

            using (var reader = new StreamReader(csvFile, Encoding.UTF8))
            using (var csv = new CsvReader(reader, CultureInfo.InvariantCulture))
            {
                await csv.ReadAsync();
                csv.ReadHeader();

                // Build field map from data mapping
                var fieldMap = BuildFieldMap(memberStatsConfig.Fields);

                while (await csv.ReadAsync())
                {
                    var lodgeCodeRaw = GetFieldValue(csv, fieldMap, "LodgeCode");
                    // Strip 'L' prefix to get unit number (e.g., "L137" → "137")
                    var unitNumber = lodgeCodeRaw?.TrimStart('L');
                    var superShortName = string.Empty;

                    // Look up super_short_name from units data
                    if (!string.IsNullOrWhiteSpace(unitNumber) && int.TryParse(unitNumber, out var unitNum))
                    {
                        unitsByNumber.TryGetValue(unitNum, out superShortName);
                    }

                    var row = new MemberStatRow
                    {
                        LodgeCode = unitNumber,  // Store numeric unit number without 'L'
                        SuperShortName = superShortName,
                        MembershipsStart = ParseInt(GetFieldValue(csv, fieldMap, "MembershipsStart")),
                        MembershipsEnd = ParseInt(GetFieldValue(csv, fieldMap, "MembershipsEnd")),
                        Difference = ParseInt(GetFieldValue(csv, fieldMap, "Difference")),
                        Resigned = ParseInt(GetFieldValue(csv, fieldMap, "Resigned")),
                        Honorary = ParseInt(GetFieldValue(csv, fieldMap, "Honorary")),
                        Cessation = ParseInt(GetFieldValue(csv, fieldMap, "Cessation")),
                        Exclusion = ParseInt(GetFieldValue(csv, fieldMap, "Exclusion")),
                        Deceased = ParseInt(GetFieldValue(csv, fieldMap, "Deceased")),
                        InitiateExaltee = ParseInt(GetFieldValue(csv, fieldMap, "InitiateExaltee")),
                        Joiner = ParseInt(GetFieldValue(csv, fieldMap, "Joiner")),
                        Rejoiner = ParseInt(GetFieldValue(csv, fieldMap, "Rejoiner"))
                    };

                    rows.Add(row);
                }
            }

            return Result<List<MemberStatRow>>.Ok(rows);
        }
        catch (Exception ex)
        {
            return Result<List<MemberStatRow>>.Fail($"Error loading membership statistics: {ex.Message}");
        }
    }

    /// <summary>
    /// Load units data and extract super_short_name for enrichment.
    /// </summary>
    private async Task<Result<Dictionary<int, string>>> LoadUnitsForEnrichmentAsync(DataSourceMapping mapping, string documentRoot, string dataRoot)
    {
        try
        {
            if (mapping.Units == null)
                return Result<Dictionary<int, string>>.Ok(new Dictionary<int, string>());

            var unitsConfig = mapping.Units;
            if (string.IsNullOrWhiteSpace(unitsConfig.Source))
                return Result<Dictionary<int, string>>.Ok(new Dictionary<int, string>());

            var csvFile = Path.Combine(dataRoot, unitsConfig.Source);
            if (!File.Exists(csvFile))
                return Result<Dictionary<int, string>>.Ok(new Dictionary<int, string>());

            var unitsByNumber = new Dictionary<int, string>();
            var fieldMap = BuildFieldMap(unitsConfig.Fields);

            using (var reader = new StreamReader(csvFile, Encoding.UTF8))
            using (var csv = new CsvReader(reader, CultureInfo.InvariantCulture))
            {
                await csv.ReadAsync();
                csv.ReadHeader();

                while (await csv.ReadAsync())
                {
                    // Check filter if present
                    if (unitsConfig.FilterField != null && unitsConfig.FilterValue != null)
                    {
                        var filterValue = csv.GetField(unitsConfig.FilterField);
                        if (filterValue != unitsConfig.FilterValue)
                            continue;
                    }

                    var unitNumberStr = GetFieldValue(csv, fieldMap, "Number");
                    var superShortName = GetFieldValue(csv, fieldMap, "SuperShortName");

                    if (!string.IsNullOrWhiteSpace(unitNumberStr) && int.TryParse(unitNumberStr, out var unitNum))
                    {
                        unitsByNumber[unitNum] = superShortName ?? string.Empty;
                    }
                }
            }

            return Result<Dictionary<int, string>>.Ok(unitsByNumber);
        }
        catch
        {
            // If units enrichment fails, return empty dict to allow stats to render anyway
            return Result<Dictionary<int, string>>.Ok(new Dictionary<int, string>());
        }
    }

    /// <summary>
    /// Build a field map from field mapping definitions.
    /// </summary>
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

    /// <summary>
    /// Get a field value from CSV using the field map.
    /// </summary>
    private string? GetFieldValue(CsvReader csv, Dictionary<string, string> fieldMap, string propertyName)
    {
        if (fieldMap.TryGetValue(propertyName, out var csvColumn))
        {
            return csv.GetField(csvColumn);
        }
        // Fallback to property name if not in map
        return csv.GetField(propertyName);
    }

    /// <summary>
    /// Parse a string to integer, returning 0 if parsing fails.
    /// </summary>
    private int ParseInt(string? value)
    {
        return int.TryParse(value?.Trim(), out var result) ? result : 0;
    }
}
