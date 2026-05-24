# v1.7 Format Analysis: Extended Provincial & London Ranks

**Date:** May 18, 2026  
**Status:** Analysis Complete  
**Format:** Row-based CSV (no columnar change from v1.6)  
**New Columns:** 4 additional rank columns  
**Applicable To:** Members, Honorary Members, Officers  
**Scope:** All degree types (Craft, Royal Arch, Mark, RAM, RCOC)

---

## CRITICAL: Rank Display Logic (Must Implement)

**This requirement changes how ranks are displayed in ALL templates and must be understood before implementation.**

### Priority-Based Rank Display Rules

1. **IF** member has `Grand Rank` → **Display ONLY the Grand Rank in bold**, all other ranks are hidden
2. **ELSE** (no Grand Rank) → Display in this exact order on separate lines:
   - `Provincial Rank` (Dorset) with `Date Accorded` in brackets
   - `Prov Rank Oth Prov` (Other Province) with `OP Date Accorded` in brackets
   - `Lndn Rank` (London) with `LR Date Accorded` in brackets
   - (Empty ranks are omitted — no blank lines)

### Why This Matters

- **Current behavior (v1.5):** All ranks are shown at once → cluttered display
- **New behavior (v1.7):** Grand Rank takes precedence, other ranks only show when Grand Rank is absent
- **Template change:** Scriban `if/else` logic in `_data-driven/unit-page.html` applies the priority rules

### Implementation Location

**Primary:** `document/templates/_data-driven/unit-page.html`
- Member rank display section
- Honorary member rank display section  
- Officer rank display section

**Secondary:** `document/templates/print.css`
- Add multi-line spacing between rank lines
- Styling for London rank (distinct color/formatting)

---

## 1. Dataset Overview

### File: `membership_v1.7.csv`

**Inheritance:** Extends v1.6 format with 4 new columns  
**Columns in v1.7:** 27 (vs 23 in v1.6)  
**Degree Types in v1.7:** 14 (vs 5 in v1.6: Craft, Mark, RA, RAM, RCOC)  
**New Degree Types:** AMD, KT, KTP, OSC, OSM, PBQ, RC, RSM, STOA (9 new degrees, no YAML yet)

**Column Structure:**
```
0  Unique Ref
1  Unit Type
2  ConCatLU
3  Unit
4  Pos No
5  OffPos
6  Office
7  Mem Type
8  Name
9  Join Date
10 Provincial Rank
11 Date Accorded
12 Active Office
13 [NEW] Prov Rank Oth Prov          ← New column
14 [NEW] OP Date Accorded            ← New column
15 [NEW] Lndn Rank                   ← New column
16 [NEW] LR Date Accorded            ← New column
17 Grand Rank
18 GR Date Accorded
19 LC No
20 OffCode
21 Join Unit
22 Past Mstr Of
23 Installed
24 First Date in Chair
25 Last Installation
```

**New Columns Total:** 4  
**Columns Added:** 13-16

---

## 1.5 Degree Types in v1.7 CSV

**Existing Degrees (v1.6+) - YAML Data Sources Exist:**
1. Craft - `craft_data_source.yaml` ✓
2. Mark - `mark_data_source.yaml` ✓
3. RA (Royal Arch) - `royalarch_data_source.yaml` ✓
4. RAM (Royal Ark Mariners) - `ram_data_source.yaml` ✓
5. RCOC - `rcoc_data_source.yaml` ✓

