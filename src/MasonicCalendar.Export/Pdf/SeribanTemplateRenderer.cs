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
    public string RenderUnitPage(Unit unit, UnitLocation? location, List<UnitOfficer>? unitOfficers = null, List<UnitPastMaster>? pastMasters = null)
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

        // Build officers list with resolved officer names
        var officersList = unitOfficers?.Select(uo => new Dictionary<string, object?>
        {
            { "name", uo.Officer?.Name ?? "Unknown" },
            { "abbreviation", uo.Officer?.Abbreviation ?? "" },
            { "lastName", uo.LastName },
            { "initials", uo.Initials },
            { "order", uo.Officer?.Order ?? 0 }
        }).OrderBy(o => (int?)o["order"] ?? 999).ToList() ?? new List<Dictionary<string, object?>>();

        // Build past masters list sorted by installed year (ascending - oldest first)
        var pastMastersList = pastMasters?.Select(pm => new Dictionary<string, object?>
        {
            { "lastName", pm.LastName },
            { "initials", pm.Initials },
            { "installed", pm.Installed },
            { "provRank", pm.ProvRank ?? "" },
            { "provRankIssued", pm.ProvRankIssued ?? "" }
        }).OrderBy(pm => pm["installed"]).ToList() ?? new List<Dictionary<string, object?>>();

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
                    { "warrantIssued", unit.WarrantIssued?.ToString("MMM d, yyyy") ?? "" },
                    { "lastInstallationDate", unit.LastInstallationDate?.ToString("MMM d, yyyy") ?? "" }
                }
            },
            { "location", locationDict },
            { "officers", officersList },
            { "pastMasters", pastMastersList },
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
