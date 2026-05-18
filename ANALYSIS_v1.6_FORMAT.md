# v1.6 Data Format Analysis: Column-Based CSV Structure

**Date:** April 30, 2026  
**File:** `document/data/sections_v1.6.csv`  
**Status:** Format assessment for multi-degree support

---

## 📋 Overview

The v1.6 format represents a **fundamental shift** from the current row-based data model to a **column-grouped (columnar) structure**. Instead of one row per person with a role column, the new format organizes data by person type across fixed column ranges, with all person data for a unit compressed into single rows.

### Current Format (v1.5)
```
Unit Type | Unit No | Name | Role/Position | Installed | Rank | ...
Craft     | 137     | Smith| WM            | 2015      |      | ...
Craft     | 137     | Jones| SW            | 2010      |      | ...
Craft     | 137     | Brown| Member        |           |      | ...
```

### New Format (v1.6)
```
Members: (Unit Type | Unit No | Name | Join Date) | Officers: (Unit Type | Unit No | Name | Office) | Past Unit Heads: (...) | Joining Past Unit Heads: (...) | Honorary Members: (...)
Craft    | 137     | Smith| 1964          |           | Craft  | 137    | Jones  | WM        |         | 137   | Craft  | Smith   | 1964   | 1978 | ... | ...
```

---

## 🗂️ Column Structure

### **Section 1: Members** (Columns A-E)
**Headers:** `Unit Type | Unit No | Name | Join Date | [empty]`

- **Purpose:** Regular members who joined the unit
- **Fields:**
  - `Unit Type` (Craft, RA, Mark, RAM, KT, KTP, OSC, PBQ, RCOC, STOA)
  - `Unit No` (e.g., 137, 170, 386)
  - `Name` (quoted, right-padded with spaces)
  - `Join Date` (4-digit year, e.g., 1964)
- **Example Row:** `Craft,137,"Howard, D  ",1964,,`

**Issues with current approach:**
- Each member row contains one person
- No indication of which person belongs to which role category
- Parsing is straightforward with CsvHelper

---

### **Section 2: Officers** (Columns F-J)
**Headers:** `Unit Type | Unit No | Name | Office | [empty]`

- **Purpose:** Current office holders
- **Fields:**
  - `Unit Type` (Craft, RA, etc.)
  - `Unit No`
  - `Name`
  - `Office` (WM, SW, JW, Chap, Treas, Sec, DC, Alm, ChStwd, MO, Mentor, SD, JD, ADC, Gst Org, ASec, IG, Stwd, Tyler, RA Rep, Hermes Admin, Comms Officer, etc.)
- **Example:** `Craft,137,"White, N J  ",WM,,`

---

### **Section 3: Past Unit Heads** (Columns K-T)
**Headers:** `Unit No | Unit Type | Name | Joined | Installed | Provincial Rank | Date Accorded | Grand Rank | GR Date Accorded | [empty]`

- **Purpose:** Past masters/principals/commanders (unit leaders)
- **Fields:**
  - `Unit No`
  - `Unit Type`
  - `Name`
  - `Joined` (4-digit year when they joined the unit)
  - `Installed` (4-digit year or comma-separated years for multiple tenures, e.g., "1998, 2006")
  - `Provincial Rank` (e.g., PProvSGW, PProvGSwdB, PPGSwdB, PPJGW, etc.)
  - `Date Accorded` (4-digit year provincial rank was given)
  - `Grand Rank` (e.g., PAGDC, PDepGOrg, PJGD, PAGDC, etc.)
  - `GR Date Accorded` (4-digit year grand rank was given)
- **Example:** `137,Craft,"Howard, D   ",1964,1978,PProvSGW,2005,,,,`

**Key difference:** This section includes fields for both provincial and grand ranks with dates accorded

---

### **Section 4: Joining Past Unit Heads** (Columns U-AD)
**Headers:** `Unit No | Lodge | Installed | Unit Type | Name | Joined | Provincial Grand Rank | Date Accorded | Grand Rank | GR Date Accorded | [empty]`

- **Purpose:** Past unit heads who joined from other lodges
- **Fields:**
  - `Unit No` (destination unit)
  - `Lodge` (origin lodge(s) - may be comma-separated, e.g., "5848, 6525")
  - `Installed` (year installed in current unit)
  - `Unit Type`
  - `Name`
  - `Joined` (year joined origin lodge)
  - `Provincial Grand Rank` (rank held in origin lodge, e.g., PPJGW)
  - `Date Accorded` (date rank was accorded in origin lodge)
  - `Grand Rank` (grand rank held in destination unit, e.g., PPGSwdB)
  - `GR Date Accorded` (date grand rank was accorded in destination unit)
- **Example:** `137,"5848, 6525",1987,Craft,"Shorto, R J  ",1959,PPJGW,2019,PPGSwdB,2025,`

**Key differences from Past Unit Heads:**
- Includes `Lodge` (origin lodge(s))
- Has TWO separate rank fields:
  - `Provincial Grand Rank` — from origin lodge
  - `Grand Rank` — from destination unit
- Different field names and order
- May have different rank interpretation

---

### **Section 5: Honorary Members** (Columns AE-AH)
**Headers:** `Unit Type | Unit No | Name | Rank`

- **Purpose:** Honorary members (often past members or dignitaries)
- **Fields:**
  - `Unit Type`
  - `Unit No`
  - `Name`
  - `Rank` (e.g., PPSGD, ProvGOrg, PProvGM (Dorset), PPJGW, etc.)
- **Example:** `Craft,137,"Hollard, R M  ",PPSGD`

---

