# Masonic Calendar - Copilot Agent Instructions

**Date Updated:** March 14, 2026  
**Project Type:** C# .NET 8.0 Console Application  
**Purpose:** Non-profit calendar/document generation system with PDF rendering via Puppeteer and Scriban templating

---

## 📋 Quick Reference

### Active Features
- **3-Column Members Table Layout** (FIXED): Inline-block CSS layout maintains structure across page breaks
- **Unit Filtering** (FIXED): `-unit` CLI parameter now correctly renders single units only
- **Paged.js Integration**: W3C-standard print rendering with automatic pagination
- **TOC Page Numbers**: JavaScript injection calculates page numbers after Paged.js pagination

### Current Issues Status
- ✅ Members table no longer collapses at page breaks
- ✅ Unit filtering parameter works end-to-end
- ✅ Members heading stays with table content

### Recent Discoveries (This Session)
1. **Unit Filter Bug Fix**: Renderer was reloading all units instead of using filtered parameter
   - **File**: `SchemaPdfRenderer.RenderSectionAsync()` ~line 348
   - **Solution**: Use `units` parameter directly (already filtered by CLI)
   - **Impact**: `-unit 3366` now outputs 87.0KB (1 unit) vs 2428.0KB (all 49)

2. **CSS Layout Solution**: Inline-block beats flexbox/grid for print media
   - Uses `display: inline-block; width: calc(33.333% - 2px);`
   - Font-size: 0 + letter-spacing removes whitespace between columns
   - Maintains 3-column structure across Paged.js page breaks

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
- `print.css` - Paged.js @page rules, margins, TOC styling
- `unit-page.html` - Scriban template for individual unit pages
- `toc-page.html` - Table of contents page template
- `cover-page.html` - Static cover page
- `forward-page.html` - Foreword/introduction page
- `meetings-calendar-page.html` - Meeting calendar section template

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

### Document Structure (master_v1 Template)

**Sections (in order):**
1. `cover` (static) - Static cover page
2. `master_toc` (toc) - Master table of contents with all sections
3. `master_foreword` (static) - Foreword/introduction
4. `craft_toc` (toc) - Craft units table of contents
5. `craft_units` (data-driven) - All craft unit pages
6. `royalarch_toc` (toc) - Royal Arch table of contents  
7. `royalarch_units` (data-driven) - All Royal Arch unit pages
8. `meetings_calendar` (data-driven) - Meetings calendar pages

**Unit Page Content:**
- Officer lists (Past Master, Master, Wardens, etc.)
- Member tables (3-column layout)
- Location information
- Meeting calendar

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
  ├── templates/
  │   ├── print.css              # Paged.js @page rules, margins
  │   ├── unit-page.html         # Unit page Scriban template
  │   ├── toc-page.html          # TOC section template
  │   ├── cover-page.html        # Cover page
  │   ├── forward-page.html      # Foreword page
  │   └── meetings-calendar-page.html  # Meetings calendar
  ├── data/
  │   ├── hermes-export.csv      # Consolidated CSV (v2 schema)
  │   ├── sample-units.csv       # Unit definitions
  │   ├── sample-unit-locations.csv
  │   ├── sample-unit-meetings.csv
  │   ├── royalarch_units.csv
  │   └── _archive/              # Old v1 schema files
  ├── data_sources/
  │   ├── master_v1.yaml         # Master template layout config
  │   ├── craft_data_source.yaml
  │   ├── royalarch_data_source.yaml
  │   └── meetings_data_source.yaml
  └── images/

src/
  ├── MasonicCalendar.Console/
  │   ├── Program.cs             # CLI entry point ⭐
  │   └── MasonicCalendar.Console.csproj
  └── MasonicCalendar.Core/
      ├── Domain/                # Business entities
      ├── Loaders/               # Data loading & template loading
      ├── Renderers/             # PDF/HTML rendering logic ⭐
      │   └── SectionRenderers/  # Section-specific renderers
      └── Services/              # Business logic (recurrence, etc.)

output/
  ├── master_v1-all-sections.html     # Full rendered document
  ├── master_v1-craft_units.html      # Craft section only
  └── master_v1-craft_units-unit3366.html  # Single unit (when -unit 3366)
```

---

## 🚀 CLI Usage Examples

**Render full master template to HTML:**
```bash
dotnet run -- -template master_v1 -output HTML
```

**Render single section to PDF:**
```bash
dotnet run -- -template master_v1 -section craft_units -output PDF
```

**Render single unit (craft section):**
```bash
dotnet run -- -template master_v1 -unit 3366 -output HTML
```

**Render single unit from royal arch:**
```bash
dotnet run -- -template master_v1 -unit 3366 -section royalarch_units -output PDF
```

**Debug mode with bleed visualization:**
```bash
dotnet run -- -template master_v1 -output HTML -debug -showbleeds
```

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

## 📚 Recent Session History

**Completed This Session:**
1. ✅ Fixed Members table 3-column layout (inline-block CSS solution)
2. ✅ Fixed duplicate headers in Members table (removed duplicate th/td elements)
3. ✅ Added `-unit` CLI parameter
4. ✅ Fixed unit filter bug in renderer (was reloading all units)
5. ✅ Verified end-to-end unit filtering works correctly

**Test Results:**
- Single unit rendering: 87.0KB, 1 unit-page div
- Full section rendering: 2428.0KB, 49 unit-page divs
- Members table: Maintains 3-column layout across page breaks
- TOC page numbers: Calculated correctly after Paged.js pagination

**Known Working:**
- Members of unit 3366: 132+ members in 3-column layout
- Page breaks: No content loss across sections
- PDF rendering: Paged.js integration with Puppeteer
- CLI parameter validation: Integer parsing with meaningful error messages

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

## 📝 Last Updated

**Date:** March 14, 2026  
**Session Focus:** Unit filtering fix + CSS layout solutions  
**Status:** All major features working, ready for production rendering
