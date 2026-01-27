using MasonicCalendar.Core.Domain;
using MasonicCalendar.Core.Services;
using QuestPDF.Fluent;
using QuestPDF.Infrastructure;
using QuestPDF.Helpers;

namespace MasonicCalendar.Export.Pdf;

public class MeetingsCalendarExporter
{
    private static readonly string[] DayHeaders = { "Sun", "Mon", "Tue", "Wed", "Thu", "Fri", "Sat" };
    private static readonly string[] DayHeadersNoSunday = { "Mon", "Tue", "Wed", "Thu", "Fri", "Sat" };
    private static readonly string[] MonthNames = { "January", "February", "March", "April", "May", "June", "July", "August", "September", "October", "November", "December" };

    private struct MeetingWithUnit
    {
        public UnitMeeting Meeting { get; set; }
        public Core.Domain.Unit? Unit { get; set; }
    }

    public void ExportMeetingsToPdf(List<UnitMeeting> meetings, int year, string outputPath, List<Core.Domain.Unit>? units = null, string pageSize = "A6", bool includeSundays = false, bool isLandscape = false)
    {
        var expanded = MeetingRecurrenceExpander.ExpandMeetings(meetings, year, new DateOnly(year, 1, 1));
        GenerateCalendarPages(expanded, outputPath, units, pageSize, includeSundays, isLandscape, new DateOnly(year, 1, 1));
    }

    public void ExportMeetingsToPdf(List<UnitMeeting> meetings, DateOnly startDate, string outputPath, List<Core.Domain.Unit>? units = null, string pageSize = "A6", bool includeSundays = false, bool isLandscape = false)
    {
        // Generate meetings for the 12-month period
        var endDate = startDate.AddMonths(12).AddDays(-1);
        var expandedDates = new List<(UnitMeeting, DateOnly)>();
        
        // Generate for both start and end years to cover boundaries
        foreach (var year in new[] { startDate.Year, startDate.AddMonths(12).Year })
        {
            var yearExpanded = MeetingRecurrenceExpander.ExpandMeetings(meetings, year, startDate);
            expandedDates.AddRange(yearExpanded);
        }
        
        // Filter to the 12-month range and remove duplicates
        var expanded = expandedDates
            .Where(x => x.Item2 >= startDate && x.Item2 <= endDate)
            .DistinctBy(x => (x.Item1.Id, x.Item2))
            .ToList();
        
        GenerateCalendarPages(expanded, outputPath, units, pageSize, includeSundays, isLandscape, startDate);
    }

    private void GenerateCalendarPages(List<(UnitMeeting, DateOnly)> expanded, string outputPath, List<Core.Domain.Unit>? units, string pageSize, bool includeSundays, bool isLandscape, DateOnly startDate)
    {
        // Create a dictionary of units by ID for lookup
        var unitDict = units?.ToDictionary(u => u.Id) ?? new Dictionary<Guid, Core.Domain.Unit>();
        
        // Group meetings by date, allowing multiple meetings per day
        var meetingsByDate = new Dictionary<DateOnly, List<MeetingWithUnit>>();
        foreach (var (meeting, date) in expanded)
        {
            var unit = unitDict.ContainsKey(meeting.UnitId) ? unitDict[meeting.UnitId] : null;
            if (!meetingsByDate.ContainsKey(date))
                meetingsByDate[date] = new List<MeetingWithUnit>();
            meetingsByDate[date].Add(new MeetingWithUnit { Meeting = meeting, Unit = unit });
        }
        
        // Convert page size string to QuestPDF PageSize
        var questPageSize = pageSize.ToUpper() switch
        {
            "A4" => PageSizes.A4,
            "A5" => PageSizes.A5,
            "A6" => PageSizes.A6,
            _ => PageSizes.A6  // Default to A6
        };
        
        // Apply landscape orientation if requested
        if (isLandscape)
            questPageSize = questPageSize.Landscape();
        
        var document = Document.Create(container =>
        {
            // Generate one page per month for the 12-month period
            for (int i = 0; i < 12; i++)
            {
                var pageDate = startDate.AddMonths(i);
                GenerateMonthPage(container, pageDate.Year, pageDate.Month, meetingsByDate, questPageSize, includeSundays, isLandscape);
            }
        });
        
        document.GeneratePdf(outputPath);
    }