## 🎯 New Degree Types

The v1.6 format includes **11 degree types** (vs. 4 in v1.5):

| Code | Full Name (Assumed) | Current Support |
|------|---------------------|-----------------|
| `Craft` | Craft Freemasonry | ✅ Yes |
| `RA` | Royal Arch | ✅ Yes |
| `Mark` | Mark Masonry | ✅ Yes |
| `RAM` | Royal Ark Mariners | ✅ Yes |
| `KT` | Knights Templar | ❌ No |
| `KTP` | Knights Templar (variant?) | ❌ No |
| `OSC` | Order of the Secret Monitor? | ❌ No |
| `PBQ` | Unknown (possibly Pembroke?) | ❌ No |
| `RCOC` | Red Cross of Constantine | ❌ No |
| `STOA` | Societas Trinitatis ad Obsequium | ❌ No |

---

## ⚠️ Key Technical Challenges

### 1. **Column-Based Instead of Row-Based**

**Current System:**
```csharp
// v1.5: One person = one row
foreach (var row in csv)
{
    var officer = new SchemaOfficer { 
        Name = row["Name"], 
        Position = row["Office"],
        ...
    };
}
```

**v1.6 Requirement:**
- Need to parse fixed column ranges
- Must skip empty separator columns
- Need to identify section boundaries programmatically or hard-code them
- All person types for a unit mixed in the same row(s)

### 2. **Section Boundary Detection**

**Problem:** The headers describe column ranges, but the ranges are not explicitly delimited.

**Current Row Structure:**
```
Columns 0-4:   Members
Columns 5-9:   Officers
Columns 10-19: Past Unit Heads (with empty col 19)
Columns 20-29: Joining Past Unit Heads (with empty col 29)
Columns 30-33: Honorary Members
```

**Solution required:**
- Hard-code column indices for each section
- OR parse the header row to detect section names and column positions
- Handle variable-width sections (Past Unit Heads ≠ Officers)

### 3. **Different Column Layouts Per Section**

| Section | # Columns | Structure |
|---------|-----------|-----------|
| Members | 5 | Unit Type, Unit No, Name, Join Date, empty |
| Officers | 5 | Unit Type, Unit No, Name, Office, empty |
| Past Unit Heads | 10 | Unit No, Unit Type, Name, Joined, Installed, Prov Rank, Date, Grand Rank, GR Date, empty |
| Joining Past Unit Heads | 11 | Unit No, Lodge, Installed, Unit Type, Name, Joined, **Prov Grand Rank** (origin), Date, **Grand Rank** (destination), GR Date, empty |
| Honorary Members | 4 | Unit Type, Unit No, Name, Rank |

**Impact:** Cannot use a single parser; need per-section parsing logic. Note: Joining Past Unit Heads has TWO rank sources: Provincial Grand Rank from origin lodge, Grand Rank from destination unit.

### 4. **Overlapping Column Headers**

Some column names appear in multiple sections:
- `Unit Type` (Members, Officers, Past Unit Heads, Joining Past Unit Heads, Honorary Members)
- `Unit No` (Members, Officers, Past Unit Heads, Honorary Members)
- `Installed` (Past Unit Heads, Joining Past Unit Heads) — but with different meanings

**Solution:** Must track section context when parsing

### 5. **Data Entry Variations**

| Issue | Example | Impact |
|-------|---------|--------|
| Multiple installed years | `1998, 2006` | Need to parse comma-separated lists |
| Multiple origin lodges | `5848, 6525` | Need to handle comma-separated lodge numbers |
| Rank with location | `PProvGM (Dorset)` | Parsing rank vs. location suffix |
| Empty cells in middle of data | Section has blank columns between data entries | Risk of misalignment if not careful |

### 6. **Field Semantics Differ Per Degree**

Different degrees may have different officer titles and rank systems:
- Craft: WM, SW, JW, PM
- RA: Z, H, J, EZ
- Mark: WM, SW, JW, PM
- RAM: Captain, Second Captain, Third Captain
- KT: Commander, Officer, etc.

Current system handles this via templates, but column structure is still shared. Need to verify if rank/office names are consistent.

### 7. **Missing Unique Row Identifier**

**Problem:** In v1.5 row-based format, each row is uniquely identifiable:
```
Row 3: Craft, 137, "Howard, D", 1964, ...  → Unique row in CSV
Row 4: Craft, 137, "Davis, A", 1971, ...   → Different person, different row
```

In v1.6 columnar format, different person types are in different columns of the same row:
```
Row 3: [Member: Howard D], [Officer: White NJ], [Past Head: Howard D], [Joining: Shorto RJ], [Honorary: Hollard RM]
```

**Issue:**
- Cannot use row number as identifier (columns within row vary by person type)
- No database-style primary key in the CSV
- Need to generate unique identifiers per person/record type

**Solution:** Composite keys based on person type and available fields

| Person Type | Composite Key Fields | Example |
|-------------|----------------------|---------|
| Members | `unit_type + unit_no + name + join_date` | `"Craft-137-Howard D-1964"` |
| Officers | `unit_type + unit_no + name + office` | `"Craft-137-White NJ-WM"` |
| Past Unit Heads | `unit_no + unit_type + name + installed` | `"137-Craft-Howard D-1978"` |
| Joining Past Unit Heads | `unit_no + name + installed_in_current_unit` | `"137-Shorto RJ-1987"` |
| Honorary Members | `unit_type + unit_no + name + rank` | `"Craft-137-Hollard RM-PPSGD"` |

### 8. **Rank Field Naming Inconsistency**

