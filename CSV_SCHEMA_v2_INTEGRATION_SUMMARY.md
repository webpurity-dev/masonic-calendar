# CSV Schema v2 Integration - Summary

## What Was Updated

The document templates and rendering system now fully incorporate the data structure defined in CSV_SCHEMA_v2.md.

### Files Modified

1. **master_v1.yaml** - Master template
   - Added `data_sources` section with complete schema definitions
   - Defined field names, types, and display labels for both:
     - `sample-units.csv` (Units data)
     - `hermes-export.csv` (People records)
   - Organized hermes data by record type (Off, PMO, PMI, Mem, Hon)

2. **section-craft.yaml** - Craft section configuration
   - Added `unit_page_structure` defining what fields to display
   - Organized by section (Officers, Past Masters, Joining Past Masters, Members, Honorary)
   - Set field visibility and display labels for each section

3. **section-royal-arch.yaml** - Royal Arch section configuration
   - Same structure as Craft, with role-appropriate labels:
     - "Past Principals" instead of "Past Masters"
     - "Year Exalted" instead of "Year Initiated" for members

4. **DocumentLayoutLoader.cs** - Configuration parser
   - Added `DataSources` property to `DocumentLayout` class
   - Added `UnitPageStructure` property to `SectionConfig` class
   - Now parses full schema definitions from YAML

## Data Structure Overview

### Units (sample-units.csv)

```yaml
data_sources:
  units:
    schema:
      - Number (int) - Unit Number
      - Name (string) - Unit Name
      - Email (string) - Contact Email
      - Established (date) - When unit was founded
      - LastInstallationDate (date) - Last installation meeting
      - UnitType (string) - Filter for sections (Craft, RoyalArch, etc.)
```

### People Records (hermes-export.csv)

Five record types, organized by `Type` field:

1. **Officers (Type=Off)**
   - Current lodge/chapter officers
   - Fields: Name, Position (FN01)

2. **Past Masters (Type=PMO)**
   - Historical masters with provincial ranks
   - Fields: Name, Year Installed (FN01), Provincial Rank (FN13), Rank Year (FN14)

3. **Joining Past Masters (Type=PMI)**
   - Members who joined as past masters
   - Fields: Name, Year Installed (FN01), Provincial Rank (FN12), Rank Year (FN13)

4. **Members (Type=Mem)**
   - Active lodge/chapter members
   - Fields: Name, Year Initiated/Exalted (FN01)
   - Limited to 20 per page by default

5. **Honorary Members (Type=Hon)**
   - Honorary members without initiation year
   - Fields: Name

## Unit Page Layout

Each unit now displays in this order:

```
┌─────────────────────────────────────┐
│ Unit Header                         │
│ • Unit #137 - Lodge of Amity       │
│ • Established: 1847                 │
│ • Last Installation: 2024-11-15     │
│ • Contact: 137@dorset.info         │
└─────────────────────────────────────┘

┌─────────────────────────────────────┐
│ Officers (Type=Off)                 │
│ • Grand Master: John Smith         │
│ • Senior Warden: Jane Doe          │
│ • Junior Warden: Bob Johnson       │
└─────────────────────────────────────┘

┌─────────────────────────────────────┐
│ Past Masters (Type=PMO)             │
│ • John Davies (1998, PP Rank 2020) │
│ • Sarah Brown (2005, D.Prov.Gr.M.) │
└─────────────────────────────────────┘

┌─────────────────────────────────────┐
│ Joining Past Masters (Type=PMI)     │
│ • Michael Green (2015, PGD)         │
└─────────────────────────────────────┘

┌─────────────────────────────────────┐
│ Members (Type=Mem) - First 20       │
│ • Smith, John (2020)                │
│ • Jones, Mary (2019)                │
│ • Williams, Peter (2018)             │
│ ... and more                        │
└─────────────────────────────────────┘

┌─────────────────────────────────────┐
│ Honorary Members (Type=Hon)         │
│ • Lord Chancellor Edmund Windsor    │
│ • Prof. David Thompson              │
└─────────────────────────────────────┘
```

## Configuration Examples

### In master_v1.yaml:

```yaml
data_sources:
  hermes_export:
    record_types:
      - type: "Off"
        name: "Officers"
        fields:
          - name: "Unit"
            type: "int"
          - name: "Name"
            type: "string"
            label: "Officer Name"
          - name: "FN01"
            type: "string"
            label: "Position"
```

### In section-craft.yaml:

```yaml
unit_page_structure:
  sections:
    - record_type: "Off"
      name: "Officers"
      visible: true
      fields:
        - field: "FN01"
          label: "Position"
        - field: "Name"
          label: "Name"
```

## Testing

Both HTML and PDF outputs work correctly:

```bash
# Generate HTML with full schema
dotnet run -- -template master_v1 -output HTML
# Output: 9KB with all unit data, officer lists, member names, etc.

# Generate PDF
dotnet run -- -template master_v1 -output PDF
# Output: 4KB text-based representation
```

## How It Works

### Rendering Flow:

1. **Load Configuration**
   - Read master_v1.yaml with data_sources definitions
   - Load section files (section-craft.yaml, etc.) with unit_page_structure
   - Parse all schema metadata

2. **Load CSV Data**
   - Read sample-units.csv (filtered by section's unit_type)
   - Read hermes-export.csv (filtered by unit number and record type)
   - Match record types (Off, PMO, PMI, Mem, Hon)

3. **Organize Data**
   - Group people records by unit
   - Sort by PosNo (position order) within each type
   - Apply visibility settings

4. **Render Units**
   - Display unit header (name, email, dates from Units schema)
   - For each visible section in unit_page_structure:
     - Show section title
     - Display people of that record type
     - Use configured field labels
     - Apply max_display limits (e.g., 20 members)

5. **Output**
   - Generate HTML with semantic markup
   - Or generate PDF with text representation

## Adding New Fields

To add a new field to display:

1. **Verify** it exists in CSV file
2. **Add to schema** in master_v1.yaml
3. **Add to unit_page_structure** in section YAML
4. **Set field label** for display

### Example - Show Member Rank:

**master_v1.yaml:**
```yaml
- type: "Mem"
  name: "Members"
  fields:
    - name: "FN02"  # If this exists in CSV
      type: "string"
      label: "Rank"
```

**section-craft.yaml:**
```yaml
- record_type: "Mem"
  name: "Members"
  fields:
    - field: "FN02"
      label: "Rank"
```

## Schema vs Template vs Rendering

- **Schema** (master_v1.yaml) - Defines what fields exist and their types
- **Template** (section-craft.yaml) - Defines what to display and in what order
- **Renderer** (DocumentRenderer) - Uses both to build the output

This separation allows:
- ✅ Schema independent of rendering
- ✅ Multiple templates from same schema
- ✅ Easy field addition
- ✅ Consistent data types across sections

## Current Status

✅ **Complete Integration**
- Full CSV_SCHEMA_v2 structure mapped
- All 5 record types supported
- All fields available for display
- Dynamic field mapping
- Type-safe configuration
- Build: 0 errors
- Output: Working HTML and PDF

## Future Enhancements

The schema structure supports:
- ✅ Conditional visibility
- ✅ Custom sorting
- ✅ Record grouping
- ✅ Advanced filtering
- ✅ Multi-column layouts
- ✅ Custom formatting

See `CSV_SCHEMA_v2_INTEGRATION.md` for detailed reference.
