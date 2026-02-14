# Masonic Calendar - AI Coding Instructions

# Project Context: Non-Profit Document & Calendar Generation System
You are an expert C# .NET developer with extensive experience in document generation, PDF rendering, and console application development. You are assisting in the development of a calendar system for a non-profit organization that generates searchable, professionally formatted PDF documents and HTML from CSV data.

This project is a **console application** that reads CSV data and generates print-ready documents using Scriban templating and Puppeteer-based PDF rendering. All libraries must be open-source (MIT/Apache 2.0) or have a free tier for non-profits.

## 🏗️ Technical Stack (CURRENT IMPLEMENTATION)
- **Framework:** .NET 8.0 (C#) Console Application
- **PDF Generation:** PuppeteerSharp 15.0.0 + Paged.js 1.0+ W3C Paged Media polyfill
- **HTML Rendering:** Chromium browser via Puppeteer (headless mode)
- **Templating:** Scriban 5.4.6 (HTML templates with Scriban expressions)
- **CSV Parsing:** CsvHelper 30.0.0
- **Page Layout:** CSS @page rules with Paged.js for pagination and margin boxes
- **Page Numbering:** CSS counters in margin boxes + JavaScript page number injection for TOC
- **Data Format:** CSV files (v1 schema with separate files, v2 schema with consolidated hermes export)

## � Key Architecture Components

### 1. Paged.js Integration
- **Purpose:** W3C-compliant print media rendering in Chromium
- **Loading:** CDN: `https://unpkg.com/pagedjs/dist/paged.polyfill.js`
- **Features:**
  - CSS @page rules for page sizing and margins
  - Automatic page break handling
  - Page dimension specifications (A4, A5, A6 support via CSS)
  - Margin boxes for headers, footers, page numbers
  - Support for left/right page styling

### 2. Page Numbering System
**Document Page Numbers (Margins):**
- Uses Paged.js CSS @page rules with @bottom-center margin box
- Content: `counter(page)` CSS counter
- Styling: 6pt centered in footer
- Applied via CSS in print.css

**TOC Page Numbers (Via JavaScript):**
- JavaScript function `injectTocPageNumbers()` runs after Paged.js pagination
- Process:
  1. Queries all `.toc-item a` links with href anchors
  2. Gets all `.pagedjs_page` elements created by Paged.js
  3. For each link, finds which page contains the target element
  4. Creates `<span class="toc-page-number">` with calculated page number
  5. Appends span to TOC link (right-aligned via flexbox)
- Styling: 9pt, right-aligned, proper spacing via CSS class
- Timing: Executes after Paged.js completes pagination via Puppeteer's `EvaluateFunctionAsync()`

### 3. Document Structure (master_v1 Template)
Documents are multi-section with:
- **Section 1: Cover Page** - Static HTML template
- **Section 2-N: Table of Contents Pages** - Generated per unit type (Craft, Royal Arch, Master)
  - Each TOC has links to units with automatic page numbers
- **Section N+: Unit Pages** - Individual unit pages, one per page
  - CSS `break-before: always` ensures each unit starts on new page
  - Contains officer lists, member tables, location info, meeting calendar

### 4. Scriban Templating
- **Template Files:** Located in `document/templates/` (unit-page.html, toc-page.html, cover-page.html)
- **Scriban Expressions:**
  - Variable access: `{{ unit.name }}`
  - Conditionals: `{% if unit.officerCount > 0 %} ... {% endif %}`
  - Loops: `{% for officer in unit.officers %} ... {% endfor %}`
  - Filters: `{{ text | upcase }}`
- **Models:** Pass unit, location, officer, member, meeting data to templates
- **Output:** HTML that Paged.js will paginate

### 5. Puppeteer PDF Generation
**Workflow:**
1. Build complete HTML document with all sections
2. Convert relative image paths to data URLs (for PDF portability)
3. Launch Chromium browser in headless mode
4. Load HTML content via `SetContentAsync()` (triggers Paged.js rendering)
5. Wait for Paged.js pagination to stabilize via polling:
   - Check for `.pagedjs_pages` container existence
   - Poll page count until it stabilizes (3 consecutive identical counts)
   - Max wait time: 60 seconds
6. Call page number injection JavaScript function
7. Generate PDF via `PrintToPdfAsync()` with PdfOptions:
   - Format: A4/A5/A6 (via paper format mapping)
   - Landscape: true/false
   - PrintBackground: true (for colors)
   - DisplayHeaderFooter: false (Paged.js @page rules handle margins)

**Console Logging:**
- Logs progress at each stage (pages found, pagination complete, injection status)
- Browser console messages containing "[injectTocPageNumbers]" are captured and displayed
- Useful for debugging page number injection issues

## 💾 Data Processing

### CSV Schema Support
**v1 Schema (Default):**
- Multiple separate CSV files
- Files: sample-units.csv, sample-unit-officers.csv, sample-unit-pmo.csv, sample-unit-pmi.csv, sample-unit-members.csv, sample-unit-honorary.csv, sample-unit-locations.csv, sample-unit-meetings.csv

**v2 Schema:**
- Single consolidated CSV: hermes-export.csv
- Type column indicates row category: Off, PMO, PMI, Mem, Hon
- Parsed by conditional logic based on Type

### Recurrence Rule Expansion
- **RecurrenceType Values:**
  - WEEKLY: Repeats on specified DayOfWeek
  - MONTHLY: Repeats on DayNumber each month
  - NTH_WEEKDAY: Repeats on WeekNumber (1-5), DayOfWeek each month
  - Custom: Uses CustomStrategyKey for special rules
  
- **Date Calculation for NTH_WEEKDAY:**
  - Formula: $FirstOfMonth + ((TargetDay - FirstOfMonthDayOfWeek + 7) \bmod 7) + (7 \times (WeekNumber - 1))$
  - Example: 4th Tuesday = Week 4, Tuesday

- **Months Field:**
  - Pipe-separated list: "01|02|03|..." (1-12) or "All" for all months
  - Allows meetings to run only during specific months
  - Example: "04|05|06" = April-June only

### Output File Organization
- PDF/HTML files saved to `output/` directory
- Filenames include template name and section info: `master_v1-all-sections.pdf`
- Debug HTML also generated: `master_v1-all-sections-debug.html`

## �️ Project Structure
```
MasonicCalendar.sln
├── MasonicCalendar.Core/
│   ├── Domain/
│   │   ├── EventSeries.cs          # Event recurrence rules & metadata
│   │   ├── EventInstance.cs        # Expanded concrete dates
│   │   ├── RecurrenceStrategy.cs   # Abstract base for custom rules
│   │   └── Entities/               # Other domain objects
│   ├── Services/
│   │   ├── RecurrenceService.cs    # Generate EventInstances from EventSeries
│   │   ├── AstronomicalService.cs  # Moon phase calculations
│   │   └── IDataIngestorService.cs # Abstract for CSV/Sheets ingestion
│   └── Utilities/
│       └── DateCalculationHelpers.cs
├── MasonicCalendar.Data/
│   ├── ApplicationDbContext.cs     # EF Core DbContext
│   ├── Migrations/                 # EF Core migrations
│   └── Repositories/               # Data access patterns (if needed)
├── MasonicCalendar.Api/
│   ├── Controllers/                # REST endpoints
│   ├── Middlewares/
│   └── Program.cs                  # Dependency injection & configuration
├── MasonicCalendar.Ingestion/
│   ├── Csv/                        # CsvHelper integration
│   └── GoogleSheets/               # Google Sheets API integration
├── MasonicCalendar.Export/
│   └── PdfExporter.cs              # QuestPDF grid-based calendar layout
├── MasonicCalendar.Tests/
│   ├── RecurrenceServiceTests.cs   # Unit tests for date calculations
│   └── AstronomicalServiceTests.cs
└── MasonicCalendar.Web/
    ├── wwwroot/                    # FullCalendar.io & Bootstrap assets
    └── Pages/                      # Razor pages or SPA entry
```

### Key Module Responsibilities
- **Core:** Date calculation logic, business rules (no EF Core dependencies)
- **Data:** SQLite schema, migrations, DbContext
- **Api:** HTTP endpoints, request/response contracts
- **Ingestion:** CSV parsing, Google Sheets authentication, data mapping
- **Export:** PDF generation with QuestPDF
- **Web:** Frontend with FullCalendar.io and search UI

## 📄 File Structure & Naming

### Template Files (document/templates/)
```
print.css          # Paged.js @page rules, margins, TOC styling
unit-page.html     # Scriban template for individual unit pages
toc-page.html      # Scriban template for TOC generation
cover-page.html    # Static cover page HTML
```

### Configuration Files (document/data_sources/)
```
craft_data_source.yaml      # YAML config specifying which CSV files contain Craft units
royalarch_data_source.yaml  # YAML config specifying which CSV files contain Royal Arch units
master_v1.yaml              # Master template config listing all sections
```

### Data Files (document/data/)
```
*.csv files                 # Unit, location, officer, member, meeting data
```

## 🔑 Key Implementation Details

### CSS Pagination
```css
.unit-page {
    break-before: always;  /* Each unit starts on new page */
    page-break-inside: avoid;
}

/* TOC Page Spacing */
.toc-item {
    display: flex;
    justify-content: space-between;
    align-items: baseline;
}

.toc-item a {
    flex: 1;
}

.toc-item .toc-page-number {
    display: inline-block;
    margin-left: 6pt;
    min-width: 30px;
    text-align: right;
}
```

### JavaScript Page Number Injection
```javascript
function injectTocPageNumbers() {
    const tocLinks = document.querySelectorAll('.toc-item a');
    const pages = document.querySelectorAll('.pagedjs_page');
    
    let injectedCount = 0;
    tocLinks.forEach(link => {
        const href = link.getAttribute('href');
        const anchorId = href.substring(1);
        const targetElement = document.getElementById(anchorId);
        
        if (targetElement) {
            // Find page containing target
            for (let i = 0; i < pages.length; i++) {
                if (pages[i].contains(targetElement)) {
                    // Create and append page number span
                    const span = document.createElement('span');
                    span.className = 'toc-page-number';
                    span.textContent = (i + 1).toString();
                    link.appendChild(span);
                    injectedCount++;
                    break;
                }
            }
        }
    });
    return injectedCount > 0;
}
```

### Main Service: SchemaPdfRenderer
**Key Methods:**
- `RenderAllSectionsAsync()` - Generate complete multi-section document (most common)
- `RenderSectionAsync()` - Generate single section (rarely used)
- `ConvertHtmlToPdf()` - Puppeteer HTML→PDF conversion with Paged.js integration

**Typical Flow:**
```csharp
// Load layout configuration (YAML)
var layout = layoutLoader.LoadMasterLayout("master_v1");

// Load data from CSVs
var units = dataLoader.LoadUnitsWithDataAsync("master_v1", "craft");

// Render all sections to HTML
var htmlContent = await renderer.RenderAllSectionsAsync(units, "master_v1", "PDF");

// Puppeteer converts to PDF (Paged.js + page number injection happens here)
var pdfBytes = await renderer.ConvertHtmlToPdf(htmlContent, pdfOptions);
```

## 📝 Coding Preferences & Patterns

- **File-Scoped Namespaces:** Use `namespace MasonicCalendar.Core.Services;` style
- **Primary Constructors:** Prefer `public class Service(IDependency dep) { }` style
- **Result Pattern:** Use `Result<T>` for operations that can fail (loading, parsing)
- **Await Pattern:** Always use async/await, avoid blocking calls
- **CSS-First Layout:** Prefer CSS flexbox/grid over programmatic layout
- **Logging:** Use `Console.WriteLine()` for user-facing messages
- **Error Handling:** Catch specific exceptions, log with context, return meaningful errors
- **Null Safety:** Check for null before accessing, provide default values

## ⚠️ Important Notes

### Paged.js Behavior in Different Contexts
- **Puppeteer:** Paged.js has access to `.pagedjs_page` elements, full rendering works
- **Browser (local HTML):** Paged.js loads from CDN, may have timeout/rendering delays
- **HTML Output (static):** Paged.js loads from CDN when opened in browser, executes page number injection

### Debugging Page Number Injection Issues
- **Check Console Logs:** Browser or Puppeteer console shows `[injectTocPageNumbers]` debug messages
- **Verify Page Count:** Ensure Paged.js pagination completed before JavaScript runs
- **Test Selectors:** Confirm `.toc-item a` and `.pagedjs_page` selectors match actual HTML
- **Review Timing:** Page number injection must run after Paged.js pagination stabilizes

### Performance Considerations
- Large documents (200+ pages): Expect 60-90 second Puppeteer rendering time
- Image Conversion: Converting images to data URLs adds processing time
- Chromium Download: First run downloads ~150MB Chromium binary
- Memory Usage: Multiple units × large images may require more heap (-Xmx settings)

## 🎯 Common Tasks

### Add a New Section to master_v1
1. Update `document/data_sources/master_v1.yaml` - Add section entry
2. Create Scriban template in `document/templates/` if needed
3. Update `RenderAllSectionsAsync()` - Add conditional block for new section
4. Test with `dotnet run -- -template master_v1 -output HTML`

### Change Page Sizing or Margins
1. Edit `document/templates/print.css` - Modify `@page` rules
2. Adjust margin box sizes, page size, or padding
3. Rebuild HTML: `dotnet run -- -template master_v1 -output HTML`
4. Test visual appearance

### Modify TOC Styling
1. Edit `document/templates/print.css` - Update `.toc-item`, `.toc-item a`, `.toc-item .toc-page-number` classes
2. Adjust font sizes, spacing, alignment
3. Review CSS flexbox properties for spacing logic
4. Rebuild and verify in HTML

### Debug Page Number Status
1. Check console output from PDF generation for `[injectTocPageNumbers]` messages
2. Generate HTML: `dotnet run -- -template master_v1 -output HTML`
3. Open HTML in browser (Paged.js loads from CDN, page numbers should appear after 3 seconds)
4. Check browser console for JavaScript errors

## 📚 Further Documentation
- README.md - Project overview and CLI usage
- IMPLEMENTATION.md - Technical implementation details
- CSV_SCHEMA.md - CSV file format specifications
- UNIT_PAGE_LAYOUT.md - Template customization guide