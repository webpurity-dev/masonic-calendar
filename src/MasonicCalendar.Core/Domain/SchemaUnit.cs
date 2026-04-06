namespace MasonicCalendar.Core.Domain;

/// <summary>
/// Schema-driven Unit domain model, generated from master_v1.yaml data_sources definition.
/// Represents a single Masonic lodge/chapter with officers, past masters, and members.
/// </summary>
public class SchemaUnit
{
    public required int Number { get; set; }
    public required string Name { get; set; }
    public string? ShortName { get; set; }
    public string? Contact { get; set; }
    public DateOnly? Established { get; set; }
    public string? LastInstallationDate { get; set; }  // Display string read directly from CSV (e.g., "21st January 2026")
    public string? Warrant { get; set; }              // Warrant / founding history text
    public string? MeetingDates { get; set; }         // Meeting schedule description
    public string? Hall { get; set; }                 // Hall / venue name
    public string? UnitType { get; set; }
    public string? LocationId { get; set; }  // Reference to location from CSV (e.g., "Weymouth")
    public SchemaLocation? Location { get; set; }
    
    // Related data, loaded based on section configuration
    public List<SchemaOfficer> Officers { get; set; } = [];
    public List<SchemaPastMaster> PastMasters { get; set; } = [];
    public List<SchemaJoinPastMaster> JoinPastMasters { get; set; } = [];
    public List<SchemaMember> Members { get; set; } = [];
    public List<SchemaHonoraryMember> HonoraryMembers { get; set; } = [];
}

/// <summary>
/// Current officer of a unit (SECTION_CODE='S39t' in CraftData.csv)
/// </summary>
public class SchemaOfficer
{
    public string? Reference { get; set; }
    public string? MemType { get; set; }   // e.g. "Off"
    public string? Office { get; set; }    // e.g. "WM", "SW", "Tyler" — same as Position abbreviation
    public string? Surname { get; set; }
    public string? Initials { get; set; }
    public required string Name { get; set; }  // Combined display name (Surname, Initials)
    public string? Position { get; set; }  // FN01
    public int? PosNo { get; set; }  // Position number for column splitting (0-based index)
}

/// <summary>
/// Past Master of a unit (SECTION_CODE='S15t' in CraftData.csv)
/// </summary>
public class SchemaPastMaster
{
    public string? Reference { get; set; }
    public string? MemType { get; set; }   // e.g. "PMO", "PMEZ", "PCO"
    public string? Surname { get; set; }
    public string? Initials { get; set; }
    public required string Name { get; set; }  // Combined display name (Surname, Initials)
    public string? YearInstalled { get; set; }  // FN01
    public string? ProvincialRank { get; set; } // FN13
    public string? RankYear { get; set; }       // FN14
}

/// <summary>
/// Joining Past Master of a unit (SECTION_CODE='S17t' in CraftData.csv)
/// </summary>
public class SchemaJoinPastMaster
{
    public string? Reference { get; set; }
    public string? MemType { get; set; }   // e.g. "JPM", "JPMEZ"
    public string? Surname { get; set; }
    public string? Initials { get; set; }
    public required string Name { get; set; }  // Combined display name (Surname, Initials)
    public string? PastUnits { get; set; }     // FN30 - Previous units
    public string? ProvincialRank { get; set; } // FN12
    public string? RankYear { get; set; }       // FN13
}

/// <summary>
/// Member of a unit (SECTION_CODE='S18t' in CraftData.csv)
/// </summary>
public class SchemaMember
{
    public string? Reference { get; set; }
    public string? MemType { get; set; }   // e.g. "Mem"
    public string? Surname { get; set; }
    public string? Initials { get; set; }
    public required string Name { get; set; }  // Combined display name (Surname, Initials)
    public string? YearInitiated { get; set; } // FN01
    public int? PosNo { get; set; }  // Position number for column splitting (0-based index)
}

/// <summary>
/// Honorary Member of a unit (SECTION_CODE='S41t' in CraftData.csv)
/// </summary>
public class SchemaHonoraryMember
{
    public string? Reference { get; set; }
    public string? MemType { get; set; }   // e.g. "Hon"
    public string? Surname { get; set; }
    public string? Initials { get; set; }
    public required string Name { get; set; }  // Combined display name (Surname, Initials)
    public string? Rank { get; set; }     // Combined rank (GrandRank + ProvincialRank)
}

/// <summary>
/// Location/venue information for a unit
/// </summary>
public class SchemaLocation
{
    public string? ID { get; set; }
    public string? Name { get; set; }
    public string? AddressLine1 { get; set; }
    public string? Town { get; set; }
    public string? Postcode { get; set; }
    public string? What3Words { get; set; }
}
