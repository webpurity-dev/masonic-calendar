# Masonic Calendar - Copilot Agent Instructions

**Date Updated:** April 30, 2026  
**Project Type:** C# .NET 8.0 Console Application  
**Purpose:** Non-profit calendar/document generation system with PDF rendering via Puppeteer and Scriban templating  
**Current Version:** v1.5 (production-ready)  
**Next Version:** v1.6 (columnar CSV format, 11 degree types) — In planning/design phase

---

## 📋 Quick Reference

### Active Features (v1.5)
- **3-Column Members Table Layout** (FIXED): Inline-block CSS layout maintains structure across page breaks
- **Unit Filtering**: `-unit` CLI parameter renders single units only (core feature)
- **Paged.js Integration**: W3C-standard print rendering with automatic pagination
- **TOC Page Numbers**: JavaScript injection calculates page numbers after Paged.js pagination
- **Membership Summary Pages**: Dedicated summary of member counts per degree (NEW v1.5)
- **Meeting Date Tables**: Full 12-month meetings table for all degrees — Craft, Mark, Royal Arch, RAM (NEW v1.5)
- **Enhanced Executive Officers**: Comprehensive officer lists with ranks and dates (NEW v1.5)
- **Alphabetical TOC**: Table of contents sorted alphabetically by unit name (NEW v1.5)
- **Data Validation**: Validation reports for meeting dates, unit counts, member deduplication (NEW v1.5)
- **4 Degree Types**: Craft, Royal Arch, Mark, Royal Ark Mariners

### Current Status
- ✅ v1.5 production-ready with all major features complete
- ✅ Members table stable across page breaks
- ✅ Unit filtering works end-to-end
- ✅ Meeting tables and membership summaries rendering correctly
- ✅ Validation pipeline operational

### v1.6 Planning (Upcoming)
- **Columnar CSV Format**: Moving from row-based to column-grouped CSV structure
- **11 Degree Types**: Support for KT, KTP, OSC, PBQ, RCOC, STOA (in addition to current 4)
- **Generic Terminology**: Replace `SchemaPastMaster` → `SchemaPastUnitHead` (works for all degrees)
- **Unique ID Generation**: Composite keys from YAML-configured fields (to replace lost row identity)
- **YAML Column Configuration**: Column ranges, filters, unique ID fields defined in data_source.yaml
- **Status**: Analysis phase complete (see ANALYSIS_v1.6_FORMAT.md), implementation starts May 1, 2026

### Recent Discoveries (This Phase)
1. **v1.5 Feature Completeness**: All planned features implemented and validated
   - Membership summaries working correctly
   - Meeting tables accurate for all degrees
   - Data validation preventing duplicate entries
   - Performance: Full document renders in 60-90 seconds

2. **v1.6 Format Analysis Complete**
   - Column structure mapped: 5 fixed sections (Members, Officers, Past Unit Heads, Joining, Honorary)
   - Unique ID strategy: Composite keys from section-specific fields (deterministic, deduplication-ready)
   - Two rank sources in Joining sections: Provincial Grand Rank (origin) vs Grand Rank (destination)
   - Reference: `/ANALYSIS_v1.6_FORMAT.md` (1300+ lines, implementation blueprint)

---

## 🏗️ Technical Stack

