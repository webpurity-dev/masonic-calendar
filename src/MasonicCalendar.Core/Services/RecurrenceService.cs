namespace MasonicCalendar.Core.Services;

using MasonicCalendar.Core.Domain;
using System.Globalization;

/// <summary>
/// Expands calendar events with recurrence rules into concrete EventInstance dates.
/// Handles Monthly (Nth weekday), Yearly, and specific date patterns.
/// </summary>
public class RecurrenceService
{
    /// <summary>
    /// Expand a calendar event into concrete instances for a given year and month range.
    /// </summary>
    public List<EventInstance> ExpandEvent(CalendarEvent evt, int startYear, int startMonth, int endYear, int endMonth)
    {
        var instances = new List<EventInstance>();

        // Determine active months from Months field (or StartMonth/EndMonth fallback)
        var activeMonths = ParseActiveMonths(evt);

        var currentDate = new DateOnly(startYear, startMonth, 1);
        var endDate = new DateOnly(endYear, endMonth, DateTime.DaysInMonth(endYear, endMonth));

        while (currentDate <= endDate)
        {
            try
            {
                // Check if this month is active for this event
                if (!activeMonths.Contains(currentDate.Month))
                {
                    currentDate = currentDate.AddMonths(1);
                    continue;
                }

                // Calculate the date for this month based on recurrence type
                var eventDate = CalculateEventDate(evt, currentDate.Year, currentDate.Month);
                if (eventDate.HasValue && eventDate >= currentDate && eventDate <= endDate)
                {
                    var isInstallation =
                        evt.Title.Equals("Installation", StringComparison.OrdinalIgnoreCase) ||
                        (!string.IsNullOrWhiteSpace(evt.InstallationMonth) && ParseMonthToken(evt.InstallationMonth) == eventDate.Value.Month);

                    instances.Add(new EventInstance
                    {
                        EventId = evt.Id,
                        UnitId = evt.UnitId,
                        UnitType = evt.UnitType,
                        Title = evt.Title,
                        Date = eventDate.Value,
                        IsInstallation = isInstallation
                    });
                }

                currentDate = currentDate.AddMonths(1);
            }
            catch (Exception ex)
            {
                // Skip this month if date calculation fails, log if needed
                System.Diagnostics.Debug.WriteLine($"Error expanding date for event {evt.Id} in {currentDate.Year}-{currentDate.Month}: {ex.Message}");
                currentDate = currentDate.AddMonths(1);
            }
        }

        return instances;
    }

    /// <summary>
    /// Calculate the actual date for an event in a given month/year.
    /// </summary>
    private DateOnly? CalculateEventDate(CalendarEvent evt, int year, int month)
    {
        return evt.RecurrenceType?.ToUpper() switch
        {
            "MONTHLY" => CalculateMonthlyDate(evt, year, month),
            "YEARLY" => CalculateYearlyDate(evt, year, month),
            "WEEKLY" => CalculateWeeklyDate(evt, year, month),
            _ => null
        };
    }

