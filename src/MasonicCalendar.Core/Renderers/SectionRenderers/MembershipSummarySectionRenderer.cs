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

        // If a single unit is pre-filtered (e.g., -unit parameter), use it; otherwise reload all
        var unitsForSection = units;
        if (units.Count != 1 && DataLoader != null && !string.IsNullOrWhiteSpace(section.DataMapping))
        {
            var reloadResult = await DataLoader.LoadUnitsWithDataAsync(masterTemplateKey, section.SectionId);
            if (reloadResult.Success)
            {
                unitsForSection = reloadResult.Data ?? [];
                if (DebugMode)
                    Console.WriteLine($"  - Loaded {unitsForSection.Count} units for membership summary");
            }
        }
        else if (units.Count == 1 && DebugMode)
        {
            Console.WriteLine($"  - Using pre-filtered unit for membership summary: {units[0].Name}");
        }

        if (unitsForSection.Count > 0)
        {
            Console.WriteLine($"      ✓ Rendering membership summary for {unitsForSection.Count} units");
        }

        // Build the summary table model with all units at once
        // Extract column heading configuration from section config
        // Support both "override_*" (new user preference) and direct heading labels from YAML
        var pastMastersHeading = GetConfigValue(section.ColumnHeadings, "override_past_masters") 
                                 ?? GetConfigValue(section.ColumnHeadings, "past_masters")
                                 ?? "Past Masters";
        var officersHeading = GetConfigValue(section.ColumnHeadings, "override_officers")
                             ?? GetConfigValue(section.ColumnHeadings, "officers")
                             ?? "Officers";
        var honoraryHeading = GetConfigValue(section.ColumnHeadings, "override_honorary")
                             ?? GetConfigValue(section.ColumnHeadings, "honorary")
                             ?? "Honorary";
        
        var hidePastMasters = GetConfigBool(section.ColumnHeadings, "hide_past_masters", false);
        var hideOfficers = GetConfigBool(section.ColumnHeadings, "hide_officers", false);
        var hideHonorary = GetConfigBool(section.ColumnHeadings, "hide_honorary", false);
        
        var includeOfficersAsMembers = section.IncludeOfficersAsMembers;
        
        // Calculate total and average members count (optionally including officers)
        var totalMembers = unitsForSection.Sum(u => u.Members.Count + (includeOfficersAsMembers ? CountActiveOfficers(u) : 0));
        var averageMembers = unitsForSection.Count > 0 ? Math.Round((double)totalMembers / unitsForSection.Count, 0) : 0;
        
        // Calculate average past masters count
        var totalPastMasters = unitsForSection.Sum(u => u.PastMasters.Count);
        var averagePastMasters = unitsForSection.Count > 0 ? Math.Round((double)totalPastMasters / unitsForSection.Count, 0) : 0;
        
        // Calculate total and average officers count (excluding vacant/not appointed)
        var totalOfficers = unitsForSection.Sum(u => CountActiveOfficers(u));
        var averageOfficers = unitsForSection.Count > 0 ? Math.Round((double)totalOfficers / unitsForSection.Count, 0) : 0;
        
        // Calculate total and average honorary members count
        var totalHonoraryMembers = unitsForSection.Sum(u => u.HonoraryMembers.Count);
        var averageHonoraryMembers = unitsForSection.Count > 0 ? Math.Round((double)totalHonoraryMembers / unitsForSection.Count, 0) : 0;
        
        var summaryModel = new Dictionary<string, object?>
        {
            { "section_title", section.SectionTitle },
            { "columnHeadings", new Dictionary<string, object?>
            {
                { "pastMasters", pastMastersHeading },
                { "officers", officersHeading },
                { "honorary", honoraryHeading },
                { "hidePastMasters", hidePastMasters },
                { "hideOfficers", hideOfficers },
                { "hideHonorary", hideHonorary }
            }},
            { "averageMembers", averageMembers },
            { "averagePastMasters", averagePastMasters },
            { "averageOfficers", averageOfficers },
            { "averageHonoraryMembers", averageHonoraryMembers },
            { "totalMembers", totalMembers },
            { "totalPastMasters", totalPastMasters },
            { "totalOfficers", totalOfficers },
            { "totalHonoraryMembers", totalHonoraryMembers },
            { "totalUnits", unitsForSection.Count },
            { "units", unitsForSection
                .Select(u => new Dictionary<string, object?>
                {
                    { "name", u.Name },
                    { "superShortName", u.SuperShortName },
                    { "shortName", u.ShortName },
                    { "number", u.Number },
                    { "unitPostfixDisplay", u.UnitPostfix ?? u.Number.ToString() },
                    { "pastMastersCount", u.PastMasters.Count },
                    { "officersCount", CountActiveOfficers(u) },
                    { "membersCount", u.Members.Count + (includeOfficersAsMembers ? CountActiveOfficers(u) : 0) },
                    { "honoraryMembersCount", u.HonoraryMembers.Count }
                })
                .ToList()
            }
        };

        // Render the entire table once with all units
        var summaryHtml = template.Render(summaryModel);
        WrapWithPageBreakAndAnchor(output, $"section_{section.SectionId}", summaryHtml, sectionIndex, section.ResetPageCounter, section.OverrideBreakBefore);
    }

    /// <summary>
    /// Extract a string value from column headings config, returning null if not found.
    /// </summary>
    private static string? GetConfigValue(Dictionary<string, string>? columnHeadings, string key)
    {
        if (columnHeadings == null || !columnHeadings.ContainsKey(key))
            return null;
        
        var value = columnHeadings[key];
        return !string.IsNullOrWhiteSpace(value) ? value : null;
    }

    /// <summary>
    /// Extract a boolean value from column headings config with a default fallback.
    /// </summary>
    private static bool GetConfigBool(Dictionary<string, string>? columnHeadings, string key, bool defaultValue)
    {
        if (columnHeadings == null || !columnHeadings.ContainsKey(key))
            return defaultValue;
        
        var value = columnHeadings[key];
        if (bool.TryParse(value, out var parsedBool))
            return parsedBool;
        
        return defaultValue;
    }

    /// <summary>
    /// Count active officers in a unit, excluding vacant/not appointed entries.
    /// An officer is considered vacant if their name is empty/whitespace or equals "Vacant".
    /// </summary>
    private static int CountActiveOfficers(SchemaUnit unit)
    {
        return unit.Officers.Count(o => 
            !string.IsNullOrWhiteSpace(o.Name) && 
            !o.Name.Equals("Vacant", StringComparison.OrdinalIgnoreCase));
    }
}