**Framework:** .NET 8.0 (C#) Console Application  
**PDF Generation:** PuppeteerSharp 15.0.0 + Paged.js 1.0+ W3C Paged Media polyfill  
**HTML Rendering:** Chromium browser (headless mode)  
**Templating:** Scriban 5.4.6 (Liquid-like syntax)  
**CSV Parsing:** CsvHelper 30.0.0  
**Page Layout:** CSS @page rules with Paged.js for pagination  
**Page Numbering:** CSS counters in margin boxes + JavaScript injection for TOC  

---

## 🎯 Architecture Overview

### Data Flow Pipeline
```
CLI Parameters (-template, -unit, -section, -output)
    ↓
Program.cs: Parameter parsing & unit filtering
    ↓
SchemaDataLoader: Load CSV data into SchemaUnit objects
    ↓
SchemaPdfRenderer: Render sections → HTML
    ↓
SectionRendererFactory: Route to specific renderer (DataDriven/Static/TOC)
    ↓
Unit-level rendering: Apply Scriban templates
    ↓
ConvertHtmlToPdf: Puppeteer + Paged.js → PDF/HTML output
```

### Key Components

**Program.cs** (CLI Entry Point)
- Lines 14-47: Parameter parsing for `-template`, `-output`, `-section`, `-unit`, `-debug`, `-showbleeds`
- Lines 80-101: Unit filtering logic (respects `-unit` parameter)
- Lines 119-134: Auto-section selection when `-unit` specified (defaults to `craft_units`)
- Line 144: Passes filtered `unitsToRender` to renderer

**SchemaPdfRenderer.cs** (Main Rendering Engine)
- `RenderAsync()` - Entry point, delegates to RenderSectionAsync or RenderAllSectionsAsync
- `RenderSectionAsync()` - Renders single section (respects filtered units parameter) ⭐ FIXED
- `RenderAllSectionsAsync()` - Renders all sections in template order
- Contains Paged.js integration: page waiting logic, page number injection
- Console logging for debugging rendering progress

**SectionRendererFactory + Specific Renderers**
- Routes sections to appropriate renderer: DataDrivenSectionRenderer, StaticSectionRenderer, TocSectionRenderer
- Each handles specific section type logic

**Document Templates** (`document/templates/`)
- `print.css` - Paged.js @page rules, margins, TOC styling, membership summary styling
- `unit-page.html` - Scriban template for individual unit pages (members, officers, past unit heads)
- `toc-page.html` - Table of contents page template with alphabetical sorting
- `cover-page.html` - Static cover page with organization branding
- `foreword-page.html` - Foreword/introduction page
- `meetings-calendar-page.html` - 12-month meetings calendar for all degrees
- `membership-summary-page.html` - NEW v1.5: Summary of member counts and statistics
- `meetings-table-page.html` - NEW v1.5: Detailed meeting dates table
- `unit-index-page.html` - NEW v1.5: Alphabetical unit index
- `companion/` - Subdirectory with degree-specific intro pages (Mark, RAM)
- `craft/` - Degree-specific templates for Craft lodges
- `royalarch/` - Degree-specific templates for Royal Arch chapters

---

## 💾 Key Implementation Details

### Members Table Layout (3-Column with Page Breaks)

**Problem Solved:** Table collapsed to 1 column on first page, then 3 columns on subsequent pages

**CSS Solution (Option B - Natural Flow):**
```css
.members-container {
    font-size: 0;              /* Eliminate whitespace between inline-block items */
    letter-spacing: -0.25em;   /* Negative letter-spacing removes gaps */
    break-inside: avoid;       /* Keep heading with tables */
}

.member-column {
    display: inline-block;
    width: calc(33.333% - 2px);  /* Explicit width prevents recalculation */
    vertical-align: top;
    font-size: 14px;             /* Reset font-size for content */
    letter-spacing: normal;      /* Reset letter-spacing for content */
    padding: 0 1px;              /* Micro padding */
}
```

**Why This Works:**
- Inline-block doesn't recalculate widths when Paged.js changes container size
- Explicit `calc()` widths override browser defaults
- Font-size: 0 on container + reset on children eliminates HTML whitespace rendering
- Allows natural page flow: content fills 3 columns then continues on next page

**Location:** `document/templates/unit-page.html` lines 111-170

---

### Unit Filtering with `-unit` Parameter

**How It Works:**
```bash
## Render only unit 3366 from craft section
dotnet run -- -template master_v1 -output html -unit 3366

## Render only unit 3366 from royal arch
dotnet run -- -template master_v1 -output html -unit 3366 -section royalarch_units
```

**CLI Implementation:**
```csharp
// Program.cs parsing (lines 80-101)
if (int.TryParse(unitNumber, out int unitNumberInt))
{
    unitsToRender = unitsToRender.Where(u => u.Number == unitNumberInt).ToList();
    Console.WriteLine($"Γ£ô Filtered to {unitsToRender.Count} unit(s) matching '{unitNumber}'");
}

// Auto-select craft_units section when unit specified (lines 119-134)
if (!string.IsNullOrWhiteSpace(unitNumber) && string.IsNullOrWhiteSpace(sectionId))
{
    sectionId = "craft_units";
    Console.WriteLine($"≡ƒôä Rendering unit {unitNumber} from craft section");
}
```

**Renderer Implementation:**
```csharp
// SchemaPdfRenderer.RenderSectionAsync() (line ~350)
// FIXED: Use filtered units parameter directly instead of reloading all
var unitsToRender = units;  // Already filtered by CLI
```

**Output Validation:**
- Single unit: 87.0KB HTML, 1 `class='unit-page'` div
- All units: 2428.0KB HTML, 49 `class='unit-page'` divs

---

### Paged.js Page Number Injection

**Process:**
1. Puppeteer loads HTML with `SetContentAsync()` - triggers Paged.js rendering
2. Wait for Paged.js pagination to stabilize:
   - Check for `.pagedjs_pages` container
   - Poll page count until stable (3 consecutive identical counts)
   - Max wait: 60 seconds
3. Call JavaScript function `injectTocPageNumbers()`
4. Function finds each TOC link and calculates which page contains its target
5. Creates `<span class="toc-page-number">` with page number
6. Generate PDF with `PrintToPdfAsync()`

**JavaScript Function:**
```javascript
function injectTocPageNumbers() {
    const tocLinks = document.querySelectorAll('.toc-item a');
    const pages = document.querySelectorAll('.pagedjs_page');
    
    tocLinks.forEach(link => {
        const href = link.getAttribute('href');
        const anchorId = href.substring(1);
        const targetElement = document.getElementById(anchorId);
        
        if (targetElement) {
            for (let i = 0; i < pages.length; i++) {
                if (pages[i].contains(targetElement)) {
                    const span = document.createElement('span');
                    span.className = 'toc-page-number';
                    span.textContent = (i + 1).toString();
                    link.appendChild(span);
                    break;
                }
            }
        }
    });
}
```

**Debugging:**
- Console output shows `[injectTocPageNumbers]` status messages
- Browser console displays success/failure
- Check `.toc-item a` and `.pagedjs_page` selectors match actual HTML

---

### Document Structure (master_v1 Template) — v1.5

**Sections (in order):**
1. `cover` (static) - Static cover page with organization branding
2. `master_toc` (toc) - Master table of contents (alphabetically sorted)
3. `master_foreword` (static) - Foreword/introduction
4. **Membership Summary** (static) - NEW v1.5: Summary statistics for all degrees
5. **Unit Index** (static) - NEW v1.5: Alphabetical listing of all units
6. `craft_toc` (toc) - Craft units table of contents
7. `craft_units` (data-driven) - All craft unit pages
8. `craft_intro` (static) - Craft degree introduction
9. **Craft Membership Summary** (static) - NEW v1.5: Craft-specific statistics
10. **Craft Meetings Table** (static) - NEW v1.5: Full 12-month meeting dates
11. `royalarch_toc` (toc) - Royal Arch table of contents
12. `royalarch_units` (data-driven) - All Royal Arch unit pages
13. `royalarch_intro` (static) - Royal Arch degree introduction
14. **Royal Arch Membership Summary** (static) - NEW v1.5
15. **Royal Arch Meetings Table** (static) - NEW v1.5
16. `mark_toc` (toc) - Mark units table of contents
17. `mark_units` (data-driven) - All Mark unit pages
18. `mark_intro` (static) - Mark degree introduction
19. **Mark Membership Summary** (static) - NEW v1.5
20. **Mark Meetings Table** (static) - NEW v1.5
21. `ram_toc` (toc) - Royal Ark Mariners table of contents
22. `ram_units` (data-driven) - All RAM unit pages
23. `ram_intro` (static) - RAM degree introduction
24. **RAM Membership Summary** (static) - NEW v1.5
25. **RAM Meetings Table** (static) - NEW v1.5

**Unit Page Content (v1.5):**
- Officers: Master, Past Master, Wardens, Secretary, Treasurer, etc. with ranks and installation dates
- Past Unit Heads: Chronological list with installation dates and provincial/grand ranks
- Joining Past Unit Heads: List of members who joined from other lodges with origin lodge numbers
- Members: 3-column layout (alphabetical by surname)
- Honorary Members: Name and rank
- Location: Address, contact information

**Data Validation (v1.5):**
- Meeting date validation: Checks for date consistency across all degree types
- Member deduplication: Identifies and removes duplicate entries
- Unit count verification: Validates total units match expectations
- CSV format validation: Ensures data structure integrity

---

## 📝 Coding Patterns & Conventions

**File-Scoped Namespaces:**
```csharp
namespace MasonicCalendar.Core.Services;
// (no braces)
```

**Primary Constructors:**
```csharp
public class Service(IDependency dep, ILogger log)
{
    private readonly IDependency _dep = dep;
}
```

**Result<T> Pattern for Operations That Can Fail:**
```csharp
public Result<List<SchemaUnit>> LoadUnits(string source)
{
    try
    {
        var units = /* ... */;
        return Result<List<SchemaUnit>>.Ok(units);
    }
    catch (Exception ex)
    {
        return Result<List<SchemaUnit>>.Fail($"Failed to load: {ex.Message}");
    }
}
```

**Async/Await (Never Blocking):**
```csharp
public async Task<Result<byte[]>> RenderAsync(/* ... */)
{
    var result = await _renderer.RenderAsync(/* ... */);
    return result;
}
```

**CSS-First Layout in Print Media:**
- Avoid programmatic positioning
- Use CSS @page rules for sizing
- Use flexbox/grid for responsive layouts
- Use inline-block for stable print layouts across page breaks

**Logging:**
```csharp
Console.WriteLine($"≡ƒôä Processing {units.Count} units");  // User-facing
Console.WriteLine($"Γ£ô Success message");                   // Status
Console.WriteLine($"⚠️ Warning message");                    // Warning
```

---

## 🔍 Common Debugging Tasks

### Debug Members Table Layout Issues
1. Generate HTML: `dotnet run -- -template master_v1 -output HTML`
2. Open in browser, check Members section
3. Verify 3-column layout on first page
4. Check that columns flow naturally across page breaks
5. If collapsing: likely CSS width recalculation - use explicit `calc()` widths

### Debug Page Number Injection
1. Generate HTML: `dotnet run -- -template master_v1 -output HTML`
2. Open HTML in browser - Paged.js loads from CDN
3. Wait 3-5 seconds for page numbers to appear in TOC
4. Check browser console for `[injectTocPageNumbers]` messages
5. Verify selectors match: `.toc-item a`, `.pagedjs_page`, anchor IDs

### Debug Unit Filtering
1. Verify CLI parameter: `dotnet run -- -unit 3366`
2. Check console output shows "Filtered to 1 unit(s)"
3. Check output file size (87.0KB for single unit vs 2428.0KB for all)
4. Count unit-page divs: `(Get-Content file.html | Select-String "unit-page" | Measure-Object).Count`

### Debug PDF Rendering Issues
1. Always test HTML output first: `-output HTML`
2. Check Paged.js pagination: Look for console messages about page count
3. Verify margin boxes in PDF: Check for page numbers at bottom
4. Check background colors: Ensure `PrintBackground: true` in PdfOptions

---

## 📂 Project File Structure

```
document/
  ├── master_v1.yaml            # Master template configuration for all sections
  ├── templates/
  │   ├── print.css              # Paged.js @page rules, margins, membership summary styling
  │   ├── unit-page.html         # Unit page Scriban template (officers, members, past heads)
  │   ├── toc-page.html          # Alphabetical TOC template (v1.5)
  │   ├── unit-index-page.html   # Unit index page (NEW v1.5)
  │   ├── membership-summary-page.html  # Member statistics (NEW v1.5)
  │   ├── meetings-table-page.html     # Meeting dates table (NEW v1.5)
  │   ├── cover-page.html        # Cover page
  │   ├── foreword-page.html     # Foreword page
  │   ├── meetings-calendar-page.html  # Calendar grid
  │   ├── copyright.html         # Copyright info
  │   ├── craft/                 # Craft degree-specific templates
  │   ├── royalarch/             # Royal Arch degree-specific templates
  │   ├── companion/             # Companion degree templates (Mark, RAM)
  │   └── *-introduction.html    # Degree intro pages
  ├── data/
  │   ├── units_v1.5.csv         # Unit master data (current version)
  │   ├── membership_v1.5.csv    # Member data (current version)
  │   ├── unit-meetings.csv      # Meeting definitions
  │   ├── units_raw_v1.4.csv     # Previous version (archive)
  │   ├── units_v1.4.csv         # Previous version (archive)
  │   ├── units_v1.3.csv         # Previous version (archive)
  │   ├── membership_v1.4.csv    # Previous version (archive)
  │   ├── membership_v1.3.csv    # Previous version (archive)
  │   └── sections_v1.6.csv      # INCOMING: Columnar v1.6 format (in progress)
  ├── data_sources/
  │   ├── craft_data_source.yaml         # Craft CSV column mappings
  │   ├── royalarch_data_source.yaml     # Royal Arch CSV column mappings
  │   ├── mark_data_source.yaml          # Mark CSV column mappings (added v1.5)
  │   ├── ram_data_source.yaml           # Royal Ark Mariners CSV mappings (added v1.5)
  │   └── meetings_data_source.yaml      # Meeting recurrence rules
  └── images/
      └── cover-img-yellow.jpg           # Cover page background image

src/
  ├── MasonicCalendar.Console/
  │   ├── Program.cs             # CLI entry point, parameter parsing
  │   └── MasonicCalendar.Console.csproj
  └── MasonicCalendar.Core/
      ├── Domain/                # Business entities (SchemaUnit, SchemaMember, etc.)
      ├── Loaders/               # Data loading (CSV, YAML, templates)
      ├── Renderers/             # PDF/HTML rendering (SchemaPdfRenderer, section renderers)
      │   └── SectionRenderers/  # Section-specific renderers (Data-driven, Static, TOC)
      └── Services/              # Business logic (RecurrenceService, TextCleaner, etc.)

output/
  ├── master_v1.1.5-all-sections.html   # Full rendered document (v1.5)
  ├── validation-YYYY-MM-DD-HHMMSS.csv  # Data validation reports (NEW v1.5)
  ├── v1.5/                             # v1.5 release outputs
  │   ├── craft/                        # Craft section outputs
  │   ├── mark/                         # Mark section outputs
  │   ├── ra/                           # Royal Arch section outputs
  │   └── ram/                          # Royal Ark Mariners section outputs
  ├── v1.4/                             # v1.4 release archive
  ├── v1.3/                             # v1.3 release archive
  └── calendar/                         # Meeting calendar exports
```

---

## 🚀 CLI Usage Examples (v1.5)

**Render full master template to HTML:**
```bash
cd src/MasonicCalendar.Console
dotnet run -- -template master_v1 -output html
```

**Render single section to PDF:**
```bash
dotnet run -- -template master_v1 -section craft_units -output pdf
```

**Render single unit (craft section):**
```bash
dotnet run -- -template master_v1 -unit 3366 -output html
```

**Render single unit from royal arch:**
```bash
dotnet run -- -template master_v1 -unit 3366 -section royalarch_units -output pdf
```

**Render membership summaries only:**
```bash
dotnet run -- -template master_v1 -section craft_membership_summary -output html
```

**Render meetings table for all degrees:**
```bash
dotnet run -- -template master_v1 -section craft_meetings_table -output html
```

**Debug mode with bleed visualization:**
```bash
dotnet run -- -template master_v1 -output html -debug -showbleeds
```

**Render with data validation:**
```bash
# Validation reports generated automatically
dotnet run -- -template master_v1 -output html
# Check: output/validation-YYYY-MM-DD-HHMMSS.csv
```

## 🔄 v1.6 Data Model Changes (Upcoming)

### New Classes
**SchemaPastUnitHead** (replaces Craft-specific SchemaPastMaster)
```csharp
public class SchemaPastUnitHead
{
    public string UniqueId { get; set; }        // Generated composite key
    public int UnitNumber { get; set; }
    public string Name { get; set; }
    public int Joined { get; set; }
    public int? Installed { get; set; }
    public string ProvincialRank { get; set; }
    public int? DateRankAccorded { get; set; }
    public string GrandRank { get; set; }       // NEW: Grand rank in unit
    public int? GrandRankDateAccorded { get; set; }
}
```

**SchemaJoiningPastUnitHead** (new, separate from Past Unit Heads)
```csharp
public class SchemaJoiningPastUnitHead
{
    public string UniqueId { get; set; }        // Generated composite key
    public int UnitNumber { get; set; }
    public List<int> OriginLodges { get; set; } // Lodges joined from
    public int InstalledInCurrentUnit { get; set; }
    public string Name { get; set; }
    public int Joined { get; set; }
    public string ProvincialGrandRank { get; set; }   // From origin lodge
    public int? DateRankAccorded { get; set; }
    public string GrandRank { get; set; }            // In destination unit
    public int? GrandRankDateAccorded { get; set; }
}
```

### Modified Classes
**SchemaMember** (adds new fields)
```csharp
// NEW properties in v1.6:
public string UniqueId { get; set; }   // Generated composite key
public int? JoinDate { get; set; }     // Year joined
```

**SchemaUnit** (collection name changes)
```csharp
// RENAMED in v1.6:
// Old: public List<SchemaPastMaster> PastMasters { get; set; }
// New: public List<SchemaPastUnitHead> PastUnitHeads { get; set; }

// Old: public List<SchemaJoiningPastMaster> JoiningPastMasters { get; set; }
// New: public List<SchemaJoiningPastUnitHead> JoiningPastUnitHeads { get; set; }
```

### Benefits of v1.6 Structure
- ✅ **Generic Terminology**: Works for all 11 degree types (PM, PP, PMM, PC, PPrM, etc.)
- ✅ **Unique Identities**: Composite keys enable deduplication and change tracking
- ✅ **Flexible Ranks**: Separate fields for different rank types and sources
- ✅ **Audit Ready**: ID generation is deterministic and logged
- ✅ **Future-Proof**: YAML configuration means no code changes for new degrees

---

## 🔄 v1.6 Columnar CSV Format

### Column Structure
```
Columns 0-4:   Members (Unit Type, Unit No, Name, Join Date, empty)
Columns 5-9:   Officers (Unit Type, Unit No, Name, Office, empty)
Columns 10-19: Past Unit Heads (Unit No, Unit Type, Name, Joined, Installed, Prov Rank, Date, Grand Rank, GR Date, empty)
Columns 20-29: Joining Past Unit Heads (Unit No, Lodge, Installed, Unit Type, Name, Joined, Prov Grand Rank, Date, Grand Rank, GR Date, empty)
Columns 30-33: Honorary Members (Unit Type, Unit No, Name, Rank)
```

### Key Differences from v1.5
- **Row-based CSV** → **Column-grouped CSV** (multiple person types per row)
- Each row contains data for 5 different person type sections simultaneously
- Column indices are configurable via YAML (not hardcoded in C#)
- Unique IDs are generated from composite key fields (defined per section in YAML)
- Supports all 11 degree types via same parsing logic + section-specific filters

### Example Row (Craft unit 137)
```
Craft,137,Howard D,1964,,Craft,137,Jones W,Engineer,,137,Craft,Shorto R J,1959,1987,PPJGW,2019,PPGSwdB,2025,,137,5848,1987,Craft,Smith P,1950,PPGM,2020,PPGSwdB,2025,,Craft,137,Brown A,PPGM
```

### ColumnarCsvParser (New in v1.6)
Will implement these key methods:
- `ParseSectionStructure()` — Detect column sections from headers
- `ExtractSectionData()` — Extract specific columns for one section type
- `GenerateUniqueId()` — Create composite key from YAML-configured fields
- `ParseMembersSection()` — Extract & deduplicate members
- `ParseOfficersSection()` — Extract & deduplicate officers
- `ParsePastUnitHeadsSection()` — Extract & deduplicate past unit heads
- `ParseJoiningPastUnitHeadsSection()` — Handle multiple lodges + dual ranks
- `ParseHonoraryMembersSection()` — Extract & deduplicate honorary members

### Unique ID Strategy
| Section | Composite Key Fields | Example |
|---------|----------------------|---------|
| Members | unit_type + unit_no + name + join_date | `Craft-137-Howard D-1964` |
| Officers | unit_type + unit_no + name + office | `Craft-137-Jones W-Engineer` |
| Past Unit Heads | unit_no + unit_type + name + installed | `137-Craft-Shorto R J-1987` |
| Joining Past | unit_no + name + installed_in_unit | `137-Smith P-1987` |
| Honorary | unit_type + unit_no + name + rank | `Craft-137-Brown A-PPGM` |

---

## ⚠️ Important Notes

### Paged.js Behavior
- **Puppeteer Context:** Has access to `.pagedjs_page` elements, full styling works
- **Browser (local HTML):** Loads from CDN, may have 3-5 second delay
- **Relative Image Paths:** Converted to data URLs for PDF portability

### Performance Considerations
- Large documents (200+ pages): 60-90 second Puppeteer rendering
- Image conversion to data URLs adds processing time
- Chromium download: ~150MB on first run
- Memory: Multiple units + large images may need heap adjustment

### CSS and Print Layout
- Paged.js recalculates container sizes on new pages
- Use explicit widths (`calc()`) instead of percentages for stability
- `break-inside: avoid` keeps elements together but may push to next page if too large
- Inline-block is more stable than flexbox for multi-column layouts in print

---

## 📚 v1.5 Session Summary (April 2026)

**Major Features Implemented:**
1. ✅ Membership Summary Pages — Per-degree member statistics and counts
2. ✅ Meeting Date Tables — Full 12-month calendars for all 4 degrees
3. ✅ Enhanced Officers — Comprehensive executive officer listings with ranks
4. ✅ Alphabetical TOC — Sorted by unit name for easier navigation
5. ✅ Data Validation — Automated checks for duplicates and date consistency
6. ✅ Degree-Specific Templates — Separate intro pages and styling per degree

**v1.5 Test Results:**
- Full document: ~200+ pages, 60-90 second render time
- Members table: Maintains 3-column layout across all page breaks
- Membership summaries: Accurate counts across all degree types
- Meeting tables: All dates parsing correctly for 12 months
- Data validation: Deduplicating entries, detecting inconsistencies
- PDF quality: Print-ready with proper margins and page numbering

**Known Working (v1.5):**
- Unit 3366 members: 132+ in 3-column layout
- Officer parsing: Names, ranks, installation dates extracted correctly
- Meeting recurrence: Monthly, quarterly, semi-annual meetings calculated
- HTML output: Paged.js pagination stable
- PDF rendering: Puppeteer + Chromium headless mode working reliably
- CLI parameters: -template, -output, -section, -unit, -debug, -showbleeds all functional

## 📚 v1.6 Planning Phase (April 30, 2026)

**Analysis Status:** ✅ COMPLETE (see `/ANALYSIS_v1.6_FORMAT.md`)

**Key Decisions Made:**
1. Columnar CSV format with 5 fixed sections per row
2. 11 total degree types (adds 7 new: KT, KTP, OSC, PBQ, RCOC, STOA)
3. Unique ID generation via composite keys from YAML-configured fields
4. Generic terminology: `SchemaPastUnitHead` works for all degrees
5. Two separate rank sources in Joining sections (origin vs destination)
6. YAML-based column range configuration (not hard-coded in C#)

**Implementation Timeline:**
- May 1-3: Domain classes + config classes + ColumnarCsvParser
- May 4-5: SchemaDataLoader updates + section parsers
- May 6-7: YAML files for new degrees + template updates
- May 8-10: Unit and integration testing
- May 11: Production release as v1.6

---

## 🔗 References

**Files to Know:**
- Principal renderer: [SchemaPdfRenderer.cs](src/MasonicCalendar.Core/Renderers/SchemaPdfRenderer.cs)
- CLI entry: [Program.cs](src/MasonicCalendar.Console/Program.cs)
- Unit template: [unit-page.html](document/templates/unit-page.html)
- Print styles: [print.css](document/templates/print.css)

**External Documentation:**
- Scriban templating: https://github.com/scriban/scriban
- Paged.js: https://pagedjs.org/
- PuppeteerSharp: https://www.puppeteersharp.com/
- CsvHelper: https://joshclose.github.io/CsvHelper/

---

## 📝 Project Status & Timeline

**Current Version:** v1.5 (Production-Ready)  
**Last Update:** April 30, 2026  
**Features:** 4 degree types, membership summaries, meeting tables, data validation  
**Status:** Stable, all major features complete and tested  

**Next Phase:** v1.6 (May 2026)  
**New Features:** 11 degree types, columnar CSV format, unique IDs via YAML config  
**Status:** In planning/analysis phase, ready for implementation  

**Documentation:**
- README.md — User and developer guide (updated for v1.5)
- ANALYSIS_v1.6_FORMAT.md — Complete v1.6 technical specification (1300+ lines)
- memory/session/v1.6_investigation.md — Implementation roadmap with 11 phased tasks
- calendar-expert.md (this file) — Agent instructions and quick reference
