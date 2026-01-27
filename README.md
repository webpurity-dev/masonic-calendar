# Masonic Calendar - PDF Export System

A comprehensive .NET solution for generating searchable, downloadable calendars for non-profit organizations. The system reads event and unit data from CSV files and generates beautifully formatted PDF documents with support for customizable HTML templates.

## 📚 Documentation

### Getting Started
- **[START_HERE.md](START_HERE.md)** - Overview of the project and status
- **[QUICKSTART.md](QUICKSTART.md)** - How to run the application in 30 seconds

### Detailed Guides
- **[IMPLEMENTATION.md](IMPLEMENTATION.md)** - Complete technical implementation details
- **[EXAMPLE_SUMMARY.md](EXAMPLE_SUMMARY.md)** - Technical deep-dive and architecture
- **[PROJECT_FILES.md](PROJECT_FILES.md)** - File structure and organization

### Data & Configuration
- **[data/CSV_SCHEMA.md](data/CSV_SCHEMA.md)** - CSV input file formats and column definitions
- **[data/UNIT_PAGE_LAYOUT.md](data/UNIT_PAGE_LAYOUT.md)** - Unit page layout templates and styling

### Development Guidelines
- **[.github/copilot-instructions.md](.github/copilot-instructions.md)** - AI development guidelines and patterns

## 🎯 Features

- ✅ CSV data ingestion with flexible date formats
- ✅ Template-driven PDF generation using Scriban + QuestPDF
- ✅ HTML template support with CSS styling
- ✅ Support for both PDF and HTML output formats
- ✅ Centered, professionally formatted documents
- ✅ Customizable layouts via HTML templates
- ✅ **Meetings Calendar** - 12-month calendar grid view with unit meeting schedules
- ✅ **Recurrence Rules** - Automatic expansion of recurring meetings (weekly, monthly, nth weekday, etc.)
- ✅ **Dynamic Date Ranges** - Generate 12-month rolling calendars from any month via `--from-date MM-YYYY`
- ✅ **Current Month Default** - Automatically generates from current month when no date specified
- ✅ **Email Integration** - HTML meetings calendar meetings are mailto links to unit email addresses

## 🏗️ Project Structure

```
src/
├── MasonicCalendar.Core/        # Domain models & CSV parsing
│   ├── Domain/                  # Business entities
│   │   ├── CalendarEvent.cs
│   │   ├── Unit.cs
│   │   └── UnitLocation.cs
│   └── Services/
│       ├── CsvIngestorService.cs
│       └── Result.cs
├── MasonicCalendar.Export/      # PDF & HTML generation
│   └── Pdf/
│       ├── UnitPdfExporter.cs
│       └── SeribanTemplateRenderer.cs
└── MasonicCalendar.Console/     # CLI application
    └── Program.cs
data/
├── templates/
│   └── unit-page.html           # Scriban HTML template
├── sample-events.csv            # Calendar events
├── sample-units.csv             # Units/lodges
└── sample-unit-locations.csv    # Meeting locations
```

## 🚀 Quick Start

```powershell
cd e:\Development\repos\masonic-calendar
dotnet run --project src/MasonicCalendar.Console                                    # Generate A6 PDF for all units (default)
dotnet run --project src/MasonicCalendar.Console -- --pagesize A4                      # Generate A4 PDF for all units
dotnet run --project src/MasonicCalendar.Console -- --output html                      # Generate HTML for all unitsdotnet run --project src/MasonicCalendar.Console -- --unit-type craft                  # Generate PDF for all Craft units
dotnet run --project src/MasonicCalendar.Console -- --unit-type royalarch --output html # Generate HTML for all RoyalArch unitsdotnet run --project src/MasonicCalendar.Console -- --137                              # Generate PDF for unit 137 only
dotnet run --project src/MasonicCalendar.Console -- --pagesize A5 --output html --137  # A5 HTML for unit 137
dotnet run --project src/MasonicCalendar.Console -- --meetings-calendar                # Generate 12-page meetings calendar
```

## 📋 Console Command-Line Switches

### Unit Pages (Default)
Generate individual unit pages with officer and location information.

#### Output Format
```
--output <format>
  pdf   - Generate PDF document (DEFAULT)
  html  - Generate browser-viewable HTML
```