    /// <summary>
    /// Calculate a monthly recurrence date (e.g., "2nd Friday").
    /// </summary>
    private DateOnly? CalculateMonthlyDate(CalendarEvent evt, int year, int month)
    {
        // If DayNumber is specified, use that (e.g., 15th of month)
        if (!string.IsNullOrWhiteSpace(evt.DayNumber))
        {
            // Try to parse as integer first, handling formats like "27", "24"
            if (int.TryParse(evt.DayNumber, out var dayNum))
            {
                var maxDaysInMonth = DateTime.DaysInMonth(year, month);
                if (dayNum >= 1 && dayNum <= maxDaysInMonth)
                {
                    try
                    {
                        var date = new DateOnly(year, month, dayNum);
                        // No meetings on Sundays — shift forward to Monday
                        if (date.DayOfWeek == DayOfWeek.Sunday)
                            date = date.AddDays(1);
                        return date;
                    }
                    catch
                    {
                        return null;
                    }
                }
                // Day doesn't exist in this month, skip
                return null;
            }
        }

        // LunarSeason: nearest occurrence of DayOfWeek to the full moon, but only
        // candidates that fall after the 2nd occurrence of that day in the month.
        // This avoids selecting an early full moon (e.g. blue-moon months where the
        // first full moon falls in the first week) and matches lodges that meet
        // "nearest Thursday to the full moon, but not earlier than the 3rd Thursday".
        if (evt.RecurrenceStrategy?.Equals("LunarSeason", StringComparison.OrdinalIgnoreCase) == true
            && !string.IsNullOrWhiteSpace(evt.DayOfWeek))
        {
            if (!Enum.TryParse<DayOfWeek>(evt.DayOfWeek, ignoreCase: true, out var lunarDay))
                return null;

            return GetNearestWeekdayAfterSecond(year, month, lunarDay);
        }

        // LunarSeasonBefore: last occurrence of DayOfWeek on or before the full moon.
        // Installation month uses the 4th occurrence instead (pre-planned, not lunar-based).
        if (evt.RecurrenceStrategy?.Equals("LunarSeasonBefore", StringComparison.OrdinalIgnoreCase) == true
            && !string.IsNullOrWhiteSpace(evt.DayOfWeek))
        {
            if (!Enum.TryParse<DayOfWeek>(evt.DayOfWeek, ignoreCase: true, out var lunarBeforeDay))
                return null;

            bool isInstallationMonth = !string.IsNullOrWhiteSpace(evt.InstallationMonth)
                && ParseMonthToken(evt.InstallationMonth) == month;

            return GetLastWeekdayOnOrBeforeFullMoon(year, month, lunarBeforeDay, isInstallationMonth);
        }

        // Otherwise use WeekNumber + DayOfWeek (e.g., 2nd Friday)
        if (!string.IsNullOrWhiteSpace(evt.WeekNumber) && !string.IsNullOrWhiteSpace(evt.DayOfWeek))
        {
            return CalculateNthWeekdayOfMonth(year, month, evt.WeekNumber, evt.DayOfWeek);
        }

        return null;
    }

    /// <summary>
    /// Returns the last occurrence of <paramref name="targetDay"/> in the given month
    /// that falls on or before the "relevant" full moon for that month.
    /// The relevant full moon is the one falling in the window [15th of month → 14th of next month],
    /// which is always the "end of month" full moon that the lodge plans around.
    /// This correctly handles blue-moon months (e.g. April 2026 has an early Apr 2 moon which
    /// is irrelevant — the lodge uses the May 1 moon) and BST/UTC offsets near midnight.
    /// For the installation month the full moon is ignored — the 4th occurrence is returned.
    /// </summary>
    private static DateOnly? GetLastWeekdayOnOrBeforeFullMoon(
        int year, int month, DayOfWeek targetDay, bool isInstallationMonth)
    {
        var daysInMonth = DateTime.DaysInMonth(year, month);
        var occurrences = new List<DateOnly>();
        for (int d = 1; d <= daysInMonth; d++)
        {
            var date = new DateOnly(year, month, d);
            if (date.DayOfWeek == targetDay)
                occurrences.Add(date);
        }

        if (occurrences.Count == 0) return null;

        // Installation month: 4th occurrence pre-planned regardless of full moon.
        if (isInstallationMonth)
            return occurrences.Count >= 4 ? occurrences[3] : occurrences.Last();

        // Window: 15th of current month to 14th of next month (UTC).
        // This selects the "end of month" full moon the lodge plans its meeting around.
        var nextMonth = month == 12 ? 1 : month + 1;
        var nextYear  = month == 12 ? year + 1 : year;
        var windowStart = new DateTime(year,      month,     15, 0, 0, 0, DateTimeKind.Utc);
        var windowEnd   = new DateTime(nextYear,  nextMonth, 14, 0, 0, 0, DateTimeKind.Utc);

        var fullMoons = GetFullMoonsInWindow(windowStart, windowEnd)
            .OrderBy(fm => fm)
            .ToList();

        if (fullMoons.Count == 0)
            return occurrences.Last(); // No full moon in window — fallback to last occurrence

        // Use the earliest full moon in the window (there should only be one).
        var fullMoon = fullMoons.First();

        // Last occurrence of targetDay in the current month on or before the full moon.
        // If the full moon falls in the next calendar month, all occurrences in the
        // current month qualify, so LastOrDefault returns the last one correctly.
        var result = occurrences.LastOrDefault(o => o <= fullMoon);

        return result != default ? result : occurrences.Last();
    }

