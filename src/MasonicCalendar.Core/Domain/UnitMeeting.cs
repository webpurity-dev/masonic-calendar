namespace MasonicCalendar.Core.Domain;

public class UnitMeeting
{
    public Guid Id { get; set; }
    public Guid UnitId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string RecurrenceType { get; set; } = string.Empty;
    public string RecurrenceStrategy { get; set; } = string.Empty;
    public string? DayOfWeek { get; set; }
    public string? WeekNumber { get; set; }
    public int? DayNumber { get; set; }
    public string StartMonth { get; set; } = string.Empty;
    public string EndMonth { get; set; } = string.Empty;
    public string? Override { get; set; }
}
