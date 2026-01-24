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
dotnet run                      # Generate PDF (default)
dotnet run --output html        # Generate HTML for preview
```

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
- **File:** `units-output_YYYYMMDD_HHMMSS.pdf`
- **Features:** One page per unit, professionally formatted, ready for printing
- **Font Sizes:** Centered headers (24pt), body text (12pt), footer (8pt)

### HTML Output
- **File:** `units-output_YYYYMMDD_HHMMSS.html`
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