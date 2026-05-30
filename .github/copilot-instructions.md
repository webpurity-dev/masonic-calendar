# Masonic Calendar - AI Coding Instructions

## Project Context
A C# .NET 8.0 console application for a non-profit Masonic organisation. Reads lodge/chapter data from CSV files and generates print-ready A6 booklet PDFs and proofing HTML via Scriban templating and Puppeteer/Paged.js rendering. All libraries must be open-source (MIT/Apache 2.0) or free for non-profits.

**Current Version:** v1.7 (May 24, 2026) — Extended rank fields with priority-based cascading display, succession lists for Craft and Royal Arch, architecture ready for 14+ degree types via YAML configuration.

---

## Workflow: Questions → Plan → Execute

**For any feature or refactoring request:**

1. **Ask Clarifying Questions** (concise bullet list):
   - Scope boundaries (affected files/components)
   - Timeline/priority (immediate vs phased)
   - Rollback strategy (commits exist?)
   - Validation criteria (tests, manual checks)

2. **Create Plan Document**:
   - File: `_work-plan-[YYYYMMDD].md` (e.g., `_work-plan-20260530.md`)
   - Sections: Scope | Approach | Files | Risks | Rollback
   - Place in workspace root for visibility

3. **Execute & Document**:
   - Update plan with actual changes as work progresses
   - Reference plan in tool calls/explanations
   - Link changed files to plan sections

---

## Technical Stack

| Component | Library | Version |
|-----------|---------|---------|
| Framework | .NET | 8.0 |
| PDF rendering | PuppeteerSharp + Chromium | 15.0.0 |
| Print pagination | Paged.js (CDN) | 1.0+ |
| Templating | Scriban | 5.4.6 |
| CSV parsing | CsvHelper | 30.0.0 |
| YAML config | YamlDotNet (UnderscoredNamingConvention) | latest |

---

## Project Structure

```
document/
+-- master_v1.yaml                    # Master document layout (sections, margins, format)
+-- templates/
|   +-- print.css                     # Paged.js @page rules, TOC styling, page breaks
|   +-- cover-page.html               # Full-bleed cover page (no margins, background colour)
|   +-- forward-page.html             # Foreword/introduction
|   +-- _data-driven/toc-page.html                 # TOC Scriban template
|   +-- _data-driven/unit-page.html                # Unit page Scriban template
|   +-- meetings-calendar-page.html   # Meetings calendar template
+-- data/
|   +-- CraftData.csv                 # Craft lodge data
|   +-- RAData.csv                    # Royal Arch chapter data
|   +-- unit-locations.csv            # Meeting locations
|   +-- sample-unit-meetings.csv      # Meeting recurrence rules
+-- data_sources/
|   +-- craft_data_source.yaml        # Column mappings for Craft CSV
|   +-- royalarch_data_source.yaml    # Column mappings for Royal Arch CSV
|   +-- meetings_data_source.yaml     # Column mappings for meetings CSV
+-- images/
    +-- cover-img-yellow.jpg          # Cover page image

src/
+-- MasonicCalendar.Console/
|   +-- Program.cs                    # CLI entry point
+-- MasonicCalendar.Core/
    +-- Domain/                       # SchemaUnit, SchemaOfficer, SchemaMember, etc.
    +-- Loaders/
    |   +-- DocumentLayoutLoader.cs   # YAML->DocumentLayout, SectionConfig, PageMargins
    |   +-- SchemaDataLoader.cs       # CSV->SchemaUnit (via data source YAML mappings)
    +-- Renderers/
    |   +-- SchemaPdfRenderer.cs      # Orchestrates HTML build + Puppeteer PDF
    |   +-- SectionRenderers/
    |   |   +-- SectionRenderer.cs              # Abstract base; WrapWithPageBreakAndAnchor
    |   |   +-- DataDrivenSectionRenderer.cs    # Renders unit pages via Scriban
    |   |   +-- StaticSectionRenderer.cs        # Renders static HTML templates
    |   |   +-- TocSectionRenderer.cs           # Builds and renders TOC sections
    |   |   +-- MeetingsCalendarSectionRenderer.cs
    |   |   +-- SectionRendererFactory.cs
    |   +-- Utilities/
    |       +-- UnitModelBuilder.cs   # Builds Scriban model dict from SchemaUnit
    |       +-- TextCleaner.cs        # Name/rank/lodge-list normalisation
    +-- Services/
        +-- RecurrenceService.cs      # Meeting recurrence rule expansion

output/                               # Generated files (gitignored)
```

