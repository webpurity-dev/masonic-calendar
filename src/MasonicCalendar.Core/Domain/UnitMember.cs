namespace MasonicCalendar.Core.Domain;

public class UnitMember
{
    private Guid _id;

    public Guid Id
    {
        get => _id == Guid.Empty ? Guid.NewGuid() : _id;
        set => _id = value;
    }

    public Guid UnitId { get; set; }

    public string LastName { get; set; } = string.Empty;

    public string FirstNames { get; set; } = string.Empty;

    public string Initials { get; set; } = string.Empty;

    public string Joined { get; set; } = string.Empty;

    public string ProvRank { get; set; } = string.Empty;

    public string Code { get; set; } = string.Empty;
}
