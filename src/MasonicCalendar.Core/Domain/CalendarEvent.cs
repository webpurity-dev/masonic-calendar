namespace MasonicCalendar.Core.Domain;

/// <summary>
/// Represents a meeting event with recurrence rules (e.g., "2nd Friday of each month").
/// </summary>
public class CalendarEvent
{
    public required string Id { get; set; }
    public required string UnitId { get; set; }
    public required string Title { get; set; }
    public required string RecurrenceType { get; set; }  // Monthly, Yearly, Weekly, Once
    public string? RecurrenceStrategy { get; set; }  // Default, LunarSeason, Custom
    public string? DayOfWeek { get; set; }  // Monday, Tuesday, ..., Sunday
    public string? WeekNumber { get; set; }  // 1st, 2nd, 3rd, 4th, Last
    public string? DayNumber { get; set; }  // 1-31 for specific day of month
    public string? InstallationMonth { get; set; }
    public string? StartMonth { get; set; }
    public string? EndMonth { get; set; }
    public string? Months { get; set; }  // Pipe-separated: "01|03|05" or "All"
    public string? Override { get; set; }
}

/// <summary>
/// Represents an expanded, concrete instance of a meeting on a specific date.
/// </summary>
public class EventInstance
{
    public required string EventId { get; set; }
    public required string UnitId { get; set; }
    public required string Title { get; set; }
    public required DateOnly Date { get; set; }
    public int Month => Date.Month;
    public int Year => Date.Year;
    public string MonthYear => Date.ToString("MMMM yyyy");
}
