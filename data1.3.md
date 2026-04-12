# Data version 1.3
We now have finalised data format consisting of two files:
- E:\Development\repos\masonic-calendar\document\data\units_v1.3.csv
- E:\Development\repos\masonic-calendar\document\data\membership_v1.3.csv

## Units 1.3
This contains all the units we need to render data for and has been extended to include the supplementary information about each unit.  This is a single file for all units with a "Unit Type" column to seperate the uniets per type.

The types are:
- Craft
- RA (Royal Arch)
- Mark
- RAM (Royal Ark Mariners)

We need to update the data sources YAML to filter to the units by "unit type" and then filter to the members by "unit type" and "unit no".

We need to change the logic to iterate each unit in the units_v1.3.csv and from here pull out the relevant officers, members, past masters, etc.

NOTE: The additional unit types (Mark and RAM) these are new.

## Membership 1.3
This contains all the members for all the units and has been cleaned to ensure the names are all in a consistent format, ranks and dates cleaned, and a unique reference for each row.  There is a new "PosNo" columns which defines the order for the tables within each unit, and a "OffPos" column whic defines the order for the officers table.

I am proposing to change the data source YAML files, so the craft_data_sources.yaml would be like this:

```
units:
  source: "units_v1.3.csv"
  filter_field: "Unit Type"
  filter_value: "Craft
  fields:    
    - name: "Number"
      csv_column: "Unit No"
      type: "int"
    - name: "Name"
      csv_column: "Unit Name"
      type: "string"
    - name: "ShortName"
      csv_column: "ShortName"
      type: "string"
    - name: "Warrant"
      csv_column: "Warrant"
      type: "string"
    - name: "MeetingDates"
      csv_column: "Meeting Dates"
      type: "string"
    - name: "LastInstallationDate"
      csv_column: "Last Installation"
      type: "string"
    - name: "Location"
      csv_column: "Location"
      type: "string"
    - name: "Email"
      csv_column: "Email"
      type: "string"

officers:
  source: "membership_v1.3.csv"
  filters:
    - filter_field: "Unit Type"
      filter_value: "Craft
    - filter_field: "MemType"
      filter_value: "Off"
  fields:
    - name: "Reference"
      csv_column: "UniqueRef"
      type: "string"
    - name: "Name"
      csv_column: "Name"
      type: "string"
    - name: "Position"
      csv_column: "Office"
      type: "string"
    - name: "PositionNo"
      csv_column: "OffPos"
      type: "int"

past_masters:
  source: "membership_v1.3.csv"
  filters:
    - filter_field: "Unit Type"
      filter_value: "Craft
    - filter_field: "MemType"
      filter_value: "PMO"
  fields:
    - name: "Reference"
      csv_column: "UniqueRef"
      type: "string"
    - name: "Name"
      csv_column: "Name"
      type: "string"
    - name: "YearInstalled"
      csv_column: "Installed"
      type: "string"
    - name: "ProvincialRank"
      csv_column: "Provincial Rank"
      type: "string"
    - name: "GrandRank"
      csv_column: "Grand Rank"
      type: "string"
    - name: "RankYear"
      csv_column: "Date Accorded"
      type: "string"
    - name: "ActiveProvincialRank"
      csv_column: "Active Provincial Rank"
      type: "string"
    - name: "ActiveRankYear"
      csv_column: "Active Accorded"
      type: "string"

joining_past_masters:
  source: "membership_v1.3.csv"
  filters:
    - filter_field: "Unit Type"
      filter_value: "Craft
    - filter_field: "MemType"
      filter_value: "PMI"
  fields:
    - name: "Reference"
      csv_column: "UniqueRef"
      type: "string"
    - name: "Name"
      csv_column: "Name"
      type: "string"
    - name: "PastUnits"
      csv_column: "Join Unit"
      type: "string"
    - name: "GrandRank"
      csv_column: "Grand Rank"
      type: "string"
    - name: "ProvincialRank"
      csv_column: "Provincial Rank"
      type: "string"
    - name: "RankYear"
      csv_column: "Date Accorded"
      type: "string"

members:
  source: "membership_v1.3.csv"
  filters:
    - filter_field: "Unit Type"
      filter_value: "Craft
    - filter_field: "MemType"
      filter_value: "Mem"
  fields:
    - name: "Reference"
      csv_column: "UniqueRef"
      type: "string"
    - name: "Name"
      csv_column: "Name"
      type: "string"
    - name: "YearInitiated"
      csv_column: "Year"
      type: "string"

honorary_members:
  source: "membership_v1.3.csv"
  filters:
    - filter_field: "Unit Type"
      filter_value: "Craft
    - filter_field: "MemType"
      filter_value: "Hon"
  fields:"
      csv_column: "UniqueRef"
      type: "string"
    - name: "Title"
      csv_column: "Hon Rank"
      type: "string"
    - name: "Name"
      csv_column: "Name"
      type: "string"
    - name: "GrandRank"
      csv_column: "Grand Rank"
      type: "string"
    - name: "ProvincialRank"
      csv_column: "Provincial Rank"
      type: "string"
```

