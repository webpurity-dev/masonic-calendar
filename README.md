# Masonic Calendar - PDF Export System

A .NET console application for generating professionally formatted, print-ready PDF and HTML documents for non-profit Masonic organisations. Reads lodge and chapter data from CSV files and produces A6 booklet-style documents via Scriban templating and Puppeteer/Paged.js rendering.

## 🎯 Features

- ✅ **A6 booklet format** — print-ready PDF with correct gutter/outer margins for binding
- ✅ **Template-driven rendering** — Scriban HTML templates with Paged.js W3C print media
- ✅ **PDF and HTML output** — HTML for proofing, PDF for print
- ✅ **Cover page** — full-bleed image with automatic binding-edge compensation
- ✅ **Table of Contents** — master TOC and per-section TOCs with JavaScript-injected page numbers
- ✅ **Page numbering** — CSS counter in footer, starts at the TOC section (cover has no number)
- ✅ **Unit pages** — officers, past masters, joining past masters, members (3-column), honorary members
- ✅ **Meetings calendar** — 12-month grid with recurrence-rule expansion
- ✅ **Craft and Royal Arch** — separate data sources and TOC sections
- ✅ **Bleed visualisation** — `-showbleeds` flag for debugging page boundaries
- ✅ **Unit filtering** — `-unit <number>` to render a single lodge/chapter for proofing
- ✅ **Name shortening** — surnames longer than 3 words automatically shortened to last 2 words
- ✅ **Lodge list normalisation** — joining past master lodge lists stripped of spaces (`1895,6194,9660`)

## 🏗️ Project Structure

```
document/
├── master_v1.yaml              # Master document layout (sections, margins, format)
├── templates/
│   ├── print.css               # Paged.js @page rules, TOC styling, page breaks
│   ├── cover-page.html         # Full-bleed cover page
│   ├── forward-page.html       # Foreword/introduction page
│   ├── toc-page.html           # Table of contents Scriban template
│   ├── unit-page.html          # Unit page Scriban template
│   └── meetings-calendar-page.html  # Meetings calendar template
├── data/
│   ├── CraftData.csv           # Craft lodge data (consolidated)
│   ├── RAData.csv              # Royal Arch chapter data (consolidated)
│   ├── unit-locations.csv      # Meeting locations
│   └── sample-unit-meetings.csv # Meeting recurrence rules
├── data_sources/
│   ├── craft_data_source.yaml      # Column mappings for Craft CSV
│   ├── royalarch_data_source.yaml  # Column mappings for Royal Arch CSV
│   └── meetings_data_source.yaml   # Column mappings for meetings CSV
└── images/
    └── cover-img-yellow.jpg    # Cover page image

src/
├── MasonicCalendar.Console/    # CLI entry point
│   └── Program.cs
└── MasonicCalendar.Core/       # Rendering engine
    ├── Domain/                 # Business entities (SchemaUnit, SchemaOfficer, etc.)
    ├── Loaders/                # YAML layout loader, CSV data loader
    ├── Renderers/
    │   ├── SchemaPdfRenderer.cs          # Main renderer (HTML + Puppeteer PDF)
    │   ├── SectionRenderers/             # Per-section renderers
    │   │   ├── DataDrivenSectionRenderer.cs
    │   │   ├── StaticSectionRenderer.cs
    │   │   ├── TocSectionRenderer.cs
    │   │   └── MeetingsCalendarSectionRenderer.cs
    │   └── Utilities/
    │       ├── UnitModelBuilder.cs       # Builds Scriban model from SchemaUnit
    │       └── TextCleaner.cs            # Name/rank/lodge-list normalisation
    └── Services/
        └── RecurrenceService.cs          # Meeting recurrence rule expansion

output/                         # Generated files (gitignored)
```

## 🚀 Quick Start

```powershell
cd src/MasonicCalendar.Console

# Render full document (all sections) to PDF
dotnet run -- -template master_v1 -output pdf

# Render full document to HTML for proofing
dotnet run -- -template master_v1 -output html

# Render with page boundary visualisation
dotnet run -- -template master_v1 -output html -showbleeds

# Render a single section
dotnet run -- -template master_v1 -output html -section craft_units

# Render a single unit for quick proofing
dotnet run -- -template master_v1 -output html -unit 3366

# Render a single Royal Arch unit
dotnet run -- -template master_v1 -output html -unit 3366 -section royalarch_units

# Debug mode (extra console output + HTML debug file)
dotnet run -- -template master_v1 -output pdf -debug
```