**Past Unit Heads:**
- `Provincial Rank` (e.g., PProvSGW)
- `Grand Rank` (e.g., PAGDC)

**Joining Past Unit Heads:**
- `Provincial Grand Rank` (e.g., PPJGW)
- `Rank` (e.g., PPGSwdB) — what does this represent?

**Honorary Members:**
- `Rank` (e.g., PPSGD) — appears to be provincial/grand rank

**Question:** Are these the same field with different names, or different semantics per section?

---

## � Unique Identifier Strategy

### Why Unique IDs Matter

In the v1.6 columnar format where rows contain multiple person types in different columns, we lose the natural row-level uniqueness. Unique IDs are essential for:

1. **Deduplication** — Detect and eliminate duplicate person records within a section
2. **Change Detection** — Identify which records changed between data updates
3. **Data Validation** — Ensure no duplicate officers in same unit, etc.
4. **Audit Trail** — Track record history across data versions
5. **Error Reporting** — Report validation errors with specific record references

### Composite Key Generation

Each person type has a natural set of fields that uniquely identify that record:

| Person Type | Fields (Composite Key) |
|-------------|----------------------|
| **Members** | UnitType + UnitNo + Name + JoinDate |
| **Officers** | UnitType + UnitNo + Name + Office |
| **Past Unit Heads** | UnitNo + UnitType + Name + InstalledYear |
| **Joining Past Unit Heads** | UnitNo + Name + InstalledInCurrentUnit |
| **Honorary Members** | UnitType + UnitNo + Name + Rank |

**Example IDs:**
- Member: `"Craft-137-Howard, D-1964"`
- Officer: `"Craft-137-White, N J-WM"`
- Past Head: `"137-Craft-Howard, D-1978"`
- Joining Past Head: `"137-Shorto, R J-1987"`
- Honorary: `"Craft-137-Hollard, R M-PPSGD"`

### YAML Configuration

Define `unique_id_fields` in each section to specify which fields compose the unique identifier:

```yaml
sections:
  members:
    unique_id_fields:
      - unit_type
      - unit_no
      - name
      - join_date
  
  officers:
    unique_id_fields:
      - unit_type
      - unit_no
      - name
      - office
  
  past_unit_heads:
    unique_id_fields:
      - unit_no
      - unit_type
      - name
      - installed
  
  joining_past_unit_heads:
    unique_id_fields:
      - unit_no
      - name
      - installed_in_current_unit
  
  honorary_members:
    unique_id_fields:
      - unit_type
      - unit_no
      - name
      - rank
```

### Implementation

The `GenerateUniqueId()` helper method:
1. Takes the field values in order
2. Concatenates them with hyphens as separator
3. Removes quotes and trims whitespace
4. Returns composite key string

This approach ensures:
- ✅ IDs are deterministic (same input = same ID)
- ✅ IDs are human-readable (can debug easily)
- ✅ IDs are efficient (simple string concatenation)
- ✅ Configuration is declarative (defined in YAML, not code)

### Future Enhancements

- Consider hashing IDs for shorter representations (e.g., SHA256 truncated to 16 chars)
- Track ID history across data versions for audit trails
- Implement deduplication rules (e.g., remove duplicates keeping most recent)
- Export unique IDs in validation reports

---

## �🔄 Parsing Strategy Options

### **Option A: Hard-Coded Column Indices**
```csharp
// Define column ranges for each section
var membersCols = new Range(0, 4);      // A-D
var officersCols = new Range(5, 9);    // F-I
var pastHeadsCols = new Range(10, 19);  // K-T
// ... etc

// Parse each section independently
foreach (var row in csvRows)
{
    var members = ParseMembers(row[membersCols]);
    var officers = ParseOfficers(row[officersCols]);
    // ... etc
}
```

**Pros:**
- Simple, deterministic parsing
- Fast performance
- Clear logic flow

**Cons:**
- Brittle — any column insertion/deletion breaks parsing
- Hard-coded magic numbers
- Difficult to maintain

### **Option B: YAML-Configured Column Ranges** (✅ Recommended)
```csharp
// Load column configuration from data source YAML
var columnConfig = LoadYamlColumnConfiguration();
// members.column_range = [0, 4]
// members.filters = [{ field: "Unit Type", value: "Craft" }]
// officers.column_range = [5, 9]
// ... etc

// Use YAML-configured ranges to parse data
foreach (var row in csvRows)
{
    var memberData = ExtractColumns(row, columnConfig["members"].ColumnRange);
    var officers = ExtractColumns(row, columnConfig["officers"].ColumnRange);
    
    // Apply filters
    if (MatchesFilters(memberData, columnConfig["members"].Filters))
    {
        // Process member
    }
}
```

**Pros:**
- Flexible — column ranges change without code changes
- Self-documenting via YAML
- Filters support dynamic unit type selection
- Maintainable — centralized configuration
- Easy to test with different data layouts
- Clear separation of config from logic

**Cons:**
- Requires YAML parsing
- More configuration files to maintain
- Need to validate YAML structure

### **Option C: Hybrid Approach** (Fallback)
1. Load configuration from YAML first
2. Fall back to header-based detection if YAML is missing
3. Cache the parsed structure for performance

---

## 📊 Data Model Changes Required

### Terminology Update: Past Masters → Past Unit Heads

**Rationale:** The current "PastMaster" terminology is Craft-specific. Different unit types have different titles:
- Craft: Past Master (PM)
- Royal Arch: Past Principal (PP)
- Mark: Past Mark Master (PMM)
- RAM: Past Commander (PC)
- KT: Past Commander
- etc.

**Generic terminology:** "Past Unit Head" encompasses all these roles.

