# Masonic Calendar - AI Coding Instructions

# Project Context: Non-Profit Searchable Calendar System
You are an expert C# .NET developer assistant with extensive experience in ASP.NET Core and Entity Framework Core.  You also have expert knowledge in software architecture and design patterns. You are assisting in the development of a calendar system for a non-profit organization that requires complex recurrence rules, PDF exports for printing, and integrations with Google Sheets and CSV files.

This project is a searchable, downloadable calendar system for a non-profit organization. All libraries must be open-source (MIT/Apache 2.0) or have a free tier for non-profits (e.g., QuestPDF Community License).

## 🏗️ Technical Stack
- **Framework:** ASP.NET Core (latest LTS)
- **Database:** SQLite (Relational) via EF Core
- **PDF Generation:** QuestPDF (Fluent API)
- **Frontend:** FullCalendar.io (JavaScript) with Bootstrap/Tailwind
- **Data Ingestion:** CsvHelper and Google Sheets API (v4)
- **Patterns:** Series-Instance Pattern for recurrences

## 📅 Domain Logic: Recurrence Rules
The system uses a 'Series-Instance' pattern.
1. **EventSeries:** Stores metadata and rules (e.g., "4th Tuesday", "Lunar Thursday").
2. **EventInstance:** Stores the expanded, concrete dates (e.g., "2026-09-22").

### Recurrence Formulas
When generating dates:
- **Nth Weekday:** To find the $N^{th}$ occurrence of a `DayOfWeek` in a month:
  $Date = FirstOfMonth + ((TargetDay - FirstOfMonthDay + 7) \pmod 7) + (7 \times (N - 1))$
- **Lunar Logic:** For events based on moon phases (e.g., "Thursday closest to full moon"), use astronomical calculation strategies before persisting to the `EventInstance` table.

## 💾 Database Schema Standards (SQLite)
- Use `DateOnly` for event dates.
- All `EventSeries` should include a `CustomStrategyKey` or `RecurrenceRule` string.
- Favor `EventInstances` for all public-facing queries to ensure performance and allow for individual date overrides/cancellations.

## 📄 PDF Export Requirements
- Use **QuestPDF**.
- Layout must be a grid-based calendar view.
- Generation must be stream-based to handle potentially large filtered datasets without memory spikes.

## ☁️ Integrations
- **Google Sheets:** Authenticate via Service Accounts.
- **CSV:** Implement robust mapping using `CsvHelper` to allow non-technical staff to batch-upload events.

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

## �📝 Coding Preferences
- Use File-Scoped Namespaces.
- Favor Primary Constructors where applicable.
- Use `Result<T>` or similar patterns for complex data ingestion from external sources (CSV/Sheets).
- Ensure all business logic for date calculation is unit-testable and decoupled from the DB context.