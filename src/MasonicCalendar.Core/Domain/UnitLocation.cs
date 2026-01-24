namespace MasonicCalendar.Core.Domain;

public class UnitLocation
{
    public Guid Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public string AddressLine1 { get; set; } = string.Empty;

    public string Town { get; set; } = string.Empty;

    public string Postcode { get; set; } = string.Empty;

    public string What3Words { get; set; } = string.Empty;
}