---

## CLI Usage

```powershell
cd src/MasonicCalendar.Console

dotnet run -- -template master_v1 -output pdf
dotnet run -- -template master_v1 -output html
dotnet run -- -template master_v1 -output html -showbleeds
dotnet run -- -template master_v1 -output html -section craft_units
dotnet run -- -template master_v1 -output html -unit 3366
dotnet run -- -template master_v1 -output html -unit 3366 -section royalarch_units
dotnet run -- -template master_v1 -output pdf -debug
```

### Parameters

| Parameter | Required | Description |
|-----------|----------|-------------|
| `-template` | Yes | Master layout name (e.g. `master_v1`) |
| `-output` | Yes | `pdf` or `html` |
| `-section` | No | Render one section only |
| `-unit` | No | Render one unit only (integer lodge number) |
| `-showbleeds` | No | Overlay red/blue borders on page boundaries |
| `-debug` | No | Extra console output + debug HTML file |

### Sections in master_v1

| Section ID | Type | Description |
|------------|------|-------------|
| `cover` | static | Full-bleed cover page |
| `master_toc` | toc | Master TOC (all sections); resets page counter to 1 |
| `master_foreword` | static | Foreword/introduction |
| `craft_toc` | toc | Craft lodges TOC |
| `craft_units` | data-driven | All Craft lodge unit pages |
| `royalarch_toc` | toc | Royal Arch chapters TOC |
| `royalarch_units` | data-driven | All Royal Arch chapter unit pages |
| `meetings_calendar` | meetings-calendar | 12-month meetings grid |

---

## Document Layout (master_v1.yaml)

- **Format:** A6 portrait (105 x 148 mm)
- **Gutter (inner/binding edge):** 10 mm on recto pages (`right_page.left`)
- **Footer:** 10 mm (space for page number)
- **Outer/top margins:** 0 mm
- **Cover (`first_page`):** all margins 0 mm; `@page :first` emits empty `@bottom-center` (no page number)
- **Bleed:** 6 mm (Paged.js default), cover extends to `-6mm` on all sides in HTML
- **Page numbering:** starts at `master_toc` via `reset_page_counter: true` -- emits `counter-reset: page 0` on the section anchor div
- **Binding-edge compensation:** renderer auto-emits `.pagedjs_first_page img { object-position: calc(50% + calc(var(--binding-gutter) / 2)) center; }` -- no manual offset needed in templates

### YAML Key Properties (UnderscoredNamingConvention)

```yaml
page_margins:
  right_page: { top, bottom, left, right }   # Odd/recto pages
  left_page:  { top, bottom, left, right }   # Even/verso pages
  first_page: { top, bottom, left, right }   # Cover -- triggers @page :first

sections:
  - section_id: "master_toc"
    reset_page_counter: true   # SectionConfig.ResetPageCounter -> counter-reset: page 0
    hide_from_parent_toc: false
```

---

## PDF Rendering Pipeline (SchemaPdfRenderer.ConvertHtmlToPdf)

Critical settings that ensure HTML and PDF output are pixel-identical:

```csharp
// Chromium launch flags
Args = new[]
{
    "--no-sandbox",
    "--force-device-scale-factor=1",          // No HiDPI scaling skewing DOM measurements
    "--disable-lcd-text",                      // Match font rendering to PDF rasteriser
    "--disable-font-subpixel-positioning"      // Eliminate sub-pixel font height differences
}

// Viewport
await page.SetViewportAsync(new ViewPortOptions { Width = 800, Height = 1000, DeviceScaleFactor = 1 });

// Force print media BEFORE SetContentAsync so Paged.js paginates under the same
// media context Chromium uses when generating the PDF.
await page.EmulateMediaTypeAsync(MediaType.Print);

// PdfOptions
new PdfOptions
{
    Format = PaperFormat.A6,
    PrintBackground = true,
    DisplayHeaderFooter = false,
    PreferCSSPageSize = true,      // CSS @page size controls dimensions
    MarginOptions = new MarginOptions { Top="0px", Bottom="0px", Left="0px", Right="0px" }
}
```

**Why EmulateMediaTypeAsync matters:** Without it, Paged.js paginates under `screen` media, then Chromium switches to `print` for PDF generation -- causing sub-pixel measurement differences that clip rows at page boundaries.

**Why no CSS column-count:** `column-count: 3` distributes rows differently in screen vs print contexts. Members are pre-split into 3 vertical lists in `UnitModelBuilder.SplitMembersIntoColumns()` and rendered as 3 explicit `inline-block` tables in the template -- deterministic, zero browser discretion.

---

## Unit Page Template (_data-driven/unit-page.html)

Sections rendered in order (all conditional on data existing):