**New in v1.7 (9 degrees) - NO YAML Data Sources Yet:**
6. AMD (Allied Masonic Degrees)
7. KT (Knight Templar)
8. KTP (Knight Templar Priests)
9. OSC (Order of Scotland)
10. OSM (Order of the Secret Monitor)
11. PBQ (Precious Brothers' Quadrant)
12. RC (Royal Craft)
13. RSM (Royal and Select Masters)
14. STOA (Societas Trinitatis in Omni Aeterna)

**Implementation Strategy:**
- **Phase 1-5 (Initial):** Implement rank display logic using Craft units only (YAML already exists)
- **Phase 6 Testing:** Validate Craft rendering with membership_v1.7.csv data
- **Post-Launch:** Create YAML files for remaining 9 degrees (same parsing logic, degree-specific filters)

---

## 2. New Column Specifications

### Column 13: `Prov Rank Oth Prov` (Provincial Rank from Other Province)

**Type:** String (nullable)  
**Format:** `"{Rank Abbreviation} ({Province Name})"` OR `"{Rank Abbreviation} ({Province Name})"` with range  
**Examples:**
- `"PProvSGW (Hants. & I. of W.)"`
- `"PProvJGW (Surrey)"`
- `"PProvGStwd (Middx.)"`
- `"PProvSGD (Somerset)"`
- `"PProvGStB (Hants. & I. of W.)"`

**Applicable To:**
- Regular members (Mem type)
- Honorary members (Hon type)
- Officers (Off type)

**Usage Context:**
Shows provincial rank held in a **different** province (not Dorset). Example: a Dorset member who holds a PP rank in Hampshire lodge.

**Presence:** ~10-15% of all members have this field populated  
**Parsing Rules:**
- Extract rank abbreviation from before first `(`
- Extract province name from within parentheses
- Some entries may have year range format (e.g., `"1993-15"` means 1993-2015)

---

### Column 14: `OP Date Accorded` (Other Province Date Accorded)

**Type:** Integer or String (nullable, flexible format)  
**Format:** 
- Single year: `"2021"`
- Year range: `"1993-15"` (1993-2015)
- Text: `"2021"` (just the year)

**Examples:**
- `"2021"` (Single year)
- `"2016"` (Single year)
- `"1992"` (Single year)
- `"1993-15"` (Range: 1993 to 2015)

**Applicable To:**
- Same as `Prov Rank Oth Prov` (paired column)

**Usage Context:**
Date or date range when the provincial rank from another province was accorded.

**Parsing Rules:**
- Check for hyphen to detect range format
- If range, store both start and end years
- If single value, store as year
- Handle both year and full date formats gracefully

---

### Column 15: `Lndn Rank` (London Rank)

**Type:** String (nullable)  
**Format:** Rank abbreviation (typically 2-4 characters)  
**Examples:**
- `"LGR"` (London Grand Rank - appears in ~5 records)
- Others expected but not yet seen in sample

**Applicable To:**
- Regular members (Mem type)
- Honorary members (Hon type)

**Usage Context:**
Rank held at the **Grand Lodge of England** level (as distinct from provincial or Dorset lodge level).

**Presence:** Very rare (~3-5 entries per 500+ members)

**Note:** This is a London/Grand level rank, not provincial. May require special display treatment.

---

### Column 16: `LR Date Accorded` (London Rank Date Accorded)

**Type:** Integer (nullable)  
**Format:** Year  
**Examples:**
- `"2011"`
- `"1984"`
- `"2018"`

**Applicable To:**
- Same as `Lndn Rank` (paired column)

**Usage Context:**
Date when the London rank was accorded.

**Parsing Rules:**
- Parse as year only
- May need special formatting in display (e.g., just show year, not full date)

---

## 3. Data Patterns & Examples

### Example 1: Member with Multiple Provincial Ranks (Different Provinces)

**Member:** Burt, M R (Craft Lodge 137, Member)
```
Provincial Rank:     PPSGW
Date Accorded:       2019
Prov Rank Oth Prov:  PProvSGW (Hants. & I. of W.)
OP Date Accorded:    2021
Lndn Rank:           [empty]
LR Date Accorded:    [empty]
Grand Rank:          [empty]
GR Date Accorded:    [empty]
```

**Interpretation:** 
- Holds PPSGW in Dorset since 2019
- Also holds PProvSGW in Hampshire & IoW since 2021
- No London or Grand rank

---

### Example 2: Member with London Rank

**Member:** Wells, B P (Craft Lodge 137, Member)
```
Provincial Rank:     [empty]
Date Accorded:       [empty]
Prov Rank Oth Prov:  PProvJGD (Northants. & Hunts.)
OP Date Accorded:    2017
Lndn Rank:           LGR
LR Date Accorded:    2011
Grand Rank:          [empty]
GR Date Accorded:    [empty]
```

**Interpretation:**
- Holds PProvJGD in Northamptonshire & Huntingdonshire since 2017
- Holds London Grand Rank (LGR) since 2011
- No Dorset provincial rank, no Grand rank

---

### Example 3: Honorary Member with Other Province Rank

**Member:** Harris, W J W (Craft Lodge 170, Honorary)
```
Mem Type:            Hon
Provincial Rank:     PPJGW
Date Accorded:       2009
Prov Rank Oth Prov:  PProvGStB (Hants. & I. of W.)
OP Date Accorded:    1982
Lndn Rank:           [empty]
LR Date Accorded:    [empty]
```

**Interpretation:**
- Honorary member in Dorset
- Holds PPJGW in Dorset since 2009
- Also holds PProvGStB in Hampshire & IoW since 1982
- No London or Grand rank

---

### Example 4: Royal Arch Member with Other Province Rank

**Member:** Poole, J S (Royal Arch Chapter 137, Member)
```
Unit Type:           RA
Provincial Rank:     PPDepGDC
Date Accorded:       2024
Prov Rank Oth Prov:  PProvGStwd (Middx.)
OP Date Accorded:    1993-15
Lndn Rank:           [empty]
LR Date Accorded:    [empty]
```

**Interpretation:**
- Royal Arch member with provincial rank
- Held PProvGStwd in Middlesex from 1993-2015
- Now holds PPDepGDC in Dorset (from 2024)

---

## 4. Domain Model Updates Required

### New/Updated Classes

#### SchemaMember (extends v1.6)

```csharp
public class SchemaMember
{
    public string UniqueId { get; set; }           // Generated composite key
    public int UnitNumber { get; set; }
    public string Name { get; set; }
    public int JoinDate { get; set; }              // Year joined
    
    // v1.6 properties
    public string ProvincialRank { get; set; }     // Dorset provincial rank
    public int? DateRankAccorded { get; set; }
    public string GrandRank { get; set; }
    public int? GrandRankDateAccorded { get; set; }
    
    // [NEW in v1.7] Other Province Provincial Rank
    public string ProvRankOtherProv { get; set; }      // e.g., "PProvSGW (Hants. & I. of W.)"
    public string OpDateAccorded { get; set; }         // Single year or range (e.g., "2021" or "1993-15")
    public int? OpDateStartYear { get; set; }          // Parsed: start of range or single year
    public int? OpDateEndYear { get; set; }            // Parsed: end of range (if applicable)
    
    // [NEW in v1.7] London Rank
    public string LondonRank { get; set; }             // e.g., "LGR"
    public int? LondonRankDateAccorded { get; set; }
}
```

**New Properties Breakdown:**
- **`ProvRankOtherProv`:** Raw string from CSV including province name
- **`OpDateAccorded`:** Raw string (flexible format: year or range)
- **`OpDateStartYear`:** Parsed start year (for calculations/sorting)
- **`OpDateEndYear`:** Parsed end year (if range detected)
- **`LondonRank`:** Raw rank abbreviation
- **`LondonRankDateAccorded`:** Parsed year

---

#### SchemaHonoraryMember (extends v1.6)

```csharp
public class SchemaHonoraryMember
{
    public string UniqueId { get; set; }
    public int UnitNumber { get; set; }
    public string Name { get; set; }
    
    // v1.6 properties
    public string Rank { get; set; }
    public int? RankDateAccorded { get; set; }
    public string GrandRank { get; set; }
    public int? GrandRankDateAccorded { get; set; }
    
    // [NEW in v1.7] Other Province Rank
    public string ProvRankOtherProv { get; set; }
    public string OpDateAccorded { get; set; }
    public int? OpDateStartYear { get; set; }
    public int? OpDateEndYear { get; set; }
    
    // [NEW in v1.7] London Rank
    public string LondonRank { get; set; }
    public int? LondonRankDateAccorded { get; set; }
}
```

---

#### SchemaOfficer (no changes required)

Officers in v1.7 may have the new rank columns populated. The `SchemaOfficer` class already includes all necessary rank fields:
- `ProvincialRank`, `DateAccorded`, `GrandRank`, `GrandRankDateAccorded`

**Add to SchemaOfficer:**
```csharp
// [NEW in v1.7] Other Province Rank
public string ProvRankOtherProv { get; set; }
public string OpDateAccorded { get; set; }
public int? OpDateStartYear { get; set; }
public int? OpDateEndYear { get; set; }

// [NEW in v1.7] London Rank
public string LondonRank { get; set; }
public int? LondonRankDateAccorded { get; set; }
```

---

## 5. Data Loading Changes

### CsvHelper Column Mapping (Data Sources)

Update `craft_data_source.yaml`, `royalarch_data_source.yaml`, `mark_data_source.yaml`, `ram_data_source.yaml`:

```yaml
column_mappings:
  members:
    - index: 0
      name: Unique Ref
    - index: 1
      name: Unit Type
    # ... existing columns ...
    - index: 13
      name: Prov Rank Oth Prov
      target_field: prov_rank_other_prov
    - index: 14
      name: OP Date Accorded
      target_field: op_date_accorded
    - index: 15
      name: Lndn Rank
      target_field: london_rank
    - index: 16
      name: LR Date Accorded
      target_field: london_rank_date_accorded
```

### SchemaDataLoader Updates

**In `SchemaDataLoader.cs` - ParseMembersFromCsv():**

```csharp
// After parsing existing fields...

// [NEW v1.7] Parse other province rank fields
var provRankOtherProv = GetCsvValue(record, 13, dataSource);
var opDateAccordedStr = GetCsvValue(record, 14, dataSource);

// Parse date range format: "1993-15" → (1993, 2015)
var (opDateStart, opDateEnd) = ParseDateRange(opDateAccordedStr);

// [NEW v1.7] Parse London rank fields
var londonRank = GetCsvValue(record, 15, dataSource);
var londonRankDateStr = GetCsvValue(record, 16, dataSource);
var londonRankDate = int.TryParse(londonRankDateStr, out int lrd) ? lrd : (int?)null;

// Assign to member object
member.ProvRankOtherProv = provRankOtherProv;
member.OpDateAccorded = opDateAccordedStr;
member.OpDateStartYear = opDateStart;
member.OpDateEndYear = opDateEnd;
member.LondonRank = londonRank;
member.LondonRankDateAccorded = londonRankDate;
```

**Helper Function for Date Range Parsing:**

```csharp
private static (int?, int?) ParseDateRange(string dateStr)
{
    if (string.IsNullOrWhiteSpace(dateStr))
        return (null, null);
    
    // Check for range format: "1993-15" or "1993-2015"
    if (dateStr.Contains("-"))
    {
        var parts = dateStr.Split('-');
        if (parts.Length == 2)
        {
            if (int.TryParse(parts[0], out int start))
            {
                // Handle abbreviated end year: "15" → "2015"
                var endStr = parts[1];
                if (endStr.Length == 2)
                    endStr = start.ToString().Substring(0, 2) + endStr;
                
                if (int.TryParse(endStr, out int end))
                    return (start, end);
            }
        }
    }
    
    // Single year
    if (int.TryParse(dateStr, out int single))
        return (single, null);
    
    return (null, null);
}
```

---

## 6. Rank Display Logic (CRITICAL)

### Display Priority Rules

**Priority 1 (Highest):** If `Grand Rank` exists → **Display ONLY Grand Rank in bold**, ignore all other ranks

**Priority 2:** If NO `Grand Rank` → Display in this exact sequence:
1. `Provincial Rank` (Dorset) with `Date Accorded` in brackets
2. `Prov Rank Oth Prov` with `OP Date Accorded` in brackets (on new line)
3. `Lndn Rank` with `LR Date Accorded` in brackets (on new line)

Each rank pair is on its own line. Empty ranks are omitted.

---

### Display Examples

#### Example A: Member with Grand Rank
```
Data:
  Grand Rank: PPGSwdB
  Grand Rank Date: 2025
  Provincial Rank: PPSGW
  Provincial Date: 2019
  Prov Rank Oth Prov: PProvSGW (Hants. & I. of W.)
  OP Date Accorded: 2021

Display Output:
  **PPGSwdB** (2025)

Note: All other ranks are HIDDEN because Grand Rank exists
```

#### Example B: Member with NO Grand Rank (Multiple Provincial Ranks)
```
Data:
  Grand Rank: [empty]
  Provincial Rank: PPSGW
  Provincial Date: 2019
  Prov Rank Oth Prov: PProvSGW (Hants. & I. of W.)
  OP Date Accorded: 2021
  Lndn Rank: [empty]

Display Output:
  PPSGW (2019)
  PProvSGW (Hants. & I. of W.) (2021)
```

#### Example C: Member with NO Grand Rank, Multiple Ranks Including London
```
Data:
  Grand Rank: [empty]
  Provincial Rank: [empty]
  Prov Rank Oth Prov: PProvJGD (Northants. & Hunts.)
  OP Date Accorded: 2017
  Lndn Rank: LGR
  LR Date Accorded: 2011

Display Output:
  PProvJGD (Northants. & Hunts.) (2017)
  LGR (2011)
```

#### Example D: Member with NO Grand Rank, Dorset Provincial Only
```
Data:
  Grand Rank: [empty]
  Provincial Rank: PPJGW
  Provincial Date: 2024
  Prov Rank Oth Prov: [empty]
  Lndn Rank: [empty]

Display Output:
  PPJGW (2024)
```

---

## 7. Template Changes

### Unit Page Template (`_data-driven/unit-page.html`)

#### Members Section - Extended Rank Display

**Current Layout:**
```html
<td class="member-name">{{ member.name }}</td>
<td class="member-rank">{{ member.provincial_rank }}</td>
```

**New Layout (v1.7) with Priority Logic:**
```html
<td class="member-name">{{ member.name }}</td>
<td class="member-rank">
    {{ if member.grand_rank }}
        {# PRIORITY 1: Grand Rank only (in bold) #}
        <strong>{{ member.grand_rank }}</strong> ({{ member.grand_rank_date_accorded }})
    {{ else }}
        {# PRIORITY 2: Provincial ranks in sequence #}
        {{ if member.provincial_rank }}
            <div>{{ member.provincial_rank }} ({{ member.date_rank_accorded }})</div>
        {{ end }}
        {{ if member.prov_rank_other_prov }}
            <div>{{ member.prov_rank_other_prov }} ({{ member.op_date_accorded }})</div>
        {{ end }}
        {{ if member.london_rank }}
            <div>{{ member.london_rank }} ({{ member.london_rank_date_accorded }})</div>
        {{ end }}
    {{ end }}
</td>
```

**CSS Addition (print.css):**
```css
/* Member rank display with line separation */
.member-rank {
    font-size: 7pt;
    line-height: 1.4;
}

.member-rank div {
    margin: 1pt 0;
}

.member-rank strong {
    font-weight: bold;
}

/* London rank styling (rare) */
.london-rank {
    color: #0066cc;
    font-weight: bold;
}

/* Other province rank styling */
.other-prov-rank {
    font-style: italic;
    color: #666;
}
```

#### Honorary Members Section - Extended Display

**Current:**
```html
<tr>
    <td>{{ hon_member.name }}</td>
    <td>{{ hon_member.rank }}</td>
</tr>
```

**New (v1.7) with Priority Logic:**
```html
<tr>
    <td>{{ hon_member.name }}</td>
    <td class="hon-rank">
        {{ if hon_member.grand_rank }}
            {# PRIORITY 1: Grand Rank only (in bold) #}
            <strong>{{ hon_member.grand_rank }}</strong> ({{ hon_member.grand_rank_date_accorded }})
        {{ else }}
            {# PRIORITY 2: Rank (if no Grand Rank) + all other ranks in sequence #}
            {{ if hon_member.rank }}
                <div>{{ hon_member.rank }} ({{ hon_member.rank_date_accorded }})</div>
            {{ end }}
            {{ if hon_member.prov_rank_other_prov }}
                <div>{{ hon_member.prov_rank_other_prov }} ({{ hon_member.op_date_accorded }})</div>
            {{ end }}
            {{ if hon_member.london_rank }}
                <div class="london-rank">{{ hon_member.london_rank }} ({{ hon_member.london_rank_date_accorded }})</div>
            {{ end }}
        {{ end }}
    </td>
</tr>
```

**CSS Addition (print.css):**
```css
.hon-rank {
    font-size: 7pt;
    line-height: 1.4;
}

.hon-rank div {
    margin: 1pt 0;
}

.hon-rank strong {
    font-weight: bold;
}

.hon-rank .london-rank {
    color: #0066cc;
    font-weight: bold;
}
```

#### Officers Section - Extended Display

**Same logic applies to Officer rank display:**
```html
<td class="officer-rank">
    {{ if officer.grand_rank }}
        {# PRIORITY 1: Grand Rank only #}
        <strong>{{ officer.grand_rank }}</strong> ({{ officer.grand_rank_date_accorded }})
    {{ else }}
        {# PRIORITY 2: Other ranks in sequence #}
        {{ if officer.provincial_rank }}
            <div>{{ officer.provincial_rank }} ({{ officer.date_rank_accorded }})</div>
        {{ end }}
        {{ if officer.prov_rank_other_prov }}
            <div>{{ officer.prov_rank_other_prov }} ({{ officer.op_date_accorded }})</div>
        {{ end }}
        {{ if officer.london_rank }}
            <div>{{ officer.london_rank }} ({{ officer.london_rank_date_accorded }})</div>
        {{ end }}
    {{ end }}
</td>
```

---

## 8. UnitModelBuilder Updates

**In `UnitModelBuilder.cs` - BuildMemberModel():**

```csharp
public static Dictionary<string, object?> BuildMemberModel(SchemaMember member)
{
    var model = new Dictionary<string, object?>
    {
        { "unique_id", member.UniqueId },
        { "name", TextCleaner.CleanName(member.Name) },
        { "join_date", member.JoinDate },
        
        // v1.6 fields
        { "provincial_rank", TextCleaner.CleanProvincialRank(member.ProvincialRank) },
        { "date_rank_accorded", member.DateRankAccorded },
        { "grand_rank", TextCleaner.CleanProvincialRank(member.GrandRank) },
        { "grand_rank_date_accorded", member.GrandRankDateAccorded },
        
        // [NEW v1.7] Other Province Rank
        { "prov_rank_other_prov", TextCleaner.CleanProvincialRank(member.ProvRankOtherProv) },
        { "op_date_accorded", member.OpDateAccorded },
        { "op_date_start_year", member.OpDateStartYear },
        { "op_date_end_year", member.OpDateEndYear },
        
        // [NEW v1.7] London Rank
        { "london_rank", TextCleaner.CleanProvincialRank(member.LondonRank) },
        { "london_rank_date_accorded", member.LondonRankDateAccorded },
    };
    
    return model;
}
```

**NOTE:** The rank display logic (Grand Rank → Provincial → Other Province → London) is handled **entirely in the Scriban template** (`_data-driven/unit-page.html`). No computed fields are needed — the template's `if/else` logic applies the priority rules.

**Template applies this logic:**
- Line 1: Check if `grand_rank` exists → if yes, display only `grand_rank` in bold
- Line 2: If NO `grand_rank`, display `provincial_rank + date`
- Line 3: Then display `prov_rank_other_prov + op_date_accorded` 
- Line 4: Then display `london_rank + london_rank_date_accorded`

All conditional rendering is done by the Scriban `if/else` blocks in the template.

---

## 9. TextCleaner Updates

**Add method to TextCleaner.cs:**

```csharp
/// <summary>
/// Extracts the rank abbreviation from a rank string that includes province name.
/// Example: "PProvSGW (Hants. & I. of W.)" → "PProvSGW"
/// </summary>
public static string ExtractRankFromProvincial(string? rankWithProvince)
{
    if (string.IsNullOrWhiteSpace(rankWithProvince))
        return "";
    
    var match = System.Text.RegularExpressions.Regex.Match(rankWithProvince, @"^([^\s]+)");
    return match.Success ? match.Groups[1].Value : rankWithProvince;
}

/// <summary>
/// Extracts the province name from a rank string.
/// Example: "PProvSGW (Hants. & I. of W.)" → "Hants. & I. of W."
/// </summary>
public static string ExtractProvinceFromRank(string? rankWithProvince)
{
    if (string.IsNullOrWhiteSpace(rankWithProvince))
        return "";
    
    var match = System.Text.RegularExpressions.Regex.Match(rankWithProvince, @"\(([^)]+)\)");
    return match.Success ? match.Groups[1].Value : "";
}
```

---

## 10. Implementation Plan

### Phase 1: Domain Models (2-3 hours)

- [ ] Update `SchemaMember` class with 6 new properties
- [ ] Update `SchemaHonoraryMember` class with 6 new properties
- [ ] Update `SchemaOfficer` class with 6 new properties
- [ ] Create unit tests for domain models

### Phase 2: Data Loading (2-3 hours)

**Initial Testing:** Focus on Craft units only
- [ ] Update `craft_data_source.yaml` with column 13-16 mappings (test only)
- [ ] Update `SchemaDataLoader.ParseMembersFromCsv()` method
- [ ] Add `ParseDateRange()` helper function
- [ ] Add unit tests for date range parsing with Craft membership data

**Full Implementation (Post-Testing):** After Craft validation succeeds, update remaining data source files:
- [ ] royalarch_data_source.yaml, mark_data_source.yaml, ram_data_source.yaml, rcoc_data_source.yaml
- [ ] Create YAML files for 9 new degrees: amd, kt, ktp, osc, osm, pbq, rc, rsm, stoa

### Phase 3: Template Updates (2-3 hours)

- [ ] Update `_data-driven/unit-page.html` member section with rank display priority logic
  - Implement `if grand_rank` → show only grand_rank (bold)
  - Implement `else` → show provincial_rank, then prov_rank_other_prov, then london_rank (each on new line)
- [ ] Update `_data-driven/unit-page.html` honorary member section with same rank logic
- [ ] Update `_data-driven/unit-page.html` officer section with same rank logic
- [ ] Add CSS rules to `print.css` for multi-line rank display with proper line spacing
- [ ] Test rendering with sample data (members with/without grand ranks, multiple province ranks, etc.)

### Phase 4: UnitModelBuilder (1-2 hours)

- [ ] Update `BuildMemberModel()` method
- [ ] Update `BuildHonoraryMemberModel()` method
- [ ] Update `BuildOfficerModel()` method
- [ ] Add tests for model building

### Phase 5: TextCleaner (1 hour)

- [ ] Add `ExtractRankFromProvincial()` method
- [ ] Add `ExtractProvinceFromRank()` method
- [ ] Add unit tests for extraction logic

### Phase 6: Integration & Testing (3-4 hours)

**Initial Craft Testing:**
- [ ] End-to-end test with Craft units from v1.7 membership data
- [ ] Verify Craft rank display logic (Grand Rank vs Provincial ranks)
- [ ] Test edge cases: date ranges, missing values, London rank only
- [ ] Generate HTML output for Craft section
- [ ] Visual review of rank display formatting with multiple examples
- [ ] Validate date parsing (single year "2021" vs range "1993-15")

**Full Testing (Post-Craft Validation):**
- [ ] Test remaining degree types (RA, Mark, RAM, RCOC)
- [ ] Verify all 14 degree types render correctly
- [ ] Generate full PDF output
- [ ] Cross-validate rank display across all degrees

### Phase 7: Documentation (1 hour)

- [ ] Update README.md with v1.7 changes (rank display priority logic)
- [ ] Document date range format in comments
- [ ] Add examples to code comments
- [ ] Document 9 new degree types requiring YAML files (AMD, KT, KTP, OSC, OSM, PBQ, RC, RSM, STOA)

### Phase 8: New Degree Type Support (Post-Launch) [NOT IN v1.7.0 RELEASE]

After v1.7.0 ships with Craft + existing degrees tested:
- [ ] Create data source YAML files for 9 new degrees
  - amd_data_source.yaml
  - kt_data_source.yaml
  - ktp_data_source.yaml
  - osc_data_source.yaml
  - osm_data_source.yaml
  - pbq_data_source.yaml
  - rc_data_source.yaml
  - rsm_data_source.yaml
  - stoa_data_source.yaml
- [ ] Update master_v1.yaml to include sections for each new degree
- [ ] Add degree-specific templates/intro pages as needed
- [ ] Test full 14-degree rendering

**Total Estimated Time (v1.7.0 Release):** 12-17 hours  
**Recommended Timeline:** 2-3 work days  
**Risk Level:** Low (backward-compatible, data only)

---

## 11. Backward Compatibility

### v1.6 → v1.7 Migration

**Fully backward-compatible:**
- All new columns are optional (nullable)
- v1.6 data without columns 13-16 will parse correctly
- Rendering logic handles empty/missing new fields gracefully
- No existing field modifications

**No database migration required**  
**No existing template changes required** (enhancements only)  
**No breaking changes to API or CLI**

---

## 12. Testing Strategy

### Unit Tests

**DateRange Parsing:**
- `"2021"` → `(2021, null)`
- `"1993-15"` → `(1993, 2015)`
- `"1993-2015"` → `(1993, 2015)`
- `""` → `(null, null)`
- `"invalid"` → `(null, null)`

**Rank Extraction:**
- `"PProvSGW (Hants. & I. of W.)"` → Rank: `"PProvSGW"`, Province: `"Hants. & I. of W."`
- `"LGR"` → Rank: `"LGR"`, Province: `""`

### Integration Tests

**CSV Loading:**
- Load v1.7 dataset
- Verify member object population
- Verify honorary member object population
- Verify officer object population

**Rank Display Logic (CRITICAL):**
- Test: Member with Grand Rank only → displays grand_rank only, all other ranks hidden ✓
- Test: Member with NO Grand Rank + Provincial Rank only → displays provincial_rank (date)
- Test: Member with NO Grand Rank + Provincial + Other Province → displays both in sequence
- Test: Member with NO Grand Rank + Provincial + Other Province + London → displays all three in sequence
- Test: Member with NO Grand Rank + London only → displays london_rank (date)
- Test: Honorary member with Grand Rank → only grand_rank shown
- Test: Honorary member with multiple ranks, no Grand → all ranks shown in sequence
- Test: Officer with Grand Rank → only grand_rank shown
- Test: Officer with multiple ranks, no Grand → all ranks shown in sequence
- Test: Empty/null rank fields are omitted from display (no blank lines)
- Test: Date brackets are properly formatted for each rank type

**Rendering:**
- HTML output with all 4 degree types
- Verify member ranks display correctly according to priority logic
- Verify honorary member ranks display correctly
- Verify officer ranks display correctly
- Verify London rank styling is distinct (color/bold)
- Verify multi-line rank display with proper line spacing

---

## 13. Known Data Patterns (Sample Analysis)

From `membership_v1.7.csv` (first 500 members):

| Metric | Count | Percentage |
|--------|-------|-----------|
| Total Members Analyzed | ~500 | 100% |
| Members with `Prov Rank Oth Prov` | ~45-50 | 9-10% |
| Members with `Lndn Rank` | ~3-5 | 0.5-1% |
| Members with both columns | ~2-3 | <1% |
| Date range format (`YYYY-YY`) | ~3-5 | <1% |
| Single year format (`YYYY`) | ~40-45 | 8-9% |

**Key Insight:** Other province ranks are relatively common (~10% of population) but London ranks are rare (~1%). This indicates strong support for broader provincial rank tracking.

---

## 14. Files to Modify

### v1.7.0 Release (Craft Testing Focus)

| File | Changes | Priority | Notes |
|------|---------|----------|-------|
| `src/MasonicCalendar.Core/Domain/SchemaMember.cs` | Add 6 properties | P1 | |
| `src/MasonicCalendar.Core/Domain/SchemaHonoraryMember.cs` | Add 6 properties | P1 | |
| `src/MasonicCalendar.Core/Domain/SchemaOfficer.cs` | Add 6 properties | P1 | |
| `src/MasonicCalendar.Core/Loaders/SchemaDataLoader.cs` | Parse columns 13-16 | P1 | |
| `src/MasonicCalendar.Core/Renderers/Utilities/TextCleaner.cs` | Add extraction methods | P2 | |
| `src/MasonicCalendar.Core/Renderers/Utilities/UnitModelBuilder.cs` | Map new properties | P2 | |
| `document/data_sources/craft_data_source.yaml` | Add column 13-16 mappings | P1 | **Initial testing only** |
| `document/data_sources/royalarch_data_source.yaml` | Add column 13-16 mappings | P1 | Post-Craft validation |
| `document/data_sources/mark_data_source.yaml` | Add column 13-16 mappings | P1 | Post-Craft validation |
| `document/data_sources/ram_data_source.yaml` | Add column 13-16 mappings | P1 | Post-Craft validation |
| `document/data_sources/rcoc_data_source.yaml` | Add column 13-16 mappings | P1 | Post-Craft validation |
| **`document/templates/_data-driven/unit-page.html`** | **Implement rank display priority logic** | **P1** | **CRITICAL: If/else logic for Grand Rank vs Provincial vs Other vs London** |
| `document/templates/print.css` | Add multi-line rank spacing CSS | P2 | |
| `README.md` | Document v1.7 changes | P3 | Document Craft-only release note |

### Post-v1.7.0 Release (9 New Degree Types)

| File | Changes | Timeline |
|------|---------|----------|
| `document/data_sources/amd_data_source.yaml` | Create new | Phase 8 |
| `document/data_sources/kt_data_source.yaml` | Create new | Phase 8 |
| `document/data_sources/ktp_data_source.yaml` | Create new | Phase 8 |
| `document/data_sources/osc_data_source.yaml` | Create new | Phase 8 |
| `document/data_sources/osm_data_source.yaml` | Create new | Phase 8 |
| `document/data_sources/pbq_data_source.yaml` | Create new | Phase 8 |
| `document/data_sources/rc_data_source.yaml` | Create new | Phase 8 |
| `document/data_sources/rsm_data_source.yaml` | Create new | Phase 8 |
| `document/data_sources/stoa_data_source.yaml` | Create new | Phase 8 |
| `document/master_v1.yaml` | Add 9 degree sections | Phase 8 |

---

## 15. Success Criteria

### v1.7.0 Release (Craft Focus)

- [x] Analysis document complete with 14 degree types documented
- [ ] All domain model properties added (6 new per class)
- [ ] CSV parsing includes columns 13-16
- [ ] Date range parsing handles all formats ("2021" and "1993-15")
- [ ] Templates display new rank information correctly
- [ ] **Craft units render without errors** (primary validation)
- [ ] Rank display priority logic verified: Grand Rank OR Provincial cascade
- [ ] Backward compatibility verified with v1.6 data
- [ ] Unit tests pass (>95% coverage)
- [ ] **Integration tests pass with Craft members from membership_v1.7.csv**
- [ ] Craft HTML and PDF output visually correct
- [ ] No performance regression
- [ ] README documents Craft-only v1.7.0 release note

### v1.7.1+ (Full Degree Support)

- [ ] All 14 degree types tested (RA, Mark, RAM, RCOC + 9 new)
- [ ] YAML files created for 9 new degrees
- [ ] Full document renders all degrees without errors
- [ ] Rank display consistent across all degree types

---

## 16. References & Notes

**Rank Abbreviations (Other Province):**
- `PProvSGW` - Past Provincial Senior Grand Warden
- `PProvJGW` - Past Provincial Junior Grand Warden
- `PProvGM` - Past Provincial Grand Master
- `PProvGReg` - Past Provincial Grand Registrar
- `PProvGStB` - Past Provincial Grand Steward B
- `PProvSGD` - Past Provincial Senior Grand Deacon
- `PProvJGD` - Past Provincial Junior Grand Deacon
- `PProvGStwd` - Past Provincial Grand Steward
- `ProvGOrg` - Provincial Grand Organist
- `PPDepGDC` - Past Provincial Deputy Grand Director of Ceremonies

**London Ranks:**
- `LGR` - London Grand Rank (rare, historical)

**Parsing Notes:**
- Date ranges in format `YYYY-YY` require special handling
- Province names include province codes (e.g., "Hants. & I. of W.")
- Some older date ranges may span decades (1993-2015)
- London rank column is very sparse - most entries are empty

---

**Document Status:** ✓ Complete  
**Ready for Implementation:** Yes  
**Next Step:** Begin Phase 1 (Domain Models)
