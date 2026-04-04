namespace MasonicCalendar.Core.Renderers.Utilities;

using MasonicCalendar.Core.Domain;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Builds Scriban model dictionaries from SchemaUnit objects.
/// Ensures consistent data mapping across all renderers.
/// </summary>
public static class UnitModelBuilder
{
    /// <summary>
    /// Format a DateOnly with ordinal day suffix (e.g., "21st January 2026")
    /// </summary>
    private static string FormatDateWithOrdinal(DateOnly date)
    {
        var day = date.Day;
        var ordinalSuffix = day switch
        {
            1 or 21 or 31 => "st",
            2 or 22 => "nd",
            3 or 23 => "rd",
            _ => "th"
        };
        return date.ToString($"d'{ordinalSuffix}' MMMM yyyy");
    }

    /// <summary>
    /// Build a complete Scriban model dictionary for a unit.
    /// </summary>
    public static Dictionary<string, object?> BuildModel(SchemaUnit unit, Dictionary<string, string>? sectionHeadings = null)
    {
        var model = new Dictionary<string, object?>
        {
            {
                "unit", new Dictionary<string, object?>
                {
                    { "name", TextCleaner.CleanName(unit.Name) },
                    { "number", unit.Number },
                    { "email", unit.Email },
                    { "established", unit.Established.HasValue ? FormatDateWithOrdinal(unit.Established.Value) : "" },
                    { "lastInstallationDate", unit.LastInstallationDate.HasValue ? FormatDateWithOrdinal(unit.LastInstallationDate.Value) : "" }
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
                    .Select(o => new Dictionary<string, object?>
                    {
                        { "reference", TextCleaner.CleanReference(o.Reference) },
                        { "name", TextCleaner.CleanName(o.Name) },
                        { "position", o.Position },
                        { "posNo", o.PosNo }
                    })
                    .ToList()
            },
            {
                "pastMasters", unit.PastMasters
                    .Select(pm => new Dictionary<string, object?>
                    {
                        { "reference", TextCleaner.CleanReference(pm.Reference) },
                        { "name", TextCleaner.CleanName(pm.Name) },
                        { "installed", pm.YearInstalled },
                        { "provRank", TextCleaner.CleanProvincialRank(pm.ProvincialRank) },
                        { "provRankIssued", TextCleaner.CleanDateIssued(pm.RankYear) }
                    })
                    .ToList()
            },
            {
                "joiningPastMasters", unit.JoinPastMasters
                    .Select(jpm => new Dictionary<string, object?>
                    {
                        { "reference", TextCleaner.CleanReference(jpm.Reference) },
                        { "name", TextCleaner.CleanName(jpm.Name) },
                        { "pastUnits", jpm.PastUnits },
                        { "provRank", TextCleaner.CleanProvincialRank(jpm.ProvincialRank) },
                        { "provRankIssued", TextCleaner.CleanDateIssued(jpm.RankYear) }
                    })
                    .ToList()
            },
            {
                "members", unit.Members
                    .Select(m => new Dictionary<string, object?>
                    {
                        { "reference", TextCleaner.CleanReference(m.Reference) },
                        { "name", TextCleaner.CleanName(m.Name) },
                        { "joined", m.YearInitiated },
                        { "posNo", m.PosNo }
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
                        { "reference", TextCleaner.CleanReference(hm.Reference) },
                        { "name", TextCleaner.CleanName(hm.Name) },
                        { "rank", hm.Rank }
                    })
                    .ToList()
            },
            {
                "sectionHeadings", BuildSectionHeadings(sectionHeadings)
            }
        };

        return model;
    }

    /// <summary>
    /// Build section heading overrides with defaults.
    /// </summary>
    private static Dictionary<string, object?> BuildSectionHeadings(Dictionary<string, string>? overrides = null)
    {
        var headings = new Dictionary<string, object?>
        {
            { "pastMasters", overrides?.TryGetValue("pastMasters", out var pm) == true ? pm : "Past Masters" },
            { "joiningPastMasters", overrides?.TryGetValue("joiningPastMasters", out var jpm) == true ? jpm : "Joining Past Masters" },
            { "honoraryMembers", overrides?.TryGetValue("honoraryMembers", out var hm) == true ? hm : "Honorary Members" }
        };
        return headings;
    }

    /// <summary>
    /// Split members into 3 vertical column lists for side-by-side table rendering.
    /// Avoids CSS column-count which recalculates breaks differently in PDF vs screen.
    /// E.g. 7 members → col0=[0,1,2,3], col1=[4,5], col2=[6]  (ceiling split)
    /// </summary>
    private static List<List<Dictionary<string, object?>>> SplitMembersIntoColumns(List<SchemaMember> members)
    {
        const int numColumns = 3;
        var col0 = new List<Dictionary<string, object?>>();
        var col1 = new List<Dictionary<string, object?>>();
        var col2 = new List<Dictionary<string, object?>>();

        if (members.Count == 0)
            return [col0, col1, col2];

        var colSize = (int)Math.Ceiling(members.Count / (double)numColumns);

        for (int i = 0; i < members.Count; i++)
        {
            var m = members[i];
            var dict = new Dictionary<string, object?>
            {
                { "reference", TextCleaner.CleanReference(m.Reference) },
                { "name", TextCleaner.CleanName(m.Name) },
                { "joined", m.YearInitiated },
                { "posNo", m.PosNo }
            };

            if (i < colSize) col0.Add(dict);
            else if (i < colSize * 2) col1.Add(dict);
            else col2.Add(dict);
        }

        return [col0, col1, col2];
    }
}
