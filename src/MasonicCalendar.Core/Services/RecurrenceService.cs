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

        // Determine active months from Months field
        var activeMonths = ParseActiveMonths(evt.Months);

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
                    instances.Add(new EventInstance
                    {
                        EventId = evt.Id,
                        UnitId = evt.UnitId,
                        Title = evt.Title,
                        Date = eventDate.Value
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
                        return new DateOnly(year, month, dayNum);
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

        // Otherwise use WeekNumber + DayOfWeek (e.g., 2nd Friday)
        if (!string.IsNullOrWhiteSpace(evt.WeekNumber) && !string.IsNullOrWhiteSpace(evt.DayOfWeek))
        {
            return CalculateNthWeekdayOfMonth(year, month, evt.WeekNumber, evt.DayOfWeek);
        }

        return null;
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
    /// Parse the Months field (pipe-separated list of month numbers: "01|03|05").
    /// Returns a list of active month numbers (1-12). If "All", returns all months.
    /// </summary>
    private List<int> ParseActiveMonths(string? monthsField)
    {
        if (string.IsNullOrWhiteSpace(monthsField))
            return Enumerable.Range(1, 12).ToList();  // Default to all months

        if (monthsField.Equals("All", StringComparison.OrdinalIgnoreCase))
            return Enumerable.Range(1, 12).ToList();

        var months = new List<int>();
        var parts = monthsField.Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        foreach (var part in parts)
        {
            if (int.TryParse(part, out var monthNum) && monthNum >= 1 && monthNum <= 12)
                months.Add(monthNum);
        }

        return months.Count > 0 ? months : Enumerable.Range(1, 12).ToList();  // Default to all months if parsing failed
    }
}
