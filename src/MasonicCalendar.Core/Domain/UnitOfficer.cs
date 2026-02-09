namespace MasonicCalendar.Core.Domain;

/// <summary>
/// Represents the assignment of an Officer to a Unit.
/// </summary>
public class UnitOfficer
{
    public Guid? Id { get; set; }

    public Guid UnitId { get; set; }

    public Guid OfficerId { get; set; }

    public string LastName { get; set; } = string.Empty;

    public string Initials { get; set; } = string.Empty;

    public string Position { get; set; } = string.Empty;

    public int PosNo { get; set; }

    // Navigation properties
    public Officer? Officer { get; set; }

    public Unit? Unit { get; set; }
}
