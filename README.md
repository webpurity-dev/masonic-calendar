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
cd src/MasonicCalendar.Console
dotnet run                                    # Generate A6 PDF for unit 6827 (default)
dotnet run --pagesize A4                      # Generate A4 PDF
dotnet run --output html                      # Generate HTML for preview
dotnet run --137                              # Generate PDF for unit 137
dotnet run --pagesize A5 --output html --137  # A5 HTML for unit 137
```

## 📋 Console Command-Line Switches

### Output Format
```
--output <format>
  pdf   - Generate PDF document (DEFAULT)
  html  - Generate browser-viewable HTML
```

### Page Size (PDF only)
```
--pagesize <size>
  A4    - Standard size (210×297mm)
  A5    - Half size (148×210mm)
  A6    - Quarter size (105×148mm) DEFAULT
```

### Unit Filter
```
--<unit-number>
  Example: --6827  Generate PDF for unit 6827 (DEFAULT)
           --137   Generate PDF for unit 137
  
  If not specified, defaults to unit 6827
```

### Complete Examples

| Command | Output |
|---------|--------|
| `dotnet run` | `units-output-6827-A6.pdf` (default A6) |
| `dotnet run --pagesize A4` | `units-output-6827-A4.pdf` (A4 size) |
| `dotnet run --137` | `units-output-137-A6.pdf` (unit 137, A6) |
| `dotnet run --output html` | `units-output-6827-A6.html` (HTML format) |
| `dotnet run --pagesize A5 --output html --137` | `units-output-137-A5.html` (A5 HTML) |

### Default Behavior

When run without arguments:
- **Unit:** 6827 (most complete data)
- **Page Size:** A6
- **Format:** PDF
- **Filename:** `units-output-6827-A6.pdf`

## 🛠️ Technology Stack

- **Framework:** .NET 8.0 (C#)
- **PDF Generation:** QuestPDF 2024.12.2 (Community License)
- **Templating:** Scriban 5.9.0
- **CSV Parsing:** CsvHelper 30.0.0
- **HTML Parsing:** HtmlAgilityPack 1.11.61
- **Database:** SQLite with Entity Framework Core (optional)

## 📋 CSV Files

The system expects three CSV files in the `data/` directory:

1. **sample-events.csv** - Calendar events with dates and descriptions
2. **sample-units.csv** - Lodge units with meeting schedules
3. **sample-unit-locations.csv** - Physical meeting locations

Detailed column definitions are available in [data/CSV_SCHEMA.md](data/CSV_SCHEMA.md).

## 📄 Output Formats

### PDF Output
- **File:** `units-output-<unit-number>-<pagesize>.pdf`
- **Example:** `units-output-6827-A6.pdf`
- **Features:** One page per unit, professionally formatted, ready for printing
- **Font Sizes:** Centered headers (18pt), body text (10pt), table text (9pt)
- **Page Sizes:**
  - A4: Full standard letter size
  - A5: Compact size (half of A4)
  - A6: Very compact size (quarter of A4)

### HTML Output
- **File:** `units-output-<unit-number>-<pagesize>.html`
- **Example:** `units-output-6827-A6.html`
- **Features:** Browser-viewable, print-friendly, useful for template previews
- **Usage:** Preview layout before PDF generation

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
- Id, Number, Name, Location, LocationId, Email, InstallationMonth, MeetingSummary, WarrantIssued

### UnitLocation
- Id, Name, AddressLine1, Town, Postcode, What3Words

## 🔄 Workflow

1. **Read CSV Files** → CsvIngestorService parses events, units, and locations
2. **Build Location Dictionary** → Map LocationId to UnitLocation objects
3. **Render Templates** → SeribanTemplateRenderer processes HTML templates with data
4. **Parse HTML → PDF** → UnitPdfExporter converts styled HTML to PDF pages
5. **Output** → Generate timestamped PDF or HTML file

## 📖 Further Reading

- Start with [QUICKSTART.md](QUICKSTART.md) for immediate usage
- Read [IMPLEMENTATION.md](IMPLEMENTATION.md) for technical architecture
- Review [CSV_SCHEMA.md](data/CSV_SCHEMA.md) for data format details
- Check [.github/copilot-instructions.md](.github/copilot-instructions.md) for development patterns