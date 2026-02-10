# CSV Schema v2 Integration Guide

## Overview

The document templates now incorporate the full data structure from CSV_SCHEMA_v2. This guide explains how the CSV data maps to the document rendering system and what fields are available for display.

## Data Sources

### 1. Units Data (sample-units.csv)

The units file contains the basic information about each lodge/chapter.

#### Available Fields:
| Field | Type | Display Name | Usage |
|-------|------|--------------|-------|
| `ID` | guid | Unit ID | Internal key (not displayed) |
| `Number` | int | Unit Number | Header, sorting, filtering |
| `Name` | string | Unit Name | Header, TOC, main title |
| `LocationID` | guid | Location ID | Link to locations (not displayed) |
| `Email` | string | Contact Email | Header, clickable link |
| `Established` | date | Established Date | Unit page header |
| `LastInstallationDate` | date | Last Installation | Unit page header |
| `UnitType` | string | Unit Type | Filtering (Craft, RoyalArch, etc.) |

#### Example in Template:
```yaml
unit_page_structure:
  header:
    - field: "Number"
      label: "Unit Number"
    - field: "Name"
      label: "Unit Name"
    - field: "Established"
      label: "Established"
      format: "date"
    - field: "Email"
      label: "Contact"
      format: "email_link"
```

### 2. Hermes Export Data (hermes-export.csv)

Single consolidated file with all person records split by `Type` field.

#### Record Types:

##### Officers (Type = "Off")
Displays current lodge/chapter officers.

**Available Fields:**
| Field | Type | Display Name |
|-------|------|--------------|
| `Unit` | int | Unit Number |
| `PosNo` | int | Position Order |
| `Name` | string | Officer Name |
| `FN01` | string | Position Title |

**Rendering:**
```yaml
- record_type: "Off"
  name: "Officers"
  fields:
    - field: "FN01"
      label: "Position"
    - field: "Name"
      label: "Name"
```

##### Past Masters (Type = "PMO")
Displays past masters/principals with provincial ranks.

**Available Fields:**
| Field | Type | Display Name |
|-------|------|--------------|
| `Unit` | int | Unit Number |
| `PosNo` | int | Position Order |
| `Name` | string | Past Master Name |
| `FN01` | string | Year Installed |
| `FN13` | string | Current Provincial Rank |
| `FN14` | string | Year Rank Issued |

**Rendering:**
```yaml
- record_type: "PMO"
  name: "Past Masters"
  fields:
    - field: "Name"
      label: "Name"
    - field: "FN01"
      label: "Year"
    - field: "FN13"
      label: "Rank"
    - field: "FN14"
      label: "Rank Year"
```

##### Joining Past Masters (Type = "PMI")
Displays members who joined as past masters with their rank details.

**Available Fields:**
| Field | Type | Display Name |
|-------|------|--------------|
| `Unit` | int | Unit Number |
| `PosNo` | int | Position Order |
| `Name` | string | Name |
| `FN01` | string | Year Installed |
| `FN12` | string | Current Provincial Rank |
| `FN13` | string | Year Rank Issued |

**Rendering:**
```yaml
- record_type: "PMI"
  name: "Joining Past Masters"
  fields:
    - field: "Name"
      label: "Name"
    - field: "FN01"
      label: "Year"
    - field: "FN12"
      label: "Rank"
    - field: "FN13"
      label: "Rank Year"
```

##### Members (Type = "Mem")
Displays active lodge/chapter members.

**Available Fields:**
| Field | Type | Display Name |
|-------|------|--------------|
| `Unit` | int | Unit Number |
| `PosNo` | int | Position Order |
| `Name` | string | Member Name |
| `FN01` | string | Year Initiated/Exalted |

**Rendering:**
```yaml
- record_type: "Mem"
  name: "Members"
  max_display: 20  # Show first 20 members
  fields:
    - field: "Name"
      label: "Name"
    - field: "FN01"
      label: "Year Initiated"
```

##### Honorary Members (Type = "Hon")
Displays honorary members.

**Available Fields:**
| Field | Type | Display Name |
|-------|------|--------------|
| `Unit` | int | Unit Number |
| `PosNo` | int | Position Order |
| `Name` | string | Honorary Member Name |

**Rendering:**
```yaml
- record_type: "Hon"
  name: "Honorary Members"
  fields:
    - field: "Name"
      label: "Name"
```

## Master Template Structure

The master template (`master_v1.yaml`) now includes:

### Data Sources Definition
```yaml
data_sources:
  units:
    file: "sample-units.csv"
    schema: [...]  # Full field definitions
  
  hermes_export:
    file: "hermes-export.csv"
    record_types:
      - type: "Off"
        name: "Officers"
        fields: [...]
      - type: "PMO"
        name: "Past Masters"
        fields: [...]
      # ... more record types
```

This provides:
- **Schema documentation** in the template
- **Field mappings** for rendering
- **Data type information** for formatting
- **Display labels** for UI consistency

## Section Structure

Each section (e.g., `section-craft.yaml`) now includes:

### Unit Page Structure
```yaml
unit_page_structure:
  header:
    # Fields displayed in unit header
    - field: "Number"
      label: "Unit Number"
    # ...
  
  sections:
    # What member categories to show
    - record_type: "Off"
      name: "Officers"
      visible: true
      fields:
        # What fields to display for each type
        - field: "FN01"
          label: "Position"
```

This defines:
- **Header content** - unit info at top of page
- **Section visibility** - which member types to show
- **Field selection** - which fields to display
- **Custom labels** - how to label the fields

## Data Filtering

Sections filter data based on:

1. **Unit Type Filter**
   ```yaml
   data_filters:
     unit_type: "Craft"  # Only Craft lodges
   ```

2. **Record Type Filter** (implicit)
   - Officers show only Type="Off" records
   - Members show only Type="Mem" records
   - Etc.

3. **Unit Number Filter** (implicit)
   - Only data matching the current unit number

## Display Formatting

Fields support various format types:

```yaml
- field: "Email"
  label: "Contact"
  format: "email_link"  # Creates clickable mailto: link

- field: "Established"
  label: "Established"
  format: "date"  # Formats as date

- field: "Name"
  label: "Name"
  format: "bold"  # Bold text

- field: "FN01"
  label: "Position"
  format: "normal"  # Default text
```

## Using the Schema in Code

The DocumentRenderer loads this schema and can:

1. **Validate** incoming CSV data against schema
2. **Map** CSV fields to display labels
3. **Filter** which fields to show
4. **Format** values based on type
5. **Sort** records by PosNo field

## Extending the Schema

To add a new field to a record type:

1. Verify the field exists in the CSV file
2. Add it to the `data_sources.hermes_export.record_types[type].fields` section
3. Add it to the relevant `unit_page_structure.sections[type].fields` in the section YAML

### Example - Adding PMO Rank Year to Display:

**master_v1.yaml:**
```yaml
- type: "PMO"
  name: "Past Masters"
  fields:
    - name: "FN14"
      type: "string"
      label: "Rank Year"  # Add this
```

**section-craft.yaml:**
```yaml
- record_type: "PMO"
  name: "Past Masters"
  fields:
    - field: "FN14"
      label: "Rank Year"  # Add this
```

## Document Rendering Flow

```
1. Load master_v1.yaml
   ├─ Load data_sources definitions
   ├─ Load sections (includes section-craft.yaml, etc.)
   └─ Define styling and structure

2. Load section configuration
   ├─ Read unit_page_structure
   ├─ Determine which record types to show
   └─ Get field display preferences

3. Load CSV data
   ├─ Read sample-units.csv
   ├─ Read hermes-export.csv
   ├─ Filter by unit type (Craft, RoyalArch, etc.)
   └─ Group records by unit and type

4. Render units
   ├─ For each unit of the section type:
   │  ├─ Display header (unit name, email, dates)
   │  ├─ Display officers (Type=Off)
   │  ├─ Display past masters (Type=PMO)
   │  ├─ Display members (Type=Mem, limited to max_display)
   │  └─ Display honorary members (Type=Hon)
   └─ Create page breaks between units
```

## Common Field Mappings

### Craft Lodge Example:
- **Officers** show: Position (FN01), Name
- **Past Masters** show: Name, Year (FN01), Rank (FN13)
- **Members** show: Name, Year Initiated (FN01) - max 20

### Royal Arch Chapter Example:
- **Officers** show: Position (FN01), Name
- **Past Principals** show: Name, Year (FN01), Rank (FN13)
- **Members** show: Name, Year Exalted (FN01) - max 20

The templates automatically adjust labels (e.g., "Past Masters" vs "Past Principals") based on section configuration.

## Future Enhancements

The schema structure supports:
- ✅ Additional fields (add to schema and template)
- ✅ Conditional visibility (add `visible: true/false`)
- ✅ Custom sorting (add `sort_by` field)
- ✅ Grouping (add `group_by` property)
- ✅ Filtering (add more `data_filters`)
- ✅ Multi-column layouts (add `columns` property)

