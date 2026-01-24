using MasonicCalendar.Core.Domain;

namespace MasonicCalendar.Core.Services;

/// <summary>
/// Service for generating unit information pages with location details.
/// </summary>
public class UnitPageGenerator
{
    /// <summary>
    /// Generates a page for a unit with title and location details.
    /// </summary>
    public string GenerateUnitPage(Unit unit, UnitLocation? location)
    {
        var title = unit.Name;
        var paragraph = GenerateLocationParagraph(unit, location);

        return $@"
{title}
{new string('=', title.Length)}

{paragraph}

Meeting Schedule: {unit.MeetingSummary}
Email: {unit.Email}
";
    }

    /// <summary>
    /// Generates a paragraph describing the unit's location.
    /// </summary>
    private string GenerateLocationParagraph(Unit unit, UnitLocation? location)
    {
        if (location == null)
        {
            return $"This lodge meets at {unit.Location}.";
        }

        var address = $"{location.AddressLine1}, {location.Town}, {location.Postcode}";
        return $"This lodge meets at {location.Name}, located at {address}.";
    }

    /// <summary>
    /// Generates pages for multiple units.
    /// </summary>
    public string GenerateAllUnitPages(List<Unit> units, Dictionary<Guid, UnitLocation> locations)
    {
        var pages = new List<string>();

        foreach (var unit in units.OrderBy(u => u.Number))
        {
            locations.TryGetValue(unit.LocationId, out var location);
            pages.Add(GenerateUnitPage(unit, location));
        }

        return string.Join("\n\n" + new string('-', 80) + "\n\n", pages);
    }
}