---

## Membership MemType Reference

| MemType | Meaning | Applies To |
|---------|---------|------------|
| `Off` | Current officers | Craft, RA, Mark, RAM |
| `PMO` | Past Masters (own lodge) | Craft |
| `PMI` | Joining Past Masters | Craft |
| `PMEZ` | Past First Principals (own chapter) | RA |
| `JPMEZ` | Joining Past First Principals | RA |
| `PCO` | Past Commanders / equivalent past officers | Mark, RAM |
| `Mem` | Subscribing members | Craft, RA, Mark, RAM |
| `Hon` | Honorary members | Craft, RA, Mark, RAM |

---

## Unit Counts (units_v1.3.csv)

| Unit Type | Count |
|-----------|-------|
| Craft | 49 |
| RA | 22 |
| Mark | 14 |
| RAM | 8 |

---

## Implementation Plan

### Phase 1 — Load Units and Produce Basic Unit Pages (TOC + Warrant / Meeting Dates / Installation Date)

This is the first milestone: all four unit types load correctly, appear in the TOC, and each unit page shows the header block with warrant, meeting dates and last installation date (no officers/members yet).

---

#### Step 1.1 — Update `DataSourceDefinition` to support multiple filters

**File:** `src/MasonicCalendar.Core/Loaders/DocumentLayoutLoader.cs`

The current `DataSourceDefinition` class only supports a single `FilterField` / `FilterValue`. The new YAML schema uses a `filters:` list. Add a `Filters` list property, and keep the single-field properties for backward compatibility.

```csharp
public class DataSourceDefinition
{
    public string? Source { get; set; }
    public string? UnitIdField { get; set; } = "Unit";
    public string? FilterField { get; set; }       // legacy single-filter (kept for compat)
    public string? FilterValue { get; set; }       // legacy single-filter (kept for compat)
    public List<DataSourceFilter>? Filters { get; set; }  // NEW: list of AND filters
    public string? OverrideHeading { get; set; }
    public List<FieldMapping>? Fields { get; set; }
}

public class DataSourceFilter   // NEW class
{
    public string? FilterField { get; set; }
    public string? FilterValue { get; set; }
}
```

A row passes if **all** filters in the list match (AND logic).

---

#### Step 1.2 — Add `Warrant`, `MeetingDates`, and `Hall` to `SchemaUnit`

**File:** `src/MasonicCalendar.Core/Domain/SchemaUnit.cs`

These three fields exist in `units_v1.3.csv` but are not yet on the domain model.

```csharp
public string? Warrant { get; set; }
public string? MeetingDates { get; set; }
public string? Hall { get; set; }
```

`LastInstallationDate` can now be read as a **string** directly from the units CSV (the `Last Installation` column already contains the formatted date string — no separate `LoadCompositePropertyAsync` step required). Change the property type from `DateOnly?` to `string?` — or keep both and read the string into `LastInstallationDate` as a string field.

> **Decision:** Keep `DateOnly? LastInstallationDate` for existing uses; add `string? LastInstallationDateRaw` as a plain string read directly from the CSV column.  Alternatively, just use a string for both since it is only ever displayed as-is and never computed. **Proposed: Change to `string?` to simplify.**

---

#### Step 1.3 — Update `LoadUnitsFromCsvAsync` in `SchemaDataLoader`

**File:** `src/MasonicCalendar.Core/Loaders/SchemaDataLoader.cs`

