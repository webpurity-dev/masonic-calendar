# Location CSV Split Implementation Plan

**Document Created:** May 10, 2026  
**Status:** Analysis Complete - Ready for Implementation  
**Complexity:** Medium (5-6 hours of dev work)  
**Risk Level:** Low

---

## 📋 Executive Summary

Splitting location data (Location, What3Words, Image) into a separate `unit_locations.csv` is **feasible and recommended** because:
- ✅ Reduces data redundancy (multiple units may share same hall)
- ✅ Improves location data maintenance (single source for location info)
- ✅ Enables enrichment (images, descriptions added independently)
- ⚠️ Requires code changes to join/lookup location data
- ⚠️ Impacts CSV data model and loading logic

---

## 🗂️ Current State Analysis

### Current CSV Structure (units_v1.6.csv)

**Columns involved:**
```
Unit Type | Unit No | Unit Name | ... | Hall | Location | Email | What3Words
Craft     | 137     | Lodge of Amity | ... | Poole | Poole Masonic Hall, 4 Market Close, Poole | 137@dorsetfreemasonry.info | groups.smiled.hush
```

**Location Data Properties:**
| Property | Type | Source | Current Use |
|----------|------|--------|-------------|
| Hall | string | units_v1.6.csv col "Hall" | Location page grouping key |
| Location | string | units_v1.6.csv col "Location" | Stored in `unit.LocationId` |
| What3Words | string | units_v1.6.csv col "What3Words" | Stored in `unit.What3Words` |
| Image | *(none)* | Generated dynamically | `GenerateLocationImagePath()` |

**Data Redundancy Example:**
```
Craft, 137, "Lodge of Amity", ..., "Poole", "Poole Masonic Hall, 4 Market Close, Poole", ..., "groups.smiled.hush"
Craft, 170, "All Souls Lodge", ..., "Poole", "Poole Masonic Hall, 4 Market Close, Poole", ..., "groups.smiled.hush"
Craft, 175, "Some Other Lodge", ..., "Poole", "Poole Masonic Hall, 4 Market Close, Poole", ..., "groups.smiled.hush"
```
→ Location address duplicated 3 times (one per unit at that hall)

### Current Code Usage

**SchemaDataLoader.LoadUnitsFromCsvAsync()** (lines 101-138)
- Line 115: `LocationId = GetFieldValueWithComposite(csv, fieldMap, "Location")`
- Line 120: `What3Words = GetFieldValueWithComposite(csv, fieldMap, "What3Words")`
- Reads directly from units CSV

**SchemaUnit Properties** (SchemaUnit.cs, lines 20-21)
```csharp
public string? LocationId { get; set; }  // Full address string
public string? What3Words { get; set; }  // e.g., "groups.smiled.hush"
```

**LocationSectionRenderer** (SectionRenderers/LocationSectionRenderer.cs)
- Line 49: `var address = hallGroup.First().LocationId ?? "Address to be confirmed";`
- Line 52: `var what3words = hallGroup.First().What3Words;`
- Line 69: `var imagePath = GenerateLocationImagePath(hallName);` (generates from hall name)

**UnitModelBuilder** (Utilities/UnitModelBuilder.cs, line 47)
```csharp
{ "location", TextCleaner.EnsureTrailingPeriod(unit.LocationId) },
```

### Data Source YAML Configuration

All 5 data source files (craft, mark, ram, royalarch, rcoc) have identical mappings:
```yaml
units:
  source: "units_v1.6.csv"
  fields:
    - name: "Location"
      csv_column: "Location"
      type: "string"
    - name: "What3Words"
      csv_column: "What3Words"
      type: "string"
```

---

## 💡 Proposed Solution

### New CSV Structure: unit_locations.csv

**File:** `document/data/unit_locations.csv`

**Columns:**
```csv
Hall,Location,What3Words,ImageFile
Poole,Poole Masonic Hall - 4 Market Close - Poole,groups.smiled.hush,poole_masonic_hall.png
Weymouth,Weymouth Masonic Hall - School Street - Weymouth,///word.word.word,weymouth_masonic_hall.png
Wareham,Wareham Freemasons Hall - Howards Lane - Wareham,///word.word.word,wareham_hall.png
Dorchester,Dorchester Freemasons Hall - Alington Street - Dorchester,///word.word.word,dorchester_hall.png
```

