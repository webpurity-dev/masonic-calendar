using Scriban;
using MasonicCalendar.Core.Domain;

namespace MasonicCalendar.Export.Pdf;

/// <summary>
/// Renders unit pages using Scriban HTML templates.
/// </summary>
public class SeribanTemplateRenderer
{
    private readonly string _templatePath;
    private string? _cachedTemplate;

    public SeribanTemplateRenderer(string templatePath)
    {
        _templatePath = templatePath;
    }

    /// <summary>
    /// Renders a unit page as HTML using the Scriban template.
    /// </summary>
    public string RenderUnitPage(Unit unit, UnitLocation? location)
    {
        var template = LoadTemplate();
        
        // Create a dictionary for the model to ensure property access works correctly in Scriban
        var locationDict = location != null ? new Dictionary<string, object?>
        {
            { "name", location.Name },
            { "addressLine1", location.AddressLine1 },
            { "address_line1", location.AddressLine1 }, // Also add snake_case version
            { "town", location.Town },
            { "postcode", location.Postcode },
            { "what3Words", location.What3Words },
            { "what3_words", location.What3Words } // Also add snake_case version
        } : null;

        var model = new Dictionary<string, object?>
        {
            {
                "unit", new Dictionary<string, object?>
                {
                    { "name", unit.Name },
                    { "number", unit.Number },
                    { "email", unit.Email },
                    { "location", unit.Location },
                    { "installationMonth", unit.InstallationMonth },
                    { "meetingSummary", unit.MeetingSummary },
                    { "warrantIssued", unit.WarrantIssued?.ToString("MMM d, yyyy") ?? "" }
                }
            },
            { "location", locationDict },
            { "now", DateTime.Now.ToString("MMM d, yyyy") }
        };

        var parsed = Template.Parse(template);
        return parsed.Render(model);
    }

    /// <summary>
    /// Loads and caches the template file.
    /// </summary>
    private string LoadTemplate()
    {
        if (_cachedTemplate != null)
            return _cachedTemplate;

        if (!File.Exists(_templatePath))
            throw new FileNotFoundException($"Template file not found: {_templatePath}");

        _cachedTemplate = File.ReadAllText(_templatePath);
        return _cachedTemplate;
    }

    /// <summary>
    /// Clears the cached template (useful for testing or when template changes).
    /// </summary>
    public void ClearCache()
    {
        _cachedTemplate = null;
    }
}