### Updated SchemaUnit Structure (v1.5)
```csharp
public class SchemaUnit
{
    public string UnitType { get; set; }
    public int Number { get; set; }
    public string Name { get; set; }
    public List<SchemaOfficer> Officers { get; set; }
    public List<SchemaMember> Members { get; set; }
    public List<SchemaPastUnitHead> PastUnitHeads { get; set; }          // Renamed from PastMasters
    public List<SchemaJoiningPastUnitHead> JoiningPastUnitHeads { get; set; }  // Renamed from JoiningPastMasters
    public List<SchemaMember> HonoraryMembers { get; set; }
}
```

### Required Changes for v1.6 (with Generic Naming)

**Problem:** v1.6 distinguishes between "Past Unit Heads" and "Joining Past Unit Heads" with different fields, and current "PastMaster" terminology is Craft-specific

**Current mapping (v1.5):**
- Officers → `SchemaOfficer`
- Members → `SchemaMember`
- PastMasters → `SchemaPastMaster` (Craft-specific name)
- JoiningPastMasters → `SchemaPastMaster` (same type)
- HonoraryMembers → `SchemaMember`

**v1.6 Requirements (with generic terminology):**
```csharp
// Generic class names (not Craft-specific)
public class SchemaPastUnitHead
{
    public string UniqueId { get; set; }           // Generated from composite key
    public int UnitNumber { get; set; }
    public string Name { get; set; }
    public int Joined { get; set; }
    public int? Installed { get; set; }           // Can be multiple years
    public string ProvincialRank { get; set; }
    public int? DateRankAccorded { get; set; }
    public string GrandRank { get; set; }
    public int? GrandRankDateAccorded { get; set; }
}

public class SchemaJoiningPastUnitHead
{
    public string UniqueId { get; set; }           // Generated from composite key
    public int UnitNumber { get; set; }
    public List<int> OriginLodges { get; set; }   // Can be multiple lodges
    public int InstalledInCurrentUnit { get; set; }
    public string Name { get; set; }
    public int Joined { get; set; }               // Joined origin lodge
    public string ProvincialGrandRank { get; set; }  // Rank from origin lodge (e.g., PPJGW)
    public int? DateRankAccorded { get; set; }       // Date rank accorded in origin lodge
    public string GrandRank { get; set; }         // Grand rank in destination unit (e.g., PPGSwdB)
    public int? GrandRankDateAccorded { get; set; }  // Date grand rank accorded in destination unit
}

public class SchemaMember
{
    public string UniqueId { get; set; }           // Generated from composite key
    // Existing fields
    public string Name { get; set; }
    public int? JoinDate { get; set; }            // NEW: for regular members
    // ... existing fields ...
}
```

---

---

## 🔧 YAML-Based Column Configuration

This approach makes the data source YAML files the source of truth for column positions and filtering, eliminating hard-coded column indices from the codebase.

### Configuration Structure

```yaml
# document/data_sources/craft_data_source.yaml

# Source CSV file and format version
csv_source: "document/data/sections_v1.6.csv"
format_version: "1.6"
unit_type_filter: "Craft"

# Define column ranges and filters for each person type section
sections:
  members:
    column_range: [0, 4]              # Columns A-E (inclusive)
    headers:
      - unit_type
      - unit_no
      - name
      - join_date
    filters:
      - field: unit_type
        value: Craft
    # NEW: Define unique identifier for deduplication and change detection
    unique_id_fields:
      - unit_type
      - unit_no
      - name
      - join_date
  
  officers:
    column_range: [5, 9]              # Columns F-J
    headers:
      - unit_type
      - unit_no
      - name
      - office
    filters:
      - field: unit_type
        value: Craft
    unique_id_fields:
      - unit_type
      - unit_no
      - name
      - office
  
  past_unit_heads:
    column_range: [10, 19]            # Columns K-T (with empty col 19)
    headers:
      - unit_no
      - unit_type
      - name
      - joined
      - installed
      - provincial_rank
      - date_rank_accorded
      - grand_rank
      - grand_rank_date_accorded
    filters:
      - field: unit_type
        value: Craft
    unique_id_fields:
      - unit_no
      - unit_type
      - name
      - installed
  
  joining_past_unit_heads:
    column_range: [20, 29]            # Columns U-AD (with empty col 29)
    headers:
      - unit_no
      - origin_lodges               # Comma-separated list
      - installed_in_current_unit
      - unit_type
      - name
      - joined
      - provincial_grand_rank
      - date_rank_accorded
      - grand_rank
      - grand_rank_date_accorded
    filters:
      - field: unit_type
        value: Craft
    unique_id_fields:
      - unit_no
      - name
      - installed_in_current_unit
  
  honorary_members:
    column_range: [30, 33]            # Columns AE-AH
    headers:
      - unit_type
      - unit_no
      - name
      - rank
    filters:
      - field: unit_type
        value: Craft
    unique_id_fields:
      - unit_type
      - unit_no
      - name
      - rank
```

### Example: Royal Arch Configuration

```yaml
# document/data_sources/royalarch_data_source.yaml

csv_source: "document/data/sections_v1.6.csv"
format_version: "1.6"
unit_type_filter: "RA"

sections:
  members:
    column_range: [0, 4]
    filters:
      - field: unit_type
        value: RA
  
  officers:
    column_range: [5, 9]
    filters:
      - field: unit_type
        value: RA
  
  # ... other sections with RA filter
```

### Example: Knights Templar Configuration