**Changes:**
1. Apply the `filter_field` / `filter_value` on the units CSV row (currently units are loaded unfiltered). Check each row against `mapping.Units.FilterField` / `FilterValue` to include only the relevant unit type.
2. Map the new fields: `Warrant`, `MeetingDates`, `Hall`, and `LastInstallationDate` (now read directly as a string from the units CSV — removing the need for the separate `LoadCompositePropertyAsync` call for `LastInstallationDate`).
3. Remove the `LoadCompositePropertyAsync` call for `LastInstallationDate` now that it is in the units CSV directly.

---

#### Step 1.4 — Update `LoadPersonTypeAsync` to support multiple filters

**File:** `src/MasonicCalendar.Core/Loaders/SchemaDataLoader.cs`

Update the filter check to support the new `Filters` list in addition to the legacy single `FilterField`/`FilterValue`. A helper method `RowPassesFilters(CsvReader csv, DataSourceDefinition dataSource)` should return `true` only if all configured filters match the current row.

Also update `LoadCompositePropertyAsync` the same way (for future use, though it will no longer be called for installation dates).

---

#### Step 1.5 — Update the `UnitModelBuilder` to expose new unit fields

**File:** `src/MasonicCalendar.Core/Renderers/Utilities/UnitModelBuilder.cs`

Add the new fields to the `unit` dictionary in `BuildModel()`:
```csharp
{ "warrant", TextCleaner.CleanName(unit.Warrant) },
{ "meetingDates", TextCleaner.CleanName(unit.MeetingDates) },
{ "hall", unit.Hall },
{ "lastInstallationDate", unit.LastInstallationDate },   // now string
```

---

#### Step 1.6 — Update `unit-page.html` template to display new fields

**File:** `document/templates/unit-page.html`

Add a section below the unit header to display (all conditional on non-empty values):
- **Warrant** text block
- **Meeting Dates** text block
- **Last Installation** (already shown but now sourced as plain string, so remove any date formatting wrapper if present)

---

#### Step 1.7 — Rewrite `craft_data_source.yaml` for v1.3 schema

**File:** `document/data_sources/craft_data_source.yaml`

Replace entirely with the new structure:
- Remove the old `unit_mapping` block (no longer needed — unit numbers come directly from the `Unit` column in membership CSV)
- Update `units` section to filter on `Unit Type = Craft` and map the new columns
- Update all membership sections (`officers`, `past_masters`, `joining_past_masters`, `members`, `honorary_members`) to use:
  - `filters:` list (two filters: `Unit Type = Craft` AND `MemType = <value>`)
  - New column names from `membership_v1.3.csv` (`Name`, `Office`, `OffPos`, `UniqueRef`, `Installed`, `Provincial Rank`, `Grand Rank`, `Date Accorded`, `Active Provincial Rank`, `Active Accorded`, `Join Unit`, `Year`, `Hon Rank`)
  - `unit_id_field: "Unit"` so the loader knows to match rows to units by the `Unit` column

---

#### Step 1.8 — Rewrite `royalarch_data_source.yaml` for v1.3 schema

**File:** `document/data_sources/royalarch_data_source.yaml`

Same structural changes as craft. Key differences:
- `filter_value: "RA"` for unit type filter
- `past_masters` uses `MemType = PMEZ` with `override_heading: "Past First Principals"`
- `joining_past_masters` uses `MemType = JPMEZ` with `override_heading: "Joining Past First Principals"`
- No `joining_past_masters` currently in RA template — confirm whether to add

---

#### Step 1.9 — Create `mark_data_source.yaml`

**File:** `document/data_sources/mark_data_source.yaml` *(new file)*

New data source for Mark lodges:
- Unit type filter: `Mark`
- `past_masters` uses `MemType = PCO` with `override_heading: "Past Commanders"` (or appropriate Mark title)
- No `joining_past_masters` (PCO serves this role)

---

#### Step 1.10 — Create `ram_data_source.yaml`

**File:** `document/data_sources/ram_data_source.yaml` *(new file)*

New data source for Royal Ark Mariners:
- Unit type filter: `RAM`
- `past_masters` uses `MemType = PCO` with appropriate `override_heading`

---

#### Step 1.11 — Add Mark and RAM sections to `master_v1.yaml`

**File:** `document/master_v1.yaml`

