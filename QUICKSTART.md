# Quick Start: CSV to PDF Calendar Export

This example demonstrates the core pattern for the Masonic Calendar system: reading events from CSV and exporting them to PDF.

## 📁 Structure

- **MasonicCalendar.Core** - Domain models and CSV ingestion logic
  - `Domain/CalendarEvent.cs` - The event model with DateOnly for dates
  - `Services/CsvIngestorService.cs` - Reads CSV → `Result<List<CalendarEvent>>`
  - `Services/Result.cs` - Result pattern for graceful error handling
  
- **MasonicCalendar.Export** - PDF export using QuestPDF
  - `Pdf/EventPdfExporter.cs` - Renders events to PDF with monthly grouping
  
- **MasonicCalendar.Console** - Entry point demonstrating the workflow
  - `Program.cs` - Orchestrates CSV read → PDF generation

## 🚀 Running the Example

```powershell
cd src/MasonicCalendar.Console
dotnet run
```

**Expected output:**
```
🗓️  Masonic Calendar - CSV to PDF Converter
==========================================

✅ Loaded 8 events from CSV
✅ PDF generated: calendar-output.pdf

Events included:
  • Tue, Feb 10 - Stated Meeting
  • Tue, Feb 24 - Degree Conferral
  ...
```

The PDF file `calendar-output.pdf` will be created in the Console project directory.

## 📝 Key Patterns Demonstrated

### 1. Result Pattern
`CsvIngestorService.ReadEventsFromCsv()` returns `Result<List<CalendarEvent>>` instead of throwing exceptions. This provides:
- Graceful error handling
- Clear success/failure indication
- Consistent with project's CSV/Sheets ingestion approach
- Better for API responses

### 2. Separation of Concerns
- **Core** handles only CSV parsing and models (no PDF/QuestPDF dependencies)
- **Export** handles QuestPDF rendering (no CSV dependencies)
- **Console** orchestrates the workflow
- Easy to test Core logic independently

### 3. QuestPDF Usage Patterns
The `EventPdfExporter` demonstrates:
- Fluent API for document building
- Column-based layout structure
- Events grouped by month
- Conditional rendering (Location/Description only if present)
- Community License setup for non-profit usage
- Stream-based generation (ready for web API responses)

### 4. DateOnly for Calendar
Uses `DateOnly` (C# 6.0+) for event dates—appropriate for calendar systems without time components.

## 📊 Sample Data

`data/sample-events.csv` contains Masonic lodge events:
```
EventId, EventName, EventDate, Description, Location
1, Stated Meeting, 2026-02-10, Regular monthly meeting, Main Lodge Room
...
```

8 sample events from Feb-Apr 2026 representing typical lodge activities:
- Stated Meetings (regular monthly)
- Degree Conferrals (Masonic ceremonies)
- Social Gatherings
- Open Houses

## 🔧 Extending This Example

To build towards the full system:

1. **Add recurrence rules** → Implement `RecurrenceService` with Series-Instance pattern
2. **Integrate Google Sheets** → Create `MasonicCalendar.Ingestion` project with Sheets API
3. **Add web API** → Create `MasonicCalendar.Api` with ASP.NET Core endpoints
4. **Add database** → Implement `MasonicCalendar.Data` with EF Core + SQLite
5. **Add search/filtering** → Build query logic in Api layer
6. **Add frontend** → Integrate FullCalendar.io in web UI

Each extension follows the layered architecture demonstrated here.

## 📦 Dependencies

- **CsvHelper** (30.0.0+) - CSV parsing with strong typing
- **QuestPDF** (2024.12.2+) - PDF generation with Fluent API
  - Community License (free for non-profits)
  - No external dependencies or fonts required