```yaml
# document/data_sources/kt_data_source.yaml

csv_source: "document/data/sections_v1.6.csv"
format_version: "1.6"
unit_type_filter: "KT"

sections:
  members:
    column_range: [0, 4]
    filters:
      - field: unit_type
        value: KT
  
  officers:
    column_range: [5, 9]
    filters:
      - field: unit_type
        value: KT
  
  # ... other sections
```

### Benefits of YAML Configuration

✅ **No hard-coded column indices** in C# code  
✅ **Easy to adapt** if CSV columns move or change  
✅ **Per-degree filtering** built into configuration  
✅ **Headers documented** in YAML (self-documenting)  
✅ **Single source of truth** for column layout  
✅ **Easy to test** with different column arrangements  
✅ **Version support** — can have v1.5.yaml and v1.6.yaml separately  

### Loader Implementation with YAML Config

```csharp
public class ColumnarDataSourceConfig
{
    public string CsvSource { get; set; }
    public string FormatVersion { get; set; }      // "1.6"
    public string UnitTypeFilter { get; set; }     // "Craft", "RA", etc.
    public Dictionary<string, SectionConfig> Sections { get; set; }
}

public class SectionConfig
{
    public int[] ColumnRange { get; set; }         // [0, 4]
    public List<string> Headers { get; set; }      // ["unit_type", "unit_no", ...]
    public List<FilterConfig> Filters { get; set; }
    public List<string> UniqueIdFields { get; set; }  // NEW: Fields that compose unique ID
}

public class FilterConfig
{
    public string Field { get; set; }              // "unit_type"
    public string Value { get; set; }              // "Craft"
}

// Loader usage
public async Task<List<SchemaUnit>> LoadUnitsWithDataAsync(
    string dataSourceYamlPath,
    string? sectionId = null)
{
    // Load YAML configuration
    var config = LoadYamlConfig<ColumnarDataSourceConfig>(dataSourceYamlPath);
    
    // Detect and validate format
    if (config.FormatVersion == "1.6")
    {
        return await LoadV16ColumnarAsync(config, sectionId);
    }
    else
    {
        return await LoadV15RowBasedAsync(config, sectionId);
    }
}

private async Task<List<SchemaUnit>> LoadV16ColumnarAsync(
    ColumnarDataSourceConfig config,
    string? sectionId)
{
    var parser = new ColumnarCsvParser();
    var units = new Dictionary<int, SchemaUnit>();
    
    var lines = await File.ReadAllLinesAsync(config.CsvSource);
    
    // Skip header rows (rows 0-1)
    for (int i = 2; i < lines.Length; i++)
    {
        var row = lines[i].Split(',');
        
        // Parse each section using YAML-configured column ranges
        foreach (var (sectionName, sectionConfig) in config.Sections)
        {
            var sectionData = ExtractColumnRange(
                row, 
                sectionConfig.ColumnRange);
            
            // Apply filters
            if (!MatchesFilters(sectionData, sectionConfig.Headers, sectionConfig.Filters))
                continue;
            
            // Parse section data
            switch (sectionName)
            {
                case "members":
                    ParseMembersSection(sectionData, sectionConfig.Headers, sectionConfig.UniqueIdFields, units);
                    break;
                case "officers":
                    ParseOfficersSection(sectionData, sectionConfig.Headers, sectionConfig.UniqueIdFields, units);
                    break;
                case "past_unit_heads":
                    ParsePastUnitHeadsSection(sectionData, sectionConfig.Headers, sectionConfig.UniqueIdFields, units);
                    break;
                case "joining_past_unit_heads":
                    ParseJoiningPastUnitHeadsSection(sectionData, sectionConfig.Headers, sectionConfig.UniqueIdFields, units);
                    break;
                case "honorary_members":
                    ParseHonoraryMembersSection(sectionData, sectionConfig.Headers, sectionConfig.UniqueIdFields, units);
                    break;
            }
        }
    }
    
    return units.Values.ToList();
}

// Helper: Extract columns based on range from YAML
private string[] ExtractColumnRange(string[] row, int[] columnRange)
{
    var start = columnRange[0];
    var end = columnRange[1];
    return row[start..(end + 1)];
}

// Helper: Check if row matches all filters
private bool MatchesFilters(
    string[] sectionData,
    List<string> headers,
    List<FilterConfig> filters)
{
    foreach (var filter in filters)
    {
        var fieldIndex = headers.IndexOf(filter.Field);
        if (fieldIndex < 0 || fieldIndex >= sectionData.Length)
            return false;
        
        if (sectionData[fieldIndex] != filter.Value)
            return false;
    }
    return true;
}

// NEW: Helper to generate unique ID from composite key fields
private string GenerateUniqueId(
    string[] sectionData,
    List<string> headers,
    List<string> uniqueIdFields)
{
    var idParts = new List<string>();
    
    foreach (var fieldName in uniqueIdFields)
    {
        var fieldIndex = headers.IndexOf(fieldName);
        if (fieldIndex >= 0 && fieldIndex < sectionData.Length)
        {
            var value = sectionData[fieldIndex].Trim();
            if (!string.IsNullOrEmpty(value))
            {
                // Remove quotes if present
                value = value.Trim('"');
                idParts.Add(value);
            }
        }
    }
    
    // Composite key: "Craft-137-Howard D-1964"
    return string.Join("-", idParts);
}

// Example: Parse members section with unique ID generation
private void ParseMembersSection(
    string[] sectionData,
    List<string> headers,
    List<string> uniqueIdFields,
    Dictionary<int, SchemaUnit> units)
{
    var unitTypeIdx = headers.IndexOf("unit_type");
    var unitNoIdx = headers.IndexOf("unit_no");
    var nameIdx = headers.IndexOf("name");
    var joinDateIdx = headers.IndexOf("join_date");
    
    if (unitTypeIdx < 0 || unitNoIdx < 0 || nameIdx < 0)
        return; // Skip if required fields missing
    
    var unitType = sectionData[unitTypeIdx].Trim();
    var unitNoStr = sectionData[unitNoIdx].Trim();
    var name = sectionData[nameIdx].Trim('"');
    var joinDateStr = joinDateIdx >= 0 ? sectionData[joinDateIdx].Trim() : null;
    
    if (!int.TryParse(unitNoStr, out var unitNo) || string.IsNullOrEmpty(name))
        return;
    
    // Generate unique ID
    var uniqueId = GenerateUniqueId(sectionData, headers, uniqueIdFields);
    
    // Get or create unit
    if (!units.TryGetValue(unitNo, out var unit))
    {
        unit = new SchemaUnit { Number = unitNo, UnitType = unitType };
        units[unitNo] = unit;
    }
    
    // Create member
    var member = new SchemaMember
    {
        UniqueId = uniqueId,
        Name = name,
        JoinDate = int.TryParse(joinDateStr, out var year) ? year : null
    };
    
    unit.Members.Add(member);
}
```
```
CSV Row → CsvHelper.Deserialize → SchemaUnit object
    ↓
SchemaDataLoader.LoadUnitsWithDataAsync()
    ↓
Filtered by unit type in YAML data source
    ↓
SchemaPdfRenderer renders pages
```

