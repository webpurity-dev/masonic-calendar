namespace MasonicCalendar.Core.Services;

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
                                if (section.Pages == null)
                                    section.Pages = sectionConfig.Pages;
                                if (section.Styling == null)
                                    section.Styling = sectionConfig.Styling;
                                if (section.UnitPageStructure == null)
                                    section.UnitPageStructure = sectionConfig.UnitPageStructure;
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
}

/// <summary>
/// Document layout configuration.
/// </summary>
public class DocumentLayout
{
    public DocumentInfo? Document { get; set; }
    public GlobalStyling? GlobalStyling { get; set; }
    public GlobalMargins? GlobalMargins { get; set; }
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
}

public class GlobalStyling
{
    public FontConfig? Fonts { get; set; }
    public ColorConfig? Colors { get; set; }
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

public class GlobalMargins
{
    public string? PageTop { get; set; }
    public string? PageBottom { get; set; }
    public string? PageLeft { get; set; }
    public string? PageRight { get; set; }
    public string? BindingEdge { get; set; }
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
    public string? SectionName { get; set; }
    public string? Include { get; set; }
    public string? Template { get; set; }
    public string? DataSource { get; set; }
    public string? UnitType { get; set; }
    public Dictionary<string, object>? UnitPageStructure { get; set; }
    public List<PageConfig>? Pages { get; set; }
    public SectionStyling? Styling { get; set; }
}

public class PageConfig
{
    public string? PageType { get; set; }
    public string? Title { get; set; }
    public string? RepeatFor { get; set; }
    public string? Template { get; set; }
    public string? DataSource { get; set; }
    public int PagesPerUnit { get; set; }
    public Dictionary<string, object>? DataFilters { get; set; }
    public Dictionary<string, object>? Styling { get; set; }
}

public class SectionStyling
{
    public Dictionary<string, string>? Margins { get; set; }
}
