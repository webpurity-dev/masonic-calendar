# Masonic Calendar - PDF Export System

A .NET console application for generating professionally formatted, print-ready PDF and HTML documents for non-profit Masonic organisations. Reads lodge and chapter data from CSV files and produces A6 booklet-style documents via Scriban templating and Puppeteer/Paged.js rendering.

**Latest Release:** v1.7 (Updated May 24, 2026) — Extended rank fields (Provincial Rank, London Rank, Other Province Rank) with priority-based display logic, succession lists for Craft and Royal Arch, support for up to 14 degree types, and architectural improvements for template path resolution.

## 🎯 Features

- ✅ **A6 booklet format** — print-ready PDF with correct gutter/outer margins for binding
- ✅ **Template-driven rendering** — Scriban HTML templates with Paged.js W3C print media
- ✅ **PDF and HTML output** — HTML for proofing, PDF for print
- ✅ **Cover page** — full-bleed image with automatic binding-edge compensation
- ✅ **Table of Contents** — master TOC and per-section TOCs with JavaScript-injected page numbers (alphabetical order)
- ✅ **Page numbering** — CSS counter in footer, starts at the TOC section (cover has no number)
- ✅ **Unit pages** — officers (with extended ranks), past unit heads, joining past unit heads, members (3-column), honorary members (all with rank cascading)
- ✅ **Meeting dates** — 12-month calendar grid plus section-specific meeting tables for all degrees, with recurrence-rule expansion
- ✅ **Multiple degree types** — Craft, Royal Arch, Mark Masonry, Royal Ark Mariners, RCOC (Royal Ark Mariners Companion Masons)
- ✅ **Membership summaries** — statistical summaries for Craft, Royal Arch, Mark, and RAM with disclaimers
- ✅ **Executive officer pages** — extended formatting for Grand Lodge sections with provincial officers per degree
- ✅ **Section placeholders** — title pages for each degree section
- ✅ **Unit filtering** — `-unit <number>` to render a single lodge/chapter for proofing
- ✅ **Section filtering** — `-section <id>` to render one section only
- ✅ **Bleed visualisation** — `-showbleeds` flag for debugging page boundaries
- ✅ **CSV export** — `-output csv` produces `{template}-meetings.csv` and `{template}-members.csv`
- ✅ **Batch rendering scripts** — PowerShell automation for rendering all 18 order types (PDF or HTML) with timing information
- ✅ **Lunar season meetings** — full moon date calculation with `LunarSeason` and `LunarSeasonBefore` strategies
- ✅ **Name shortening** — surnames longer than 3 words automatically shortened to last 2 words
- ✅ **Lodge list normalisation** — joining past master lodge lists stripped of spaces (`1895,6194,9660`)
- ✅ **Grand rank styling** — Grand Ranks display in bold (font-weight: 600) for visual emphasis
- ✅ **Extended rank fields** — Provincial Rank, London Rank, Other Province Rank with cascading display logic (Grand Rank takes precedence)
- ✅ **Succession lists** — Craft and Royal Arch succession pages with officer tables, years, and special characters (resigned/dismissed marks)
- ✅ **Logo integration** — cover pages and section headers with updated branding
- ✅ **Data validation** — comprehensive validation scripts and output reporting
- ✅ **Extended degree type support** — Architecture ready for 14+ degree types (Craft, Royal Arch, Mark, RAM, RCOC, plus 9 new types via YAML config)

## 🏗️ Project Structure