### Required v1.6 Flow

```
CSV Header Rows (Row 1-2) → Parse section structure
    ↓ (cache structure)
    ↓
CSV Data Rows (Row 3+) → Extract per-section column ranges
    ↓
Section parsers (Members, Officers, Past Heads, etc.) →  Convert to domain objects
    ↓
SchemaUnit aggregation → Merge all person types
    ↓
Filter by unit type → Apply YAML data source filtering
    ↓
SchemaPdfRenderer renders pages
```

### New Classes Needed

```csharp
namespace MasonicCalendar.Core.Loaders;

/// <summary>
/// Represents a column section in the v1.6 columnar CSV format
/// </summary>
public class ColumnSection
{
    public string SectionName { get; set; }              // "Members", "Officers", etc.
    public int StartColumnIndex { get; set; }            // e.g., 0
    public int EndColumnIndex { get; set; }              // e.g., 4
    public List<string> ColumnHeaders { get; set; }      // ["Unit Type", "Unit No", "Name", "Join Date"]
    public SectionType SectionType { get; set; }         // Members, Officers, PastHeads, JoiningPastHeads, Honorary
}

public enum SectionType
{
    Members,
    Officers,
    PastUnitHeads,
    JoiningPastUnitHeads,
    HonoraryMembers
}

/// <summary>
/// Parses v1.6 columnar CSV format
/// </summary>
public class ColumnarCsvParser
{
    /// <summary>
    /// Parse header rows to detect section boundaries and column names
    /// </summary>
    public List<ColumnSection> ParseSectionStructure(string[] headerRows);
    
    /// <summary>
    /// Extract columns for a specific section from a data row
    /// </summary>
    public List<string> ExtractSectionData(string[] dataRow, ColumnSection section);
    
    /// <summary>
    /// Parse a single section's data into domain objects
    /// </summary>
    public List<T> ParseSectionData<T>(List<List<string>> sectionData, ColumnSection section) 
        where T : class;
}
```

---

## 🔧 Loader Implementation Changes

### `SchemaDataLoader.cs`

**Changes needed:**

1. **Detect format version** when loading:
   ```csharp
   private async Task<SchemaDataFormatVersion> DetectFormatAsync(string csvPath)
   {
       var firstRow = await ReadFirstRowAsync(csvPath);
       
       // v1.5: "Unit Type" at column 0, standard layout
       // v1.6: "Members" section header, columnar layout
       
       if (firstRow.Contains("Members,,,,,Officers,"))
           return SchemaDataFormatVersion.V16Columnar;
       else
           return SchemaDataFormatVersion.V15RowBased;
   }
   ```

2. **Add format-specific parsers:**
   ```csharp
   public async Task<List<SchemaUnit>> LoadUnitsWithDataAsync(
       string templateKey, 
       string? sectionId = null)
   {
       var format = await DetectFormatAsync(csvPath);
       
       return format switch
       {
           SchemaDataFormatVersion.V15RowBased => await LoadV15Async(csvPath, sectionId),
           SchemaDataFormatVersion.V16Columnar => await LoadV16Async(csvPath, sectionId),
           _ => throw new NotSupportedException($"Format {format} not supported")
       };
   }
   ```

3. **Implement v1.6 specific loader:**
   ```csharp
   private async Task<List<SchemaUnit>> LoadV16Async(string csvPath, string? sectionId)
   {
       // Read header rows
       var lines = await File.ReadAllLinesAsync(csvPath);
       var headerRows = new[] { lines[0], lines[1] };
       
       // Parse column structure
       var parser = new ColumnarCsvParser();
       var sections = parser.ParseSectionStructure(headerRows);
       
       // Build unit dictionary (Unit No → SchemaUnit)
       var units = new Dictionary<int, SchemaUnit>();
       
       // Parse data rows
       for (int i = 2; i < lines.Length; i++)
       {
           var row = lines[i].Split(',');
           
           // Process each section
           foreach (var section in sections)
           {
               var sectionData = parser.ExtractSectionData(row, section);
               
               switch (section.SectionType)
               {
                   case SectionType.Members:
                       ParseMembersSection(sectionData, units);
                       break;
                   case SectionType.Officers:
                       ParseOfficersSection(sectionData, units);
                       break;
                   // ... etc for other sections
               }
           }
       }
       
       return units.Values.ToList();
   }
   ```