## 📋 CLI Parameters

| Parameter | Required | Values | Description |
|-----------|----------|--------|-------------|
| `-template` | Yes | `master_v1` | Master layout template name |
| `-output` | Yes | `pdf` / `html` | Output format |
| `-section` | No | See below | Render one section only (default: all) |
| `-unit` | No | Lodge number | Render one unit only (e.g. `-unit 3366`) |
| `-showbleeds` | No | flag | Overlay red/blue borders on page boundaries |
| `-debug` | No | flag | Extra console output + debug HTML file |

### Sections in `master_v1`

| Section ID | Type | Description |
|------------|------|-------------|
| `cover` | static | Full-bleed cover page |
| `master_toc` | toc | Master table of contents (all sections) |
| `master_foreword` | static | Foreword/introduction |
| `craft_toc` | toc | Craft lodges table of contents |
| `craft_units` | data-driven | All Craft lodge unit pages |
| `royalarch_toc` | toc | Royal Arch chapters table of contents |
| `royalarch_units` | data-driven | All Royal Arch chapter unit pages |
| `meetings_calendar` | meetings-calendar | 12-month meetings grid |

### Output File Naming

| Command | Output file |
|---------|-------------|
| `-template master_v1 -output pdf` | `output/master_v1-all-sections.pdf` |
| `-template master_v1 -output html` | `output/master_v1-all-sections.html` |
| `-template master_v1 -output html -section craft_units` | `output/master_v1-craft_units.html` |
| `-template master_v1 -output html -unit 3366` | `output/master_v1-craft_units-unit3366.html` |
| `-template master_v1 -output html -showbleeds` | `output/master_v1-all-sections-showBleeds.html` |

## 🛠️ Technology Stack

| Component | Library | Version |
|-----------|---------|---------|
| Framework | .NET | 8.0 |
| PDF rendering | PuppeteerSharp + Chromium | 15.0.0 |
| Print pagination | Paged.js (CDN) | 1.0+ |
| Templating | Scriban | 5.4.6 |
| CSV parsing | CsvHelper | 30.0.0 |
| YAML config | YamlDotNet | latest |

## 📄 Document Layout

The document is A6 portrait (105 × 148 mm) with:
- **Gutter margin:** 10 mm (inner/binding edge)
- **Footer:** 10 mm (page number)
- **Bleed:** 6 mm (Paged.js default, extended on cover)
- **Cover page:** zero margins, full-bleed image with `#ffeb9a` background; binding-edge compensation applied automatically via CSS variable

Page numbering starts at the `master_toc` section (`reset_page_counter: true` in YAML). The cover page has no page number (`@page :first` with empty `@bottom-center`).

## 📋 Data Format

Data is loaded from CSV files via YAML data source mappings in `document/data_sources/`. Each mapping file specifies:
- Which CSV file to read
- Column name mappings for each person type (Officers, PastMasters, JoiningPastMasters, Members, HonoraryMembers)
- Optional heading overrides (e.g. "Excellent Kings" instead of "Past Masters" for Royal Arch)

### Unit Page Sections

Each unit page renders (where data exists):
1. **Header** — lodge name, number, installation date
2. **Location** — meeting address and email
3. **Officers** — two-column table split at position 12
4. **Past Masters** — name, year installed, provincial rank, rank year
5. **Joining Past Masters** — name, lodge list (comma-separated, no spaces), provincial rank
6. **Members** — 3 pre-split inline-block tables (deterministic, no CSS column reflow)
7. **Honorary Members** — name and rank

## 🔧 PDF Rendering Notes

The PDF pipeline uses Puppeteer with these settings to ensure HTML and PDF output are pixel-identical:

- `EmulateMediaTypeAsync(Print)` — Paged.js paginates under the same media context Chromium uses for PDF
- `PreferCSSPageSize = true` — CSS `@page` size controls page dimensions, not Chromium defaults
- `MarginOptions` all zero — all margins are handled by Paged.js `@page` rules
- `--force-device-scale-factor=1` — prevents HiDPI scaling skewing DOM height measurements
- `--disable-lcd-text` — consistent font rendering between screen and PDF rasteriser
- `--disable-font-subpixel-positioning` — eliminates sub-pixel font placement differences
- `DeviceScaleFactor = 1` on viewport — matches scale factor to PDF output