    private void GenerateMonthPage(IDocumentContainer container, int year, int month, Dictionary<DateOnly, List<MeetingWithUnit>> meetingsByDate, PageSize pageSize, bool includeSundays, bool isLandscape)
    {
        container.Page(page =>
        {
            page.Size(pageSize);
            page.Margin(8);
            
            // Calculate calendar grid layout variables first
            var firstDay = new DateOnly(year, month, 1);
            var lastDay = new DateOnly(year, month, DateTime.DaysInMonth(year, month));
            var startDayOfWeek = (int)firstDay.DayOfWeek; // 0 = Sunday
            var daysInWeek = includeSundays ? 7 : 6;
            var adjustedStartDay = includeSundays ? startDayOfWeek : (startDayOfWeek == 0 ? 0 : startDayOfWeek - 1);
            
            // Calculate number of weeks (rows) needed
            var totalCells = adjustedStartDay + lastDay.Day;
            var numWeeks = (int)Math.Ceiling((double)totalCells / daysInWeek);
            
            // Set fixed row height based on page size (one row per week)
            // A6: 15mm per week, A5: 22mm per week, A4: 35mm per week
            // Landscape heights are slightly smaller due to less vertical space
            var cellHeightPerWeek = isLandscape 
                ? (pageSize == PageSizes.A6 ? 11 : pageSize == PageSizes.A5 ? 16 : 25)
                : (pageSize == PageSizes.A6 ? 15 : pageSize == PageSizes.A5 ? 22 : 35);
            
            page.Content().Column(col =>
            {
                // Title
                col.Item().PaddingBottom(4).Text($"{MonthNames[month - 1]} {year}").FontSize(12).Bold().AlignCenter();
                
                // Day headers
                var headers = includeSundays ? DayHeaders : DayHeadersNoSunday;
                col.Item().Row(row =>
                {
                    foreach (var dayName in headers)
                    {
                        row.RelativeItem().AlignCenter().PaddingVertical(2).Text(dayName).FontSize(8).Bold();
                    }
                });
                
                // Calendar grid
                col.Item().Table(table =>
                {
                    table.ColumnsDefinition(cols =>
                    {
                        for (int i = 0; i < daysInWeek; i++)
                            cols.RelativeColumn();
                    });
                    
                    // Empty cells for days before month starts
                    for (int i = 0; i < adjustedStartDay; i++)
                    {
                        table.Cell().Padding(0.5f).MinHeight((float)cellHeightPerWeek).BorderColor(Colors.Grey.Lighten3).Border(0.5f).Text("");
                    }
                    
                    // Calendar days with meetings
                    int cellsInCurrentRow = adjustedStartDay;
                    for (int day = 1; day <= lastDay.Day; day++)
                    {
                        var currentDate = new DateOnly(year, month, day);
                        
                        // Skip Sunday cells if not including Sundays
                        if (!includeSundays && (int)currentDate.DayOfWeek == 0)
                        {
                            continue;
                        }
                        
                        var hasMeeting = meetingsByDate.ContainsKey(currentDate);
                        
                        table.Cell().Padding(0.5f).MinHeight((float)cellHeightPerWeek).BorderColor(Colors.Grey.Lighten3).Border(0.5f).Column(dayCol =>
                        {
                            // Day number
                            dayCol.Item().Text(day.ToString()).FontSize(7).Bold();
                            
                            // Meetings for this day
                            if (hasMeeting)
                            {
                                var meetingsList = meetingsByDate[currentDate];
                                foreach (var meetingData in meetingsList)
                                {
                                    var meeting = meetingData.Meeting;
                                    var unit = meetingData.Unit;
                                    
                                    // Determine unit prefix and text color based on type
                                    var unitPrefix = unit?.UnitType switch
                                    {
                                        "Craft" => "",
                                        "RoyalArch" => "C",
                                        _ => ""
                                    };
                                    var unitColor = unit?.UnitType switch
                                    {
                                        "Craft" => Color.FromHex("1e73be"),
                                        "RoyalArch" => Colors.Red.Medium,
                                        _ => Colors.Black
                                    };
                                    
                                    var unitDisplay = unit != null ? $"{unitPrefix}{unit.Number}" : "Unknown Unit";
                                    // var unitDisplay = unit != null ? $"{unitPrefix}{unit.Number} - {unit.Name}" : "Unknown Unit";
                                    var meetingLine = $"{unitDisplay}-{meeting.Title}";
                                    
                                    dayCol.Item().Text(meetingLine).FontSize(5).FontColor(unitColor);
                                }
                            }
                        });
                        
                        cellsInCurrentRow++;
                    }
                });
            });
        });
    }
}