---

## 🎨 Template Changes

### Update `unit-page.html` Template

The template should use generic "past_unit_heads" and "joining_past_unit_heads" rather than Craft-specific "past_masters":

```scriban
{# Members #}
{{ unit.members | sort: 'join_date' }}

{# Officers #}
{{ unit.officers | sort: 'pos_no' }}

{# NEW: Past unit heads (generic term for PM, PP, PMM, PC, etc.) #}
{{ unit.past_unit_heads | sort: 'installed' }}

{# NEW: Joining past unit heads (from other lodges) #}
{{ unit.joining_past_unit_heads | sort: 'joined' }}

{# Honorary members #}
{{ unit.honorary_members | sort: 'rank' }}
```

This approach allows the same template to work for all degree types without Craft-specific naming.

---

## 📝 Supporting 11 Degree Types

### New Sections Needed in master_v1.yaml

```yaml
sections:
  # Existing (Craft, RA, Mark, RAM)
  - section_id: "craft_units"
    type: "data-driven"
    ...
  
  # NEW: Knights Templar
  - section_id: "kt_intro"
    type: "static"
    template: "companion/kt-introduction.html"
  
  - section_id: "kt_membership_summary"
    type: "membership-summary"
    ...
  
  - section_id: "kt_units"
    type: "data-driven"
    data_mapping: "data_sources/kt_data_source.yaml"
  
  # NEW: Red Cross of Constantine
  - section_id: "rcoc_intro"
    # ... similar structure
  
  # ... etc for OSC, PBQ, KTP, STOA
```

### New Data Source Files Needed

- `kt_data_source.yaml`
- `ktp_data_source.yaml`
- `osc_data_source.yaml`
- `pbq_data_source.yaml`
- `rcoc_data_source.yaml`
- `stoa_data_source.yaml`

Each with format:
```yaml
csv_paths:
  - document/data/sections_v1.6.csv

unit_type: "KT"  # Filter for this degree

columns:
  unit_number: "Unit No"
  unit_name: "Name"
  officers:
    name: "Name"
    position: "Office"
  # ... etc
```

---

## ✅ Implementation Checklist

### Phase 1: Infrastructure
- [ ] Add format detection to `SchemaDataLoader`
- [ ] Create `ColumnarCsvParser` class
- [ ] Define `ColumnSection` and `SectionType`
- [ ] Create `SectionConfig` with `UniqueIdFields` property
- [ ] Implement unique ID generation from composite key fields
- [ ] Add unit tests for header parsing
- [ ] Add unit tests for column extraction
- [ ] Add unit tests for unique ID generation

### Phase 2: Data Model
- [ ] Create `SchemaPastUnitHead` class
- [ ] Create `SchemaJoiningPastUnitHead` class
- [ ] Update `SchemaMember` to include `JoinDate`
- [ ] Update `SchemaUnit` to include new lists
- [ ] Update field mapping documentation

### Phase 3: Parsing Logic
- [ ] Implement `ParseMembersSection()`
- [ ] Implement `ParseOfficersSection()`
- [ ] Implement `ParsePastUnitHeadsSection()`
- [ ] Implement `ParseJoiningPastUnitHeadsSection()`
- [ ] Implement `ParseHonoraryMembersSection()`
- [ ] Handle multiple-year fields (e.g., "1998, 2006")
- [ ] Handle multiple-lodge fields (e.g., "5848, 6525")

### Phase 4: Integration
- [ ] Update `SchemaDataLoader.LoadUnitsWithDataAsync()`
- [ ] Add v1.6 format support to factory methods
- [ ] Test end-to-end with sample data

### Phase 5: Templates & Output
- [ ] Update `unit-page.html` for new fields
- [ ] Create templates for new degree types
- [ ] Add intro pages for new degrees
- [ ] Update `master_v1.yaml` with new sections

### Phase 6: Validation & Testing
- [ ] Comprehensive CSV validation
- [ ] Test with all 11 degree types
- [ ] Performance testing (large datasets)
- [ ] Regression testing for v1.5 compatibility

---

## ⚡ Performance Considerations

### Parsing Performance
- **v1.5:** CsvHelper direct deserialization (fast)
- **v1.6:** Custom column-based parsing (potentially slower)

**Optimization strategies:**
1. Cache parsed section structure (parse headers once)
2. Use indexer-based access instead of LINQ
3. Pre-allocate lists based on row count
4. Consider parallel parsing for different sections

### Memory Impact
- v1.6 requires holding 5 different person type lists per unit
- Larger SchemaUnit objects
- Potential for memory bloat with 11 degrees

**Mitigation:**
- Use lazy loading for person lists
- Consider streaming parsing for very large files
- Profile memory usage before implementation

---

## 🚨 Risk Assessment

| Risk | Likelihood | Severity | Mitigation |
|------|-----------|----------|-----------|
| Column index misalignment | Medium | High | Unit tests for header parsing, regression tests |
| Breaking v1.5 compatibility | Medium | High | Feature flag or dual-format support |
| Performance degradation | Medium | Medium | Profiling, caching, optimization |
| Field semantic confusion (e.g., "Rank" meaning) | High | Medium | Clarify with data provider, document assumptions |
| New degree template issues | Low | Medium | Templating review, visual regression tests |
| Multiple-value field parsing errors | Medium | Medium | Comprehensive unit tests for edge cases |

