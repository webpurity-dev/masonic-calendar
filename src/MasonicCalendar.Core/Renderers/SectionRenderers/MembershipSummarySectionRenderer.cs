namespace MasonicCalendar.Core.Services.Renderers.SectionRenderers;

using MasonicCalendar.Core.Domain;
using MasonicCalendar.Core.Loaders;
using Scriban;
using System.Text;

/// <summary>
/// Renders membership summary sections (single table with all units as rows).
/// </summary>
public class MembershipSummarySectionRenderer(string templateRoot, SchemaDataLoader? dataLoader, bool debugMode)
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

        // Always load units for this section from the data source mapping
        var unitsForSection = units;
        if (DataLoader != null && !string.IsNullOrWhiteSpace(section.DataMapping))
        {
            var reloadResult = await DataLoader.LoadUnitsWithDataAsync(masterTemplateKey, section.SectionId);
            if (reloadResult.Success)
            {
                unitsForSection = reloadResult.Data ?? [];
                if (DebugMode)
                    Console.WriteLine($"  - Loaded {unitsForSection.Count} units for membership summary");
            }
        }

        if (unitsForSection.Count > 0)
        {
            Console.WriteLine($"      ✓ Rendering membership summary for {unitsForSection.Count} units");
        }

        // Build the summary table model with all units at once
        // Extract column heading overrides from section config
        var pastMastersHeading = section.ColumnHeadings?.TryGetValue("past_masters", out var heading) == true ? heading : "Past Masters";
        var joiningPmHeading = section.ColumnHeadings?.TryGetValue("joining_pm", out var joiningHeading) == true ? joiningHeading : "Joining P.M.";
        var includeOfficersAsMembers = section.IncludeOfficersAsMembers;
        
        // Calculate total and average members count (optionally including officers)
        var totalMembers = unitsForSection.Sum(u => u.Members.Count + (includeOfficersAsMembers ? u.Officers.Count : 0));
        var averageMembers = unitsForSection.Count > 0 ? Math.Round((double)totalMembers / unitsForSection.Count, 0) : 0;
        
        // Calculate average past masters count
        var totalPastMasters = unitsForSection.Sum(u => u.PastMasters.Count);
        var averagePastMasters = unitsForSection.Count > 0 ? Math.Round((double)totalPastMasters / unitsForSection.Count, 0) : 0;
        
        // Calculate total and average honorary members count
        var totalHonoraryMembers = unitsForSection.Sum(u => u.HonoraryMembers.Count);
        var averageHonoraryMembers = unitsForSection.Count > 0 ? Math.Round((double)totalHonoraryMembers / unitsForSection.Count, 0) : 0;
        
        var summaryModel = new Dictionary<string, object?>
        {
            { "section_title", section.SectionTitle },
            { "columnHeadings", new Dictionary<string, object?>
            {
                { "pastMasters", pastMastersHeading },
                { "joiningPm", joiningPmHeading }
            }},
            { "averageMembers", averageMembers },
            { "averagePastMasters", averagePastMasters },
            { "averageHonoraryMembers", averageHonoraryMembers },
            { "totalMembers", totalMembers },
            { "totalPastMasters", totalPastMasters },
            { "totalHonoraryMembers", totalHonoraryMembers },
            { "totalUnits", unitsForSection.Count },
            { "units", unitsForSection
                .Select(u => new Dictionary<string, object?>
                {
                    { "name", u.Name },
                    { "superShortName", u.SuperShortName },
                    { "shortName", u.ShortName },
                    { "number", u.Number },
                    { "pastMastersCount", u.PastMasters.Count },
                    { "joiningPastMastersCount", u.JoinPastMasters.Count },
                    { "membersCount", u.Members.Count + (includeOfficersAsMembers ? u.Officers.Count : 0) },
                    { "honoraryMembersCount", u.HonoraryMembers.Count }
                })
                .ToList()
            }
        };

        // Render the entire table once with all units
        var summaryHtml = template.Render(summaryModel);
        WrapWithPageBreakAndAnchor(output, $"section_{section.SectionId}", summaryHtml, sectionIndex, section.ResetPageCounter, section.OverrideBreakBefore);
    }
}
