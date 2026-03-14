namespace MasonicCalendar.Core.Services.Renderers.SectionRenderers;

using MasonicCalendar.Core.Domain;
using MasonicCalendar.Core.Loaders;
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

        // Reload units for this section
        var unitsForSection = new List<SchemaUnit>();
        if (DataLoader != null && !string.IsNullOrWhiteSpace(section.DataMapping))
        {
            var reloadResult = await DataLoader.LoadUnitsWithDataAsync(masterTemplateKey, section.SectionId);
            if (reloadResult.Success)
                unitsForSection = reloadResult.Data ?? [];
        }
        else
        {
            unitsForSection = units;
        }

        if (DebugMode)
            Console.WriteLine($"  - Section '{section.SectionId}' ({section.Type}): {unitsForSection.Count} units");

        // Render each unit
        if (unitsForSection.Count > 0)
        {
            Console.WriteLine($"      ✓ Rendering {unitsForSection.Count} units");
        }
        foreach (var unit in unitsForSection)
        {
            var anchorId = GenerateAnchorId(unit);
            var unitHtml = RenderUnitWithScriban(unit, template);
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

    private string RenderUnitWithScriban(SchemaUnit unit, Template template)
    {
        var model = new Dictionary<string, object?>
        {
            {
                "unit", new Dictionary<string, object?>
                {
                    { "name", CleanName(unit.Name) },
                    { "number", unit.Number },
                    { "email", unit.Email },
                    { "established", unit.Established?.ToString("d MMMM yyyy") ?? "" },
                    { "lastInstallationDate", unit.LastInstallationDate?.ToString("d MMMM yyyy") ?? "" }
                }
            },
            {
                "location", unit.Location != null ? new Dictionary<string, object?>
                {
                    { "name", unit.Location.Name },
                    { "addressLine1", unit.Location.AddressLine1 },
                    { "town", unit.Location.Town },
                    { "postcode", unit.Location.Postcode },
                    { "what3words", unit.Location.What3Words }
                } : null
            },
            {
                "officers", unit.Officers
                    .OrderBy(o => o.DisplayOrder ?? 999)
                    .Select(o => new Dictionary<string, object?>
                    {
                        { "reference", CleanReference(o.Reference) },
                        { "name", CleanName(o.Name) },
                        { "position", o.Position },
                        { "posNo", o.DisplayOrder ?? 0 }
                    })
                    .ToList()
            },
            {
                "pastMasters", unit.PastMasters
                    .Select(pm => new Dictionary<string, object?>
                    {
                        { "reference", CleanReference(pm.Reference) },
                        { "name", CleanName(pm.Name) },
                        { "installed", pm.YearInstalled },
                        { "provRank", CleanProvincialRank(pm.ProvincialRank) },
                        { "provRankIssued", CleanDateIssued(pm.RankYear) }
                    })
                    .ToList()
            },
            {
                "joiningPastMasters", unit.JoinPastMasters
                    .Select(jpm => new Dictionary<string, object?>
                    {
                        { "reference", CleanReference(jpm.Reference) },
                        { "name", CleanName(jpm.Name) },
                        { "provRank", CleanProvincialRank(jpm.ProvincialRank) },
                        { "provRankIssued", CleanDateIssued(jpm.RankYear) }
                    })
                    .ToList()
            },
            {
                "members", unit.Members
                    .Select(m => new Dictionary<string, object?>
                    {
                        { "reference", CleanReference(m.Reference) },
                        { "name", CleanName(m.Name) },
                        { "joined", m.YearInitiated }
                    })
                    .ToList()
            },
            {
                "memberColumns", SplitMembersIntoColumns(unit.Members)
            },
            {
                "honoraryMembers", unit.HonoraryMembers
                    .Select(hm => new Dictionary<string, object?>
                    {
                        { "reference", CleanReference(hm.Reference) },
                        { "name", CleanName(hm.Name) },
                        { "grandRank", "" },
                        { "provRank", "" }
                    })
                    .ToList()
            }
        };

        return template.Render(model);
    }

    private string CleanName(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return "";

        var cleaned = System.Text.RegularExpressions.Regex.Replace(name, @"^(The|A)\s+", "", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        cleaned = cleaned.Replace("•", " ");  // Replace bullet char with space
        cleaned = cleaned.Replace("\ufffd", " ");  // Replace Unicode Replacement Character with space
        
        // Collapse multiple spaces to single space
        while (cleaned.Contains("  "))
            cleaned = cleaned.Replace("  ", " ");
        
        return cleaned;
    }

    private string CleanProvincialRank(string? rank)
    {
        if (string.IsNullOrWhiteSpace(rank))
            return "";

        var cleaned = System.Text.RegularExpressions.Regex.Replace(rank, @"^(Past\s+)|(Provincial\s+)", "");

        return cleaned.Trim(',').Trim();
    }

    private string CleanReference(string? reference)
    {
        if (string.IsNullOrWhiteSpace(reference))
            return "";

        // Replace spaces with hyphens, remove/replace other special characters
        var cleaned = System.Text.RegularExpressions.Regex.Replace(reference, @"[^\w\-]", "-");
        // Remove consecutive hyphens
        cleaned = System.Text.RegularExpressions.Regex.Replace(cleaned, @"-+", "-");
        // Remove leading/trailing hyphens
        cleaned = cleaned.Trim('-');
        return cleaned.ToLower();
    }

    private string CleanDateIssued(string? dateStr)
    {
        if (string.IsNullOrWhiteSpace(dateStr))
            return "";

        // Remove parentheses if present
        return dateStr.Replace("(", "").Replace(")", "").Trim();
    }

    private List<List<Dictionary<string, object?>>> SplitMembersIntoColumns(List<SchemaMember> members)
    {
        // Split members into 3 roughly equal columns
        var membersData = members
            .Select(m => new Dictionary<string, object?>
            {
                { "reference", CleanReference(m.Reference) },
                { "name", CleanName(m.Name) },
                { "joined", m.YearInitiated }
            })
            .ToList();

        if (membersData.Count == 0)
            return new List<List<Dictionary<string, object?>>> { new(), new(), new() };

        var itemsPerColumn = (int)Math.Ceiling(membersData.Count / 3.0);
        var col1 = membersData.Take(itemsPerColumn).ToList();
        var col2 = membersData.Skip(itemsPerColumn).Take(itemsPerColumn).ToList();
        var col3 = membersData.Skip(itemsPerColumn * 2).ToList();

        return new List<List<Dictionary<string, object?>>> { col1, col2, col3 };
    }
}
