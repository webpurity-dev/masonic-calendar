namespace MasonicCalendar.Core.Domain;

/// <summary>
/// Represents a Past Master (PMO) of a Unit.
/// </summary>
public class UnitPastMaster
{
    public Guid? Id { get; set; }

    public Guid UnitId { get; set; }

    public string LastName { get; set; } = string.Empty;

    public string Initials { get; set; } = string.Empty;

    public string Installed { get; set; } = string.Empty;

    public string? ProvRank { get; set; }

    public string? ProvRankIssued { get; set; }
}
