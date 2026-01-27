using CsvHelper.Configuration;
using MasonicCalendar.Core.Domain;

namespace MasonicCalendar.Core.Services;

public class UnitMeetingMap : ClassMap<UnitMeeting>
{
    public UnitMeetingMap()
    {
        Map(m => m.Id).Name("ID");
        Map(m => m.UnitId).Name("UnitID");
        Map(m => m.Title).Name("Title");
        Map(m => m.RecurrenceType).Name("RecurrenceType");
        Map(m => m.RecurrenceStrategy).Name("RecurrenceStrategy");
        Map(m => m.DayOfWeek).Name("DayOfWeek").Optional();
        Map(m => m.WeekNumber).Name("WeekNumber").Optional();
        Map(m => m.DayNumber).Name("DayNumber").Optional();
        Map(m => m.InstallationMonth).Name("InstallationMonth").Optional();
        Map(m => m.StartMonth).Name("StartMonth");
        Map(m => m.EndMonth).Name("EndMonth");
        Map(m => m.Months).Name("Months").Optional();
        Map(m => m.Override).Name("Override").Optional();
    }
}
