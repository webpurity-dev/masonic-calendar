namespace MasonicCalendar.Core.Domain;

/// <summary>
/// Represents a Masonic officer position (e.g., Worshipful Master, Senior Warden).
/// </summary>
public class Officer
{
    public Guid Id { get; set; }

    public int Order { get; set; }

    public string Abbreviation { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;
}
