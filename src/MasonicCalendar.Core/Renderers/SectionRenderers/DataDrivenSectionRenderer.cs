namespace MasonicCalendar.Core.Services.Renderers.SectionRenderers;

using MasonicCalendar.Core.Domain;
using MasonicCalendar.Core.Loaders;
using MasonicCalendar.Core.Renderers.Utilities;
using Scriban;
using System.Text;

/// <summary>
/// Renders data-driven sections (unit pages).
/// </summary>
public class DataDrivenSectionRenderer(string templateRoot, SchemaDataLoader? dataLoader, bool debugMode)
    : SectionRenderer(templateRoot, dataLoader, debugMode)
{
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

        // Check if the previous section is a TOC that targets this section
        // If so, don't add a page break since the TOC already provided one
        var previousSection = sectionIndex > 0 ? allSections[sectionIndex - 1] : null;
        var precededbytargetedtoc = previousSection?.Type?.Equals("toc", StringComparison.OrdinalIgnoreCase) == true &&
                                     previousSection?.ForSection?.Equals(section.SectionId, StringComparison.OrdinalIgnoreCase) == true;

        // Add section anchor for TOC links (with page break wrapper, unless first section or preceded by targeted TOC)
        var startPageBreak = sectionIndex > 0 && !precededbytargetedtoc;
        
        // Place anchor at the very start of the section, before any wrappers
        output.AppendLine($"<a id=\"section_{section.SectionId}\"></a>");
        
        if (startPageBreak)
        {
            output.AppendLine($"<div class='section-divider'>");
        }

        // Use the filtered units passed in (respects -unit parameter and has posNo assigned)
        var unitsForSection = units;

        if (DebugMode)
            Console.WriteLine($"  - Section '{section.SectionId}' ({section.Type}): {unitsForSection.Count} units");

        // Render each unit
        if (unitsForSection.Count > 0)
        {
            Console.WriteLine($"      ✓ Rendering {unitsForSection.Count} units");
        }

        // Load section heading overrides from data source mapping
        var sectionHeadings = await LoadSectionHeadingsAsync(section);

        foreach (var unit in unitsForSection)
        {
            var anchorId = GenerateAnchorId(unit);
            var unitHtml = RenderUnitWithScriban(unit, template, sectionHeadings);
            output.AppendLine($"<div id=\"{anchorId}\" class='unit-page'>");
            output.Append(unitHtml);
            output.AppendLine("</div>");
        }

        // Close section divider if it was opened
        if (startPageBreak)
        {
            output.AppendLine("</div>");
        }
    }

    private string RenderUnitWithScriban(SchemaUnit unit, Template template, Dictionary<string, string>? sectionHeadings = null)
    {
        var model = UnitModelBuilder.BuildModel(unit, sectionHeadings);
        return template.Render(model);
    }

    private async Task<Dictionary<string, string>?> LoadSectionHeadingsAsync(SectionConfig section)
    {
        if (DataLoader == null || string.IsNullOrWhiteSpace(section.DataMapping))
        {
            if (DebugMode)
                Console.WriteLine($"    [LoadSectionHeadingsAsync] Skipping: DataLoader={DataLoader != null}, DataMapping={section.DataMapping}");
            return null;
        }

        try
        {
            // Get document root (parent of templates folder)
            var documentRoot = Path.GetDirectoryName(TemplateRoot)?.TrimEnd(Path.DirectorySeparatorChar) 
                ?? TemplateRoot;
            
            // Load data source mapping to extract heading overrides
            var layoutLoader = new DocumentLayoutLoader(documentRoot);
            var mappingResult = layoutLoader.LoadDataSourceMapping(section.DataMapping);
            if (!mappingResult.Success)
            {
                if (DebugMode)
                    Console.WriteLine($"    [LoadSectionHeadingsAsync] Failed to load mapping: {mappingResult.Error}");
                return null;
            }

            var mapping = mappingResult.Data;
            var headings = new Dictionary<string, string>();

            // Extract override_heading from each person type section
            if (!string.IsNullOrWhiteSpace(mapping?.PastMasters?.OverrideHeading))
            {
                headings["pastMasters"] = mapping.PastMasters.OverrideHeading;
                if (DebugMode)
                    Console.WriteLine($"    [LoadSectionHeadings] pastMasters: {mapping.PastMasters.OverrideHeading}");
            }

            if (!string.IsNullOrWhiteSpace(mapping?.JoiningPastMasters?.OverrideHeading))
            {
                headings["joiningPastMasters"] = mapping.JoiningPastMasters.OverrideHeading;
                if (DebugMode)
                    Console.WriteLine($"    [LoadSectionHeadings] joiningPastMasters: {mapping.JoiningPastMasters.OverrideHeading}");
            }

            if (!string.IsNullOrWhiteSpace(mapping?.HonoraryMembers?.OverrideHeading))
            {
                headings["honoraryMembers"] = mapping.HonoraryMembers.OverrideHeading;
                if (DebugMode)
                    Console.WriteLine($"    [LoadSectionHeadings] honoraryMembers: {mapping.HonoraryMembers.OverrideHeading}");
            }

            if (DebugMode && headings.Count == 0)
                Console.WriteLine($"    [LoadSectionHeadings] No headings found in {section.DataMapping}");

            return headings.Count > 0 ? headings : null;
        }
        catch (Exception ex)
        {
            if (DebugMode)
                Console.WriteLine($"    [LoadSectionHeadings] Exception: {ex.Message}");
            return null;
        }
    }
}
