namespace MasonicCalendar.Core.Loaders;

/// <summary>
/// Configuration for provincial/grand order officers section.
/// Extends basic data source with metadata like headings and officer groups.
/// </summary>
public class ProvincialOfficersConfig
{
    public string? Source { get; set; }
    public List<FieldMapping>? Fields { get; set; }
    public string? Heading1 { get; set; }
    public string? Heading2 { get; set; }
    public string? Heading3 { get; set; }
    public string? Crest { get; set; }
    public List<OfficerGroup>? Heads { get; set; }
    public List<OfficerGroup>? DeputyHeads { get; set; }
}

/// <summary>
/// Represents a group of officers with a rank/title.
/// </summary>
public class OfficerGroup
{
    public string? Rank { get; set; }
    public string? Name { get; set; }
}