```
document/
├── master_v1.yaml              # Master document layout (sections, margins, format)
├── templates/
│   ├── print.css               # Paged.js @page rules, TOC styling, page breaks
│   ├── cover-page.html         # Full-bleed cover page
│   ├── foreword-page.html      # Foreword/introduction page
│   ├── copyright.html          # Copyright page
│   ├── _data-driven/toc-page.html           # Table of contents Scriban template
│   ├── _data-driven/unit-page.html          # Unit page Scriban template
│   ├── _data-driven/unit-index-page.html    # Unit index page
│   ├── meetings-calendar-page.html  # 12-month meetings calendar template
│   ├── _data-driven/meetings-table-page.html     # Section-specific meetings table template
│   ├── craft/                  # Craft-specific templates
│   │   ├── introduction.html
│   │   └── executive-officers.html
│   ├── royalarch/              # Royal Arch-specific templates
│   │   ├── introduction.html
│   │   ├── executive-officers.html
│   │   └── _placeholder.html
│   ├── ugle/                   # UGLE/Grand Lodge templates
│   │   ├── ugle-officers.html
│   │   ├── grand-officers.html
│   │   └── _placeholder.html
│   └── companion/              # Companion degrees (Mark, RAM, etc.)
│       ├── mark-introduction.html
│       ├── ram-introduction.html
│       └── _placeholder.html
├── data/
│   ├── units_v1.3.csv         # Unit data (Craft, RA, Mark, RAM; v1.3 schema)
│   ├── membership_v1.3.csv    # Member data (v1.3 schema)
│   ├── unit_meetings.csv      # Meeting recurrence rules and dates
│   └── _archive/              # Previous schema versions
├── data_sources/
│   ├── craft_data_source.yaml              # Column mappings for Craft data + succession lists
│   ├── royalarch_data_source.yaml         # Column mappings for Royal Arch data + succession lists
│   ├── mark_data_source.yaml              # Column mappings for Mark data
│   ├── ram_data_source.yaml               # Column mappings for RAM data
│   ├── meetings_data_source.yaml          # Column mappings for meetings data
│   ├── kt_data_source.yaml                # Knight Templar (v1.7+)
│   ├── ktp_data_source.yaml               # Knight Templar Priest (v1.7+)
│   ├── osc_data_source.yaml               # Order of Secret Monitor (v1.7+)
│   └── [9 additional degree types]        # Extended degrees (YAML-driven configuration)
└── images/
    └── (cover and decorative images)

src/
├── MasonicCalendar.Console/    # CLI entry point
│   └── Program.cs
├── MasonicCalendar.Core/       # Rendering engine
│   ├── Domain/                 # Business entities (SchemaUnit, SchemaOfficer, etc.)
│   ├── Loaders/                # YAML layout loader, CSV data loader
│   ├── Renderers/
│   │   ├── SchemaPdfRenderer.cs          # Main renderer (HTML + Puppeteer PDF)
│   │   ├── SectionRenderers/             # Per-section renderer implementations
│   │   │   ├── SectionRenderer.cs                        # Abstract base class
│   │   │   ├── SectionRendererFactory.cs                 # Routes sections to correct renderer
│   │   │   ├── StaticSectionRenderer.cs                  # Static pages (intro, officers)
│   │   │   ├── DataDrivenSectionRenderer.cs              # Unit pages from CSV
│   │   │   ├── TocSectionRenderer.cs                     # TOC generation
│   │   │   ├── MeetingsTableSectionRenderer.cs           # Section-specific meeting tables
│   │   │   └── MeetingsCalendarSectionRenderer.cs        # 12-month calendar grid
│   │   └── Utilities/
│   │       ├── UnitModelBuilder.cs       # Builds Scriban model from SchemaUnit
│   │       └── TextCleaner.cs            # Name/rank/lodge-list normalisation
│   └── Services/
│       ├── RecurrenceService.cs          # Meeting recurrence rule expansion
│       └── CsvExportService.cs           # CSV export (meetings + members)
└── MasonicCalendar.Tests/      # xUnit test suite
    └── RecurrenceServiceLunarTests.cs    # Lunar recurrence regression tests

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

# Render all static pages together
dotnet run -- -template master_v1 -output html -section static

# Render all officer list sections together
dotnet run -- -template master_v1 -output html -section list_officers

# Render all succession list sections (Craft PGM/DPGM/APGM + RA GS/DGS)
dotnet run -- -template master_v1 -output html -section succession-list

# Render all membership summary sections for all degrees
dotnet run -- -template master_v1 -output html -section membership-summary

# Render all meetings table sections for all degrees
dotnet run -- -template master_v1 -output html -section meetings-table

# Render a single section of one degree type
dotnet run -- -template master_v1 -output html -section craft_units

# Render Mark Masonry units only
dotnet run -- -template master_v1 -output html -section mark_units

# Render Royal Arch meeting dates table
dotnet run -- -template master_v1 -output html -section ra_meetings_table

# Render a single unit for quick proofing
dotnet run -- -template master_v1 -output html -unit 3366

# Render a single Royal Arch unit
dotnet run -- -template master_v1 -output html -unit 3366 -section royalarch_units

# Render RAM (Royal Ark Mariners) units only
dotnet run -- -template master_v1 -output html -section ram_units

# Render RCOC (Companion Masons) units only
dotnet run -- -template master_v1 -output html -section rcoc_units

# Render membership summary for a specific degree
dotnet run -- -template master_v1 -output html -section craft_membership_summary

# Render RCOC meeting dates table
dotnet run -- -template master_v1 -output html -section rcoc_meetings_table

# Render meeting dates for all Mark lodges
dotnet run -- -template master_v1 -output html -section mark_meetings_table

# Debug mode (extra console output + HTML debug file)
dotnet run -- -template master_v1 -output pdf -debug

# Export all meeting dates and member lists to CSV
dotnet run -- -template master_v1 -output csv
```