---

## 📌 Recommendations

1. **Use YAML-configured column ranges** — Define column indices in data source YAML files, not in code
   - Craft: `column_range: [0, 4]` for members, `[5, 9]` for officers, etc.
   - Same column ranges work for all degree types (just different filters)
   - Easy to adapt if CSV structure changes

2. **Configure unique ID generation in YAML** — Define which fields compose the unique identifier per section
   - Members: `unique_id_fields: [unit_type, unit_no, name, join_date]`
   - Officers: `unique_id_fields: [unit_type, unit_no, name, office]`
   - Past Unit Heads: `unique_id_fields: [unit_no, unit_type, name, installed]`
   - Joining Past Unit Heads: `unique_id_fields: [unit_no, name, installed_in_current_unit]`
   - Honorary Members: `unique_id_fields: [unit_type, unit_no, name, rank]`
   - Enables deduplication, change detection, and data validation

3. **Generic terminology** — Use `past_unit_heads` and `joining_past_unit_heads` throughout
   - Works for all degrees: PM (Craft), PP (RA), PMM (Mark), PC (RAM), etc.
   - Update templates to use generic names
   - More flexible for future degrees

3. **Support both v1.5 and v1.6 simultaneously**
   - Auto-detect format at load time
   - Use separate YAML files: `craft_data_source_v1.5.yaml` and `craft_data_source_v1.6.yaml`
   - OR use single YAML with `format_version` field

4. **Incremental rollout**
   - Start with v1.6 parsing logic (columnar + YAML config)
   - Test with Craft and RA (existing degrees)
   - Add new degrees (KT, RCOC, etc.) incrementally
   - Maintain v1.5 support as fallback

5. **Extensive testing**
   - Unit tests for YAML parsing and column extraction
   - Integration tests for each degree type
   - Edge case tests for comma-separated values (years, lodges)
   - Regression tests for v1.5 compatibility

6. **Documentation**
   - Document YAML structure and column ranges
   - Clarify semantics of "Rank" field in joining_past_unit_heads
   - Document degree-specific officer titles vs. generic terms
   - Add examples for each degree type

7. **Clarify ambiguities with data provider**
   - What does "Rank" field represent in joining_past_unit_heads?
   - Should multiple years/lodges be parsed as separate records or kept as comma-separated strings?
   - Are all 11 degrees fully populated with data?

---

## 📖 Examples for Reference

### Example: Member Entry
```
Members Section: Craft,137,"Howard, D  ",1964
→ SchemaMember { Name="Howard, D", JoinDate=1964, UnitType="Craft", UnitNumber=137 }
```

### Example: Officer Entry
```
Officers Section: Craft,137,"White, N J  ",WM
→ SchemaOfficer { Name="White, N J", Position="WM", UnitType="Craft", UnitNumber=137 }
```

### Example: Past Unit Head Entry
```
Past Unit Heads Section: 137,Craft,"Howard, D   ",1964,1978,PProvSGW,2005,,
→ SchemaPastUnitHead { 
    UnitNumber=137, 
    Name="Howard, D", 
    Joined=1964, 
    Installed=1978, 
    ProvincialRank="PProvSGW",
    DateRankAccorded=2005
  }
```

### Example: Joining Past Unit Head Entry
```
Joining Section: 137,"5848, 6525",1987,Craft,"Shorto, R J  ",1959,PPJGW,2019,PPGSwdB,2025
→ SchemaJoiningPastUnitHead {
    UnitNumber=137,
    OriginLodges=[5848, 6525],
    InstalledInCurrentUnit=1987,
    Name="Shorto, R J",
    Joined=1959,
    ProvincialGrandRank="PPJGW",           // From origin lodge
    DateRankAccorded=2019,                 // Date accorded in origin lodge
    GrandRank="PPGSwdB",                   // Grand rank in destination unit
    GrandRankDateAccorded=2025             // Date accorded in destination unit
  }
```

**Two rank sources:**
- **ProvincialGrandRank (PPJGW)** — Rank held in the **origin lodge** (from which member came)
- **GrandRank (PPGSwdB)** — Grand rank held in the **destination unit** (current unit)

---

## 🔗 Related Files to Review

- `src/MasonicCalendar.Core/Loaders/SchemaDataLoader.cs` — Current v1.5 parser
- `src/MasonicCalendar.Core/Domain/SchemaUnit.cs` — Current data model
- `document/data_sources/craft_data_source.yaml` — Current data mapping example
- `document/templates/unit-page.html` — Current template structure
- `document/master_v1.yaml` — Current document structure

---

## ❓ Open Questions

1. **Multiple installed years:** How should "1998, 2006" be represented? 
   - Option A: Parse into separate records (one per year)?
   - Option B: Keep as single comma-separated string?
   - Option C: Store as List<int>?
2. **Multiple origin lodges:** Should "5848, 6525" be stored as:
   - Option A: List<int> (recommended)?
   - Option B: Comma-separated string?
3. **Officer titles per degree:** Do officer titles standardize across degrees? (e.g., is "WM" only in Craft, or does it appear in Mark/RAM too?)
4. **Unit data:** Where is unit metadata (name, location, meeting dates, warrants, installation dates) stored in v1.6? In a separate file, or missing from sections_v1.6.csv?
5. **New degree support timeline:** Should all 11 degrees be supported immediately, or roll out incrementally?
6. **Backward compatibility:** Should v1.5 CSV support be maintained indefinitely, or sunset at a specific date?
7. **Data availability:** Are all 11 degree types fully populated in sections_v1.6.csv, or is some data incomplete?
