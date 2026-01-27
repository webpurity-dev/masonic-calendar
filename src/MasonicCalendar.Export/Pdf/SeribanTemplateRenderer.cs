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
    public string RenderUnitPage(Unit unit, UnitLocation? location, List<UnitOfficer>? unitOfficers = null, List<UnitPastMaster>? pastMasters = null, List<UnitPMI>? joiningPastMasters = null, List<UnitMember>? members = null, List<UnitHonrary>? honoraryMembers = null)
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
            { "lastName", string.IsNullOrWhiteSpace(uo.LastName) ? "" : uo.LastName.Trim() },
            { "initials", string.IsNullOrWhiteSpace(uo.Initials) ? "" : uo.Initials.Trim() },
            { "order", uo.Officer?.Order ?? 0 }
        }).OrderBy(o => (int?)o["order"] ?? 999).ToList() ?? new List<Dictionary<string, object?>>();

        // Build past masters list sorted by installed year (ascending - oldest first)
        var pastMastersList = pastMasters?.Select(pm => new Dictionary<string, object?>
        {
            { "lastName", string.IsNullOrWhiteSpace(pm.LastName) ? "" : pm.LastName.Trim() },
            { "initials", string.IsNullOrWhiteSpace(pm.Initials) ? "" : pm.Initials.Trim() },
            { "installed", pm.Installed },
            { "provRank", string.IsNullOrWhiteSpace(pm.ProvRank) ? "" : pm.ProvRank.Trim() },
            { "provRankIssued", string.IsNullOrWhiteSpace(pm.ProvRankIssued) ? "" : pm.ProvRankIssued.Trim() }
        }).OrderBy(pm => pm["installed"]).ToList() ?? new List<Dictionary<string, object?>>();

        // Build joining past masters list
        var joiningPastMastersList = joiningPastMasters?.Select(jpm => new Dictionary<string, object?>
        {
            { "lastName", string.IsNullOrWhiteSpace(jpm.LastName) ? "" : jpm.LastName.Trim() },
            { "initials", string.IsNullOrWhiteSpace(jpm.Initials) ? "" : jpm.Initials.Trim() },
            { "provRank", string.IsNullOrWhiteSpace(jpm.ProvRank) ? "" : jpm.ProvRank.Trim() },
            { "provRankIssued", string.IsNullOrWhiteSpace(jpm.ProvRankIssued) ? "" : jpm.ProvRankIssued.Trim() }
        }).ToList() ?? new List<Dictionary<string, object?>>();

        // Build members list
        var membersList = members?.Select(m => new Dictionary<string, object?>
        {
            { "lastName", string.IsNullOrWhiteSpace(m.LastName) ? "" : m.LastName.Trim() },
            { "firstName", string.IsNullOrWhiteSpace(m.FirstNames) ? "" : m.FirstNames.Trim() },
            { "initials", string.IsNullOrWhiteSpace(m.Initials) ? "" : m.Initials.Trim() },
            { "joined", string.IsNullOrWhiteSpace(m.Joined) ? "" : m.Joined.Trim() }
        }).ToList() ?? new List<Dictionary<string, object?>>();

        // Split members into left and right columns
        var membersMidpoint = (membersList.Count + 1) / 2;
        var membersLeft = membersList.Take(membersMidpoint).ToList();
        var membersRight = membersList.Skip(membersMidpoint).ToList();

        // Build honorary members list
        var honoraryMembersList = honoraryMembers?.Select(hm => new Dictionary<string, object?>
        {
            { "lastName", string.IsNullOrWhiteSpace(hm.LastName) ? "" : hm.LastName.Trim() },
            { "initials", string.IsNullOrWhiteSpace(hm.Initials) ? "" : hm.Initials.Trim() },
            { "grandRank", string.IsNullOrWhiteSpace(hm.GrandRank) ? "" : hm.GrandRank.Trim() },
            { "provRank", string.IsNullOrWhiteSpace(hm.ProvRank) ? "" : hm.ProvRank.Trim() }
        }).ToList() ?? new List<Dictionary<string, object?>>();

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
                    { "established", unit.Established.HasValue ? FormatDateWithOrdinal(unit.Established.Value) : "" },
                    { "lastInstallationDate", unit.LastInstallationDate.HasValue ? FormatDateWithOrdinal(unit.LastInstallationDate.Value) : "" }
                }
            },
            { "location", locationDict },
            { "officers", officersList },
            { "pastMasters", pastMastersList },
            { "joiningPastMasters", joiningPastMastersList },
            { "members", membersList },
            { "membersLeft", membersLeft },
            { "membersRight", membersRight },
            { "honoraryMembers", honoraryMembersList },
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

    /// <summary>
    /// Formats a DateOnly as "d MMMM yyyy" with ordinal suffix (e.g., "1st April 1765").
    /// </summary>
    private string FormatDateWithOrdinal(DateOnly date)
    {
        var day = date.Day;
        var suffix = day switch
        {
            1 or 21 or 31 => "st",
            2 or 22 => "nd",
            3 or 23 => "rd",
            _ => "th"
        };

        return date.ToString($"d'{suffix}' MMMM yyyy");
    }
}