#### Page Size (PDF only)
```
--pagesize <size>
  A4    - Standard size (210×297mm)
  A5    - Half size (148×210mm)
  A6    - Quarter size (105×148mm) DEFAULT
```

#### Unit Filter
```
--<unit-number>
  Example: --137   Generate PDF for unit 137 only
           --6827  Generate PDF for unit 6827 only
  
  If not specified, generates for ALL units (default)

--unit-type <type>
  craft     Generate for all Craft units (49 units)
  royalarch Generate for all RoyalArch units (3 units)
  
  Example: --unit-type craft generates all Craft lodge units
           --unit-type royalarch generates all RoyalArch chapter units
  
  Note: Cannot be used together with --<unit-number>
```

### Meetings Calendar
Generate a 12-month calendar grid showing all unit meetings with recurrence rules expanded.

```
--meetings-calendar
  Generates: meetings-output-2026-<pagesize>-<orientation>.pdf or meetings-output-2026.html
  Output:    12-page PDF with one month per page, or responsive HTML
  Features:  Calendar grid layout with unit number and meeting title on each date
  Supported: PDF (A4/A5/A6), HTML
```

#### Page Size (PDF only)
```
--pagesize <size>
  A4        - Large format (210×297mm) 
  A5        - Medium format (148×210mm)
  A6        - Small format (105×148mm) DEFAULT
```

#### Orientation (PDF only)
```
--landscape
  portrait  - Taller than wide (DEFAULT)
  landscape - Wider than tall (use --landscape flag)
```

#### Include Sundays
```
--incSunday
  Includes Sunday column (DEFAULT: excludes Sundays as no meetings scheduled)
  Filename adds "-withsunday" suffix when used
```

#### Output Format
```
--output <format>
  pdf   - Generate PDF document (DEFAULT)
  html  - Generate browser-viewable HTML
```

#### Date Range Parameter
```
--from-date MM-YYYY
  Generates a 12-month rolling calendar starting from the specified month
  Format: MM-YYYY (e.g., 06-2026 for June 2026)
  Default: Current month if not specified
  Example: --from-date 10-2026 generates Oct 2026 through Sep 2027
```

#### Meetings Calendar Examples
```
--meetings-calendar                                      # A6 portrait PDF (default, current month)
--meetings-calendar --pagesize A4                        # A4 portrait PDF (current month)
--meetings-calendar --pagesize A6 --landscape            # A6 landscape PDF (current month)
--meetings-calendar --output html                        # HTML format (current month)
--meetings-calendar --pagesize A6 --incSunday            # Include Sunday column (current month)
--meetings-calendar --from-date 10-2026                  # A6 PDF for Oct 2026 - Sep 2027
--meetings-calendar --output html --from-date 06-2026    # HTML for Jun 2026 - May 2027
```

### Complete Examples

| Command | Output |
|---------|--------|
| `dotnet run --project src/MasonicCalendar.Console` | `units-output-all-units-A6.pdf` (all 52 units, A6) |
| `dotnet run --project src/MasonicCalendar.Console -- --pagesize A4` | `units-output-all-units-A4.pdf` (all units, A4) |
| `dotnet run --project src/MasonicCalendar.Console -- --output html` | `units-output-all-units-A6.html` (all units, HTML) |
| `dotnet run --project src/MasonicCalendar.Console -- --unit-type craft` | `units-output-craft-A6.pdf` (49 Craft units, A6) |
| `dotnet run --project src/MasonicCalendar.Console -- --unit-type craft --output html` | `units-output-craft-A6.html` (49 Craft units, HTML) |
| `dotnet run --project src/MasonicCalendar.Console -- --unit-type royalarch` | `units-output-royalarch-A6.pdf` (3 RoyalArch units, A6) |
| `dotnet run --project src/MasonicCalendar.Console -- --unit-type royalarch --pagesize A5` | `units-output-royalarch-A5.pdf` (3 RoyalArch units, A5) |
| `dotnet run --project src/MasonicCalendar.Console -- --137` | `units-output-137-A6.pdf` (unit 137 only, A6) |
| `dotnet run --project src/MasonicCalendar.Console -- --output html --137` | `units-output-137-A6.html` (unit 137 only, HTML) |
| `dotnet run --project src/MasonicCalendar.Console -- --pagesize A5 --output html --137` | `units-output-137-A5.html` (unit 137 only, A5 HTML) |
| `dotnet run --project src/MasonicCalendar.Console -- --meetings-calendar` | `meetings-output-01-2026-A6-portrait.pdf` (current month, 12-month calendar) |
| `dotnet run --project src/MasonicCalendar.Console -- --meetings-calendar --pagesize A4` | `meetings-output-01-2026-A4-portrait.pdf` (A4, current month) |
| `dotnet run --project src/MasonicCalendar.Console -- --meetings-calendar --landscape` | `meetings-output-01-2026-A6-landscape.pdf` (landscape, current month) |
| `dotnet run --project src/MasonicCalendar.Console -- --meetings-calendar --output html` | `meetings-output-01-2026.html` (HTML calendar, current month, with mailto links) |
| `dotnet run --project src/MasonicCalendar.Console -- --meetings-calendar --incSunday` | `meetings-output-01-2026-A6-portrait-withsunday.pdf` (with Sundays, current month) |
| `dotnet run --project src/MasonicCalendar.Console -- --meetings-calendar --from-date 10-2026` | `meetings-output-10-2026-A6-portrait.pdf` (Oct 2026 - Sep 2027) |
| `dotnet run --project src/MasonicCalendar.Console -- --meetings-calendar --output html --from-date 06-2026` | `meetings-output-06-2026.html` (Jun 2026 - May 2027, with mailto links) |

