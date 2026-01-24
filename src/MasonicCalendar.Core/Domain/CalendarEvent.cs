namespace MasonicCalendar.Core.Domain;

/// <summary>
/// Represents a calendar event with basic details.
/// </summary>
public class CalendarEvent
{
    public int EventId { get; set; }
    public required string EventName { get; set; }
    public DateOnly EventDate { get; set; }
    public string? Description { get; set; }
    public string? Location { get; set; }
}
