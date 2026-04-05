namespace MasonicCalendar.Core.Services.Renderers.SectionRenderers;

using MasonicCalendar.Core.Domain;
using MasonicCalendar.Core.Loaders;
using System.Text;

/// <summary>
/// Renders static template sections (cover, foreword, etc.).
/// </summary>
public class StaticSectionRenderer(string templateRoot, SchemaDataLoader? dataLoader, bool debugMode)
    : SectionRenderer(templateRoot, dataLoader, debugMode)
{
    public override Task RenderAsync(
        SectionConfig section,
        int sectionIndex,
        List<SectionConfig> allSections,
        string masterTemplateKey,
        List<SchemaUnit> units,
        StringBuilder output)
    {
        if (string.IsNullOrWhiteSpace(section.Template))
            return Task.CompletedTask;

        var template = LoadTemplate(section.Template);
        if (template == null)
            return Task.CompletedTask;

        // Add section anchor for TOC links
        var anchorId = $"section_{section.SectionId}";
        
        // Render static template with page break wrapper (except for first section)
        var staticModel = new Dictionary<string, object?>();
        var staticHtml = template.Render(staticModel);

        WrapWithPageBreakAndAnchor(output, anchorId, staticHtml, sectionIndex, section.ResetPageCounter);

        return Task.CompletedTask;
    }
}
