namespace MasonicCalendar.Core.Domain;

public class Unit
{
    public Guid Id { get; set; }

    public int Number { get; set; }

    public string Name { get; set; } = string.Empty;

    public string Location { get; set; } = string.Empty;

    public Guid LocationId { get; set; }

    public string Email { get; set; } = string.Empty;

    public string InstallationMonth { get; set; } = string.Empty;

    public string MeetingSummary { get; set; } = string.Empty;

    public DateOnly? Established { get; set; }

    public DateOnly? LastInstallationDate { get; set; }

    public string? UnitType { get; set; }

    public virtual UnitLocation? LocationDetails { get; set; }
}
