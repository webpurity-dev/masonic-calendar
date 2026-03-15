namespace MasonicCalendar.Core.Loaders;

using MasonicCalendar.Core.Domain;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

/// <summary>
/// Loads and parses document layout from YAML configuration files.
/// </summary>
public class DocumentLayoutLoader
{
    private readonly string _documentRoot;
    private readonly IDeserializer _deserializer;

    public DocumentLayoutLoader(string documentRoot)
    {
        _documentRoot = documentRoot;
        _deserializer = new DeserializerBuilder()
            .WithNamingConvention(UnderscoredNamingConvention.Instance)
            .Build();
    }

    /// <summary>
    /// Loads a master layout from a YAML file.
    /// </summary>
    public Result<DocumentLayout> LoadMasterLayout(string templateName)
    {
        try
        {
            var layoutFile = Path.Combine(_documentRoot, $"{templateName}.yaml");
            if (!File.Exists(layoutFile))
                return Result<DocumentLayout>.Fail($"Template file not found: {layoutFile}");

            var yaml = File.ReadAllText(layoutFile);
            var layout = _deserializer.Deserialize<DocumentLayout>(yaml);

            if (layout == null)
                return Result<DocumentLayout>.Fail("Failed to deserialize layout");

            // Load included sections and merge their properties
            if (layout.Sections?.Count > 0)
            {
                for (int i = 0; i < layout.Sections.Count; i++)
                {
                    var section = layout.Sections[i];
                    
                    if (!string.IsNullOrEmpty(section.Include))
                    {
                        var sectionFile = Path.Combine(_documentRoot, "sections", section.Include);
                        if (File.Exists(sectionFile))
                        {
                            var sectionYaml = File.ReadAllText(sectionFile);
                            var sectionConfig = _deserializer.Deserialize<SectionConfig>(sectionYaml);
                            if (sectionConfig != null)
                            {
                                // Merge properties from the included section
                                if (string.IsNullOrEmpty(section.Title))
                                    section.Title = sectionConfig.Title;
                                if (string.IsNullOrEmpty(section.SectionName))
                                    section.SectionName = sectionConfig.SectionName;
                                if (string.IsNullOrEmpty(section.UnitType))
                                    section.UnitType = sectionConfig.UnitType;
                                if (string.IsNullOrEmpty(section.DataSource))
                                    section.DataSource = sectionConfig.DataSource;
                                if (string.IsNullOrEmpty(section.Template))
                                    section.Template = sectionConfig.Template;
                                if (section.DataFilters == null)
                                    section.DataFilters = sectionConfig.DataFilters;
                                if (section.Styling == null)
                                    section.Styling = sectionConfig.Styling;
                            }
                        }
                    }
                }
            }

            return Result<DocumentLayout>.Ok(layout);
        }
        catch (Exception ex)
        {
            return Result<DocumentLayout>.Fail($"Error loading layout: {ex.Message}");
        }
    }

    /// <summary>
    /// Loads data source mappings from a YAML file (e.g., craft_data_source.yaml).
    /// </summary>
    public Result<DataSourceMapping> LoadDataSourceMapping(string mappingFileName)
    {
        try
        {
            var mappingFile = Path.Combine(_documentRoot, mappingFileName);
            if (!File.Exists(mappingFile))
                return Result<DataSourceMapping>.Fail($"Data mapping file not found: {mappingFile}");

            var yaml = File.ReadAllText(mappingFile);
            var mapping = _deserializer.Deserialize<DataSourceMapping>(yaml);

            if (mapping == null)
                return Result<DataSourceMapping>.Fail("Failed to deserialize data mapping");

            return Result<DataSourceMapping>.Ok(mapping);
        }
        catch (Exception ex)
        {
            return Result<DataSourceMapping>.Fail($"Error loading data mapping: {ex.Message}");
        }
    }
}

/// <summary>
/// Document layout configuration.
/// </summary>
public class DocumentLayout
{
    public DocumentInfo? Document { get; set; }
    public GlobalStyling? GlobalStyling { get; set; }
    public PageMargins? PageMargins { get; set; }  // Paged.js CSS @page margin configuration
    public PageNumbering? PageNumbering { get; set; }
    public Dictionary<string, object>? DataSources { get; set; }
    public Dictionary<string, object>? CsvColumnMappings { get; set; }
    public Dictionary<string, object>? TypeCoercion { get; set; }
    public Dictionary<string, object>? DefaultPageTypes { get; set; }
    public List<SectionConfig>? Sections { get; set; }
}

