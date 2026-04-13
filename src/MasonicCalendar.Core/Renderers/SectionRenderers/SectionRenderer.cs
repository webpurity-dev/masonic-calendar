namespace MasonicCalendar.Core.Services.Renderers.SectionRenderers;

using MasonicCalendar.Core.Domain;
using MasonicCalendar.Core.Loaders;
using Scriban;
using System.Text;

/// <summary>
/// Base class for rendering different types of document sections.
/// </summary>
public abstract class SectionRenderer
{
    protected readonly string TemplateRoot;
    protected readonly SchemaDataLoader? DataLoader;
    protected readonly bool DebugMode;

    protected SectionRenderer(string templateRoot, SchemaDataLoader? dataLoader, bool debugMode)
    {
        TemplateRoot = templateRoot;
        DataLoader = dataLoader;
        DebugMode = debugMode;
    }

    /// <summary>
    /// Render a section and append HTML to the output.
    /// </summary>
    public abstract Task RenderAsync(
        SectionConfig section,
        int sectionIndex,
        List<SectionConfig> allSections,
        string masterTemplateKey,
        List<SchemaUnit> units,
        StringBuilder output);

    /// <summary>
    /// Load and parse a template file.
    /// </summary>
    protected Template? LoadTemplate(string templateFileName)
    {
        if (string.IsNullOrWhiteSpace(templateFileName))
            return null;

        var templateFile = Path.Combine(TemplateRoot, templateFileName);
        if (!File.Exists(templateFile))
            return null;

        var templateContent = File.ReadAllText(templateFile);
        var template = Template.Parse(templateContent);
        
        if (template.HasErrors)
            return null;

        return template;
    }

    /// <summary>
    /// Create a clean anchor ID from a unit.
    /// </summary>
    protected string GenerateAnchorId(SchemaUnit unit)
    {
        var cleanName = System.Text.RegularExpressions.Regex.Replace(unit.Name ?? "", @"[^a-zA-Z0-9]", "_");
        var cleanType = System.Text.RegularExpressions.Regex.Replace(unit.UnitType ?? "", @"[^a-zA-Z0-9]", "_");
        return $"unit_{cleanType}_{unit.Number}_{cleanName}".ToLower();
    }

    /// <summary>
    /// Wrap content with page break CSS class (unless it's the first section).
    /// </summary>
    protected void WrapWithPageBreak(StringBuilder output, string content, int sectionIndex)
    {
        if (sectionIndex == 0)
        {
            output.AppendLine(content);
        }
        else
        {
            output.AppendLine("<div class='section-divider'>");
            output.AppendLine(content);
            output.AppendLine("</div>");
        }
    }

    /// <summary>
    /// Wrap content with page break CSS class, appending additional lines before and after (unless it's the first section).
    /// When resetPageCounter is true, emits counter-reset: page 0 so this section displays as page 1.
    /// When overrideBreakBefore is true, disables the page break, letting content flow naturally.
    /// </summary>
    protected void WrapWithPageBreakAndAnchor(StringBuilder output, string anchorId, string content, int sectionIndex, bool resetPageCounter = false, bool? overrideBreakBefore = null)
    {
        var anchorStyle = resetPageCounter ? " style=\"counter-reset: page 0;\"" : "";
        
        // Default: break before all sections except the first
        // If overrideBreakBefore is true, skip the page break (flow naturally)
        var shouldBreakBefore = (overrideBreakBefore != true) && (sectionIndex != 0);
        
        if (shouldBreakBefore)
        {
            output.AppendLine("<div class='section-divider'>");
            output.AppendLine($"<div id=\"{anchorId}\"{anchorStyle}></div>");
            output.AppendLine(content);
            output.AppendLine("</div>");
        }
        else
        {
            output.AppendLine($"<div id=\"{anchorId}\"{anchorStyle}></div>");
            output.AppendLine(content);
        }
    }

    /// <summary>
    /// Close page break wrapper if section index > 0.
    /// </summary>
    protected void ClosePageBreakIfNeeded(StringBuilder output, int sectionIndex)
    {
        if (sectionIndex > 0)
        {
            output.AppendLine("</div>");
        }
    }

    /// <summary>
    /// Filter sections for TOC display: includes data-driven, static, toc, meetings-table, meetings-calendar, and membership-summary sections,
    /// but excludes those marked with HideFromParentToc.
    /// </summary>
    public static List<SectionConfig> FilterSectionsForToc(List<SectionConfig> sections, int skipAfterIndex)
    {
        return sections
            .Skip(skipAfterIndex + 1)
            .Where(s => 
                ((s.Type?.Equals("data-driven", StringComparison.OrdinalIgnoreCase) ?? false) ||
                (s.Type?.Equals("static", StringComparison.OrdinalIgnoreCase) ?? false) ||
                (s.Type?.Equals("toc", StringComparison.OrdinalIgnoreCase) ?? false) ||
                (s.Type?.Equals("meetings-calendar", StringComparison.OrdinalIgnoreCase) ?? false) ||
                (s.Type?.Equals("meetings-table", StringComparison.OrdinalIgnoreCase) ?? false) ||
                (s.Type?.Equals("membership-summary", StringComparison.OrdinalIgnoreCase) ?? false)) &&
                !s.HideFromParentToc)
            .ToList();
    }
}
