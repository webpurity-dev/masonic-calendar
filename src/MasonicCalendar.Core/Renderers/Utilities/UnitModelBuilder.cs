namespace MasonicCalendar.Core.Renderers.Utilities;

using MasonicCalendar.Core.Domain;
using MasonicCalendar.Core.Loaders;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;

/// <summary>
/// Builds Scriban model dictionaries from SchemaUnit objects.
/// Ensures consistent data mapping across all renderers.
/// </summary>
public static class UnitModelBuilder
{
    /// <summary>
    /// Gets or sets the configured label for vacant officer positions.
    /// Defaults to "Not appointed" if not set. Set from master YAML via Program.cs.
    /// </summary>
    public static string? ConfiguredVacantOfficerLabel { get; set; }

    /// <summary>
    /// Gets or sets the rank display configuration for past masters and honorary members.
    /// Defines priority order of ranks and when to include dates. Set from master YAML via Program.cs.
    /// </summary>
    public static RankDisplay? ConfiguredRankDisplay { get; set; }

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
        // v1.10: Check if any joining past master has PastUnits data
        var hasPastUnitsData = unit.JoinPastMasters.Any(jpm => !string.IsNullOrWhiteSpace(jpm.PastUnits));
        
        var model = new Dictionary<string, object?>
        {
            {
                "unit", new Dictionary<string, object?>
                {
                    { "name", TextCleaner.CleanName(unit.Name) },
                    { "number", unit.Number },
                    { "unitPostfixDisplay", unit.UnitPostfix ?? unit.Number.ToString() },
                    { "hideUnitNumber", unit.HideUnitNumber },
                    { "hideUnitName", unit.HideUnitName },
                    { "contact", unit.Contact },
                    { "established", unit.Established.HasValue ? FormatDateWithOrdinal(unit.Established.Value) : "" },
                    { "lastInstallationDate", unit.LastInstallationDate },
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
                    .Where(o => o.Office != "0")  // Filter out placeholder rows
                    .Select(o => new Dictionary<string, object?>
                    {
                        { "reference", TextCleaner.CleanReference(o.Reference) },
                        { "dataId", BuildDataId(o.Reference, o.MemType, o.Office) },
                        { "name", CleanOfficerName(o.Name, ConfiguredVacantOfficerLabel) },
                        { "position", TextCleaner.CleanOfficePosition(o.Position) },
                        { "posNo", o.PosNo },
                        { "isNotAppointed", IsVacantOfficer(o.Name) }
                    })
                    .ToList()
            },
            {
                "officerColumns", SplitOfficersIntoColumns(unit.Officers.Where(o => o.Office != "0").ToList())
            },
            {
                "pastMasters", unit.PastMasters
                    .Select(pm => new Dictionary<string, object?>
                    {
                        { "reference", TextCleaner.CleanReference(pm.Reference) },
                        { "dataId", BuildDataId(pm.Reference, pm.MemType, null) },
                        { "name", TextCleaner.CleanName(pm.Name) },
                        { "installed", pm.YearInstalled?.Replace(" ", "") },
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
                        { "joinedDate", jpm.JoinedDate?.Replace(" ", "") },
                        { "pastUnits", FormatJoiningUnitsDisplay(jpm.PastUnits) },
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
                        { "posNo", m.PosNo }
                    })
                    .ToList()
            },
            {
                "memberColumns", SplitMembersIntoColumns(unit.Members)
            },
            {
                "groupedMembers", BuildGroupedMembers(unit.Members)  // v1.9: Support grouped members (e.g., RC degrees)
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
                "sectionHeadings", BuildSectionHeadings(sectionHeadings, hasPastUnitsData)
            }
        };

        return model;
    }

