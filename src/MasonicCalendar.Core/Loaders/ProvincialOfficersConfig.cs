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
    public string? Website { get; set; }
    public string? DistrictHeading { get; set; }
    public string? OfficersHeading { get; set; }
    public string? Crest { get; set; }
    public List<OfficerGroup>? Heads { get; set; }
    public List<OfficerGroup>? DeputyHeads { get; set; }
    public List<OfficerGroup>? DistrictHeads { get; set; }
}

/// <summary>
/// Represents a group of officers with a rank/title and optional contact email.
/// </summary>
public class OfficerGroup
{
    public string? Rank { get; set; }
    public string? Title { get; set; }
    public string? Name { get; set; }
    public string? Contact { get; set; }
}

/// <summary>
/// Configuration for order summary section (consolidated metadata: title, crest, website, heads, deputy_heads, district_heads).
/// Used by ExecutiveOfficersSectionRenderer and ProvincialOfficersSectionRenderer for consistent branding.
/// </summary>
public class OrderSummaryConfig
{
    public string? Title { get; set; }
    public string? Crest { get; set; }
    public string? Website { get; set; }
    public List<OfficerGroup>? Heads { get; set; }
    public List<OfficerGroup>? DeputyHeads { get; set; }
    public List<OfficerGroup>? DistrictHeads { get; set; }
}
