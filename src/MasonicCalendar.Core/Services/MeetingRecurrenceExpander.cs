using System.Globalization;
using MasonicCalendar.Core.Domain;

namespace MasonicCalendar.Core.Services;

public static class MeetingRecurrenceExpander
{
    public static List<(UnitMeeting meeting, DateOnly date)> ExpandMeetings(List<UnitMeeting> meetings, int year, DateOnly? fromDate = null)
    {
        var results = new List<(UnitMeeting, DateOnly)>();
        foreach (var m in meetings)
        {
            var months = GetMonthRange(m.StartMonth, m.EndMonth, year);
            foreach (var (month, actualYear) in months)
            {
                if (m.DayNumber.HasValue)
                {
                    // Fixed day in month
                    int day = m.DayNumber.Value;
                    if (DateOnly.TryParse($"{actualYear}-{month:D2}-{day:D2}", out var date))
                    {
                        if (fromDate == null || date >= fromDate)
                            results.Add((m, date));
                    }
                }
                else if (!string.IsNullOrWhiteSpace(m.WeekNumber) && !string.IsNullOrWhiteSpace(m.DayOfWeek))
                {
                    // Nth weekday in month
                    var dayOfWeek = ParseDayOfWeek(m.DayOfWeek);
                    var weekNum = m.WeekNumber.ToLower();
                    var date = GetNthWeekdayOfMonth(actualYear, month, dayOfWeek, weekNum);
                    if (date != null && (fromDate == null || date >= fromDate))
                        results.Add((m, date.Value));
                }
                else if (!string.IsNullOrWhiteSpace(m.DayOfWeek) && m.RecurrenceStrategy == "LunarSeason")
                {
                    // Placeholder: lunar logic not implemented
                    // For now, just use first occurrence of DayOfWeek in month
                    var dayOfWeek = ParseDayOfWeek(m.DayOfWeek);
                    var date = GetNthWeekdayOfMonth(actualYear, month, dayOfWeek, "1st");
                    if (date != null && (fromDate == null || date >= fromDate))
                        results.Add((m, date.Value));
                }
            }
        }
        return results.OrderBy(t => t.Item2).ToList();
    }

    private static List<(int month, int year)> GetMonthRange(string startMonth, string endMonth, int baseYear)
    {
        var months = new List<(int, int)>();
        var start = ParseMonth(startMonth);
        var end = ParseMonth(endMonth);
        if (start == 0 || end == 0) return months;
        
        // If startMonth > endMonth, the series wraps across years
        // We need to include both: previous year's start to current year's end, AND current year's start to next year's end
        if (start > end)
        {
            // Previous year: Sep 2025 - Jan 2026
            int y = baseYear - 1;
            int m = start;
            while (m <= 12)
            {
                months.Add((m, y));
                m++;
            }
            m = 1;
            while (m <= end)
            {
                months.Add((m, baseYear));
                m++;
            }
            
            // Current year: Sep 2026 - Jan 2027
            y = baseYear;
            m = start;
            while (m <= 12)
            {
                months.Add((m, y));
                m++;
            }
            m = 1;
            while (m <= end)
            {
                months.Add((m, baseYear + 1));
                m++;
            }
        }
        else
        {
            // Normal case: months are in the same year
            int y = baseYear;
            int m = start;
            while (m <= end)
            {
                months.Add((m, y));
                m++;
            }
        }
        
        return months;
    }

    private static int ParseMonth(string month)
    {
        if (DateTime.TryParseExact(month, "MMM", CultureInfo.InvariantCulture, DateTimeStyles.None, out var dt))
            return dt.Month;
        return 0;
    }

    private static DayOfWeek ParseDayOfWeek(string day)
    {
        return Enum.TryParse<DayOfWeek>(day, true, out var d) ? d : DayOfWeek.Monday;
    }

    private static DateOnly? GetNthWeekdayOfMonth(int year, int month, DayOfWeek dayOfWeek, string weekNum)
    {
        int n = weekNum switch
        {
            "1st" => 1,
            "2nd" => 2,
            "3rd" => 3,
            "4th" => 4,
            "5th" => 5,
            "last" => -1,
            _ => 1
        };
        if (n > 0)
        {
            var first = new DateOnly(year, month, 1);
            int offset = ((int)dayOfWeek - (int)first.DayOfWeek + 7) % 7;
            var date = first.AddDays(offset + 7 * (n - 1));
            if (date.Month == month)
                return date;
        }
        else if (n == -1)
        {
            // Last occurrence
            var last = new DateOnly(year, month, DateTime.DaysInMonth(year, month));
            int offset = ((int)last.DayOfWeek - (int)dayOfWeek + 7) % 7;
            var date = last.AddDays(-offset);
            if (date.Month == month)
                return date;
        }
        return null;
    }
}