## � PowerShell Batch Rendering Scripts

The `scripts/unit-render/` directory contains PowerShell scripts for automated rendering of all 18 Masonic order types. Each script supports batch rendering to both PDF and HTML formats.

### Available Order-Specific Scripts

| Order | Script | Unit Type |
|-------|--------|-----------|
| Craft Lodges | `render-all-craft-units.ps1` | `Craft` |
| Royal Arch Chapters | `render-all-ra-units.ps1` | `RA` |
| Mark Lodges | `render-all-mark-units.ps1` | `Mark` |
| Royal Ark Mariners | `render-all-ram-units.ps1` | `RAM` |
| RCOC (Companion Masons) | `render-all-rcoc-units.ps1` | `RCOC` |
| Allied Masonic Degrees | `render-all-amd-units.ps1` | `AMD` |
| Rose Croix (A&AR) | `render-all-rc-units.ps1` | `RC` |
| St Thomas of Acon | `render-all-stoa-units.ps1` | `STOA` |
| Knights Beneficent | `render-all-kbhc-units.ps1` | `KBHC` |
| Knights Templar | `render-all-kt-units.ps1` | `KT` |
| Knight Templar Priests | `render-all-ktp-units.ps1` | `KTP` |
| Order of Athelstan | `render-all-ooa-units.ps1` | `OOA` |
| Order of Scarlet Cord | `render-all-osc-units.ps1` | `OSC` |
| Order of Secret Monitor | `render-all-osm-units.ps1` | `OSM` |
| Royal Order of Scotland | `render-all-ros-units.ps1` | `ROS` |
| Royal & Select Masters | `render-all-rsm-units.ps1` | `RSM` |
| Societas Rosicruciana | `render-all-sria-units.ps1` | `SRIA` |
| The Operatives (Purbeck) | `render-all-pbq-units.ps1` | `PBQ` |

### Usage

```powershell
# Render all Craft units to HTML (default)
.\scripts\unit-render\render-all-craft-units.ps1

# Render first 5 Royal Arch units to HTML
.\scripts\unit-render\render-all-ra-units.ps1 -Limit 5

# Render all AMD units to PDF
.\scripts\unit-render\render-all-amd-units.ps1 -Limit 0  # -Limit 0 = no limit

# Batch render ALL 18 order types as HTML
.\scripts\unit-render\render-all-unit-sections.ps1

# Batch render ALL sections as PDF
.\scripts\unit-render\render-all-unit-sections.ps1 -OutputFormat pdf
```