    /// <summary>
    /// Returns the occurrence of <paramref name="targetDay"/> in the given month that is
    /// (a) strictly after the 2nd occurrence of that day, and
    /// (b) nearest to any full moon in the same calendar month.
    /// Handles blue-moon months (two full moons) correctly by only considering full moons
    /// that fall after day 9 of the month (i.e., after the 2nd possible weekday occurrence).
    /// </summary>
    private static DateOnly? GetNearestWeekdayAfterSecond(int year, int month, DayOfWeek targetDay)
    {
        // Enumerate all occurrences of targetDay in the month
        var daysInMonth = DateTime.DaysInMonth(year, month);
        var occurrences = new List<DateOnly>();
        for (int d = 1; d <= daysInMonth; d++)
        {
            var date = new DateOnly(year, month, d);
            if (date.DayOfWeek == targetDay)
                occurrences.Add(date);
        }

        if (occurrences.Count == 0) return null;

        // Candidates: strictly after the 2nd occurrence (index 1); fallback to last if fewer
        var candidates = occurrences.Count > 2
            ? occurrences.Skip(2).ToList()
            : new List<DateOnly> { occurrences.Last() };

        // Gather full moons in a window from day 9 of the month to a few days past month-end
        // (day 9 ensures we skip any full moon before the 2nd possible Thursday)
        var windowStart = new DateTime(year, month, 9, 0, 0, 0, DateTimeKind.Utc);
        var windowEnd = new DateTime(year, month, daysInMonth, 0, 0, 0, DateTimeKind.Utc).AddDays(4);
        var fullMoons = GetFullMoonsInWindow(windowStart, windowEnd);

        // If no full moon found in window, widen slightly backwards
        if (fullMoons.Count == 0)
        {
            windowStart = new DateTime(year, month, 1, 0, 0, 0, DateTimeKind.Utc);
            fullMoons = GetFullMoonsInWindow(windowStart, windowEnd);
        }

        if (fullMoons.Count == 0) return candidates.First();

        // Pick the candidate with smallest distance to any full moon in the window
        DateOnly? best = null;
        int bestDist = int.MaxValue;
        foreach (var candidate in candidates)
        {
            foreach (var fm in fullMoons)
            {
                int dist = Math.Abs(candidate.DayNumber - fm.DayNumber);
                if (dist < bestDist)
                {
                    bestDist = dist;
                    best = candidate;
                }
            }
        }

        return best ?? candidates.First();
    }

    /// <summary>
    /// Returns all full moon dates (as DateOnly) whose DateTime falls within the given UTC window.
    /// Uses mean synodic period from the reference full moon of 21 Jan 2000 UTC.
    /// </summary>
    private static List<DateOnly> GetFullMoonsInWindow(DateTime windowStart, DateTime windowEnd)
    {
        const double synodicMonth = 29.530588853;
        var reference = new DateTime(2000, 1, 21, 0, 0, 0, DateTimeKind.Utc);

        double daysToStart = (windowStart - reference).TotalDays;
        double k = Math.Floor(daysToStart / synodicMonth);

        var fullMoons = new List<DateOnly>();
        // Check up to 3 consecutive lunations to cover the window
        for (int i = 0; i <= 2; i++)
        {
            var fm = reference.AddDays((k + i) * synodicMonth);
            if (fm >= windowStart.AddDays(-1) && fm <= windowEnd.AddDays(1))
                fullMoons.Add(DateOnly.FromDateTime(fm));
        }

        return fullMoons;
    }

    /// <summary>
    /// Calculate a yearly recurrence date.
    /// </summary>
    private DateOnly? CalculateYearlyDate(CalendarEvent evt, int year, int month)
    {
        // For yearly, apply the same logic as monthly
        return CalculateMonthlyDate(evt, year, month);
    }