**Example Data Migration:**
```
Before (units_v1.6.csv - 3 units at Poole):
Craft,137,Lodge of Amity,...,Poole,Poole Masonic Hall - 4 Market Close - Poole,137@dorsetfreemasonry.info,groups.smiled.hush
Craft,170,All Souls Lodge,...,Poole,Poole Masonic Hall - 4 Market Close - Poole,170@dorsetfreemasonry.info,groups.smiled.hush
Craft,175,Another Lodge,...,Poole,Poole Masonic Hall - 4 Market Close - Poole,175@dorsetfreemasonry.info,groups.smiled.hush

After (units_v1.6.csv - Location removed):
Craft,137,Lodge of Amity,...,Poole,137@dorsetfreemasonry.info
Craft,170,All Souls Lodge,...,Poole,170@dorsetfreemasonry.info
Craft,175,Another Lodge,...,Poole,175@dorsetfreemasonry.info

New (unit_locations.csv):
Poole,Poole Masonic Hall - 4 Market Close - Poole,groups.smiled.hush,poole_masonic_hall.png
```

**Benefits:**
- ✅ Poole location address stored once (not 3 times)
- ✅ What3Words code stored once
- ✅ Image file name can be explicit (not auto-generated)
- ✅ Easy to add location description/metadata in future
- ✅ Images can be updated independently of unit data

---

## 🔧 Code Changes Required

### 1. SchemaUnit.cs - Minimal Changes

**Current:**
```csharp
public string? LocationId { get; set; }  // Full address string
public string? What3Words { get; set; }
```

**Proposed:**
```csharp
// Keep for backward compatibility during transition
public string? LocationId { get; set; }  // Hall name (lookup key)
public string? What3Words { get; set; }  // What3Words code (will be populated from joined data)

// New property to hold loaded location data
public SchemaLocation? Location { get; set; }  // Full location object with all details
```

**SchemaLocation class (already defined, lines 111-120):**
```csharp
public class SchemaLocation
{
    public string? Id { get; set; }           // = Hall name (primary key)
    public string? Name { get; set; }         // = Location address string
    public string? What3Words { get; set; }   // What3Words code
    public string? ImageFile { get; set; }   // Image filename (new)
    public string? AddressLine1 { get; set; } // (for future use)
    public string? Town { get; set; }         // (for future use)
    public string? Postcode { get; set; }    // (for future use)
}
```

### 2. SchemaDataLoader.cs - New Location Loading Logic

**New method needed:**
```csharp
/// <summary>
/// Loads unit_locations.csv and creates a lookup by Hall name.
/// Returns Dict<hallName, SchemaLocation>
/// </summary>
private async Task<Result<Dictionary<string, SchemaLocation>>> LoadLocationsFromCsvAsync(
    string locationsFile)
{
    // Read unit_locations.csv
    // Key by Hall column
    // Populate SchemaLocation objects
    // Return dictionary
}
```

**Modified LoadUnitsWithDataAsync() method:**
```csharp
public async Task<Result<List<SchemaUnit>>> LoadUnitsWithDataAsync(
    string masterTemplateKey, string? sectionId = null)
{
    // 1. Load units from units_v1.6.csv (Location/What3Words columns removed)
    var unitsResult = await LoadUnitsFromCsvAsync(mapping!);
    if (!unitsResult.Success) return /* error */;
    
    units = unitsResult.Data ?? [];
    
    // 2. Load locations from unit_locations.csv
    var locationsResult = await LoadLocationsFromCsvAsync(
        Path.Combine(_dataRoot, "unit_locations.csv"));
    if (!locationsResult.Success)
    {
        Console.WriteLine("⚠️ Warning: Could not load locations; continuing with hall names only");
        // Continue without location enrichment (graceful degradation)
    }
    else
    {
        // 3. Join location data to units by Hall
        var locationsDict = locationsResult.Data ?? new();
        foreach (var unit in units)
        {
            if (!string.IsNullOrWhiteSpace(unit.Hall) && 
                locationsDict.TryGetValue(unit.Hall, out var location))
            {
                unit.Location = location;
                unit.LocationId = location.Name;     // Keep for backward compat
                unit.What3Words = location.What3Words;
            }
        }
    }
    
    // 4. Continue with membership loading
    var hermesResult = await LoadHermesDataAsync(mapping!, units);
    // ...
}
```

