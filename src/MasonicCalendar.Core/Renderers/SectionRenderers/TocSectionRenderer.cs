namespace MasonicCalendar.Core.Services.Renderers.SectionRenderers;

using MasonicCalendar.Core.Domain;
using MasonicCalendar.Core.Loaders;
using System.Text;

/// <summary>
/// Renders table of contents sections.
/// </summary>
public class TocSectionRenderer(string templateRoot, SchemaDataLoader? dataLoader, bool debugMode)
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

        // Render table of contents with Paged.js target-counter() for automatic page numbers
        List<Dictionary<string, object?>> tocData;
        
        if (section.ForSection?.Equals("all", StringComparison.OrdinalIgnoreCase) ?? false)
        {
            // Build TOC with sections AFTER this TOC section
            tocData = BuildSectionsTocData(allSections, sectionIndex);
        }
        else if (section.ForSection?.Equals("all_units", StringComparison.OrdinalIgnoreCase) ?? false)
        {
            // Build a grouped index of every unit across all data-driven sections
            tocData = await BuildAllUnitsTocDataAsync(allSections, masterTemplateKey);

            var indexModel = new Dictionary<string, object?>
            {
                { "section_title", section.SectionTitle },
                { "toc_by_section", tocData }
            };
            var indexHtml = template.Render(indexModel);
            var indexAnchorId = $"section_{section.SectionId}";
            WrapWithPageBreakAndAnchor(output, indexAnchorId, indexHtml, sectionIndex, section.ResetPageCounter);
            return;
        }
        else if (!string.IsNullOrWhiteSpace(section.ForSection))
        {
            var unitsForToc = new List<SchemaUnit>();
            var targetSection = allSections.FirstOrDefault(s =>
                s.SectionId?.Equals(section.ForSection, StringComparison.OrdinalIgnoreCase) ?? false);

            if (DataLoader != null && targetSection != null && !string.IsNullOrWhiteSpace(targetSection.DataMapping))
            {
                var reloadResult = await DataLoader.LoadUnitsWithDataAsync(masterTemplateKey, targetSection.SectionId);
                if (reloadResult.Success)
                    unitsForToc = reloadResult.Data ?? [];
            }
            else
            {
                unitsForToc = units;
            }

            tocData = BuildTocData(unitsForToc, allSections, section.ForSection);
        }
        else
        {
            tocData = BuildTocData(units, allSections);
        }

        var tocModel = new Dictionary<string, object?>
        {
            { "section_title", section.SectionTitle },
            { "toc_by_section", tocData }
        };
        var tocHtml = template.Render(tocModel);

        var anchorId = $"section_{section.SectionId}";
        WrapWithPageBreakAndAnchor(output, anchorId, tocHtml, sectionIndex, section.ResetPageCounter);
    }

    /// <summary>
    /// Builds a grouped index of every unit from all data-driven sections that follow this TOC.
    /// Groups are separated by a non-linked bold heading row (show_group_heading = true).
    /// </summary>
    private async Task<List<Dictionary<string, object?>>> BuildAllUnitsTocDataAsync(
        List<SectionConfig> allSections,
        string masterTemplateKey)
    {
        var tocData = new List<Dictionary<string, object?>>();

        if (allSections == null || DataLoader == null)
            return tocData;

        var dataDrivenSections = allSections
            .Where(s => s.Type?.Equals("data-driven", StringComparison.OrdinalIgnoreCase) == true
                        && !string.IsNullOrWhiteSpace(s.DataMapping))
            .ToList();

        foreach (var dataSection in dataDrivenSections)
        {
            var loadResult = await DataLoader.LoadUnitsWithDataAsync(masterTemplateKey, dataSection.SectionId);
            if (!loadResult.Success || loadResult.Data == null || loadResult.Data.Count == 0)
                continue;

            var items = loadResult.Data
                .Select(u => (object?)new Dictionary<string, object?>
                {
                    { "unit_number", u.Number },
                    { "unit_name", CleanName(u.Name) },
                    { "short_name", CleanName(u.SuperShortName ?? u.ShortName ?? u.Name) },
                    { "anchor_id", GenerateAnchorId(u) }
                })
                .ToList();

            // Split into 2 vertical halves for 2-column layout
            int half = (int)Math.Ceiling(items.Count / 2.0);
            var col1 = items.Take(half).ToList();
            var col2 = items.Skip(half).ToList();

            var sectionTitle = dataSection.SectionTitle ?? dataSection.Title ?? dataSection.SectionId ?? "Unknown";

            tocData.Add(new Dictionary<string, object?>
            {
                { "section_title", sectionTitle },
                { "show_group_heading", true },
                { "items", items },
                { "col1", col1 },
                { "col2", col2 }
            });
        }

        return tocData;
    }

    /// <summary>
    /// Builds TOC data for all data-driven, static, and toc sections that come after this TOC.
    /// </summary>
    private List<Dictionary<string, object?>> BuildSectionsTocData(List<SectionConfig> sections, int tocSectionIndex)
    {
        var tocSections = new List<Dictionary<string, object?>>();

        if (sections == null)
            return tocSections;

        // Filter sections for display in TOC
        var dataDrivenAndStaticSections = FilterSectionsForToc(sections, tocSectionIndex);

        int estimatedPageNumber = tocSectionIndex + 2;

        for (int i = 0; i < dataDrivenAndStaticSections.Count; i++)
        {
            var section = dataDrivenAndStaticSections[i];
            var sectionTitle = section.SectionTitle ?? section.Title ?? section.SectionName ?? section.SectionId ?? "Unknown";

            tocSections.Add(new Dictionary<string, object?>
            {
                { "section_id", section.SectionId },
                { "section_title", sectionTitle },
                { "page_number", estimatedPageNumber },
                { "is_child", section.IsChild },
                { "items", new List<object?>() }
            });

            estimatedPageNumber++;
        }

        return tocSections;
    }

    /// <summary>
    /// Builds TOC data for all data-driven sections.
    /// </summary>
    private List<Dictionary<string, object?>> BuildTocData(List<SchemaUnit> units, List<SectionConfig>? sections)
    {
        var tocSections = new List<Dictionary<string, object?>>();

        if (sections == null)
            return tocSections;

        int currentPageNumber = 3;
        int unitIndexPerSection = 0;

        foreach (var section in sections)
        {
            if (!section.Type?.Equals("data-driven", StringComparison.OrdinalIgnoreCase) ?? true)
                continue;

            var sectionTitle = section.SectionTitle ?? section.Title ?? section.SectionName ?? section.SectionId ?? "Unknown";

            var unitsForSection = units;
            if (!string.IsNullOrWhiteSpace(section.UnitType))
            {
                unitsForSection = units.Where(u => u.UnitType == section.UnitType).ToList();
            }

            var items = new List<object?>();
            unitIndexPerSection = 0;
            int pagesPerUnit = section.PagesPerUnit ?? 1;

            foreach (var u in unitsForSection)
            {
                int pageNumber = currentPageNumber + (unitIndexPerSection * pagesPerUnit);
                
                items.Add(new Dictionary<string, object?>
                {
                    { "unit_number", u.Number },
                    { "unit_name", CleanName(u.Name) },
                    { "short_name", CleanName(u.ShortName ?? u.Name) },
                    { "anchor_id", GenerateAnchorId(u) },
                    { "page_number", pageNumber }
                });

                unitIndexPerSection++;
            }

            currentPageNumber += unitIndexPerSection * pagesPerUnit;

            tocSections.Add(new Dictionary<string, object?>
            {
                { "section_title", sectionTitle },
                { "items", items }
            });
        }

        return tocSections;
    }

    /// <summary>
    /// Builds TOC data for a specific section only.
    /// </summary>
    private List<Dictionary<string, object?>> BuildTocData(List<SchemaUnit> units, List<SectionConfig>? sections, string forSection)
    {
        var tocSections = new List<Dictionary<string, object?>>();

        if (sections == null)
            return tocSections;

        int currentPageNumber = 3;
        int unitIndexPerSection = 0;

        var targetSection = sections.FirstOrDefault(s => 
            s.SectionId?.Equals(forSection, StringComparison.OrdinalIgnoreCase) ?? false);

        if (targetSection == null || targetSection.Type?.Equals("data-driven", StringComparison.OrdinalIgnoreCase) != true)
            return tocSections;

        var sectionTitle = targetSection.SectionTitle ?? targetSection.Title ?? targetSection.SectionName ?? targetSection.SectionId ?? "Unknown";

        var unitsForSection = units;
        if (!string.IsNullOrWhiteSpace(targetSection.UnitType))
        {
            unitsForSection = units.Where(u => u.UnitType == targetSection.UnitType).ToList();
        }

        var items = new List<object?>();
        unitIndexPerSection = 0;
        int pagesPerUnit = targetSection.PagesPerUnit ?? 1;

        foreach (var u in unitsForSection)
        {
            int pageNumber = currentPageNumber + (unitIndexPerSection * pagesPerUnit);
            
            items.Add(new Dictionary<string, object?>
            {
                { "unit_number", u.Number },
                { "unit_name", CleanName(u.Name) },
                { "short_name", CleanName(u.ShortName ?? u.Name) },
                { "anchor_id", GenerateAnchorId(u) },
                { "page_number", pageNumber }
            });

            unitIndexPerSection++;
        }

        tocSections.Add(new Dictionary<string, object?>
        {
            { "section_title", sectionTitle },
            { "items", items }
        });

        return tocSections;
    }

    private string CleanName(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return "";

        var cleaned = System.Text.RegularExpressions.Regex.Replace(name, @"^(The|A)\s+", "", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        cleaned = cleaned.Replace("\u2022", " ");  // Replace bullet char with space
        cleaned = cleaned.Replace("\ufffd", " ");  // Replace Unicode Replacement Character with space
        
        // Collapse multiple spaces to single space
        while (cleaned.Contains("  "))
            cleaned = cleaned.Replace("  ", " ");
        
        return cleaned;
    }
}
