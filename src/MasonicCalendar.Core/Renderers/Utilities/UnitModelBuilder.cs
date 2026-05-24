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
                    { "contact", unit.Contact },
                    { "established", unit.Established.HasValue ? FormatDateWithOrdinal(unit.Established.Value) : "" },
                    { "lastInstallationDate", unit.LastInstallationDate ?? "" },
                    { "warrant", TextCleaner.EnsureTrailingPeriod(TextCleaner.CleanText(unit.Warrant)) },
                    { "meetingDates", TextCleaner.EnsureTrailingPeriod(TextCleaner.CleanText(unit.MeetingDates)) },
                    { "hall", unit.Hall },
                    { "location", TextCleaner.EnsureTrailingPeriod(unit.LocationId) },
                    { "pastMastersCount", unit.PastMasters.Count },
                    { "joiningPastMastersCount", unit.JoinPastMasters.Count },
                    { "membersCount", unit.Members.Count },
                    { "honoraryMembersCount", unit.HonoraryMembers.Count }
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
                        { "dataId", BuildDataId(o.Reference, o.MemType, o.Office) },
                        { "name", TextCleaner.CleanName(o.Name) },
                        { "position", o.Position },
                        { "posNo", o.PosNo },
                        // v1.6 fields
                        { "grand_rank", o.GrandRank },
                        { "grand_rank_date_accorded", o.GrandRankDateAccorded },
                        // v1.7 NEW
                        { "prov_rank_other_prov", o.ProvRankOtherProv },
                        { "op_date_accorded", o.OpDateAccorded },
                        { "op_date_start_year", o.OpDateStartYear },
                        { "op_date_end_year", o.OpDateEndYear },
                        { "london_rank", o.LondonRank },
                        { "london_rank_date_accorded", o.LondonRankDateAccorded }
                    })
                    .ToList()
            },
            {
                "officerColumns", SplitOfficersIntoColumns(unit.Officers)
            },
            {
                "pastMasters", unit.PastMasters
                    .Select(pm => new Dictionary<string, object?>
                    {
                        { "reference", TextCleaner.CleanReference(pm.Reference) },
                        { "dataId", BuildDataId(pm.Reference, pm.MemType, null) },
                        { "name", TextCleaner.CleanName(pm.Name) },
                        { "installed", pm.YearInstalled },
                        { "display_rank", BuildDisplayRankWithDates(pm.GrandRank, pm.GrandRankDateAccorded, pm.ProvincialRank, pm.DateRankAccorded, pm.ProvRankOtherProv, pm.OpDateStartYear, pm.LondonRank, pm.LondonRankDateAccorded) },
                        { "isGrandRank", pm.IsGrandRank }
                    })
                    .ToList()
            },
            {
                "joiningPastMasters", unit.JoinPastMasters
                    .Select(jpm => new Dictionary<string, object?>
                    {
                        { "reference", TextCleaner.CleanReference(jpm.Reference) },
                        { "dataId", BuildDataId(jpm.Reference, jpm.MemType, null) },
                        { "name", TextCleaner.CleanName(jpm.Name) },
                        { "pastUnits", jpm.PastUnits },
                        { "display_rank", BuildDisplayRankWithDates(jpm.GrandRank, jpm.GrandRankDateAccorded, jpm.ProvincialRank, jpm.DateRankAccorded, jpm.ProvRankOtherProv, jpm.OpDateStartYear, jpm.LondonRank, jpm.LondonRankDateAccorded) },
                        { "isGrandRank", jpm.IsGrandRank }
                    })
                    .ToList()
            },
            {
                "members", unit.Members
                    .Select(m => new Dictionary<string, object?>
                    {
                        { "reference", TextCleaner.CleanReference(m.Reference) },
                        { "dataId", BuildDataId(m.Reference, m.MemType, null) },
                        { "name", TextCleaner.CleanName(m.Name) },
                        { "joined", m.YearInitiated },
                        { "posNo", m.PosNo },
                        // v1.6 fields
                        { "provincial_rank", m.ProvincialRank },
                        { "date_rank_accorded", m.DateRankAccorded },
                        { "grand_rank", m.GrandRank },
                        { "grand_rank_date_accorded", m.GrandRankDateAccorded },
                        // v1.7 NEW
                        { "prov_rank_other_prov", m.ProvRankOtherProv },
                        { "op_date_accorded", m.OpDateAccorded },
                        { "op_date_start_year", m.OpDateStartYear },
                        { "op_date_end_year", m.OpDateEndYear },
                        { "london_rank", m.LondonRank },
                        { "london_rank_date_accorded", m.LondonRankDateAccorded }
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
                        { "dataId", BuildDataId(hm.Reference, hm.MemType, null) },
                        { "name", TextCleaner.CleanName(hm.Name) },
                        { "display_rank", BuildDisplayRankWithCommaSimple(hm.GrandRank, hm.ProvincialRank, hm.ProvRankOtherProv, hm.LondonRank) },
                        { "isGrandRank", hm.IsGrandRank }
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
            { "joiningPastMastersUnitsColumn", overrides?.TryGetValue("joiningPastMastersUnitsColumn", out var jpmuc) == true ? jpmuc : "Lodges" },
            { "honoraryMembers", overrides?.TryGetValue("honoraryMembers", out var hm) == true ? hm : "Honorary Members" }
        };
        return headings;
    }

    /// <summary>
    /// Split officers into 2 vertical column lists with an even ceiling split.
    /// E.g. 7 officers → left=[0..3], right=[4..6]
    /// Avoids the hardcoded posNo<=11 threshold so columns balance regardless of unit size.
    /// </summary>
    private static List<List<Dictionary<string, object?>>> SplitOfficersIntoColumns(List<SchemaOfficer> officers)
    {
        var left = new List<Dictionary<string, object?>>();
        var right = new List<Dictionary<string, object?>>();

        if (officers.Count == 0)
            return [left, right];

        var splitAt = (int)Math.Ceiling(officers.Count / 2.0);

        for (int i = 0; i < officers.Count; i++)
        {
            var o = officers[i];
            var dict = new Dictionary<string, object?>
            {
                { "reference", TextCleaner.CleanReference(o.Reference) },
                { "dataId", BuildDataId(o.Reference, o.MemType, o.Office) },
                { "name", TextCleaner.CleanName(o.Name) },
                { "position", o.Position },
                { "posNo", o.PosNo },
                // v1.6 fields
                { "grand_rank", o.GrandRank },
                { "grand_rank_date_accorded", o.GrandRankDateAccorded },
                // v1.7 NEW
                { "prov_rank_other_prov", o.ProvRankOtherProv },
                { "op_date_accorded", o.OpDateAccorded },
                { "op_date_start_year", o.OpDateStartYear },
                { "op_date_end_year", o.OpDateEndYear },
                { "london_rank", o.LondonRank },
                { "london_rank_date_accorded", o.LondonRankDateAccorded }
            };

            if (i < splitAt) left.Add(dict);
            else right.Add(dict);
        }

        return [left, right];
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
                { "dataId", BuildDataId(m.Reference, m.MemType, null) },
                { "name", TextCleaner.CleanName(m.Name) },
                { "joined", m.YearInitiated },
                { "posNo", m.PosNo },
                // v1.6 fields
                { "provincial_rank", m.ProvincialRank },
                { "date_rank_accorded", m.DateRankAccorded },
                { "grand_rank", m.GrandRank },
                { "grand_rank_date_accorded", m.GrandRankDateAccorded },
                // v1.7 NEW
                { "prov_rank_other_prov", m.ProvRankOtherProv },
                { "op_date_accorded", m.OpDateAccorded },
                { "op_date_start_year", m.OpDateStartYear },
                { "op_date_end_year", m.OpDateEndYear },
                { "london_rank", m.LondonRank },
                { "london_rank_date_accorded", m.LondonRankDateAccorded }
            };

            if (i < colSize) col0.Add(dict);
            else if (i < colSize * 2) col1.Add(dict);
            else col2.Add(dict);
        }

        return [col0, col1, col2];
    }

    /// <summary>
    /// Build a composite data-id string: Reference-MemType[-Office]
    /// Uniquely identifies every row including multi-office holders and vacant positions.
    /// Office is only appended when non-empty (officers section only).
    /// </summary>
    private static string BuildDataId(string? reference, string? memType, string? office)
    {
        var parts = new System.Text.StringBuilder();
        parts.Append(TextCleaner.CleanReference(reference) ?? "");
        if (!string.IsNullOrWhiteSpace(memType))
            parts.Append('-').Append(memType.Trim());
        if (!string.IsNullOrWhiteSpace(office))
            parts.Append('-').Append(office.Trim());
        return parts.ToString();
    }
   
    /// <summary>
    /// Build a display rank string with dates in square brackets.
    /// Format: "Rank [Year]" for each applicable rank, joined by ", "
    /// If grand_rank exists: return "grand_rank [year]"
    /// If no grand_rank: return "provincial_rank [year], prov_rank_other_prov [year], london_rank [year]" (all non-empty ranks with dates)
    /// Example: "PProvAGReg (Hants. & IoW) [2021], LGR [2020]"
    /// </summary>
    private static string? BuildDisplayRankWithDates(
        string? grandRank, int? grandRankDateAccorded,
        string? provincialRank, int? dateRankAccorded,
        string? provRankOtherProv, int? opDateAccorded,
        string? londonRank, int? londonRankDateAccorded)
    {
        // If grand rank exists, show only that (highest priority)
        if (!string.IsNullOrWhiteSpace(grandRank))
        {
            if (grandRankDateAccorded.HasValue)
                return $"{grandRank} [{grandRankDateAccorded}]";
            return grandRank;
        }

        // Otherwise, collect all applicable ranks with their dates
        var ranks = new List<string>();
        
        if (!string.IsNullOrWhiteSpace(provincialRank))
        {
            if (dateRankAccorded.HasValue)
                ranks.Add($"{provincialRank} [{dateRankAccorded}]");
            else
                ranks.Add(provincialRank);
        }
        
        if (!string.IsNullOrWhiteSpace(provRankOtherProv))
        {
            if (opDateAccorded.HasValue)
                ranks.Add($"{provRankOtherProv} [{opDateAccorded}]");
            else
                ranks.Add(provRankOtherProv);
        }
        
        if (!string.IsNullOrWhiteSpace(londonRank))
        {
            if (londonRankDateAccorded.HasValue)
                ranks.Add($"{londonRank} [{londonRankDateAccorded}]");
            else
                ranks.Add(londonRank);
        }

        return ranks.Count > 0 ? string.Join(", ", ranks) : null;
    }

    /// <summary>
    /// Build display rank string with comma prefix for honorary members (ranks only, no dates).
    /// If rank exists, returns ", Rank" format. If no rank, returns null (no trailing comma).
    /// If grand_rank exists: return ", grand_rank"
    /// If no grand_rank: return ", provincial_rank, prov_rank_other_prov, london_rank" (all non-empty ranks joined by ", ")
    /// </summary>
    private static string? BuildDisplayRankWithCommaSimple(string? grandRank, string? provincialRank, 
        string? provRankOtherProv, string? londonRank)
    {
        var displayRank = BuildDisplayRankSimple(grandRank, provincialRank, provRankOtherProv, londonRank);
        return !string.IsNullOrWhiteSpace(displayRank) ? $", {displayRank}" : null;
    }

    /// <summary>
    /// Build a comma-separated display rank string (ranks only, no dates).
    /// If grand_rank exists: return only grand_rank (highest priority)
    /// If no grand_rank: return all non-empty ranks joined by ", " (provincial, prov_rank_other_prov, london_rank)
    /// </summary>
    private static string? BuildDisplayRankSimple(string? grandRank, string? provincialRank, 
        string? provRankOtherProv, string? londonRank)
    {
        // If grand rank exists, show only that (highest priority)
        if (!string.IsNullOrWhiteSpace(grandRank))
            return grandRank;

        // Otherwise, collect all applicable ranks
        var ranks = new List<string>();
        if (!string.IsNullOrWhiteSpace(provincialRank))
            ranks.Add(provincialRank);
        if (!string.IsNullOrWhiteSpace(provRankOtherProv))
            ranks.Add(provRankOtherProv);
        if (!string.IsNullOrWhiteSpace(londonRank))
            ranks.Add(londonRank);

        return ranks.Count > 0 ? string.Join(", ", ranks) : null;
    }

    /// <summary>
    /// Build display rank string with comma prefix for past masters and joining past masters (with dates).
    /// If rank exists, returns ", Rank [Year]" format. If no rank, returns null (no trailing comma).
    /// Uses same logic as BuildDisplayRankWithDates: grand_rank only, or all other ranks with dates
    /// </summary>
    private static string? BuildDisplayRankWithComma(string? grandRank, int? grandRankDateAccorded,
        string? provincialRank, int? dateRankAccorded,
        string? provRankOtherProv, int? opDateAccorded,
        string? londonRank, int? londonRankDateAccorded)
    {
        var displayRank = BuildDisplayRankWithDates(grandRank, grandRankDateAccorded, provincialRank, dateRankAccorded, provRankOtherProv, opDateAccorded, londonRank, londonRankDateAccorded);
        return !string.IsNullOrWhiteSpace(displayRank) ? $", {displayRank}" : null;
    }
}