1. **Header** -- unit name + number, installation date
2. **Location** -- meeting address, email
3. **Officers** -- two flex-column tables split at `posNo <= 11` / `posNo > 11` with extended rank display
4. **Past Unit Heads** -- name, year installed, ranks (provincial, london, other province) with dates, provincial grand ranks
5. **Joining Past Unit Heads** -- name, lodge list (comma-separated no spaces), ranks with dates
6. **Members** -- 3 `inline-block` tables from `memberColumns` (pre-split vertically in C#) with extended rank display
7. **Honorary Members** -- name and extended rank display

### v1.7 Rank Display Logic (Priority-Based Cascading)

**Critical Rule:** The following logic applies to all person types (Officers, Members, Honorary Members, Past Unit Heads):

1. **IF** person has `grand_rank` → Display ONLY the Grand Rank in bold (`<strong>{{grand_rank}}</strong>`), hide all other ranks
2. **ELSE** (no Grand Rank) → Display in order on separate lines:
   - Provincial Rank with Date Accorded in parentheses
   - Prov Rank Other Province (if present) with Date Accorded
   - London Rank (if present) with Date Accorded
   - Empty ranks are completely omitted (no blank lines)

**Example (Member with Grand Rank):** `William Jones` displays only `**Past Grand Master**` (bold)

**Example (Member without Grand Rank):**
```
Peter Brown
Prov GM (2020)
London Rank (2022)
```

**Template Implementation:** In `_data-driven/unit-page.html`, the rank display uses Scriban if/else logic:
```scriban
{{ if person.grand_rank }}
  <strong>{{ person.grand_rank }}</strong>
{{ else }}
  {{ if person.provincial_rank }}<div>{{ person.provincial_rank }}{{ if person.date_rank_accorded }} ({{ person.date_rank_accorded }}){{ end }}</div>{{ end }}
  {{ if person.prov_rank_other_prov }}<div>{{ person.prov_rank_other_prov }}{{ if person.op_date_accorded }} ({{ person.op_date_accorded }}){{ end }}</div>{{ end }}
  {{ if person.london_rank }}<div>{{ person.london_rank }}{{ if person.london_rank_date_accorded }} ({{ person.london_rank_date_accorded }}){{ end }}</div>{{ end }}
{{ end }}
```

**Key CSS rules:**
- No `height` on `<tr>` elements -- let browser size from content, so Paged.js measures true height
- `break-before: always` on `.unit-page` in print.css
- `break-inside: avoid` on officer/past-master sections to keep heading with table
- Multi-line rank display with proper spacing between rank lines

---

## TextCleaner Utility

Key methods in `MasonicCalendar.Core.Renderers.Utilities.TextCleaner`:

| Method | Purpose |
|--------|---------|
| `CleanName(string?)` | Strips newlines, bullet chars, collapses whitespace |
| `CleanProvincialRank(string?)` | Removes commas, brackets, excess whitespace |
| `CleanPastUnits(string?)` | Splits on `,`, trims tokens, rejoins with no spaces: `"1895,6194,9660"` |
| `CombineNameInitialsAndFirstName(...)` | Builds `"Surname I.N."` display name; applies `ShortenSurname` |
| `CombineNameAndInitials(...)` | Builds `"Surname I."` display name; applies `ShortenSurname` |
| `ShortenSurname(string)` | If >3 words, keeps last 2: `"Andrade De Azeredo Coutinho"` -> `"Azeredo Coutinho"` |

---

## Data Loading

- `SchemaDataLoader.LoadUnitsWithDataAsync(templateKey, sectionId?)` -- loads CSV per data source YAML mapping
- v1.7 rank field extraction: `ParseDateRange()` handles formats like "2021", "1993-15", "1993-2025" for date fields
- All person types (SchemaOfficer, SchemaMember, SchemaHonoraryMember) load 8 rank-related fields:
  - `grand_rank` (string?), `grand_rank_date_accorded` (int?)
  - `provincial_rank` (string?), `date_rank_accorded` (int?)
  - `prov_rank_other_prov` (string?), `op_date_accorded` (int?)
  - `london_rank` (string?), `london_rank_date_accorded` (int?)
- When `-unit <N>` is specified, `Program.cs` filters the list *before* passing to renderer
- `DataDrivenSectionRenderer` uses the `units` parameter directly -- it does **not** reload from disk, preserving the filter
- `SchemaPdfRenderer.RenderSectionAsync` reloads only when `units.Count > 1` and a DataMapping exists (i.e. not pre-filtered)

---

## Coding Patterns

- **File-scoped namespaces:** `namespace MasonicCalendar.Core.Services;`
- **Primary constructors:** `public class Renderer(IDep dep, bool debug) { }`
- **Result<T> pattern:** for all operations that can fail (loading, parsing)
- **No `async` without `await`:** use `Task.FromResult(...)` instead of `async` on sync methods
- **Nullable guards:** always check `_dataLoader != null` before calling its methods (it is optional in the constructor)
- **CSS-first layout:** prefer CSS over programmatic positioning
- **Logging:** `Console.WriteLine()` only -- no logging framework

---

## Section Renderer Types (v1.7+)

| Renderer Type | Purpose | Configuration | Example |
|---------------|---------|---|---|
| `static` | Pre-rendered HTML files (foreword, introductions, cover) | Points to `.html` file path | `type: "static"` + `template: "foreword-page.html"` |
| `data-driven` | CSV data → Scriban template (unit pages) | References `data_mapping` YAML file + CSV source | `type: "data-driven"` + `data_mapping: "data_sources/craft_data_source.yaml"` |
| `toc` | Auto-generated table of contents | Indexes child sections | `type: "toc"` + `section_id: "craft_toc"` |
| `succession-list` | Officer succession tables (PGM, DPGM, APGM for Craft; GS, DGS for RA) | Multiple tables defined in data_source YAML `succession_list` section | `type: "succession-list"` + `data_mapping: "data_sources/craft_data_source.yaml"` |
| `membership-summary` | Statistical summaries per degree | Calculates counts from loaded units | `type: "membership-summary"` + `degree_filter: "Craft"` |
| `meetings-calendar` | 12-month meetings grid | Processes meeting rules from CSV | `type: "meetings-calendar"` |
| `meetings-table` | Per-degree meeting dates | Expands recurrence rules | `type: "meetings-table"` + `degree_filter: "Craft"` |

### Succession List Configuration (craft_data_source.yaml)

Example from v1.7:

```yaml
succession_list:
  tables:
    - title: "PROVINCIAL GRAND MASTERS"
      source: "data/craft_pgm_succession_v1.7.csv"
      font_size: "8pt"
      columns:
        - name: "Appointed"
          csv_column: "Appointed"
          width: "18%"
          alignment: "left"
        - name: "PROVINCIAL GRAND MASTERS"
          csv_column: "Name"
          width: "70%"
          alignment: "left"
        - name: "Years"
          csv_column: "Years"
          width: "12%"
          alignment: "right"
      table_caption: "† Resigned from Office. ‡ Dismissed from Office by the Grand Master."
```

**Key Points:**
- Multiple tables per section
- Years column includes special characters († resigned, ‡ dismissed) postfixed to year value
- Table captions defined in YAML (optional, only rendered if not null)
- Column widths sum to 100% for proper CSS layout
- Alignment: "left" or "right" for CSS text-align

---

## Known Gotchas

| Issue | Cause | Fix Applied |
|-------|-------|-------------|
| Rows clipped in PDF | `height: Npx` on `<tr>` smaller than content; Paged.js uses declared height | Remove all `height` from `<tr>` elements |
| Screen/print pagination mismatch | Paged.js paginates in screen media, Chromium generates PDF in print media | `EmulateMediaTypeAsync(Print)` before `SetContentAsync` |
| HiDPI font height difference | OS scale factor changes DOM pixel measurements vs PDF rasteriser | `--force-device-scale-factor=1` + `DeviceScaleFactor=1` |
| CSS column reflow in PDF | `column-count` distributes rows differently in print context | Pre-split members in C# -> 3 explicit `inline-block` tables |
| Cover bleed hidden by image | `position:absolute` div creates stacking context above bleed overlays | Bleed visualisation uses `::after` pseudo-elements with `z-index:99999` |
| Long surname overflow | 4-word surnames (e.g. "Andrade De Azeredo Coutinho") overflow officer cell | `ShortenSurname` keeps last 2 words when >3 words |
| Lodge list with spaces | CSV may contain `"1895, 6194, 9660"` with spaces | `CleanPastUnits` splits, trims, rejoins with no spaces |
| Rank display precedence | Multiple rank fields in CSV, user needs to know which displays | Grand Rank takes absolute priority (displayed only if present); cascade: Provincial → Other Prov → London when Grand absent |
| Template path resolution | Hardcoded template paths inconsistent across renderers | v1.7 fix: All renderers now use `section.Template` from YAML config (e.g. `template: "_data-driven/unit-page.html"`) |
| Date format parsing | v1.7 date fields use "2021", "1993-15", "1993-2025" formats | `ParseDateRange()` handles all variants with century inference |
| Succession list table captions | Special characters in table notes | Store directly in CSV Years column with special character postfix (e.g., "7 †", "9 ‡")

---

## Common Tasks

### Add a new section to master_v1
1. Add entry to `document/master_v1.yaml`
2. Create Scriban template in `document/templates/` if needed
3. Add renderer case to `SectionRendererFactory` if new type
4. Test: `dotnet run -- -template master_v1 -output html`

### Change page margins
1. Edit `page_margins` block in `document/master_v1.yaml`
2. `GeneratePageMarginsCss()` in `SchemaPdfRenderer` picks these up automatically
3. Test: `dotnet run -- -template master_v1 -output html -showbleeds`

### Debug missing rows in PDF
1. Check for `height` attributes on `<tr>` elements in template
2. Verify `EmulateMediaTypeAsync(Print)` is called before `SetContentAsync`
3. Check `--force-device-scale-factor=1` is in Chromium args
4. Check `column-count` is not used for multi-column layouts -- use pre-split tables instead

### Render a single unit for rapid iteration
```powershell
dotnet run -- -template master_v1 -output html -unit 3366
dotnet run -- -template master_v1 -output html -unit 3366 -section royalarch_units
```