### Default Behavior

When run without arguments:
- **Units:** All 52 units
- **Page Size:** A6
- **Format:** PDF
- **Filename:** `units-output-all-units-A6.pdf`

## 🛠️ Technology Stack

- **Framework:** .NET 8.0 (C#)
- **PDF Generation:** QuestPDF 2024.12.2 (Community License)
- **Templating:** Scriban 5.9.0
- **CSV Parsing:** CsvHelper 30.0.0
- **HTML Parsing:** HtmlAgilityPack 1.11.61
- **Database:** SQLite with Entity Framework Core (optional)

## 📋 CSV Files

The system expects multiple CSV files in the `data/` directory:

### Required Files
1. **sample-events.csv** - Calendar events with dates and descriptions
2. **sample-units.csv** - Lodge units with meeting schedules
3. **sample-unit-locations.csv** - Physical meeting locations

### Optional Files
4. **sample-officers.csv** - Officer positions and titles
5. **sample-unit-officers.csv** - Current officers for each unit
6. **sample-unit-pmo.csv** - Past Masters for each unit
7. **sample-unit-meetings.csv** - Meeting recurrence rules for each unit

Detailed column definitions are available in [data/CSV_SCHEMA.md](data/CSV_SCHEMA.md).

## 📄 Output Formats

### Unit Pages PDF
- **File:** `units-output-<unit-number>-<pagesize>.pdf`
- **Example:** `units-output-6827-A6.pdf`
- **Features:** One page per unit, professionally formatted, ready for printing
- **Font Sizes:** Centered headers (18pt), body text (10pt), table text (9pt)
- **Page Sizes:**
  - A4: Full standard letter size
  - A5: Compact size (half of A4)
  - A6: Very compact size (quarter of A4)

### Unit Pages HTML
- **File:** `units-output-<unit-number>-<pagesize>.html`
- **Example:** `units-output-6827-A6.html`
- **Features:** Browser-viewable, print-friendly, useful for template previews
- **Usage:** Preview layout before PDF generation

### Meetings Calendar PDF
- **File:** `meetings-output-<MM>-<YYYY>-<pagesize>-<orientation>.pdf`
- **Examples:** 
  - `meetings-output-01-2026-A6-portrait.pdf` (default, Jan 2026-Dec 2026)
  - `meetings-output-10-2026-A4-landscape.pdf` (large landscape, Oct 2026-Sep 2027)
  - `meetings-output-06-2026-A6-portrait-withsunday.pdf` (with Sundays included, Jun 2026-May 2027)
- **Features:** 12-page calendar (one month per page), professional grid layout
- **Page Sizes:** A4 (large), A5 (medium), A6 (small/default)
- **Orientations:** Portrait (default) or Landscape
- **Content Per Date Cell:**
  - Date number (bold, 7pt)
  - Unit number and meeting title (6pt, color-coded by unit type)
  - Craft units: Blue (#1e73be)
  - Royal Arch units: Red (#c41e3a) with "C" prefix
- **Data Source:** Reads from `sample-unit-meetings.csv` with recurrence rules expanded
- **Default Behavior:** Excludes Sundays (no meetings scheduled); use `--incSunday` to include

### Meetings Calendar HTML
- **File:** `meetings-output-<MM>-<YYYY>.html` or `meetings-output-<MM>-<YYYY>-withsunday.html`
- **Examples:**
  - `meetings-output-01-2026.html` (Jan 2026-Dec 2026)
  - `meetings-output-10-2026-withsunday.html` (Oct 2026-Sep 2027, with Sundays)
- **Features:** Responsive calendar grid, browser-viewable, print-friendly, clickable mailto links
- **Content:** Same as PDF format with color-coded units, each meeting is a clickable mailto link to the unit's email address
- **Usage:** Preview or web-based calendar view with email integration

## 🎨 Template Customization

The PDF layout is controlled by `data/templates/unit-page.html`, a Scriban template that supports:

- **Scriban Variables:** Access to unit and location data (e.g., `{{ unit.name }}`, `{{ location.addressLine1 }}`)
- **CSS Styling:** Font sizes, colors, alignment, spacing
- **Conditional Blocks:** Show/hide sections based on data availability
- **HTML Structure:** Full HTML5 support with inline styles

See [UNIT_PAGE_LAYOUT.md](data/UNIT_PAGE_LAYOUT.md) for detailed template documentation.

## 💾 Data Models

### CalendarEvent
- EventId, EventName, EventDate (DateOnly), Description, Location

### Unit
- Id, Number, Name, Location, LocationId, Email, InstallationMonth, MeetingSummary, WarrantIssued, LastInstallationDate

### UnitLocation
- Id, Name, AddressLine1, Town, Postcode, What3Words

### Officer
- Id, Order, Abbreviation, Name

### UnitOfficer
- Id, UnitId, OfficerId, LastName, Initials

### UnitPastMaster
- Id, UnitId, LastName, Initials, Installed, ProvRank, ProvRankIssued

### UnitMeeting
- Id, UnitId, Title, RecurrenceType, RecurrenceStrategy, DayOfWeek, WeekNumber, DayNumber, StartMonth, EndMonth, Months, Override

### Unit
- Id, Number, Name, Location, LocationId, Email, InstallationMonth, MeetingSummary, WarrantIssued, LastInstallationDate, UnitType

### Unit Pages
1. **Read CSV Files** → CsvIngestorService parses events, units, locations, officers, and past masters
2. **Build Location Dictionary** → Map LocationId to UnitLocation objects
3. **Render Templates** → SeribanTemplateRenderer processes HTML templates with data
4. **Parse HTML → PDF** → UnitPdfExporter converts styled HTML to PDF pages
5. **Output** → Generate timestamped PDF or HTML file

### Meetings Calendar
1. **Read CSV Files** → CsvIngestorService parses meetings and units (including UnitType and Email)
2. **Expand Recurrence Rules** → MeetingRecurrenceExpander converts meeting rules into actual dates with Months field support
3. **Lookup Unit Information** → Map each UnitId to Unit details (number, type, email) for color-coding and mailto links
4. **Generate Calendar Grid** → MeetingsCalendarExporter creates 12-month rolling calendar layout with:
   - Configurable page size (A4/A5/A6)
   - Portrait or Landscape orientation
   - Optional Sunday column (default: excluded)
   - Color-coded by unit type (Craft: blue, Royal Arch: red)
   - Dynamic date ranges (--from-date MM-YYYY parameter, defaults to current month)
5. **Output** → Generate PDF (multiple page sizes/orientations) or responsive HTML
   - HTML meetings are clickable mailto links to unit email addresses
   - Filenames include date range (e.g., meetings-output-10-2026-A6-portrait.pdf)

## 📖 Further Reading

- Start with [QUICKSTART.md](QUICKSTART.md) for immediate usage
- Read [IMPLEMENTATION.md](IMPLEMENTATION.md) for technical architecture
- Review [CSV_SCHEMA.md](data/CSV_SCHEMA.md) for data format details
- Check [.github/copilot-instructions.md](.github/copilot-instructions.md) for development patterns