### Script Parameters

| Order-Specific Scripts (`render-all-*-units.ps1`) | | |
|---|---|---|
| Parameter | Type | Description |
| `-Limit` | int | Max units to render (0 = no limit; default: 0) |

| Master Batch Script (`render-all-unit-sections.ps1`) | | |
|---|---|---|
| Parameter | Type | Description |
| `-OutputFormat` | string | `pdf` or `html` (default: `html`) |
| `-TemplateDir` | string | Path to document templates (default: `e:\Development\repos\masonic-calendar\document`) |
| `-ConsoleDir` | string | Path to console project (default: `e:\Development\repos\masonic-calendar\src\MasonicCalendar.Console`) |

### Output Files

All renders output to the `output/` directory with naming convention:
- **HTML:** `master_v1.X.Y-[section]-[optional unit].html`
- **PDF:** `master_v1.X.Y-[section]-[optional unit].pdf`

Each render includes timing information to help identify performance considerations:
- Single unit: ~30–60 seconds
- Full order (50+ units): ~5–15 minutes
- All sections: ~30–60 minutes

## �📋 CLI Parameters

| Parameter | Required | Values | Description |
|-----------|----------|--------|-------------|
| `-template` | Yes | `master_v1` | Master layout template name |
| `-output` | Yes | `pdf` / `html` | Output format |
| `-section` | No | Section ID or Type | Render one section only, or all sections of a type (e.g. `-section craft_units` or `-section list_officers`) |
| `-unit` | No | Lodge number | Render one unit only (e.g. `-unit 3366`) |
| `-showbleeds` | No | flag | Overlay red/blue borders on page boundaries |
| `-debug` | No | flag | Extra console output + debug HTML file |
| `-output csv` | — | — | Exports `{template}-meetings.csv` (all expanded dates) and `{template}-members.csv` (all people per unit) to `output/` |

**Section Parameter Modes:**
- **Specific section:** `-section craft_units` renders only that section
- **Type matching:** `-section list_officers` renders **all** sections where `type: "list_officers"` (useful for aggregating similar sections)
- **Available types:** `static`, `data-driven`, `toc`, `list_officers`, `succession-list`, `membership-statistics`, `membership-summary`, `meetings-table`, `meetings-calendar`, `locations`

### Sections in `master_v1`

| Section ID | Type | Description |
|------------|------|-------------|
| `cover` | static | Full-bleed cover page |
| `master_toc` | toc | Master table of contents (all sections, alphabetical order) |
| `master_foreword` | static | Foreword/introduction |
| `ugle_officers` | static | United Grand Lodge of England officers |
| `grand_officers` | static | Provincial Grand Officers |
| `craft_succession_lists` | succession-list | Craft PGM/DPGM/APGM succession tables |
| `ra_succession_lists` | succession-list | Royal Arch GS/DGS succession tables |
| **Craft Freemasonry** | — | — |
| `craft` | static | Craft Freemasonry introduction |
| `craft_executive_officers` | static | Provincial Craft Executive officers |
| `craft_membership_summary` | membership-summary | Craft membership statistics with disclaimers |
| `craft_meetings_table` | meetings-table | Craft meeting dates table |
| `craft_toc` | toc | Craft lodges table of contents |
| `craft_units` | data-driven | All Craft lodge unit pages |
| **Royal Arch** | — | — |
| `royalarch` | static | Royal Arch Freemasonry introduction |
| `ra_executive_officers` | static | Provincial Royal Arch Executive officers |
| `ra_membership_summary` | membership-summary | Chapter membership statistics with disclaimers |
| `ra_meetings_table` | meetings-table | Royal Arch meeting dates table |
| `royalarch_toc` | toc | Royal Arch chapters table of contents |
| `royalarch_units` | data-driven | All Royal Arch chapter unit pages |
| **Mark Masonry** | — | — |
| `mark_intro` | static | Mark Masonry introduction |
| `mark_membership_summary` | membership-summary | Mark membership statistics with disclaimers |
| `mark_meetings_table` | meetings-table | Mark meeting dates table |
| `mark_toc` | toc | Mark lodges table of contents |
| `mark_units` | data-driven | All Mark lodge unit pages |
| **Royal Ark Mariners** | — | — |
| `ram_intro` | static | Royal Ark Mariners introduction |
| `ram_membership_summary` | membership-summary | RAM membership statistics with disclaimers |
| `ram_meetings_table` | meetings-table | RAM meeting dates table |
| `ram_toc` | toc | RAM lodges table of contents |
| `ram_units` | data-driven | All RAM lodge unit pages |
| **RCOC (Companion Masons)** | — | — |
| `rcoc_intro` | static | RCOC introduction |
| `rcoc_membership_summary` | membership-summary | RCOC membership statistics with disclaimers |
| `rcoc_meetings_table` | meetings-table | RCOC meeting dates table |
| `rcoc_toc` | toc | RCOC lodges table of contents |
| `rcoc_units` | data-driven | All RCOC lodge unit pages |

