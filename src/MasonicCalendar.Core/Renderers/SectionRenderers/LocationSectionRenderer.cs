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
        // Load the location template
        var template = LoadTemplate("location-page.html");
        if (template == null)
            return;
        
        // Add section anchor for TOC links and page counter reset if needed
        var anchorStyle = section.ResetPageCounter ? " style=\"counter-reset: page 0;\"" : "";
        output.AppendLine($"<div id=\"section_{section.SectionId}\"{anchorStyle}></div>");
        
        // Group units by Hall and build location models
        var locationGroups = units
            .Where(u => !string.IsNullOrWhiteSpace(u.Hall))
            .GroupBy(u => u.Hall)
            .OrderBy(g => g.Key)
            .ToList();

        if (DebugMode)
            Console.WriteLine($"  - Section '{section.SectionId}' ({section.Type}): {locationGroups.Count} locations");

        // Render each location
        var isFirstLocation = true;
        foreach (var hallGroup in locationGroups)
        {
            var hallName = hallGroup.Key;
            
            // Get the full location address from CSV (stored in LocationId)
            var address = hallGroup.First().LocationId ?? "Address to be confirmed";
            
            // Get What3Words from the first unit at this location
            var what3words = hallGroup.First().What3Words;
            
            // Sort units by Number and build unit dicts
            var sortedUnits = hallGroup
                .OrderBy(u => u.Number)
                .Select(u => new Dictionary<string, object?>
                {
                    { "super_short_name", u.SuperShortName ?? u.ShortName ?? u.Name },
                    { "unit_no", u.Number },
                    { "unit_type", u.UnitType ?? "Unknown" }
                })
                .ToList();

            // Split units into 2 columns for better page fit
            var unitColumns = SplitUnitsIntoColumns(sortedUnits, 2);
            
            // Generate image path for location
            var imagePath = GenerateLocationImagePath(hallName);

            var locationModel = new Dictionary<string, object?>
            {
                { "hall_name", hallName },
                { "address", address },
                { "town", ExtractTownFromHall(hallName) },
                { "description", null }, // Can be extended in future
                { "location_image", imagePath },
                { "what3words", what3words },
                { "units", sortedUnits },
                { "unitColumns", unitColumns }
            };

            // Add page break before each location (except the first)
            if (!isFirstLocation)
            {
                output.AppendLine("<div style=\"page-break-before: always;\"></div>");
            }
            
            var renderedHtml = template.Render(locationModel);
            output.AppendLine(renderedHtml);
            
            isFirstLocation = false;
        }

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

    /// <summary>
    /// Generates the relative path to a location image file.
    /// Converts hall name to lowercase with underscores and appends .png extension.
    /// Path is relative from the output HTML file location.
    /// E.g., "Lyme Regis" → "../document/images/locations/lyme_regis.png"
    /// </summary>
    private static string GenerateLocationImagePath(string hallName)
    {
        if (string.IsNullOrWhiteSpace(hallName))
            return string.Empty;
        
        // Convert to lowercase, replace spaces with underscores
        var imageName = hallName
            .ToLower()
            .Replace(" ", "_")
            .Replace("-", "_");
        
        // Path is relative from output directory to document/images/locations
        return $"../document/images/locations/{imageName}.png";
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

