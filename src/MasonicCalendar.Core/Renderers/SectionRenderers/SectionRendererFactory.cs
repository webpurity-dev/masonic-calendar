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

    public SectionRendererFactory(string templateRoot, SchemaDataLoader? dataLoader = null, bool debugMode = false)
    {
        _templateRoot = templateRoot;
        _dataLoader = dataLoader;
        _debugMode = debugMode;
    }

    /// <summary>
    /// Create a renderer for the given section type.
    /// </summary>
    public SectionRenderer CreateRenderer(string? sectionType)
    {
        return sectionType?.ToLowerInvariant() switch
        {
            "toc" => new TocSectionRenderer(_templateRoot, _dataLoader, _debugMode),
            "static" => new StaticSectionRenderer(_templateRoot, _dataLoader, _debugMode),
            "data-driven" => new DataDrivenSectionRenderer(_templateRoot, _dataLoader, _debugMode),
            _ => new StaticSectionRenderer(_templateRoot, _dataLoader, _debugMode)
        };
    }
}