### Section Types

| Type | Purpose | Renderer |
|------|---------|----------|
| `static` | Pre-rendered HTML pages (UGLE, Foreword, Introductions, Executive Officers, etc.) | `StaticSectionRenderer` |
| `toc` | Auto-generated table of contents for a section (alphabetical order) | `TocSectionRenderer` |
| `data-driven` | Unit pages rendered from CSV data via Scriban template | `DataDrivenSectionRenderer` |
| `succession-list` | Officer succession tables (PGM/DPGM/APGM for Craft, GS/DGS for RA) with dates and notes | `SuccessionListSectionRenderer` |
| `membership-summary` | Statistical summaries per degree type (Craft, Royal Arch, Mark, RAM, RCOC) with disclaimers | `MembershipSummarySectionRenderer` |
| `meetings-table` | Section-specific meeting dates table (per degree type) | `MeetingsTableSectionRenderer` |
| `meetings-calendar` | Full 12-month calendar grid (supports multiple degree types) | `MeetingsCalendarSectionRenderer` |

### Output File Naming

| Command | Output file |
|---------|-------------|
| `-template master_v1 -output pdf` | `output/master_v1-all-sections.pdf` |
| `-template master_v1 -output html` | `output/master_v1-all-sections.html` |
| `-template master_v1 -output html -section static` | `output/master_v1-static-sections.html` |
| `-template master_v1 -output html -section list_officers` | `output/master_v1-list_officers-sections.html` |
| `-template master_v1 -output html -section succession-list` | `output/master_v1-succession-list-sections.html` |
| `-template master_v1 -output html -section membership-summary` | `output/master_v1-membership-summary-sections.html` |
| `-template master_v1 -output html -section craft_units` | `output/master_v1-craft_units.html` |
| `-template master_v1 -output html -section royalarch_units` | `output/master_v1-royalarch_units.html` |
| `-template master_v1 -output html -section mark_units` | `output/master_v1-mark_units.html` |
| `-template master_v1 -output html -unit 3366` | `output/master_v1-craft_units-unit3366.html` |
| `-template master_v1 -output html -unit 3366 -section royalarch_units` | `output/master_v1-royalarch_units-unit3366.html` |
| `-template master_v1 -output html -showbleeds` | `output/master_v1-all-sections-showBleeds.html` |

## 🧪 Unit Tests

Tests live in `src/MasonicCalendar.Tests/` and are run with:

```powershell
# From the repo root
dotnet test src/MasonicCalendar.Tests/MasonicCalendar.Tests.csproj
```

### Recurrence Service — Lunar Season Tests

