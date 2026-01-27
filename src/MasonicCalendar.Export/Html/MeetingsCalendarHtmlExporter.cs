using MasonicCalendar.Core.Domain;

namespace MasonicCalendar.Export.Html;

public class MeetingsCalendarHtmlExporter
{
    private static readonly string[] MonthNames = { "January", "February", "March", "April", "May", "June", "July", "August", "September", "October", "November", "December" };
    private static readonly string[] DayHeaders = { "Sun", "Mon", "Tue", "Wed", "Thu", "Fri", "Sat" };
    private static readonly string[] DayHeadersNoSunday = { "Mon", "Tue", "Wed", "Thu", "Fri", "Sat" };

    public void ExportMeetingsToHtml(List<UnitMeeting> meetings, int year, string outputPath, List<Unit>? units = null, bool includeSundays = false)
    {
        var expanded = MasonicCalendar.Core.Services.MeetingRecurrenceExpander.ExpandMeetings(meetings, year, new DateOnly(year, 1, 1));
        GenerateHtmlCalendar(expanded, outputPath, units, includeSundays, new DateOnly(year, 1, 1));
    }

    public void ExportMeetingsToHtml(List<UnitMeeting> meetings, DateOnly startDate, string outputPath, List<Unit>? units = null, bool includeSundays = false)
    {
        // Generate meetings for the 12-month period
        var endDate = startDate.AddMonths(12).AddDays(-1);
        var expandedDates = new List<(UnitMeeting, DateOnly)>();
        
        // Generate for both start and end years to cover boundaries
        foreach (var year in new[] { startDate.Year, startDate.AddMonths(12).Year })
        {
            var yearExpanded = MasonicCalendar.Core.Services.MeetingRecurrenceExpander.ExpandMeetings(meetings, year, startDate);
            expandedDates.AddRange(yearExpanded);
        }
        
        // Filter to the 12-month range and remove duplicates
        var expanded = expandedDates
            .Where(x => x.Item2 >= startDate && x.Item2 <= endDate)
            .DistinctBy(x => (x.Item1.Id, x.Item2))
            .ToList();
        
        GenerateHtmlCalendar(expanded, outputPath, units, includeSundays, startDate);
    }

    private void GenerateHtmlCalendar(List<(UnitMeeting, DateOnly)> expanded, string outputPath, List<Unit>? units, bool includeSundays, DateOnly startDate)
    {
        // Create a dictionary of units by ID for lookup
        var unitDict = units?.ToDictionary(u => u.Id) ?? new Dictionary<Guid, Unit>();
        
        // Group meetings by date
        var meetingsByDate = new Dictionary<DateOnly, List<(UnitMeeting, Unit?)>>();
        foreach (var (meeting, date) in expanded)
        {
            var unit = unitDict.ContainsKey(meeting.UnitId) ? unitDict[meeting.UnitId] : null;
            if (!meetingsByDate.ContainsKey(date))
                meetingsByDate[date] = new List<(UnitMeeting, Unit?)>();
            meetingsByDate[date].Add((meeting, unit));
        }
        
        var html = new System.Text.StringBuilder();
        html.AppendLine("<!DOCTYPE html>");
        html.AppendLine("<html lang=\"en\">");
        html.AppendLine("<head>");
        html.AppendLine("  <meta charset=\"UTF-8\">");
        html.AppendLine("  <meta name=\"viewport\" content=\"width=device-width, initial-scale=1.0\">");
        html.AppendLine($"  <title>Masonic Calendar - Meetings {startDate.Year}</title>");
        html.AppendLine("  <style>");
        html.AppendLine("    body { font-family: Arial, sans-serif; margin: 20px; background: #f5f5f5; }");
        html.AppendLine("    .container { max-width: 1200px; margin: 0 auto; }");
        html.AppendLine("    h1 { text-align: center; color: #333; }");
        html.AppendLine("    .month-section { background: white; margin-bottom: 30px; padding: 20px; border-radius: 5px; box-shadow: 0 2px 4px rgba(0,0,0,0.1); }");
        html.AppendLine("    .month-title { font-size: 24px; font-weight: bold; margin-bottom: 15px; color: #333; }");
        html.AppendLine("    table { width: 100%; border-collapse: collapse; table-layout: fixed; }");
        html.AppendLine("    th { background: #f0f0f0; padding: 10px; text-align: center; border: 1px solid #ddd; font-weight: bold; word-wrap: break-word; overflow-wrap: break-word; }");
        html.AppendLine("    td { padding: 8px; border: 1px solid #e0e0e0; height: 80px; vertical-align: top; word-wrap: break-word; overflow-wrap: break-word; }");
        html.AppendLine("    td.day-number { font-weight: bold; font-size: 11px; background: #fafafa; }");
        html.AppendLine("    .meeting { font-size: 11px; margin-bottom: 4px; line-height: 1.3; word-wrap: break-word; overflow-wrap: break-word; }");
        html.AppendLine("    .craft { color: #1e73be; }");
        html.AppendLine("    .royal-arch { color: #c41e3a; }");
        html.AppendLine("  </style>");
        html.AppendLine("</head>");
        html.AppendLine("<body>");
        html.AppendLine("  <div class=\"container\">");
        html.AppendLine($"    <h1>Masonic Meetings Calendar - {startDate:MMM yyyy} to {startDate.AddMonths(12).AddDays(-1):MMM yyyy}</h1>");
        
        // Generate one month per section for the 12-month period
        for (int i = 0; i < 12; i++)
        {
            var pageDate = startDate.AddMonths(i);
            GenerateMonthHtml(html, pageDate.Year, pageDate.Month, meetingsByDate, includeSundays);
        }
        
        html.AppendLine("  </div>");
        html.AppendLine("</body>");
        html.AppendLine("</html>");
        
        File.WriteAllText(outputPath, html.ToString());
    }

