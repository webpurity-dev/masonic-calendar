# CSV Data Format v1.4 to v1.3 Conversion Guide

## Overview

This guide documents the differences between **units_v1.4.csv** and **units_v1.3.csv**, and explains how the conversion script transforms v1.4 data to match v1.3 structure.

## Data Format Comparison

### v1.3 Structure (Target)
| Column | Source | Notes |
|--------|--------|-------|
| Unit Type | v1.4 Field 1 | Craft, RA, Mark, RAM |
| Unit No | v1.4 Field 2 | Lodge/Chapter number (integer) |
| Unit Name | v1.4 Field 3 | Full formal name |
| Short Name | Derived from Unit Name | Same as Unit Name in v1.3 |
| Super Short Name | Derived from Unit Name | Abbreviated name (see rules below) |
| Warrant | v1.4 Field 4 | Warrant date and history text |
| Meeting Dates | v1.4 Field 5 | Meeting schedule description |
| Last Installation | v1.4 Field 6 | Most recent installation date |
| Hall | v1.4 Field 7 | Hall/meeting city (single word/phrase) |
| Location | Extracted from v1.4 Field 8 | Full address with city, building name |
| Email | Generated | `{Unit No}@dorsetfreemasonry.info` |

### v1.4 Structure (Source)
| Column | Notes |
|--------|-------|
| Unit Type | Same as v1.3 |
| Unit No | Same as v1.3 |
| Unit Name | Same as v1.3 |
| Warrant | Same as v1.3 |
| Meeting Dates | Same as v1.3 |
| Last Installation | Same as v1.3 |
| Hall | Hall/city name |
| Add No Postcode | **MERGED LOCATION**: Full "City Name, Hall Name, Street, Postcode" |
| H1-H6 | Parsed installation date components (ordinal, weekday, month) |
| Last Installation: | Header row garbage - IGNORED |
| Additional columns | Empty/junk headers - IGNORED |

## Conversion Rules

### Super Short Name Derivation

The conversion script generates Super Short Name from Unit Name using these rules:

1. **"Lodge of X" or "Chapter of X" pattern**: Extract everything after "of"
   - `Lodge of Amity` → `Amity`
   - `Lodge of Faith and Unanimity` → `Faith & Unanimity`
   - `Chapter of Exeter` → `Exeter`

2. **"X Lodge" or "X Chapter" suffix**: Remove the suffix
   - `Montagu Lodge` → `Montagu`
   - `St Mary's Lodge` → `St Mary's`
   - `All Souls Lodge` → `All Souls`

3. **Replace "and" with "&"**: Normalize compound names
   - `Faith and Unanimity` → `Faith & Unanimity`
   - `Honour and Friendship` → `Honour & Friendship`

4. **Default**: Use Unit Name as-is (for names not matching above patterns)

### Location Extraction

The script extracts location from the "Add No Postcode" field:
- **Input**: `"Poole Freemasons Hall, 4 Market Close, Poole"`
- **Extraction**: Takes everything up to the first comma (city name)
- **Output**: `"Poole,Poole Freemasons Hall, 4 Market Close, Poole"`

### Email Generation

Email addresses are generated from Unit No with domain suffix:
- **Pattern**: `{Unit No}@dorsetfreemasonry.info`
- **Example**: Unit No `137` → `137@dorsetfreemasonry.info`
- **Configurable**: Domain can be changed via `-DomainName` parameter

## Conversion Statistics

- **Total units converted**: 91
- **Headers normalized**: UTF8 encoding with proper quoting
- **Rows validated**: Minimum 8 columns required per row
- **Junk rows filtered**: Header corruption and empty rows removed

## Data Quality Notes

### Preserved Exactly
- ✓ Unit Type (Craft, RA, Mark, RAM)
- ✓ Unit No (lodge/chapter number)
- ✓ Unit Name (formal name)
- ✓ Warrant (date and history)
- ✓ Meeting Dates (schedule description)
- ✓ Last Installation (date)
- ✓ Hall (city/location)