**LoadLocationsFromCsvAsync() implementation:**
```csharp
private async Task<Result<Dictionary<string, SchemaLocation>>> LoadLocationsFromCsvAsync(
    string locationsFile)
{
    try
    {
        var locations = new Dictionary<string, SchemaLocation>(StringComparer.OrdinalIgnoreCase);
        
        if (!File.Exists(locationsFile))
            return Result<Dictionary<string, SchemaLocation>>.Fail(
                $"Locations file not found: {locationsFile}");
        
        using var reader = new StreamReader(locationsFile, Encoding.UTF8);
        using var csv = new CsvReader(reader, CultureInfo.InvariantCulture);
        
        await csv.ReadAsync();
        csv.ReadHeader();
        
        while (await csv.ReadAsync())
        {
            var hallName = csv.GetField("Hall")?.Trim();
            if (string.IsNullOrWhiteSpace(hallName))
                continue;
            
            var location = new SchemaLocation
            {
                Id = hallName,
                Name = csv.GetField("Location")?.Trim(),
                What3Words = csv.GetField("What3Words")?.Trim(),
                ImageFile = csv.GetField("ImageFile")?.Trim()
            };
            
            locations[hallName] = location;
        }
        
        return Result<Dictionary<string, SchemaLocation>>.Ok(locations);
    }
    catch (Exception ex)
    {
        return Result<Dictionary<string, SchemaLocation>>.Fail(
            $"Error loading locations CSV: {ex.Message}");
    }
}
```

### 3. LoadUnitsFromCsvAsync() - Minor Changes

**Current (lines 101-138):**
```csharp
LocationId = GetFieldValueWithComposite(csv, fieldMap, "Location"),
What3Words = GetFieldValueWithComposite(csv, fieldMap, "What3Words"),
```

**Updated:**
```csharp
// Location data will be populated from unit_locations.csv join
// For now, set to null or hall name
LocationId = GetFieldValueWithComposite(csv, fieldMap, "Location") ?? 
    GetFieldValueWithComposite(csv, fieldMap, "Hall"),
What3Words = GetFieldValueWithComposite(csv, fieldMap, "What3Words"),
```

### 4. Data Source YAML Files (5 files)

**Current craft_data_source.yaml (lines 34-41):**
```yaml
    - name: "Location"
      csv_column: "Location"
      type: "string"
    - name: "What3Words"
      csv_column: "What3Words"
      type: "string"
```

**Updated (remove these fields or mark as deprecated):**
```yaml
    # Location and What3Words now loaded from unit_locations.csv
    # Keeping for backward compatibility - data loader will handle lookup
    - name: "Location"
      csv_column: "Location"
      type: "string"
      optional: true    # Mark as optional since column will be removed
    - name: "What3Words"
      csv_column: "What3Words"
      type: "string"
      optional: true
```

**Action:** Update all 5 data_source YAML files (craft, mark, ram, royalarch, rcoc)

### 5. LocationSectionRenderer.cs - No Major Changes

**Current logic (line 49):**
```csharp
var address = hallGroup.First().LocationId ?? "Address to be confirmed";
```

**Still works because:**
- `unit.LocationId` is populated from unit_locations.csv join
- No code changes needed to LocationSectionRenderer

**Enhancement opportunity:**
```csharp
// Now we have access to full SchemaLocation object
var location = hallGroup.First().Location;
var address = location?.Name ?? hallGroup.First().LocationId ?? "Address to be confirmed";
var imagePath = location?.ImageFile != null 
    ? $"../document/images/locations/{location.ImageFile}"
    : GenerateLocationImagePath(hallName);  // Fallback to auto-generation
```

### 6. UnitModelBuilder.cs - Optional Enhancement

**Current (line 47):**
```csharp
{ "location", TextCleaner.EnsureTrailingPeriod(unit.LocationId) },
```