    /// <summary>
    /// Build section heading overrides with defaults.
    /// v1.10: Added showPastUnitsColumn flag to auto-hide empty units column
    /// </summary>
    private static Dictionary<string, object?> BuildSectionHeadings(Dictionary<string, string>? overrides = null, bool hasPastUnitsData = true)
    {
        var headings = new Dictionary<string, object?>
        {
            { "members", overrides?.TryGetValue("members", out var m) == true ? m : "Members" },  // v1.9: Support override with default "Members"
            { "pastMasters", overrides?.TryGetValue("pastMasters", out var pm) == true ? pm : "Past Masters" },  // Supports null/empty/space values from override_heading
            { "joiningPastMasters", overrides?.TryGetValue("joiningPastMasters", out var jpm) == true ? jpm : "Joining Past Masters" },  // Supports null/empty/space values
            { "joiningPastMastersUnitsColumn", overrides?.TryGetValue("joiningPastMastersUnitsColumn", out var jpmuc) == true ? jpmuc : "Lodges" },  // Supports null/empty/space values
            { "honoraryMembers", overrides?.TryGetValue("honoraryMembers", out var hm) == true ? hm : "Honorary Members" },  // Supports null/empty/space values
            { "installationHeading", overrides?.TryGetValue("installationHeading", out var ih) == true ? ih : "Installation" },  // v1.9: Support override (e.g., "Enthronement" for RC)
            { "memberCaption", overrides?.TryGetValue("memberCaption", out var mc) == true ? mc : "" },  // v1.9: Optional caption under member table
            { "showPastUnitsColumn", hasPastUnitsData }  // v1.10: Hide units column if no joining past masters have past units data
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
                { "name", CleanOfficerName(o.Name, ConfiguredVacantOfficerLabel) },
                { "position", TextCleaner.CleanOfficePosition(o.Position) },
                { "posNo", o.PosNo },
                { "isNotAppointed", IsVacantOfficer(o.Name) }
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
        var columns = new List<List<Dictionary<string, object?>>> { col0, col1, col2 };

        if (members.Count == 0)
            return columns;

        // Balanced distribution: distribute members evenly across columns
        var baseSize = members.Count / numColumns;
        var remainder = members.Count % numColumns;

        int memberIndex = 0;
        for (int colIndex = 0; colIndex < numColumns; colIndex++)
        {
            // First 'remainder' columns get baseSize + 1 members
            int colCapacity = baseSize + (colIndex < remainder ? 1 : 0);

            for (int j = 0; j < colCapacity && memberIndex < members.Count; j++)
            {
                var m = members[memberIndex];
                var dict = new Dictionary<string, object?>
                {
                    { "reference", TextCleaner.CleanReference(m.Reference) },
                    { "dataId", BuildDataId(m.Reference, m.MemType, null) },
                    { "name", TextCleaner.CleanName(m.Name) },
                    { "joined", m.YearInitiated },
                    { "posNo", m.PosNo },
                    { "suffix", string.IsNullOrWhiteSpace(m.Suffix) || m.Suffix == "0" ? "" : m.Suffix }  // v1.9: Add suffix if not blank or "0"
                };

                columns[colIndex].Add(dict);
                memberIndex++;
            }
        }

        return columns;
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
    /// Uses cascading priority from configuration: priority_order defines which rank to show.
    /// If grand_rank exists and is first in priority_order: return "grand_rank [year]"
    /// Shows only the first non-empty rank in the priority order.
    /// Example: "PProvAGReg (Hants. & IoW) [2021]"
    /// Configuration loaded from master_v1.yaml ui_preferences.rank_display
    /// </summary>
    private static string? BuildDisplayRankWithDates(
        string? grandRank, int? grandRankDateAccorded,
        string? provincialRank, int? dateRankAccorded,
        string? provRankOtherProv, int? opDateAccorded,
        string? londonRank, int? londonRankDateAccorded)
    {
        // Use default priority order if config not set
        var priorityOrder = ConfiguredRankDisplay?.PriorityOrder ?? 
            new List<string> { "grand_rank", "provincial_rank", "prov_rank_other_prov", "london_rank" };
        
        var showDates = ConfiguredRankDisplay?.ShowDates?.PastMasters ?? true;

        // Map field names to (rank value, date value) tuples
        var rankMap = new Dictionary<string, (string?, int?)>
        {
            { "grand_rank", (grandRank, grandRankDateAccorded) },
            { "provincial_rank", (provincialRank, dateRankAccorded) },
            { "prov_rank_other_prov", (provRankOtherProv, opDateAccorded) },
            { "london_rank", (londonRank, londonRankDateAccorded) }
        };

        // Find the first non-empty rank in priority order
        foreach (var fieldName in priorityOrder)
        {
            if (rankMap.TryGetValue(fieldName, out var rankTuple))
            {
                var rankValue = rankTuple.Item1;
                var dateValue = rankTuple.Item2;
                
                if (!string.IsNullOrWhiteSpace(rankValue))
                {
                    if (showDates && dateValue.HasValue)
                        return $"{rankValue} [{dateValue}]";
                    return rankValue;
                }
            }
        }

        return null;
    }

    /// <summary>
    /// Build display rank string with comma prefix for honorary members (ranks only, no dates).
    /// If rank exists, returns ", Rank" format. If no rank, returns null (no trailing comma).
    /// Uses cascading priority: grand_rank > provincial_rank > prov_rank_other_prov > london_rank
    /// </summary>
    private static string? BuildDisplayRankWithCommaSimple(string? grandRank, string? provincialRank, 
        string? provRankOtherProv, string? londonRank)
    {
        var displayRank = BuildDisplayRankSimple(grandRank, provincialRank, provRankOtherProv, londonRank);
        return !string.IsNullOrWhiteSpace(displayRank) ? $", {displayRank}" : null;
    }

    /// <summary>
    /// Build a rank string for honorary members (ranks only, no dates).
    /// Uses cascading priority from configuration: priority_order defines which rank to show.
    /// Shows only the first non-empty rank in the priority order.
    /// Configuration loaded from master_v1.yaml ui_preferences.rank_display
    /// </summary>
    private static string? BuildDisplayRankSimple(string? grandRank, string? provincialRank, 
        string? provRankOtherProv, string? londonRank)
    {
        // Use default priority order if config not set
        var priorityOrder = ConfiguredRankDisplay?.PriorityOrder ?? 
            new List<string> { "grand_rank", "provincial_rank", "prov_rank_other_prov", "london_rank" };

        // Map field names to rank values
        var rankMap = new Dictionary<string, string?>
        {
            { "grand_rank", grandRank },
            { "provincial_rank", provincialRank },
            { "prov_rank_other_prov", provRankOtherProv },
            { "london_rank", londonRank }
        };

        // Find the first non-empty rank in priority order
        foreach (var fieldName in priorityOrder)
        {
            if (rankMap.TryGetValue(fieldName, out var rankValue) && !string.IsNullOrWhiteSpace(rankValue))
                return rankValue;
        }

        return null;
    }

    /// <summary>
    /// Build display rank string with comma prefix for past masters and joining past masters (with dates).
    /// If rank exists, returns ", Rank [Year]" format. If no rank, returns null (no trailing comma).
    /// Uses cascading priority: grand_rank > provincial_rank > prov_rank_other_prov > london_rank
    /// </summary>
    private static string? BuildDisplayRankWithComma(string? grandRank, int? grandRankDateAccorded,
        string? provincialRank, int? dateRankAccorded,
        string? provRankOtherProv, int? opDateAccorded,
        string? londonRank, int? londonRankDateAccorded)
    {
        var displayRank = BuildDisplayRankWithDates(grandRank, grandRankDateAccorded, provincialRank, dateRankAccorded, provRankOtherProv, opDateAccorded, londonRank, londonRankDateAccorded);
        return !string.IsNullOrWhiteSpace(displayRank) ? $", {displayRank}" : null;
    }

    /// <summary>
    /// Build grouped members structure for units with grouping (e.g., RC degrees like "33°", "32°", etc).
    /// Returns a list of groups, each with a groupKey and list of members in that group.
    /// If no members have a Grouping value, returns empty list.
    /// </summary>
    private static List<Dictionary<string, object?>> BuildGroupedMembers(List<SchemaMember> members)
    {
        // Check if any members have grouping
        if (members.All(m => string.IsNullOrWhiteSpace(m.Grouping)))
            return [];

        // Group members by their grouping value
        var grouped = members
            .Where(m => !string.IsNullOrWhiteSpace(m.Grouping))
            .GroupBy(m => m.Grouping ?? "")
            // Sort groups by numeric value in descending order (33°, 32°, 31°, etc.)
            .OrderByDescending(g => ExtractNumericFromGrouping(g.Key))
            .Select(g => 
            {
                // Sort members within each group by year initiated (ascending)
                var sortedMembers = g.OrderBy(m => GetSortableYear(m.YearInitiated)).ToList();
                
                return new Dictionary<string, object?>
                {
                    { "groupKey", g.Key },
                    { "columns", SplitGroupMembers(sortedMembers) }  // v1.9: Split into 2 columns
                };
            })
            .ToList();

        return grouped;
    }

    /// <summary>
    /// Extract numeric value from grouping string (e.g., "33°" → 33)
    /// </summary>
    private static int ExtractNumericFromGrouping(string grouping)
    {
        if (string.IsNullOrWhiteSpace(grouping))
            return 0;
        
        // Extract digits from the beginning of the string
        var numericPart = new string(grouping.TakeWhile(char.IsDigit).ToArray());
        return int.TryParse(numericPart, out var num) ? num : 0;
    }

    /// <summary>
    /// Extract sortable year from year initiated string (e.g., "1996" → 1996)
    /// </summary>
    private static int GetSortableYear(int? year)
    {
        return year ?? int.MaxValue;  // Return year if set, otherwise max value to sort to end
    }

    /// <summary>
    /// Split group members into 2 columns for display (for grouped members like RC degrees)
    /// </summary>
    private static List<List<Dictionary<string, object?>>> SplitGroupMembers(List<SchemaMember> members)
    {
        const int numColumns = 2;
        var col0 = new List<Dictionary<string, object?>>();
        var col1 = new List<Dictionary<string, object?>>();
        var columns = new List<List<Dictionary<string, object?>>> { col0, col1 };

        if (members.Count == 0)
            return columns;

        // Balanced distribution: distribute members evenly across columns
        var baseSize = members.Count / numColumns;
        var remainder = members.Count % numColumns;

        int memberIndex = 0;
        for (int colIndex = 0; colIndex < numColumns; colIndex++)
        {
            // First 'remainder' columns get baseSize + 1 members
            int colCapacity = baseSize + (colIndex < remainder ? 1 : 0);

            for (int j = 0; j < colCapacity && memberIndex < members.Count; j++)
            {
                var m = members[memberIndex];
                var dict = new Dictionary<string, object?>
                {
                    { "reference", TextCleaner.CleanReference(m.Reference) },
                    { "dataId", BuildDataId(m.Reference, m.MemType, null) },
                    { "name", TextCleaner.CleanName(m.Name) },
                    { "joined", m.YearInitiated },
                    { "suffix", string.IsNullOrWhiteSpace(m.Suffix) || m.Suffix == "0" ? "" : m.Suffix }  // v1.9: Add suffix if not blank or "0"
                };

                columns[colIndex].Add(dict);
                memberIndex++;
            }
        }

        return columns;
    }

    /// <summary>
    /// Check if officer is vacant/not appointed (empty or "Vacant" string)
    /// </summary>
    private static bool IsVacantOfficer(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return true;
        return name.Equals("Vacant", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Clean officer name: if vacant/empty, return the configured label; otherwise clean and return.
    /// </summary>
    private static string CleanOfficerName(string? name, string? vacantLabel = null)
    {
        var label = vacantLabel ?? "Not appointed";
        
        if (string.IsNullOrWhiteSpace(name) || name.Equals("Vacant", StringComparison.OrdinalIgnoreCase))
            return label;
        
        return TextCleaner.CleanName(name) ?? label;
    }

    /// <summary>
    /// Format joining past master units display.
    /// If more than 3 units, returns "X unit(s)"; otherwise returns the original list.
    /// Example: "1895,6194,9660,9900,9689,9697" (6 units) → "6 unit(s)"
    /// Example: "1895,6194,9660" (3 units) → "1895,6194,9660"
    /// </summary>
    private static string? FormatJoiningUnitsDisplay(string? pastUnits)
    {
        if (string.IsNullOrWhiteSpace(pastUnits))
            return pastUnits;
        
        const int JOINING_UNITS_THRESHOLD = 3;
        
        // Count units by splitting on comma (handles spaces and commas)
        var units = pastUnits.Split(',', System.StringSplitOptions.RemoveEmptyEntries);
        
        if (units.Length > JOINING_UNITS_THRESHOLD)
            return $"{units.Length} unit(s)";
        
        return pastUnits;
    }
}