### Generated/Derived
- ⚙ Short Name = Unit Name (straightforward copy)
- ⚙ Super Short Name = Derived from Unit Name (see rules)
- ⚙ Location = First part of address + full address
- ⚙ Email = Unit No + domain suffix

### Limitations & Known Issues

1. **Location field redundancy**: Currently outputs both city + full address
   - Example: `"Poole,Poole Freemasons Hall, 4 Market Close, Poole"`
   - This preserves all data but may include duplication
   - Consider post-processing if cleaner format needed

2. **Super Short Name special cases**:
   - Ambiguous names may not abbreviate optimally
   - Manually verify names like "De Moulham Lodge" → "De Moulham"
   - Multi-word names may need manual adjustment

3. **Email addresses format**:
   - Assumes dorsetfreemasonry.info domain
   - Verify domain is correct for your data
   - Can specify alternate domain: `.\convert-v1.4-to-v1.3.ps1 -DomainName "your-domain.org"`

## Usage

### Basic Conversion
```powershell
.\convert-v1.4-to-v1.3.ps1
```
Output: `units_v1.3-converted.csv`

### Custom Output Path
```powershell
.\convert-v1.4-to-v1.3.ps1 -OutputFile "C:\path\to\new_units.csv"
```

### Custom Email Domain
```powershell
.\convert-v1.4-to-v1.3.ps1 -DomainName "example.org"
```

### All Parameters
```powershell
.\convert-v1.4-to-v1.3.ps1 `
  -InputFile "e:\data\units_v1.4.csv" `
  -OutputFile "e:\data\units_v1.3-final.csv" `
  -DomainName "masons.org"
```

## Next Steps

1. **Review converted output**:
   ```powershell
   Import-Csv "units_v1.3-converted.csv" | Select -First 10
   ```

2. **Compare sample rows** with original v1.3:
   ```powershell
   # Original
   Import-Csv "units_v1.3.csv" | Select -First 1 | Format-List
   # Converted
   Import-Csv "units_v1.3-converted.csv" | Select -First 1 | Format-List
   ```

3. **Validate data integrity**:
   ```powershell
   # Count rows
   (Import-Csv "units_v1.3.csv").Count
   (Import-Csv "units_v1.3-converted.csv").Count
   ```

4. **Backup and replace** (if satisfied):
   ```powershell
   Move-Item "units_v1.3.csv" "units_v1.3.csv.bak"
   Move-Item "units_v1.3-converted.csv" "units_v1.3.csv"
   ```

5. **Update membership data** (v1.4 → v1.3 conversion also needed):
   ```powershell
   # Similar script can convert membership_v1.4.csv to v1.3 format
   # Update unit references if Unit No mapping changed
   ```

## Column Mapping Reference

| v1.3 Column | v1.4 Column | Transformation |
|-------------|-------------|-----------------|
| Unit Type | Field 1 | Direct copy |
| Unit No | Field 2 | Direct copy |
| Unit Name | Field 3 | Direct copy |
| Short Name | Unit Name | Direct copy |
| Super Short Name | Unit Name | Apply abbreviation rules |
| Warrant | Field 4 | Direct copy |
| Meeting Dates | Field 5 | Direct copy |
| Last Installation | Field 6 | Direct copy |
| Hall | Field 7 | Direct copy |
| Location | Field 8 | Extract city + preserve full address |
| Email | Unit No | Generate `{Unit No}@{Domain}` |

## Validation Checklist

- [ ] Output file created successfully
- [ ] Row count matches expected (91 units)
- [ ] All 11 columns present
- [ ] Unicode/UTF8 encoding correct
- [ ] No truncated fields
- [ ] Email addresses properly formatted
- [ ] Super Short Names appear reasonable
- [ ] Warrant history preserved
- [ ] Meeting dates preserved
- [ ] Installation dates accurate
- [ ] Location addresses complete