**Optional: Use full location object when available**
```csharp
{
    "location", unit.Location != null 
        ? new Dictionary<string, object?>
        {
            { "name", unit.Location.Name },
            { "what3words", unit.Location.What3Words },
            { "image", unit.Location.ImageFile }
        }
        : TextCleaner.EnsureTrailingPeriod(unit.LocationId)
},
```

---

## 📊 Data Migration Script

**PowerShell script needed: `document/data/migrate-to-locations-csv.ps1`**

```powershell
# 1. Extract unique Hall values from units_v1.6.csv
# 2. For each unique hall:
#    - Find first occurrence in units_v1.6.csv
#    - Extract Location and What3Words columns
#    - Create row in unit_locations.csv
# 3. Remove Location and What3Words columns from units_v1.6.csv
# 4. Backup original to units_v1.6-backup.csv

# Pseudocode:
# $locations = @{}
# foreach ($row in csv) {
#     $hall = $row["Hall"]
#     if (!$locations.ContainsKey($hall)) {
#         $locations[$hall] = @{
#             Hall = $hall
#             Location = $row["Location"]
#             What3Words = $row["What3Words"]
#             ImageFile = ""  # Leave blank - user can fill in later
#         }
#     }
# }
# Export $locations to unit_locations.csv
```

---

## 🎯 Impact on Different Components

### ✅ **LocationSectionRenderer** (Minimal)
- **Current:** Works as-is with LocationId field
- **Change:** Can optionally use `unit.Location` object for richer data
- **Risk:** None
- **Effort:** 0-15 minutes for optional enhancement

### ✅ **UnitModelBuilder** (Minimal)
- **Current:** Uses `unit.LocationId` string
- **Change:** Can optionally use `unit.Location` object
- **Risk:** None (backward compatible)
- **Effort:** 0-10 minutes for optional enhancement

### ✅ **Templates** (No Changes)
- unit-page.html: Uses `unit.location` → still works
- location-page.html: Receives location data in model → still works
- No template changes needed

### ⚠️ **SchemaDataLoader** (Medium)
- **Current:** Loads Location/What3Words from units CSV
- **Change:** Add new location loading + join logic
- **Risk:** Must handle missing location file gracefully
- **Effort:** 1.5-2 hours for robust implementation

### ⚠️ **Data Source YAML** (Minor)
- **Current:** Map Location and What3Words from units CSV
- **Change:** Mark as optional or remove entirely
- **Risk:** Backward compatibility - old mapping still works
- **Effort:** 15 minutes to update 5 files

### ⚠️ **CSV Data Files** (Medium)
- **Current:** Location/What3Words in units_v1.6.csv
- **Change:** Remove from units CSV, create unit_locations.csv
- **Risk:** Data loss if not migrated properly
- **Effort:** 30 minutes for script + 15 minutes to run/verify

---

## 📋 Implementation Checklist

### Phase 1: Preparation
- [ ] Create unit_locations.csv with sample data
- [ ] Create migration script (PowerShell)
- [ ] Backup current units_v1.6.csv
- [ ] Test migration script on backup

### Phase 2: Code Implementation
- [ ] Add LoadLocationsFromCsvAsync() to SchemaDataLoader
- [ ] Update LoadUnitsWithDataAsync() to call location loading
- [ ] Update LoadUnitsFromCsvAsync() with optional field handling
- [ ] Add SchemaLocation population logic (join by Hall)
- [ ] Add unit tests for location loading
- [ ] Add unit tests for join logic

### Phase 3: Configuration Updates
- [ ] Update craft_data_source.yaml (mark Location/What3Words optional)
- [ ] Update mark_data_source.yaml
- [ ] Update ram_data_source.yaml
- [ ] Update royalarch_data_source.yaml
- [ ] Update rcoc_data_source.yaml

### Phase 4: Data Migration
- [ ] Run migration script on units_v1.6.csv
- [ ] Verify unit_locations.csv created correctly
- [ ] Verify units_v1.6.csv has Location/What3Words removed
- [ ] Test rendering with new data structure
- [ ] Commit changes with backups