The `RecurrenceServiceLunarTests` class guards the two lunar-based meeting strategies against regressions. Ground-truth dates are verified against actual lodge meeting schedules and the [Royal Observatory Greenwich full moon calendar](https://www.rmg.co.uk/stories/topics/full-moon-calendar) for 2026 (UK local time / BST).

| Test class | Tests | What it covers |
|------------|-------|---------------|
| `Unit472_LunarSeason_Thursday_MatchesActual` | 8 | Lodge of Friendship & Sincerity — `LunarSeason` strategy, Apr–Nov 2026 |
| `Unit1266_LunarSeasonBefore_Tuesday_MatchesActual` | 9 | Lodge of Honour & Friendship — `LunarSeasonBefore` strategy, Apr–Dec 2026 including installation month |
| `LunarSeasonBefore_BlueMoonApril2026_UsesEndOfMonthMoon` | 1 | Edge case: April 2026 has two full moons (Apr 2 and Apr 30); the early Apr 2 moon must be ignored |
| `LunarSeasonBefore_JuneMoon_CorrectlyUsesUtcDate` | 1 | Edge case: June full moon at 12:57am BST = 11:57pm Jun 29 UTC — must yield Jun 23, not Jun 30 |

#### Recurrence Strategies

| `RecurrenceStrategy` (CSV) | Behaviour |
|---------------------------|-----------|
| `Default` | Nth weekday of month (e.g. `2nd Friday`) |
| `LunarSeason` | Nearest weekday to the full moon, from candidates **after** the 2nd occurrence of that weekday in the month. Handles blue-moon months where an early full moon would otherwise select a date too early in the month. |
| `LunarSeasonBefore` | **Last** weekday on or before the end-of-month full moon (window: 15th of month → 14th of next). Installation month uses the 4th occurrence instead (pre-planned, independent of the moon). |

The full moon calculation uses the mean synodic period (29.530588853 days) from the reference full moon of 21 January 2000 UTC, accurate to ±1 day for dates in the present era. All calculations use UTC to avoid BST offset errors (notably the June 2026 full moon at 12:57am BST = 29 June UTC).

## 🛠️ Technology Stack

| Component | Library | Version |
|-----------|---------|---------|
| Framework | .NET | 8.0 |
| PDF rendering | PuppeteerSharp + Chromium | 15.0.0 |
| Print pagination | Paged.js (CDN) | 1.0+ |
| Templating | Scriban | 5.4.6 |
| CSV parsing | CsvHelper | 30.0.0 |
| YAML config | YamlDotNet | latest |

## 📝 Version History

### v1.10 (Updated July 12, 2026)
- **New:** Comprehensive PowerShell batch rendering scripts for all 18 Masonic order types
  - Individual scripts for each order (e.g., `render-all-craft-units.ps1`, `render-all-amd-units.ps1`, etc.)
  - Master batch script `render-all-unit-sections.ps1` to render all sections at once
  - Support for both PDF and HTML output formats via `-OutputFormat` parameter
- **Enhanced:** Rendering pipeline with timing information per section (helps identify performance considerations)
- **Documentation:** Updated README with PowerShell script reference and usage examples
- **Status:** v1.10 production-ready with full order type coverage and automation support

### v1.7 (Updated May 24, 2026)
- **New:** Extended rank fields — 4 new rank columns per person (Provincial Rank, London Rank, Prov Rank Other Prov, with associated date fields)
- **New:** Priority-based rank display — Grand Rank displays in bold only; other ranks cascade (Provincial → Other Prov → London) only when Grand Rank absent
- **New:** Succession lists — Craft PGM/DPGM/APGM and Royal Arch GS/DGS tables with appointment years and special characters († resigned, ‡ dismissed)
- **Enhanced:** LocationSectionRenderer now uses template path from YAML config (architectural consistency improvement)
- **Enhanced:** SchemaMember, SchemaOfficer, SchemaHonoraryMember all support new rank fields with robust date range parsing
- **Architecture:** Foundation laid for 14+ degree type support (additional degrees via data_source YAML files)
- **Status:** Craft units fully tested and rendering; additional degrees can be added via YAML configuration
- **Data Format:** membership_v1.7.csv with 27 columns including new rank fields

### v1.5 (Updated May 3, 2026)
- **New:** RCOC (Royal Ark Mariners Companion Masons) degree type with full support — officers, members, past unit heads, membership summaries, and meeting tables
- **New:** Bold Grand Rank styling — Grand Ranks now display with font-weight: 600 for visual emphasis
- **New:** Logo integration — enhanced branding on cover pages and section headers
- **Enhanced:** Meeting dates updated for all units including draft/new entries (unit 9689, etc.)
- **Enhanced:** Render scripts updated to support all unit types (Craft, RA, Mark, RAM, RCOC)
- **Fixed:** Email formatting with proper newline handling
- **Improved:** Membership summary pages for all 5 degree types with statistics and disclaimers
- **Status:** Production-ready, all features stable and tested

### v1.5 (April 2026)
- **New:** Membership summary pages for all degree types (Craft, Royal Arch, Mark, RAM) with statistics and disclaimers
- **New:** Meeting date tables for Mark Masonry and Royal Ark Mariners (added to Craft and Royal Arch)
- **Enhanced:** Executive officer pages with extended formatting and styling
- **Enhanced:** Section placeholder pages with degree titles
- **Enhanced:** Table of Contents now in alphabetical order per section
- **Improved:** Data validation with comprehensive output reporting
- **Removed:** Meridian section (no longer included)
- **Updated:** Mark and RAM data now fully integrated with all rendering features
- **Fixed:** Various data quality issues in unit data and membership records

### Earlier versions
- v1.4: Initial multi-degree support (Craft, Royal Arch, Mark, RAM)
- v1.3: Added Royal Arch sections and Mark Masonry support
- v1.0-v1.2: Core functionality with Craft lodges only

## 📄 Document Layout

The document is A6 portrait (105 × 148 mm) with:
- **Gutter margin:** 10 mm (inner/binding edge)
- **Footer:** 10 mm (page number)
- **Bleed:** 6 mm (Paged.js default, extended on cover)
- **Cover page:** zero margins, full-bleed image with `#ffeb9a` background; binding-edge compensation applied automatically via CSS variable

Page numbering starts at the `master_toc` section (`reset_page_counter: true` in YAML). The cover page has no page number (`@page :first` with empty `@bottom-center`).

## 📋 Data Format

The project uses a **consolidated v1.3+ CSV schema** (v1.5 with validation improvements) with two main files:

- **`units_v1.5.csv`** — All units (Craft, Royal Arch, Mark, RAM) with a `Unit Type` column for filtering
- **`membership_v1.5.csv`** — All members with `Unit No` and `Unit Type` columns to link to units

Each YAML data source (e.g. `craft_data_source.yaml`, `royalarch_data_source.yaml`, `mark_data_source.yaml`, `ram_data_source.yaml`) specifies:
- Which CSV files to read
- Filter criteria (`Unit Type` = "Craft", "RA", "Mark", "RAM")
- Column name mappings for each person type (Officers, PastMasters, JoiningPastMasters, Members, HonoraryMembers)
- Optional heading overrides (e.g. "Excellent Kings" instead of "Past Masters" for Royal Arch; "Past Mark Masters" for Mark; "Past Commanders" for RAM)

The consolidated approach simplifies data maintenance — a single pair of CSV files serves all degree types, with degree-specific filtering handled in the YAML data sources. The v1.5 release includes comprehensive data validation and quality improvements across all degree types.

### Unit Page Sections

Each unit page renders (where data exists):
1. **Header** — lodge/chapter name, number, warrant date, installation date
2. **Location** — meeting address, email, meeting dates
3. **Officers** — two-column table split at position 12, ordered by `OffPos`
4. **Past Masters** — name, year installed, provincial rank, rank year
5. **Joining Past Masters** — name, lodge list (comma-separated, no spaces), provincial rank
6. **Members** — 3 pre-split inline-block tables ordered by `PosNo` (deterministic, no CSS column reflow)
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