    private void GenerateMonthHtml(System.Text.StringBuilder html, int year, int month, Dictionary<DateOnly, List<(UnitMeeting, Unit?)>> meetingsByDate, bool includeSundays)
    {
        var firstDay = new DateOnly(year, month, 1);
        var lastDay = new DateOnly(year, month, DateTime.DaysInMonth(year, month));
        var startDayOfWeek = (int)firstDay.DayOfWeek;
        var adjustedStartDay = includeSundays ? startDayOfWeek : (startDayOfWeek == 0 ? 0 : startDayOfWeek - 1);
        var daysInWeek = includeSundays ? 7 : 6;
        
        html.AppendLine("    <div class=\"month-section\">");
        html.AppendLine($"      <div class=\"month-title\">{MonthNames[month - 1]} {year}</div>");
        html.AppendLine("      <table>");
        
        // Day headers
        var headers = includeSundays ? DayHeaders : DayHeadersNoSunday;
        html.AppendLine("        <tr>");
        foreach (var dayName in headers)
        {
            html.AppendLine($"          <th>{dayName}</th>");
        }
        html.AppendLine("        </tr>");
        
        // Calendar grid
        html.AppendLine("        <tr>");
        
        // Empty cells for days before month starts
        for (int i = 0; i < adjustedStartDay; i++)
        {
            html.AppendLine("          <td></td>");
        }
        
        // Calendar days
        int cellsInCurrentRow = adjustedStartDay;
        for (int day = 1; day <= lastDay.Day; day++)
        {
            var currentDate = new DateOnly(year, month, day);
            
            // Skip Sunday cells if not including Sundays
            if (!includeSundays && (int)currentDate.DayOfWeek == 0)
            {
                continue;
            }
            
            html.AppendLine("          <td>");
            html.AppendLine($"            <div class=\"day-number\">{day}</div>");
            
            if (meetingsByDate.ContainsKey(currentDate))
            {
                var meetingsList = meetingsByDate[currentDate];
                foreach (var (meeting, unit) in meetingsList)
                {
                    var unitPrefix = unit?.UnitType switch
                    {
                        "Craft" => "",
                        "RoyalArch" => "C",
                        _ => ""
                    };
                    var cssClass = unit?.UnitType switch
                    {
                        "Craft" => "craft",
                        "RoyalArch" => "royal-arch",
                        _ => ""
                    };
                    var unitDisplay = unit != null ? $"{unitPrefix}{unit.Number}" : "Unknown";
                    var meetingLine = $"{unitDisplay}-{meeting.Title}";
                    html.AppendLine($"            <div class=\"meeting {cssClass}\">{System.Net.WebUtility.HtmlEncode(meetingLine)}</div>");
                }
            }
            
            html.AppendLine("          </td>");
            
            cellsInCurrentRow++;
            if (cellsInCurrentRow % daysInWeek == 0 && day < lastDay.Day)
            {
                html.AppendLine("        </tr>");
                html.AppendLine("        <tr>");
            }
        }
        
        // Fill remaining cells in last row
        while (cellsInCurrentRow % daysInWeek != 0)
        {
            html.AppendLine("          <td></td>");
            cellsInCurrentRow++;
        }
        
        html.AppendLine("        </tr>");
        html.AppendLine("      </table>");
        html.AppendLine("    </div>");
    }
}
