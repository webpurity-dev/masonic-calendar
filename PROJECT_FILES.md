# Masonic Calendar - Project Files Overview

## 📁 Complete File Structure

```
masonic-calendar/
├── .github/
│   └── copilot-instructions.md      ⭐ AI Coding Guidelines
├── src/
│   ├── MasonicCalendar.Core/        📦 Domain & Services
│   │   ├── Domain/
│   │   │   └── CalendarEvent.cs     🗓️ Event model
│   │   ├── Services/
│   │   │   ├── CsvIngestorService.cs 📥 CSV reader
│   │   │   └── Result.cs             ✅ Result pattern
│   │   └── MasonicCalendar.Core.csproj
│   │
│   ├── MasonicCalendar.Export/      📄 PDF Export
│   │   ├── Pdf/
│   │   │   └── EventPdfExporter.cs   📋 QuestPDF exporter
│   │   └── MasonicCalendar.Export.csproj
│   │
│   └── MasonicCalendar.Console/     ▶️ Entry Point
│       ├── Program.cs               🚀 Main app
│       ├── calendar-output.pdf      📄 Generated PDF
│       └── MasonicCalendar.Console.csproj
│
├── data/
│   └── sample-events.csv            📊 Sample data (8 events)
│
├── MasonicCalendar.sln              🔧 Solution file
├── QUICKSTART.md                    📖 Running the example
├── EXAMPLE_SUMMARY.md               📝 Implementation details
├── README.md                        🏠 Main readme
└── .git/                            🔐 Git repository

```

## 📄 Key Files

### Configuration & Documentation
| File | Purpose |
|------|---------|
| `MasonicCalendar.sln` | Solution with 3 projects |
| `.github/copilot-instructions.md` | AI guidelines for development |
| `QUICKSTART.md` | How to run the example |
| `EXAMPLE_SUMMARY.md` | Detailed implementation notes |

### Source Code
| File | Purpose |
|------|---------|
| `src/MasonicCalendar.Core/Domain/CalendarEvent.cs` | Event model (DateOnly, string properties) |
| `src/MasonicCalendar.Core/Services/CsvIngestorService.cs` | CSV parsing with Result<T> pattern |
| `src/MasonicCalendar.Core/Services/Result.cs` | Result<T> for error handling |
| `src/MasonicCalendar.Export/Pdf/EventPdfExporter.cs` | PDF generation with QuestPDF |
| `src/MasonicCalendar.Console/Program.cs` | Orchestrates CSV→PDF workflow |

### Data
| File | Purpose |
|------|---------|
| `data/sample-events.csv` | 8 sample lodge events (Feb-Apr 2026) |
| `src/MasonicCalendar.Console/calendar-output.pdf` | Generated output (30KB) |

## 🚀 How to Use

### Build the solution:
```powershell
cd e:\Development\repos\masonic-calendar
dotnet build
```

### Run the example:
```powershell
cd src/MasonicCalendar.Console
dotnet run
```

### Output:
- PDF file created: `src/MasonicCalendar.Console/calendar-output.pdf`
- Console output shows loaded events and generation status

## 📦 Project Dependencies

### MasonicCalendar.Core
- **CsvHelper** 30.0.0+ - Strong-typed CSV parsing

### MasonicCalendar.Export
- **QuestPDF** 2024.12.2+ - Fluent PDF generation
  - Community License (free for non-profits)

### MasonicCalendar.Console
- References: Core, Export

## ✨ Architecture Highlights

1. **Layered Design**
   - Core (domain logic, no framework deps)
   - Export (specialized rendering)
   - Console (orchestration)

2. **Error Handling Pattern**
   - Result<T> instead of exceptions
   - Clear success/failure semantics

3. **Calendar-Specific Choices**
   - `DateOnly` for event dates (no time component)
   - Event grouping by month in PDF
   - Monthly calendar layout

4. **Extensibility**
   - Easy to add recurrence rules
   - Ready for Google Sheets integration
   - Foundation for web API/database layer

## 📚 Documentation

- **QUICKSTART.md** - Step-by-step guide to running the example
- **EXAMPLE_SUMMARY.md** - Technical implementation details
- **.github/copilot-instructions.md** - Full project guidelines for AI assistants

## ✅ Verification Checklist

- [x] Solution builds without errors
- [x] All NuGet packages restore correctly
- [x] Console app runs successfully
- [x] CSV file is parsed correctly (8 events)
- [x] PDF file is generated (~30KB)
- [x] Events are displayed in console
- [x] Code follows project conventions
  - [x] File-scoped namespaces
  - [x] Result pattern for CSV operations
  - [x] DateOnly for calendar dates
  - [x] Separation of concerns

---

**Last Updated**: January 24, 2026  
**Status**: ✅ All systems working