    /// <summary>
    /// Calculate a weekly recurrence date (same weekday each week).
    /// </summary>
    private DateOnly? CalculateWeeklyDate(CalendarEvent evt, int year, int month)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(evt.DayOfWeek))
                return null;

            if (!Enum.TryParse<DayOfWeek>(evt.DayOfWeek, ignoreCase: true, out var targetDay))
                return null;

            var firstDay = new DateOnly(year, month, 1);
            var lastDayOfMonth = DateTime.DaysInMonth(year, month);
            var firstMatch = firstDay;

            // Find the first occurrence of the target day
            while (firstMatch.Day <= lastDayOfMonth && firstMatch.DayOfWeek != targetDay)
            {
                firstMatch = new DateOnly(year, month, firstMatch.Day + 1);
            }

            if (firstMatch.Day <= lastDayOfMonth)
                return firstMatch;

            return null;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Calculate the Nth weekday of a month (e.g., 2nd Friday, Last Monday).
    /// </summary>
    private DateOnly? CalculateNthWeekdayOfMonth(int year, int month, string weekNumber, string dayOfWeekStr)
    {
        try
        {
            if (!Enum.TryParse<DayOfWeek>(dayOfWeekStr, ignoreCase: true, out var targetDay))
                return null;

            var firstDay = new DateOnly(year, month, 1);
            var lastDayOfMonth = DateTime.DaysInMonth(year, month);
            
            // Find all occurrences of the target day in this month
            var occurrences = new List<DateOnly>();
            var currentDate = firstDay;
            while (currentDate.Day <= lastDayOfMonth)
            {
                if (currentDate.DayOfWeek == targetDay)
                    occurrences.Add(currentDate);
                
                // Instead of AddDays which might overflow, manually increment
                if (currentDate.Day < lastDayOfMonth)
                    currentDate = new DateOnly(year, month, currentDate.Day + 1);
                else
                    break;
            }

            if (occurrences.Count == 0)
                return null;

            // Parse weekNumber and return the appropriate occurrence
            return weekNumber?.ToLower() switch
            {
                "1st" => occurrences.Count >= 1 ? occurrences[0] : null,
                "2nd" => occurrences.Count >= 2 ? occurrences[1] : null,
                "3rd" => occurrences.Count >= 3 ? occurrences[2] : null,
                "4th" => occurrences.Count >= 4 ? occurrences[3] : null,
                "5th" => occurrences.Count >= 5 ? occurrences[4] : null,
                "last" => occurrences[occurrences.Count - 1],
                _ => null
            };
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Parse active months from a CalendarEvent.
    /// Priority: 1) Months field (pipe-separated numbers "01|03|05" or colon-separated names "Jan:Mar:Sep:Nov")
    ///           2) StartMonth/EndMonth range (e.g., "Feb"–"Feb" or "Sep"–"Jun")
    ///           3) All 12 months (legacy fallback)
    /// </summary>
    private List<int> ParseActiveMonths(CalendarEvent evt)
    {
        if (!string.IsNullOrWhiteSpace(evt.Months) &&
            !evt.Months.Equals("All", StringComparison.OrdinalIgnoreCase))
        {
            // Detect separator: pipe (|) for numbers, colon (:) for names
            char sep = evt.Months.Contains(':') ? ':' : '|';
            var parts = evt.Months.Split(sep, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            var months = new List<int>();
            foreach (var part in parts)
            {
                var m = ParseMonthToken(part);
                if (m.HasValue) months.Add(m.Value);
            }
            if (months.Count > 0) return months;
        }

        // Fallback: use StartMonth/EndMonth range
        if (!string.IsNullOrWhiteSpace(evt.StartMonth))
        {
            var start = ParseMonthToken(evt.StartMonth);
            var end = !string.IsNullOrWhiteSpace(evt.EndMonth)
                ? ParseMonthToken(evt.EndMonth)
                : start;

            if (start.HasValue && end.HasValue)
                return BuildMonthRange(start.Value, end.Value);
        }

        return Enumerable.Range(1, 12).ToList();
    }

    /// <summary>
    /// Parse a single month token: integer ("2"), zero-padded ("02"),
    /// abbreviated name ("Feb"), or full name ("February").
    /// </summary>
    private static int? ParseMonthToken(string token)
    {
        if (string.IsNullOrWhiteSpace(token)) return null;

        if (int.TryParse(token, out var num) && num >= 1 && num <= 12)
            return num;

        var dtf = CultureInfo.InvariantCulture.DateTimeFormat;
        for (int i = 1; i <= 12; i++)
        {
            if (dtf.AbbreviatedMonthNames[i - 1].Equals(token, StringComparison.OrdinalIgnoreCase) ||
                dtf.MonthNames[i - 1].Equals(token, StringComparison.OrdinalIgnoreCase))
                return i;
        }

        return null;
    }

    /// <summary>
    /// Build an inclusive month range, wrapping around year-end if start &gt; end
    /// (e.g., Sep(9) → Jun(6) = 9,10,11,12,1,2,3,4,5,6).
    /// </summary>
    private static List<int> BuildMonthRange(int start, int end)
    {
        var months = new List<int>();
        var current = start;
        while (true)
        {
            months.Add(current);
            if (current == end) break;
            current = current % 12 + 1;
            if (months.Count > 12) break; // Safety valve
        }
        return months;
    }
}
