namespace MasonicCalendar.Core.Services.Renderers.SectionRenderers;

using Scriban;
using System.Text;
using MasonicCalendar.Core.Domain;
using MasonicCalendar.Core.Loaders;

/// <summary>
/// Renders location pages grouped by masonic hall.
/// Groups all units by their Hall, sorts units by Unit No within each group,
/// and renders a dedicated page for each location.
/// </summary>
public class LocationSectionRenderer(string templateRoot, SchemaDataLoader? dataLoader, bool debugMode = false) : SectionRenderer(templateRoot, dataLoader, debugMode)
{
    public override async Task RenderAsync(
        SectionConfig section,
        int sectionIndex,
        List<SectionConfig> allSections,
        string masterTemplateKey,
        List<SchemaUnit> units,
        StringBuilder output)
    {
        // Load the location template from config
        if (string.IsNullOrWhiteSpace(section.Template))
            return;
        
        var template = LoadTemplate(section.Template);
        if (template == null)
            return;
        
        // Group units by Hall and build location models
        var locationGroups = units
            .Where(u => !string.IsNullOrWhiteSpace(u.Hall) && (u.Location == null || !u.Location.Exclude))
            .GroupBy(u => u.Hall)
            .OrderBy(g => g.Key)
            .ToList();

        if (DebugMode)
            Console.WriteLine($"  - Section '{section.SectionId}' ({section.Type}): {locationGroups.Count} locations");

        // Build all location content first (like MembershipSummarySectionRenderer does)
        var contentBuilder = new StringBuilder();
        var isFirstLocation = true;
        foreach (var hallGroup in locationGroups)
        {
            var hallName = hallGroup.Key ?? "Unknown Location";
            var unit = hallGroup.First();
            
            // Use the Location object from the unit (populated by location join in SchemaDataLoader)
            var locationName = unit.Location?.Name ?? hallName;
            var locationAddress = unit.Location?.AddressLine1 ?? "";
            var what3words = unit.Location?.What3Words;
            var imagePath = unit.Location?.ImageFile != null 
                ? GenerateLocationImagePath(unit.Location.ImageFile)
                : GenerateLocationImagePath(hallName);

            var unitTypePriorities = section.UnitTypeSortPriority?
                .Select((unitType, index) => new { UnitType = unitType.Trim(), Priority = index })
                .Where(item => !string.IsNullOrWhiteSpace(item.UnitType))
                .GroupBy(item => item.UnitType, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(group => group.Key, group => group.First().Priority, StringComparer.OrdinalIgnoreCase)
                ?? new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            var fallbackPriority = section.UnitTypeSortPriority?.Count ?? 0;
            
            // Sort configured unit-type groups first, then sort each group by number.
            var sortedUnits = hallGroup
                .OrderBy(u => GetUnitTypePriority(u.UnitType, unitTypePriorities, fallbackPriority))
                .ThenBy(u => u.Number)
                .Select(u => new Dictionary<string, object?>
                {
                    { "super_short_name", u.SuperShortName ?? u.ShortName ?? u.Name },
                    { "unit_no", u.Number },
                    { "unit_postfix", u.UnitPostfix },  // Don't fall back to number; let template decide
                    { "hide_unit_number", u.HideUnitNumber },
                    { "unit_type", u.UnitType ?? "Unknown" }
                })
                .ToList();

            // Split units into 2 columns for better page fit
            var unitColumns = SplitUnitsIntoColumns(sortedUnits, 2);

            var locationModel = new Dictionary<string, object?>
            {
                { "location", new Dictionary<string, object?>
                {
                    { "name", locationName },
                    { "address_line1", locationAddress },
                    { "town", unit.Location?.Town ?? (hallName != null ? ExtractTownFromHall(hallName) : "") },
                    { "what3_words", what3words },
                    { "image_file", imagePath },
                    { "parking", unit.Location?.Parking }
                }},
                { "units", sortedUnits },
                { "unitColumns", unitColumns }
            };

            var renderedHtml = template.Render(locationModel);
            
            // Wrap each location in a container with proper CSS class for page breaks
            // First location doesn't need break (handled by section-divider), subsequent ones do
            var breakClass = isFirstLocation ? "location-page" : "location-page location-page-break";
            contentBuilder.AppendLine($"<div class=\"{breakClass}\">");
            contentBuilder.AppendLine(renderedHtml);
            contentBuilder.AppendLine("</div>");
            
            isFirstLocation = false;
        }

        // Wrap content with section anchor and page break (exactly like MembershipSummarySectionRenderer)
        WrapWithPageBreakAndAnchor(output, $"section_{section.SectionId}", contentBuilder.ToString(), sectionIndex, section.ResetPageCounter, section.OverrideBreakBefore);

        await Task.CompletedTask;
    }

    /// <summary>
    /// Splits units into balanced vertical columns for side-by-side rendering.
    /// </summary>
    private static List<List<Dictionary<string, object?>>> SplitUnitsIntoColumns(
        List<Dictionary<string, object?>> units, int numColumns)
    {
        var columns = Enumerable.Range(0, numColumns)
            .Select(_ => new List<Dictionary<string, object?>>())
            .ToList();

        if (units.Count == 0)
            return columns;

        var colSize = (int)Math.Ceiling(units.Count / (double)numColumns);

        for (int i = 0; i < units.Count; i++)
        {
            var columnIndex = i / colSize;
            if (columnIndex >= numColumns)
                columnIndex = numColumns - 1;
            columns[columnIndex].Add(units[i]);
        }

        return columns;
    }

    private static int GetUnitTypePriority(
        string? unitType,
        IReadOnlyDictionary<string, int> unitTypePriorities,
        int fallbackPriority)
    {
        return !string.IsNullOrWhiteSpace(unitType) && unitTypePriorities.TryGetValue(unitType, out var priority)
            ? priority
            : fallbackPriority;
    }

    /// <summary>
    /// Generates the relative path to a location image file.
    /// Converts hall name to lowercase with underscores and appends .png extension.
    /// Path is relative from the output HTML file location.
    /// E.g., "Lyme Regis" → "../images/locations/lyme_regis.png"
    /// </summary>
    private static string GenerateLocationImagePath(string? hallName)
    {
        if (string.IsNullOrWhiteSpace(hallName))
            return string.Empty;
        
        // Convert to lowercase, replace spaces with underscores
        var imageName = hallName
            .ToLower()
            .Replace(" ", "_")
            .Replace("-", "_");
        
        // Path is relative from output directory to document/images/locations
        // Format: ../images/locations/filename.png (matches SchemaPdfRenderer's regex pattern)
        return $"../images/locations/{imageName}.png";
    }

    /// <summary>
    /// Extracts a simplified town name from the hall name for display.
    /// E.g., "Poole Masonic Hall" → "Poole"
    /// </summary>
    private static string ExtractTownFromHall(string hallName)
    {
        if (string.IsNullOrWhiteSpace(hallName))
            return "Unknown";
            
        // Simple extraction: take the first word before "Masonic", "Freemasons", or "Hall"
        var parts = hallName.Split(new[] { " Masonic", " Freemasons", " Hall" }, StringSplitOptions.None);
        return parts[0].Trim();
    }
}

