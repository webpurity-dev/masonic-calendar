namespace MasonicCalendar.Core.Services.Renderers.SectionRenderers;

using MasonicCalendar.Core.Loaders;

/// <summary>
/// Factory for creating appropriate section renderers based on section type.
/// </summary>
public class SectionRendererFactory
{
    private readonly string _templateRoot;
    private readonly SchemaDataLoader? _dataLoader;
    private readonly bool _debugMode;
    private readonly DocumentInfo? _documentInfo;
    private readonly UiPreferences? _uiPreferences;

    public SectionRendererFactory(string templateRoot, SchemaDataLoader? dataLoader = null, bool debugMode = false, DocumentInfo? documentInfo = null, UiPreferences? uiPreferences = null)
    {
        _templateRoot = templateRoot;
        _dataLoader = dataLoader;
        _debugMode = debugMode;
        _documentInfo = documentInfo;
        _uiPreferences = uiPreferences;
    }

    /// <summary>
    /// Create a renderer for the given section type.
    /// </summary>
    public SectionRenderer CreateRenderer(string? sectionType)
    {
        return sectionType?.ToLowerInvariant() switch
        {
            "toc" => new TocSectionRenderer(_templateRoot, _dataLoader, _debugMode),
            "static" => new StaticSectionRenderer(_templateRoot, _dataLoader, _debugMode, _documentInfo),
            "data-driven" => new DataDrivenSectionRenderer(_templateRoot, _dataLoader, _debugMode),
            "meetings-calendar" => new MeetingsCalendarSectionRenderer(_templateRoot, _dataLoader, _debugMode),
            "meetings-table" => new MeetingsTableSectionRenderer(_templateRoot, _dataLoader, _debugMode),
            "membership-summary" => new MembershipSummarySectionRenderer(_templateRoot, _dataLoader, _debugMode),
            "membership-statistics" => new MembershipStatisticsSectionRenderer(_templateRoot, _dataLoader, _debugMode),
            "list_officers" => new ProvincialOfficersSectionRenderer(_templateRoot, _dataLoader, _debugMode, _uiPreferences?.OrderOfficersDisplay),
            "list_executive_officers" => new ExecutiveOfficersSectionRenderer(_templateRoot, _dataLoader, _debugMode),
            "locations" => new LocationSectionRenderer(_templateRoot, _dataLoader, _debugMode),
            "succession-list" => new SuccessionListSectionRenderer(_templateRoot, _dataLoader, _debugMode),
            _ => new StaticSectionRenderer(_templateRoot, _dataLoader, _debugMode, _documentInfo)
        };
    }
}
