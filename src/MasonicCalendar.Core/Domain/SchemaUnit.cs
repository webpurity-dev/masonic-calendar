namespace MasonicCalendar.Core.Domain;

/// <summary>
/// Schema-driven Unit domain model, generated from master_v1.yaml data_sources definition.
/// Represents a single Masonic lodge/chapter with officers, past masters, and members.
/// </summary>
public class SchemaUnit
{
    public required int Number { get; set; }
    public required string Name { get; set; }
    public string? Email { get; set; }
    public DateOnly? Established { get; set; }
    public DateOnly? LastInstallationDate { get; set; }
    public string? UnitType { get; set; }
    
    // Related data, loaded based on section configuration
    public List<SchemaOfficer> Officers { get; set; } = [];
    public List<SchemaPastMaster> PastMasters { get; set; } = [];
    public List<SchemaJoinPastMaster> JoinPastMasters { get; set; } = [];
    public List<SchemaMember> Members { get; set; } = [];
    public List<SchemaHonoraryMember> HonoraryMembers { get; set; } = [];
}

/// <summary>
/// Current officer of a unit (Type='Off' in hermes-export.csv)
/// </summary>
public class SchemaOfficer
{
    public required string Name { get; set; }
    public string? Position { get; set; }  // FN01
    public int? DisplayOrder { get; set; } // PosNo
}

/// <summary>
/// Past Master of a unit (Type='PMO' in hermes-export.csv)
/// </summary>
public class SchemaPastMaster
{
    public required string Name { get; set; }
    public string? YearInstalled { get; set; }  // FN01
    public string? ProvincialRank { get; set; } // FN13
    public string? RankYear { get; set; }       // FN14
    public int? DisplayOrder { get; set; }      // PosNo
}

/// <summary>
/// Joining Past Master of a unit (Type='PMI' in hermes-export.csv)
/// </summary>
public class SchemaJoinPastMaster
{
    public required string Name { get; set; }
    public string? YearInstalled { get; set; }  // FN01
    public string? ProvincialRank { get; set; } // FN12
    public string? RankYear { get; set; }       // FN13
    public int? DisplayOrder { get; set; }      // PosNo
}

/// <summary>
/// Member of a unit (Type='Mem' in hermes-export.csv)
/// </summary>
public class SchemaMember
{
    public required string Name { get; set; }
    public string? YearInitiated { get; set; } // FN01
    public int? DisplayOrder { get; set; }     // PosNo
}

/// <summary>
/// Honorary Member of a unit (Type='Hon' in hermes-export.csv)
/// </summary>
public class SchemaHonoraryMember
{
    public required string Name { get; set; }
    public int? DisplayOrder { get; set; } // PosNo
}
