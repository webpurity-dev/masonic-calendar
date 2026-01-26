using MasonicCalendar.Core.Domain;
using MasonicCalendar.Core.Services;
using QuestPDF.Fluent;
using QuestPDF.Infrastructure;
using QuestPDF.Helpers;

namespace MasonicCalendar.Export.Pdf;

public class MeetingsCalendarExporter
{
    private static readonly string[] DayHeaders = { "Sunday", "Monday", "Tuesday", "Wednesday", "Thursday", "Friday", "Saturday" };
    private static readonly string[] MonthNames = { "January", "February", "March", "April", "May", "June", "July", "August", "September", "October", "November", "December" };

    private struct MeetingWithUnit
    {
        public UnitMeeting Meeting { get; set; }
        public Core.Domain.Unit? Unit { get; set; }
    }

    public void ExportMeetingsToPdf(List<UnitMeeting> meetings, int year, string outputPath, List<Core.Domain.Unit>? units = null)
    {
        var expanded = MeetingRecurrenceExpander.ExpandMeetings(meetings, year, new DateOnly(year, 1, 1));
        
        // Create a dictionary of units by ID for lookup
        var unitDict = units?.ToDictionary(u => u.Id) ?? new Dictionary<Guid, Core.Domain.Unit>();
        
        // Group meetings by month, storing expanded meeting with unit info
        var meetingsByDate = new Dictionary<DateOnly, MeetingWithUnit>();
        foreach (var (meeting, date) in expanded)
        {
            var unit = unitDict.ContainsKey(meeting.UnitId) ? unitDict[meeting.UnitId] : null;
            meetingsByDate[date] = new MeetingWithUnit { Meeting = meeting, Unit = unit };
        }
        
        var document = Document.Create(container =>
        {
            // Generate one page per month
            for (int month = 1; month <= 12; month++)
            {
                GenerateMonthPage(container, year, month, meetingsByDate);
            }
        });
        
        document.GeneratePdf(outputPath);
    }

    private void GenerateMonthPage(IDocumentContainer container, int year, int month, Dictionary<DateOnly, MeetingWithUnit> meetingsByDate)
    {
        container.Page(page =>
        {
            page.Size(PageSizes.A4);
            page.Margin(15);
            page.Content().Column(col =>
            {
                // Title
                col.Item().PaddingBottom(10).Text($"{MonthNames[month - 1]} {year}").FontSize(18).Bold().AlignCenter();
                
                // Day headers
                col.Item().Row(row =>
                {
                    foreach (var dayName in DayHeaders)
                    {
                        row.RelativeItem().AlignCenter().PaddingVertical(5).Text(dayName).FontSize(10).Bold();
                    }
                });
                
                // Calendar grid
                var firstDay = new DateOnly(year, month, 1);
                var lastDay = new DateOnly(year, month, DateTime.DaysInMonth(year, month));
                var startDayOfWeek = (int)firstDay.DayOfWeek; // 0 = Sunday
                
                col.Item().Table(table =>
                {
                    table.ColumnsDefinition(cols =>
                    {
                        for (int i = 0; i < 7; i++)
                            cols.RelativeColumn();
                    });
                    
                    // Empty cells for days before month starts
                    for (int i = 0; i < startDayOfWeek; i++)
                    {
                        table.Cell().Padding(1).MinHeight(70).BorderColor(Colors.Grey.Lighten3).Border(0.5f).Text("");
                    }
                    
                    // Calendar days with meetings
                    for (int day = 1; day <= lastDay.Day; day++)
                    {
                        var currentDate = new DateOnly(year, month, day);
                        var hasMeeting = meetingsByDate.ContainsKey(currentDate);
                        
                        table.Cell().Padding(1).MinHeight(70).BorderColor(Colors.Grey.Lighten3).Border(0.5f).Column(dayCol =>
                        {
                            // Day number
                            dayCol.Item().Text(day.ToString()).FontSize(11).Bold();
                            
                            // Meetings for this day
                            if (hasMeeting)
                            {
                                var meetingData = meetingsByDate[currentDate];
                                var meeting = meetingData.Meeting;
                                var unit = meetingData.Unit;
                                var unitDisplay = unit != null ? $"{unit.Number} - {unit.Name}" : "Unknown Unit";
                                dayCol.Item().Text(unitDisplay).FontSize(8).Bold();
                                dayCol.Item().Text(meeting.Title).FontSize(8).Italic();
                            }
                        });
                    }
                });
            });
        });
    }
}