### Phase 5: Testing & Validation
- [ ] Test full document render (-output html)
- [ ] Test individual section render (-section locations)
- [ ] Test with missing locations file (graceful degradation)
- [ ] Test what3words rendering
- [ ] Test location images
- [ ] Verify no regressions in unit pages

### Phase 6: Documentation
- [ ] Update README.md with new file structure
- [ ] Add migration notes to changelog
- [ ] Document unit_locations.csv format
- [ ] Update data_sources documentation

---

## ⚠️ Risks & Mitigations

| Risk | Impact | Mitigation |
|------|--------|-----------|
| **Missing location file** | Rendering fails | Implement graceful degradation: fall back to LocationId only |
| **Hall names mismatch** | Locations not joined | Use case-insensitive dictionary lookup, log warnings |
| **Data migration errors** | Data loss | Create backup before migration, validate row counts before/after |
| **Template breaks** | Rendering fails | Location is optional; templates already handle null checks |
| **Image paths incorrect** | Images not found | Use explicit ImageFile column instead of auto-generation |
| **Backward compatibility** | Old code breaks | Keep LocationId field populated during join |

---

## 🚀 Benefits

### Data Quality
- ✅ **Single source of truth** for location info
- ✅ **Reduced redundancy** (20-30% CSV size reduction estimated)
- ✅ **Easier maintenance** (update location once, not 3+ times)

### Flexibility
- ✅ **Explicit image filenames** (not auto-generated from hall name)
- ✅ **Future fields** (description, phone, contact, hours)
- ✅ **Independent updates** (images without changing units CSV)

### Code Quality
- ✅ **Cleaner separation of concerns** (location data separate)
- ✅ **Testable logic** (location loading testable independently)
- ✅ **Lookup pattern** (join logic useful for other data)

### Maintainability
- ✅ **Smaller CSV files** (easier to review/edit manually)
- ✅ **Clear semantics** (location-specific columns separate)
- ✅ **Consistent with best practices** (normalization)

---

## 📈 Estimated Effort

| Task | Estimate | Notes |
|------|----------|-------|
| Create unit_locations.csv | 30 min | Manual extraction from current CSV |
| Migration script | 1 hour | PowerShell to extract unique halls |
| SchemaDataLoader changes | 1.5 hrs | LoadLocationsFromCsvAsync + join logic |
| YAML updates | 20 min | 5 files, same change |
| Unit tests | 45 min | Location loading, join, fallback |
| Integration testing | 1 hour | Full render, edge cases |
| Documentation | 30 min | README, changelog |
| **Total** | **5-6 hours** | Moderate complexity, low risk |

---

## 🔄 Transition Strategy

### Backward Compatibility
1. Keep `LocationId` property on SchemaUnit (populated from CSV column or join)
2. Keep `What3Words` property (populated from join)
3. All code using these properties continues to work
4. Templates don't require changes

### Phased Rollout
1. **Phase 1:** Create unit_locations.csv alongside existing CSV (not in use yet)
2. **Phase 2:** Add location loading code (disabled by flag)
3. **Phase 3:** Enable location loading, test thoroughly
4. **Phase 4:** Remove Location/What3Words from units CSV
5. **Phase 5:** Require unit_locations.csv (v1.7+)

### Rollback Option
If needed, can recreate Location/What3Words columns in units CSV from unit_locations.csv in reverse

---

## 📝 Next Steps

When ready to start implementation:

1. **Review** this document
2. **Create** unit_locations.csv from current data
3. **Implement** LoadLocationsFromCsvAsync()
4. **Test** with full document render
5. **Migrate** production data
6. **Archive** old CSV structure

---

## 📎 References

- [Location Column CSV Parsing Analysis](/memories/repo/location-column-parsing.md)
- Current impact assessment: `/memories/session/location_csv_split_impact.md`
- SchemaDataLoader: `src/MasonicCalendar.Core/Loaders/SchemaDataLoader.cs`
- LocationSectionRenderer: `src/MasonicCalendar.Core/Renderers/SectionRenderers/LocationSectionRenderer.cs`
- SchemaUnit: `src/MasonicCalendar.Core/Domain/SchemaUnit.cs`

