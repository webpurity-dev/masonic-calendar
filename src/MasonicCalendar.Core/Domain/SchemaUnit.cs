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
    public string? SuperShortName { get; set; }
    public string? Contact { get; set; }
    public DateOnly? Established { get; set; }
    public string? LastInstallationDate { get; set; }  // Display string read directly from CSV (e.g., "21st January 2026")
    public string? Warrant { get; set; }              // Warrant / founding history text
    public string? MeetingDates { get; set; }         // Meeting schedule description
    public string? Hall { get; set; }                 // Hall / venue name
    public string? UnitType { get; set; }
    public string? LocationId { get; set; }  // Reference to location from CSV (e.g., "Weymouth")
    public string? What3Words { get; set; }  // What3Words location code (e.g., "///word.word.word")
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
    
    // v1.6 fields
    public string? GrandRank { get; set; }         // Grand rank (preferred if exists)
    public int? GrandRankDateAccorded { get; set; } // Year rank was accorded
    
    // v1.7 NEW: Other Province Rank
    public string? ProvRankOtherProv { get; set; }     // Provincial rank from different province
    public string? OpDateAccorded { get; set; }       // OP date accorded (raw format: "2021" or "1993-15")
    public int? OpDateStartYear { get; set; }        // Parsed start year from date range
    public int? OpDateEndYear { get; set; }          // Parsed end year from date range
    
    // v1.7 NEW: London Rank
    public string? LondonRank { get; set; }           // London Grand Rank (rare)
    public int? LondonRankDateAccorded { get; set; } // Year London rank was accorded
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
    public string? Rank { get; set; }  // Legacy: computed display rank (Grand > Provincial)
    public string? RankYear { get; set; }
    public bool IsGrandRank { get; set; }  // True if Rank is from GrandRank field (vs ProvincialRank)
    
    // v1.6 fields
    public string? ProvincialRank { get; set; }
    public int? DateRankAccorded { get; set; }
    public string? GrandRank { get; set; }
    public int? GrandRankDateAccorded { get; set; }
    
    // v1.7 NEW fields
    public string? ProvRankOtherProv { get; set; }
    public string? OpDateAccorded { get; set; }
    public int? OpDateStartYear { get; set; }
    public int? OpDateEndYear { get; set; }
    public string? LondonRank { get; set; }
    public int? LondonRankDateAccorded { get; set; }
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
    public string? PastUnits { get; set; } 
    public string? Rank { get; set; }  // Legacy: computed display rank
    public string? RankYear { get; set; }
    public bool IsGrandRank { get; set; }  // True if Rank is from GrandRank field (vs ProvincialRank)
    
    // v1.6 fields
    public string? ProvincialRank { get; set; }
    public int? DateRankAccorded { get; set; }
    public string? GrandRank { get; set; }
    public int? GrandRankDateAccorded { get; set; }
    
    // v1.7 NEW fields
    public string? ProvRankOtherProv { get; set; }
    public string? OpDateAccorded { get; set; }
    public int? OpDateStartYear { get; set; }
    public int? OpDateEndYear { get; set; }
    public string? LondonRank { get; set; }
    public int? LondonRankDateAccorded { get; set; }
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
    
    // v1.6 fields
    public string? ProvincialRank { get; set; }        // Provincial rank (Dorset)
    public int? DateRankAccorded { get; set; }        // Year rank was accorded
    public string? GrandRank { get; set; }             // Grand rank (preferred if exists)
    public int? GrandRankDateAccorded { get; set; }   // Year Grand rank was accorded
    
    // v1.7 NEW: Other Province Rank
    public string? ProvRankOtherProv { get; set; }     // Provincial rank from different province
    public string? OpDateAccorded { get; set; }       // OP date accorded (raw format: "2021" or "1993-15")
    public int? OpDateStartYear { get; set; }        // Parsed start year from date range
    public int? OpDateEndYear { get; set; }          // Parsed end year from date range
    
    // v1.7 NEW: London Rank
    public string? LondonRank { get; set; }           // London Grand Rank (rare)
    public int? LondonRankDateAccorded { get; set; } // Year London rank was accorded
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
    
    // v1.6 fields
    public string? GrandRank { get; set; }             // Grand rank (preferred if exists)
    public int? GrandRankDateAccorded { get; set; }   // Year Grand rank was accorded
    public string? ProvincialRank { get; set; }        // Provincial rank (Dorset, fallback if no grand rank)
    public int? DateRankAccorded { get; set; }        // Year Provincial rank was accorded
    public string? Rank { get; set; }                 // Display rank (GrandRank or ProvincialRank)
    public bool IsGrandRank { get; set; }             // True if Rank is from GrandRank field
    
    // v1.7 NEW: Other Province Rank
    public string? ProvRankOtherProv { get; set; }     // Provincial rank from different province
    public string? OpDateAccorded { get; set; }       // OP date accorded (raw format: "2021" or "1993-15")
    public int? OpDateStartYear { get; set; }        // Parsed start year from date range
    public int? OpDateEndYear { get; set; }          // Parsed end year from date range
    
    // v1.7 NEW: London Rank
    public string? LondonRank { get; set; }           // London Grand Rank (rare)
    public int? LondonRankDateAccorded { get; set; } // Year London rank was accorded
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
    public string? ImageFile { get; set; }  // Image filename for location pages
    public string? Parking { get; set; }     // Parking information/instructions
    public bool Exclude { get; set; }        // If true, skip rendering on locations page
}