Add four new sections after `royalarch_units`:
```yaml
- section_id: "mark_toc"
  section_title: "Mark Master Masons Lodges in numerical order"
  type: "toc"

- section_id: "mark_units"
  section_title: "Mark Master Masons Lodges in numerical order"
  type: "data-driven"
  data_mapping: "data_sources/mark_data_source.yaml"

- section_id: "ram_toc"
  section_title: "Royal Ark Mariners in numerical order"
  type: "toc"

- section_id: "ram_units"
  section_title: "Royal Ark Mariners in numerical order"
  type: "data-driven"
  data_mapping: "data_sources/ram_data_source.yaml"
```

---

#### Step 1.12 — Remove the old `unit_mapping` / `BuildUnitMappingAsync` path

**File:** `src/MasonicCalendar.Core/Loaders/SchemaDataLoader.cs`

The old `BuildUnitMappingAsync` method built a row-index-to-unit-number dictionary by scanning `CraftData.csv` / `RAData.csv` for `S01` marker rows. With v1.3 data, the `Unit` column in `membership_v1.3.csv` directly contains the unit number for every row. This entire mechanism can be removed (or left dormant if backward compat with old CSV files is wanted).

---

### Phase 2 — Officers

After Phase 1 is verified working:

1. Update `LoadPersonTypeAsync` to use the `Name` column directly (no more combining `Surname` + `Initials` + `FirstName` from separate columns — the new CSV has a pre-combined `Name` column).
2. Use `OffPos` column for officer ordering (sort officers by `OffPos` after loading).
3. Use `PosNo` column for member/PM ordering (sort each group by `PosNo` after loading).
4. Update `SchemaOfficer` — `Name` field is now populated directly; `Surname`/`Initials`/`FirstName` separate fields are no longer needed from CSV.
5. Update `LoadHermesDataAsync` officer block to read `Name` directly rather than combining parts.
6. Test with `-unit <N>` for a known unit.

---

### Phase 3 — Past Masters and Joining Past Masters

1. Load `PMO` / `PMEZ` / `PCO` rows as past masters with appropriate heading overrides.
2. Load `PMI` / `JPMEZ` rows as joining past masters.
3. Verify `GrandRank` field is displayed correctly (new field not in old schema).
4. Verify `ActiveProvincialRank` / `ActiveRankYear` display logic (use active rank if present, fall back to accorded rank).

---

### Phase 4 — Members and Honorary Members

1. Load `Mem` rows. Order by `PosNo`.
2. Load `Hon` rows. Map `Hon Rank` → `Title` and `Hon Title` fields.
3. Verify 3-column member split still works correctly with new data.

---

### Phase 5 — Validation and Cleanup

1. Run full render for all four section types and compare output.
2. Remove all references to old CSV files (`CraftData.csv`, `RAData.csv`, `royalarch_units.csv`, `unit-locations.csv`) from data source YAMLs.
3. Update `copilot-instructions.md` and repo memory to reflect v1.3 schema.
4. Archive old data source YAMLs.

---

## Files Changed Summary

| File | Change |
|------|--------|
| `src/MasonicCalendar.Core/Domain/SchemaUnit.cs` | Add `Warrant`, `MeetingDates`, `Hall`; change `LastInstallationDate` to `string?` |
| `src/MasonicCalendar.Core/Loaders/DocumentLayoutLoader.cs` | Add `DataSourceFilter` class; add `Filters` list to `DataSourceDefinition` |
| `src/MasonicCalendar.Core/Loaders/SchemaDataLoader.cs` | Filter units CSV by unit type; read new fields; multi-filter support; remove `BuildUnitMappingAsync` (Phase 1) |
| `src/MasonicCalendar.Core/Renderers/Utilities/UnitModelBuilder.cs` | Expose `warrant`, `meetingDates`, `hall` in model |
| `document/templates/unit-page.html` | Add warrant / meeting dates / hall display blocks |
| `document/data_sources/craft_data_source.yaml` | Full rewrite for v1.3 column names and filter list |
| `document/data_sources/royalarch_data_source.yaml` | Full rewrite for v1.3 column names and filter list |
| `document/data_sources/mark_data_source.yaml` | **New file** |
| `document/data_sources/ram_data_source.yaml` | **New file** |
| `document/master_v1.yaml` | Add `mark_toc`, `mark_units`, `ram_toc`, `ram_units` sections |