public class DocumentInfo
{
    public string? Title { get; set; }
    public string? Version { get; set; }
    public string? Copyright { get; set; }
    public string? Format { get; set; }
    public string? Orientation { get; set; }
    public GlobalStyling? GlobalStyling { get; set; }
}

public class GlobalStyling
{
    public FontConfig? Fonts { get; set; }
    public ColorConfig? Colors { get; set; }
    public FooterConfig? Footer { get; set; }
}

public class FontConfig
{
    public string? DefaultFamily { get; set; }
    public Dictionary<string, string>? Sizes { get; set; }
}

public class ColorConfig
{
    public string? TextPrimary { get; set; }
    public string? TextSecondary { get; set; }
    public string? Links { get; set; }
    public string? Accent { get; set; }
}

public class FooterConfig
{
    public string? FontFamily { get; set; }
    public string? FontSize { get; set; }
    public string? TextAlign { get; set; }
}

public class PageNumbering
{
    public bool Enabled { get; set; }
    public string? Position { get; set; }
    public string? Format { get; set; }
    public string? FontSize { get; set; }
}

public class SectionConfig
{
    public string? SectionId { get; set; }
    public string? Type { get; set; }
    public string? Title { get; set; }
    public string? SectionTitle { get; set; }
    public string? SectionName { get; set; }
    public string? Include { get; set; }
    public string? Template { get; set; }
    public string? DataSource { get; set; }
    public string? DataMapping { get; set; }
    public string? UnitType { get; set; }
    public string? ForSection { get; set; }
    public int? PagesPerUnit { get; set; }
    public bool HideFromParentToc { get; set; } = false;
    public Dictionary<string, object>? DataFilters { get; set; }
    public Dictionary<string, object>? Styling { get; set; }
}

public class SectionStyling
{
    public Dictionary<string, string>? Margins { get; set; }
}

/// <summary>
/// Unit mapping configuration for data sources with unit numbers embedded in rows.
/// Tracks unit identifiers (e.g., S01 rows with FN03 containing unit number) and applies them 
/// to subsequent rows until the next unit identifier is encountered.
/// Data source agnostic - works with any CSV structure.
/// </summary>
public class UnitMapping
{
    public string? Source { get; set; }                      // CSV filename to map (e.g., "CraftData.csv")
    public string? RowIdentifierField { get; set; }          // Column identifying unit definition rows (e.g., "SECTION_CODE")
    public string? RowIdentifierValue { get; set; }          // Value that marks unit definition rows (e.g., "S01")
    public string? UnitNumberField { get; set; }             // Column containing the unit number (e.g., "FN03")
    public string? UnitIdField { get; set; } = "ORG_ID";     // Interim grouping field before S01 mapping applied
}

/// <summary>
/// Data source mapping configuration loaded from YAML files like craft_data_source.yaml
/// </summary>
public class DataSourceMapping
{
    public UnitMapping? UnitMapping { get; set; }
    public DataSourceDefinition? Units { get; set; }
    public DataSourceDefinition? Officers { get; set; }
    public DataSourceDefinition? PastMasters { get; set; }
    public DataSourceDefinition? JoiningPastMasters { get; set; }
    public DataSourceDefinition? Members { get; set; }
    public DataSourceDefinition? HonoraryMembers { get; set; }
    public DataSourceDefinition? Locations { get; set; }
    public DataSourceDefinition? Meetings { get; set; }
}

/// <summary>
/// Defines a single data source (CSV file) with field mappings
/// </summary>
public class DataSourceDefinition
{
    public string? Source { get; set; }
    public string? UnitIdField { get; set; } = "Unit";  // Field name for unit ID (default: "Unit")
    public string? FilterField { get; set; }
    public string? FilterValue { get; set; }
    public string? OverrideHeading { get; set; }        // Optional custom heading for this section (e.g., "Past First Principals")
    public List<FieldMapping>? Fields { get; set; }
}

/// <summary>
/// Maps a domain model property to a CSV column
/// </summary>
public class FieldMapping
{
    public string? Name { get; set; }           // Domain property name (e.g., "Position")
    public string? CsvColumn { get; set; }      // CSV column name (e.g., "FN01")
    public string? Type { get; set; }           // Data type: string, int, date
}
