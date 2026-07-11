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
        // Respect section.OverrideBreakBefore: if true, skip the page break to allow natural flow
        var startPageBreak = (section.OverrideBreakBefore != true) && (sectionIndex > 0 && !precededbytargetedtoc);
        
        // Place anchor at the very start of the section, before any wrappers
        var anchorStyle = section.ResetPageCounter ? " style=\"counter-reset: page 0;\"" : "";
        output.AppendLine($"<div id=\"section_{section.SectionId}\"{anchorStyle}></div>");
        
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

        var unitIndex = 0;
        foreach (var unit in unitsForSection)
        {
            var anchorId = GenerateAnchorId(unit);
            var unitHtml = RenderUnitWithScriban(unit, template, sectionHeadings);
            
            // For the first unit in the section, respect override_break_before:
            // If true, add inline style to disable the break-before CSS rule
            var styleAttr = "";
            if (unitIndex == 0 && section.OverrideBreakBefore == true)
            {
                styleAttr = " style=\"break-before: auto;\"";
            }
            
            output.AppendLine($"<div id=\"{anchorId}\" class='unit-page'{styleAttr}>");
            output.Append(unitHtml);
            output.AppendLine("</div>");
            
            unitIndex++;
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

    private Task<Dictionary<string, string>?> LoadSectionHeadingsAsync(SectionConfig section)
    {
        if (DataLoader == null || string.IsNullOrWhiteSpace(section.DataMapping))
        {
            if (DebugMode)
                Console.WriteLine($"    [LoadSectionHeadingsAsync] Skipping: DataLoader={DataLoader != null}, DataMapping={section.DataMapping}");
            return Task.FromResult<Dictionary<string, string>?>(null);
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
                return Task.FromResult<Dictionary<string, string>?>(null);
            }

            var mapping = mappingResult.Data;
            var headings = new Dictionary<string, string>();

            // Extract override_heading from each person type section
            // v1.7: Use new property names (unit_ prefix for unit-level data)
            // NOTE: Allow null-coalescing only; allow space-only values (" ") to suppress headings
            if (mapping?.UnitPastHeads?.OverrideHeading != null)
            {
                headings["pastMasters"] = mapping.UnitPastHeads.OverrideHeading;
                if (DebugMode)
                    Console.WriteLine($"    [LoadSectionHeadings] pastMasters: {(mapping.UnitPastHeads.OverrideHeading == " " ? "(space)" : mapping.UnitPastHeads.OverrideHeading)}");
            }

            if (mapping?.UnitJoiningPastHeads?.OverrideHeading != null)
            {
                headings["joiningPastMasters"] = mapping.UnitJoiningPastHeads.OverrideHeading;
                if (DebugMode)
                    Console.WriteLine($"    [LoadSectionHeadings] joiningPastMasters: {(mapping.UnitJoiningPastHeads.OverrideHeading == " " ? "(space)" : mapping.UnitJoiningPastHeads.OverrideHeading)}");
            }

            if (mapping?.UnitJoiningPastHeads?.UnitsColumnHeading != null)
            {
                headings["joiningPastMastersUnitsColumn"] = mapping.UnitJoiningPastHeads.UnitsColumnHeading;
                if (DebugMode)
                    Console.WriteLine($"    [LoadSectionHeadings] joiningPastMastersUnitsColumn: {(mapping.UnitJoiningPastHeads.UnitsColumnHeading == " " ? "(space)" : mapping.UnitJoiningPastHeads.UnitsColumnHeading)}");
            }

            if (mapping?.UnitHonoraryMembers?.OverrideHeading != null)
            {
                headings["honoraryMembers"] = mapping.UnitHonoraryMembers.OverrideHeading;
                if (DebugMode)
                    Console.WriteLine($"    [LoadSectionHeadings] honoraryMembers: {(mapping.UnitHonoraryMembers.OverrideHeading == " " ? "(space)" : mapping.UnitHonoraryMembers.OverrideHeading)}");
            }

            // v1.9: Extract installation heading override from Units section
            if (mapping?.Units?.OverrideInstallationHeading != null)
            {
                headings["installationHeading"] = mapping.Units.OverrideInstallationHeading;
                if (DebugMode)
                    Console.WriteLine($"    [LoadSectionHeadings] installationHeading: {(mapping.Units.OverrideInstallationHeading == " " ? "(space)" : mapping.Units.OverrideInstallationHeading)}");
            }

            if (DebugMode && headings.Count == 0)
                Console.WriteLine($"    [LoadSectionHeadings] No headings found in {section.DataMapping}");

            return Task.FromResult<Dictionary<string, string>?>(headings.Count > 0 ? headings : null);
        }
        catch (Exception ex)
        {
            if (DebugMode)
                Console.WriteLine($"    [LoadSectionHeadings] Exception: {ex.Message}");
            return Task.FromResult<Dictionary<string, string>?>(null);
        }
    }
}
