# Frontend Management System Analysis
## Masonic Calendar - UI for Document and Data Source Management

**Date:** May 24, 2026  
**Purpose:** Plan and analyze options for a frontend interface to manage the Masonic Calendar system  
**Scope:** Complete document/section management, data source configuration, and rendering control

---

## 1. Executive Summary

The current Masonic Calendar system is CLI-driven with YAML/CSV file management. A frontend UI would significantly improve usability for non-technical stakeholders by providing:

- **Visual document structure editor** for master_v1.yaml sections
- **Data source configuration interface** for YAML metadata and CSV mappings
- **CSV data editor** for officers, members, and other tables
- **One-click rendering** of full documents or individual sections
- **Validation feedback** before rendering
- **Change tracking and backup** for rollback capability

**Recommended Approach:** Hybrid architecture with **ASP.NET Core Razor Pages** (web) or **WPF** (desktop), backed by the existing .NET Core renderer engine, with a file-based or SQL database for change tracking.

---

## 2. Current System Architecture

### 2.1 Existing Components

```
User (CLI)
  ↓
Program.cs (Parameter parsing)
  ↓
SchemaPdfRenderer (Orchestration)
  ├── DocumentLayoutLoader (YAML → C# objects)
  ├── SchemaDataLoader (CSV → SchemaUnit objects)
  └── SectionRendererFactory (Route to specific renderer)
      ├── DataDrivenSectionRenderer (Unit pages)
      ├── StaticSectionRenderer (Fixed templates)
      ├── TocSectionRenderer (Table of contents)
      └── ProvincialOfficersSectionRenderer (Officer lists)
  ↓
PuppeteerSharp + Paged.js
  ↓
Output: PDF or HTML
```

### 2.2 File Structure for Management

**Configuration Files (YAML):**
- `document/master_v1.yaml` — 50+ section definitions
- `document/data_sources/{degree}_data_source.yaml` — Per-degree officer/member configuration (14 files)

**Data Files (CSV):**
- `document/data/{type}_v1.7.csv` — Unit data, membership data, officer lists
- ~12 CSV files total

**Templates (Scriban):**
- `document/templates/*.html` — 20+ HTML templates with embedded Scriban logic

### 2.3 Key Data Models

**Master Document Structure (from master_v1.yaml):**
```yaml
sections:
  - section_id: "craft_units"
    type: "data-driven"
    template: "_data-driven/unit-page.html"
    data_mapping: "data_sources/craft_data_source.yaml"
    reset_page_counter: true
    hide_from_parent_toc: false
```

**Data Source Configuration (from {degree}_data_source.yaml):**
```yaml
sections:
  - section_id: "members"
    source: "membership_v1.7.csv"
    fields:
      - name: "Name"
        csv_column: "Member Name"
        type: "string"
```

**Data Files (CSV):**
```
Row-based tabular data with headers:
Name, Lodge Number, Rank, Join Date, ...
```

---

## 3. Frontend Architecture Options

### Option 1: Web-Based (ASP.NET Core + React/Blazor)

#### 3.1.1 Advantages
- ✅ Cross-platform (Windows, Mac, Linux)
- ✅ No installation required (browser access)
- ✅ Easy to deploy on server
- ✅ Multi-user capable (with authentication)
- ✅ Real-time collaboration possible
- ✅ Mobile-friendly responsive design

#### 3.1.2 Disadvantages
- ❌ Network-dependent
- ❌ More complex deployment
- ❌ Database required for persistence
- ❌ File locking issues with shared CSV editing

#### 3.1.3 Technology Stack
```
Backend:     ASP.NET Core 8.0 WebAPI
Frontend:    Blazor Server (C# + .NET) OR React (TypeScript)
Database:    SQL Server/PostgreSQL or file-based JSON
File Store:  Azure Blob Storage or local filesystem
Auth:        AAD / Custom user management
Rendering:   Existing SchemaPdfRenderer engine
```

#### 3.1.4 Component Architecture
```
UI Layer (Blazor Components)
  ├── DocumentStructureEditor
  │   ├── SectionListViewer
  │   ├── SectionDetailEditor
  │   └── SectionReorder (drag-drop)
  ├── DataSourceManager
  │   ├── DataSourceListViewer
  │   ├── MappingEditor
  │   └── FieldConfigEditor
  ├── CsvDataEditor
  │   ├── TableViewer (sortable, filterable)
  │   ├── RowEditor (modal)
  │   └── BulkImport
  └── RenderingManager
      ├── RenderPreview (live)
      ├── ValidationReporter
      └── OutputDownload

API Layer (Controllers)
  ├── DocumentController (CRUD master_v1.yaml)
  ├── DataSourceController (CRUD {degree}_data_source.yaml)
  ├── CsvDataController (CRUD CSV files)
  ├── RendererController (Trigger rendering)
  └── ValidationController (Run checks)

Persistence Layer
  ├── FileSystemRepository (YAML/CSV)
  ├── ChangeTrackingService (Git or custom)
  └── BackupService (Scheduled snapshots)

Integration Layer
  └── SchemaPdfRenderer (Existing engine)
```

---

### Option 2: Desktop (WPF or WinForms)

#### 3.2.1 Advantages
- ✅ Native performance and responsiveness
- ✅ Offline-capable (no network needed)
- ✅ Simple file management (direct file access)
- ✅ Lower infrastructure cost
- ✅ Mature tooling (Visual Studio designer)

#### 3.2.2 Disadvantages
- ❌ Windows-only (or cross-platform with challenges)
- ❌ Single-user (no collaboration)
- ❌ Installation/update management required
- ❌ No multi-user access control

#### 3.2.3 Technology Stack
```
Framework:   WPF (.NET 8.0) or WinForms
UI Library:  XAML (WPF) or designer forms (WinForms)
Data Binding: MVVM Toolkit (WPF)
File Access: Direct filesystem API
Rendering:   Existing SchemaPdfRenderer engine
```

#### 3.2.4 Component Architecture
```
WPF Main Window
  ├── DocumentStructurePane (TreeView + Editor)
  ├── DataSourcePane (TabControl)
  ├── CsvEditorPane (DataGrid)
  ├── RenderingPane (Preview + Output)
  └── StatusBar (Validation messages)

ViewModels (MVVM)
  ├── DocumentStructureViewModel
  ├── DataSourceViewModel
  ├── CsvEditorViewModel
  └── RenderingViewModel

Services (Direct file access)
  ├── YamlFileService (Read/write YAML)
  ├── CsvFileService (Read/write CSV)
  ├── RendererService (Wrapper around SchemaPdfRenderer)
  └── ValidationService (Pre-render checks)
```

---

### Option 3: Hybrid (Electron-based Cross-Platform)

#### 3.3.1 Advantages
- ✅ Desktop app feel with cross-platform support
- ✅ Fast, responsive UI
- ✅ Offline-capable
- ✅ File system access for direct editing

#### 3.3.2 Disadvantages
- ❌ Large app size
- ❌ JavaScript/TypeScript required (skill gap)
- ❌ .NET integration complex

---

## 4. Recommended Approach: ASP.NET Core Web UI

**Rationale:**
- Aligns with existing .NET 8.0 stack
- Supports multiple users (future-proof)
- Easier to extend and maintain
- Deployable to cloud or on-premises
- Responsive design works on tablets for field use

**Architecture Pattern:** MVC + MVVM (Blazor Server)

---

## 5. Feature Requirements & UI Flows

### 5.1 Document Structure Manager

**Current State:** Manual YAML editing  
**Desired State:** Visual editor with drag-drop and validation

#### UI Features:
```
┌─ Document Structure Editor ─────────────────────────────┐
│                                                         │
│ [+ New Section] [Import from Template]                 │
│                                                         │
│ Section List (Reorderable):                            │
│ ┌─────────────────────────────────────────────────┐   │
│ │ 1. [≡] cover          (static)      [Edit] [×]  │   │
│ │ 2. [≡] master_toc     (toc)         [Edit] [×]  │   │
│ │ 3. [≡] craft_units    (data-driven) [Edit] [×]  │   │
│ │ 4. [≡] craft_intro    (static)      [Edit] [×]  │   │
│ └─────────────────────────────────────────────────┘   │
│                                                         │
│ Section Details Panel (selected section):              │
│ ┌─────────────────────────────────────────────────┐   │
│ │ Section ID:     craft_units                      │   │
│ │ Type:           [data-driven ▼]                 │   │
│ │ Template:       [_data-driven/unit-page.html ▼]             │   │
│ │ Data Mapping:   [craft_data_source.yaml ▼]    │   │
│ │ ☐ Reset Page Counter                            │   │
│ │ ☐ Hide from Parent TOC                          │   │
│ │ ☐ Override Break Before                         │   │
│ │                                                  │   │
│ │ [Save] [Cancel] [Duplicate]                     │   │
│ └─────────────────────────────────────────────────┘   │
└─────────────────────────────────────────────────────────┘
```

**Operations:**
- ✅ Add/Edit/Delete sections
- ✅ Reorder sections (drag-drop or up/down arrows)
- ✅ View section dependency tree
- ✅ Duplicate sections from templates
- ✅ Validate references (template exists, data mapping exists)
- ✅ Preview section in isolation

**Data Flow:**
```
UI Form Input → SectionViewModel → DocumentController.UpdateSection() 
→ YamlFileService.SaveMaster_v1() → Filesystem
```

---

### 5.2 Data Source Configuration Manager

**Current State:** Manual YAML editing  
**Desired State:** Form-based editor with field mapping UI

#### UI Features:
```
┌─ Data Source Manager ────────────────────────────────┐
│                                                      │
│ [Craft Lodges] [Royal Arch] [Mark] [RAM] [Order...] │
│                                                      │
│ Data Source: craft_data_source.yaml                 │
│                                                      │
│ General Settings:                                    │
│ ┌──────────────────────────────────────────────┐   │
│ │ Data Source File:    craft_officers_v1.7.csv │   │
│ │ Primary CSV:         units_v1.7.csv         │   │
│ └──────────────────────────────────────────────┘   │
│                                                      │
│ Officer Metadata:                                    │
│ ┌──────────────────────────────────────────────┐   │
│ │ Heading 1: Provincial Grand Lodge of Dorset │   │
│ │ Heading 2: [                            ]    │   │
│ │ Officers Heading: Provincial Officers 2025  │   │
│ │ Crest Image: [Browse...] provincial-c...jpg │   │
│ └──────────────────────────────────────────────┘   │
│                                                      │
│ Officer Groups:                                      │
│ ┌──────────────────────────────────────────────┐   │
│ │ Heads:        [Add] [+ PGM: name]             │   │
│ │ Deputy Heads: [Add] [+ DPGM: name]            │   │
│ │ District Heads: [Add]                         │   │
│ └──────────────────────────────────────────────┘   │
│                                                      │
│ [Save] [Cancel] [View YAML]                        │
└──────────────────────────────────────────────────────┘
```

**Operations:**
- ✅ Select degree/order from dropdown
- ✅ Edit metadata (headings, crest, etc.)
- ✅ Manage officer groups (add/edit/delete ranks and names)
- ✅ Map CSV columns to fields
- ✅ View/edit raw YAML
- ✅ Validate CSV file exists and has required columns

**Data Flow:**
```
UI Form Input → DataSourceViewModel → DataSourceController.UpdateDataSource()
→ YamlFileService.Save{Degree}_data_source() → Filesystem
```

---

### 5.3 CSV Data Editor

**Current State:** Manual CSV editing in Excel/VS Code  
**Desired State:** In-app table editor with validation

#### UI Features:
```
┌─ CSV Data Editor ────────────────────────────────────┐
│                                                      │
│ CSV File: [membership_v1.7.csv ▼] [+ New Table]    │
│                                                      │
│ ┌─ Table View ─────────────────────────────────┐   │
│ │  [Add Row] [Delete Selected] [Import CSV]    │   │
│ ├────────────────────────────────────────────┤   │
│ │ # │ Name        │ Lodge │ Rank    │ Joined │   │
│ ├────────────────────────────────────────────┤   │
│ │ 1 │ John Smith  │ 3366  │ Master  │ 1990   │ ✎  │
│ │ 2 │ James Brown │ 5848  │ Fellow  │ 2000   │ ✎  │
│ │ 3 │ Peter Jones │ 3366  │ Entered │ 2020   │ ✎  │
│ └────────────────────────────────────────────┘   │
│                                                      │
│ Row Editor (Modal):                                 │
│ ┌─────────────────────────────────────────┐        │
│ │ Name:      [John Smith]                 │        │
│ │ Lodge:     [3366]                       │        │
│ │ Rank:      [Master ▼]                   │        │
│ │ Joined:    [1990]                       │        │
│ │                                         │        │
│ │ [Save] [Cancel]                        │        │
│ └─────────────────────────────────────────┘        │
│                                                      │
│ [Save to File] [Cancel] [Export as CSV]            │
└──────────────────────────────────────────────────────┘
```

**Operations:**
- ✅ View/edit table data in grid
- ✅ Add/edit/delete rows
- ✅ Import CSV (append or replace)
- ✅ Export current table to CSV
- ✅ Validate required columns
- ✅ Detect duplicate entries
- ✅ Sort/filter before rendering

**Data Flow:**
```
UI GridView Input → CsvEditorViewModel → CsvDataController.UpdateCsvFile()
→ CsvFileService.SaveCsv() → Filesystem
```

---

### 5.4 Document Rendering & Preview

**Current State:** CLI with -output parameter  
**Desired State:** One-click render with live preview option

#### UI Features:
```
┌─ Rendering Manager ──────────────────────────────────┐
│                                                      │
│ Render Options:                                      │
│ ┌──────────────────────────────────────────────┐   │
│ │ Output Format: (○ PDF  ○ HTML  ○ Both)      │   │
│ │ Scope:         (○ All  ○ Section ▼  ○ Unit) │   │
│ │ ☐ Show Bleeds (Print margins)                │   │
│ │ ☐ Debug Mode (Extra logging)                 │   │
│ │ ☐ Validate Only (Skip rendering)            │   │
│ └──────────────────────────────────────────────┘   │
│                                                      │
│ [Render] [Preview HTML] [Cancel]                   │
│                                                      │
│ Validation Report:                                   │
│ ┌──────────────────────────────────────────────┐   │
│ │ ✓ All YAML files valid                       │   │
│ │ ✓ All CSV files found                        │   │
│ │ ✓ All templates exist                        │   │
│ │ ⚠ Unit 1234: No members defined             │   │
│ │ ⚠ Craft officers heading: Will be empty     │   │
│ └──────────────────────────────────────────────┘   │
│                                                      │
│ Rendering Progress:                                  │
│ ┌──────────────────────────────────────────────┐   │
│ │ [████████░░░░░░░░░░░░░░░░░░░░░░░░] 35%      │   │
│ │ Currently: Rendering craft_units section...  │   │
│ └──────────────────────────────────────────────┘   │
│                                                      │
│ Output:                                              │
│ Size: 17,255 KB | Time: 2m 34s                     │
│ [Download PDF] [View HTML] [Open Folder]           │
└──────────────────────────────────────────────────────┘
```

**Operations:**
- ✅ Select output format and scope
- ✅ Run pre-render validation
- ✅ Display validation warnings/errors
- ✅ Render with progress indicator
- ✅ Download output files
- ✅ View HTML preview in browser
- ✅ Keep render history/versions

**Data Flow:**
```
UI Selection → RenderingViewModel → RendererController.RenderDocument()
→ ValidationService.Validate() + SchemaPdfRenderer.RenderAsync()
→ Output file storage + Response with download link
```

---

### 5.5 Change Tracking & Backup

**Current State:** Manual file backups  
**Desired State:** Version control with rollback

#### UI Features:
```
┌─ Version History ────────────────────────────────────┐
│                                                      │
│ Recent Changes:                                      │
│ ┌──────────────────────────────────────────────┐   │
│ │ Time       │ File                    │ Change │   │
│ ├────────────────────────────────────────────┤   │
│ │ 2026-05-24 │ craft_data_source.yaml  │ Edit  │ ↶ │
│ │ 2026-05-24 │ membership_v1.7.csv     │ Add 3 │ ↶ │
│ │ 2026-05-23 │ master_v1.yaml          │ Add   │ ↶ │
│ │ 2026-05-23 │ craft_officers_v1.7.csv│ Edit  │ ↶ │
│ └──────────────────────────────────────────────┘   │
│                                                      │
│ [Restore Selected] [View Diff] [Export Backup]     │
└──────────────────────────────────────────────────────┘
```

**Operations:**
- ✅ Track all file changes with timestamp
- ✅ Store change metadata (user, timestamp, file, action)
- ✅ Rollback to previous version
- ✅ Compare versions (diff view)
- ✅ Export full backup

**Data Flow:**
```
Any File Change → ChangeTrackingService.LogChange() → Database/File
```

---

## 6. Technical Implementation Details

### 6.1 Database Schema (if using SQL)

```sql
-- Document versions
CREATE TABLE DocumentVersions (
    VersionId INT PRIMARY KEY IDENTITY,
    VersionNumber INT,
    MasterContent NVARCHAR(MAX),
    CreatedAt DATETIME,
    CreatedBy NVARCHAR(255),
    ChangeDescription NVARCHAR(500)
);

-- Data source versions
CREATE TABLE DataSourceVersions (
    VersionId INT PRIMARY KEY IDENTITY,
    Degree NVARCHAR(50),
    DataSourceContent NVARCHAR(MAX),
    CreatedAt DATETIME,
    CreatedBy NVARCHAR(255)
);

-- CSV data audit trail
CREATE TABLE CsvChanges (
    ChangeId INT PRIMARY KEY IDENTITY,
    CsvFileName NVARCHAR(255),
    ChangeType NVARCHAR(20), -- 'INSERT', 'UPDATE', 'DELETE'
    RowData NVARCHAR(MAX),
    CreatedAt DATETIME,
    CreatedBy NVARCHAR(255)
);

-- Rendering history
CREATE TABLE RenderJobs (
    JobId INT PRIMARY KEY IDENTITY,
    RenderOptions NVARCHAR(MAX),
    OutputPath NVARCHAR(500),
    Status NVARCHAR(20), -- 'PENDING', 'IN_PROGRESS', 'SUCCESS', 'FAILED'
    ErrorMessage NVARCHAR(MAX),
    Duration INT, -- milliseconds
    OutputSize INT, -- bytes
    CreatedAt DATETIME,
    CreatedBy NVARCHAR(255)
);
```

### 6.2 API Endpoint Structure

```
POST   /api/document/structure          - Create/update master_v1.yaml
GET    /api/document/structure          - Retrieve current structure
DELETE /api/document/structure/{id}     - Delete section

POST   /api/datasource/{degree}         - Update data source YAML
GET    /api/datasource/{degree}         - Retrieve data source config
GET    /api/datasource/list             - List all available degrees

POST   /api/csv/{tableName}             - Upload/update CSV data
GET    /api/csv/{tableName}             - Retrieve CSV as JSON
POST   /api/csv/{tableName}/rows        - Add/edit individual rows
DELETE /api/csv/{tableName}/rows/{id}   - Delete row

POST   /api/render                      - Trigger rendering job
GET    /api/render/{jobId}              - Get render job status
GET    /api/render/{jobId}/download     - Download output file

GET    /api/validation/preview          - Run validation only
GET    /api/history/changes             - Get change log
POST   /api/history/restore/{versionId} - Rollback to version
```

### 6.3 File Locking Strategy

**Problem:** Multiple users editing same files

**Solution:** File-based locking with queue
```csharp
public class FileLockService
{
    // Lock file: {filename}.lock
    // Contains: { userId, timestamp, lockToken }
    
    public async Task<bool> TryLockFileAsync(string filePath, string userId)
    {
        var lockFile = filePath + ".lock";
        if (File.Exists(lockFile))
        {
            var lockData = JsonConvert.DeserializeObject<FileLock>(
                File.ReadAllText(lockFile)
            );
            
            // Expire lock after 30 minutes
            if (DateTime.UtcNow.Subtract(lockData.AcquiredAt) > TimeSpan.FromMinutes(30))
            {
                File.Delete(lockFile);
            }
            else
            {
                return false; // File locked by another user
            }
        }
        
        var lockContent = JsonConvert.SerializeObject(new FileLock
        {
            UserId = userId,
            AcquiredAt = DateTime.UtcNow,
            Token = Guid.NewGuid().ToString()
        });
        
        File.WriteAllText(lockFile, lockContent);
        return true;
    }
    
    public async Task ReleaseLockAsync(string filePath)
    {
        File.Delete(filePath + ".lock");
    }
}
```

---

## 7. Integration with Existing Renderer

### 7.1 Service Wrapper

```csharp
public interface IDocumentRenderer
{
    Task<RenderResult> RenderDocumentAsync(
        RenderOptions options,
        CancellationToken cancellationToken
    );
}

public class DocumentRendererService : IDocumentRenderer
{
    private readonly SchemaPdfRenderer _renderer;
    private readonly ILogger<DocumentRendererService> _logger;
    
    public async Task<RenderResult> RenderDocumentAsync(
        RenderOptions options, 
        CancellationToken cancellationToken
    )
    {
        // Validate before rendering
        var validation = await _validator.ValidateAsync(options);
        if (!validation.IsValid)
        {
            return new RenderResult 
            { 
                Success = false,
                Errors = validation.Errors 
            };
        }
        
        try
        {
            var startTime = DateTime.UtcNow;
            
            // Load document structure
            var mapping = await _layoutLoader.LoadDocumentLayoutAsync(
                options.TemplateName
            );
            
            // Filter sections if needed
            var sections = options.SectionId != null
                ? mapping.Sections.Where(s => s.SectionId == options.SectionId).ToList()
                : mapping.Sections;
            
            // Load data
            var units = await _dataLoader.LoadUnitsWithDataAsync(
                options.TemplateName
            );
            
            // Render
            var output = new StringBuilder();
            await _renderer.RenderAllSectionsAsync(
                mapping,
                sections,
                units,
                output
            );
            
            // Save output
            var outputPath = Path.Combine(
                _config.OutputPath,
                $"master_{options.TemplateName}_{DateTime.UtcNow:yyyyMMdd_HHmmss}.html"
            );
            
            File.WriteAllText(outputPath, output.ToString());
            
            // If PDF requested, convert
            byte[] pdfBytes = null;
            if (options.OutputFormat == OutputFormat.Pdf || 
                options.OutputFormat == OutputFormat.Both)
            {
                pdfBytes = await _renderer.ConvertHtmlToPdfAsync(output.ToString());
            }
            
            return new RenderResult
            {
                Success = true,
                HtmlPath = outputPath,
                PdfBytes = pdfBytes,
                Duration = DateTime.UtcNow.Subtract(startTime),
                Size = new FileInfo(outputPath).Length
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Rendering failed");
            return new RenderResult
            {
                Success = false,
                Errors = new[] { ex.Message }
            };
        }
    }
}
```

---

## 8. Workflow Scenarios

### 8.1 Scenario 1: Update Officer Information

```
User: Lodge secretary needs to update officer names for annual changeover

Flow:
1. Open UI → Select "Craft Provincial Officers" data source
2. Edit metadata: Change "Provincial Officers 2025" to "Provincial Officers 2026"
3. Click "Officers" tab
4. Edit grid: Update Master name, DPGM name, etc.
5. Add new District Heads entries
6. Click [Save to File]
7. UI shows: "Changes saved. Ready to render."
8. Click [Render] → Validate → Output ready
9. Download PDF
```

### 8.2 Scenario 2: Add New Section

```
User: Manager wants to add a new "Annual Report" section

Flow:
1. Open UI → Document Structure tab
2. Click [+ New Section]
3. Fill form:
   - Section ID: annual_report
   - Type: static
   - Template: annual-report.html (already exists)
4. Click [Save]
5. UI shows section added after craft_toc
6. Validate: ✓ Template exists
7. Click [Render] → Confirm new section renders
```

### 8.3 Scenario 3: Bulk Import Member Data

```
User: Data entry has created new members spreadsheet

Flow:
1. Open UI → CSV Data tab → membership_v1.7.csv
2. Click [Import CSV]
3. Select file: new_members_2026.csv
4. Mode: (○ Replace All  ● Append  ○ Merge by key)
5. Click [Preview]
6. UI shows: "Will add 47 new members"
7. Click [Import]
8. CSV updated, validation runs
9. Ready to render with new data
```

---

## 9. Validation Rules

### 9.1 Pre-Render Validation

```csharp
public class DocumentValidator
{
    public ValidationResult Validate(DocumentState state)
    {
        var errors = new List<string>();
        var warnings = new List<string>();
        
        // 1. Check YAML files exist and are valid
        if (!File.Exists(state.MasterYamlPath))
            errors.Add("master_v1.yaml not found");
        
        // 2. Check data source files match references
        foreach (var section in state.Sections)
        {
            if (section.DataMapping != null)
            {
                var dataSourcePath = Path.Combine(
                    state.DocumentRoot, 
                    section.DataMapping
                );
                if (!File.Exists(dataSourcePath))
                    errors.Add($"Data source not found: {section.DataMapping}");
            }
        }
        
        // 3. Check templates exist
        foreach (var section in state.Sections)
        {
            var templatePath = Path.Combine(
                state.TemplateRoot,
                section.Template
            );
            if (!File.Exists(templatePath))
                errors.Add($"Template not found: {section.Template}");
        }
        
        // 4. Check CSV files exist and have required columns
        foreach (var dataSource in state.DataSources)
        {
            foreach (var csvSection in dataSource.Sections)
            {
                if (!File.Exists(csvSection.Source))
                    errors.Add($"CSV not found: {csvSection.Source}");
                else
                {
                    // Validate headers
                    var headers = GetCsvHeaders(csvSection.Source);
                    var required = csvSection.Fields
                        .Select(f => f.CsvColumn)
                        .ToList();
                    
                    var missing = required.Except(headers).ToList();
                    if (missing.Any())
                        warnings.Add(
                            $"{Path.GetFileName(csvSection.Source)}: " +
                            $"Missing columns: {string.Join(", ", missing)}"
                        );
                }
            }
        }
        
        // 5. Check for duplicate entries (optional)
        // 6. Check for orphaned references
        
        return new ValidationResult
        {
            IsValid = !errors.Any(),
            Errors = errors,
            Warnings = warnings
        };
    }
}
```

---

## 10. Implementation Roadmap

### Phase 1: Core Framework (Weeks 1-2)
- [ ] Create ASP.NET Core WebAPI project
- [ ] Create Blazor UI project
- [ ] Implement file service layer (YAML, CSV read/write)
- [ ] Create validation service
- [ ] Integrate existing SchemaPdfRenderer

### Phase 2: Document Manager (Weeks 3-4)
- [ ] Build section list component
- [ ] Build section editor component
- [ ] Implement drag-drop reordering
- [ ] Add section duplication from templates
- [ ] Validate section references

### Phase 3: Data Source Manager (Weeks 5-6)
- [ ] Build degree selector
- [ ] Build metadata editor forms
- [ ] Build field mapping UI
- [ ] Implement officer group management
- [ ] Add raw YAML viewer

### Phase 4: CSV Editor (Weeks 7-8)
- [ ] Build DataGrid component
- [ ] Implement add/edit/delete row modals
- [ ] Add CSV import/export
- [ ] Implement data validation per column
- [ ] Add duplicate detection

### Phase 5: Rendering & Preview (Week 9)
- [ ] Build render options UI
- [ ] Implement progress tracking
- [ ] Add validation reporter
- [ ] Build output file management
- [ ] Add HTML preview viewer

### Phase 6: Change Tracking & Backup (Week 10)
- [ ] Implement file locking
- [ ] Build change log UI
- [ ] Add version rollback
- [ ] Create backup/restore functions
- [ ] Add audit trail logging

### Phase 7: Testing & Deployment (Week 11-12)
- [ ] Unit tests for services
- [ ] Integration tests for workflows
- [ ] UI testing (cross-browser if web)
- [ ] Performance testing
- [ ] Deploy to production

---

## 11. Security Considerations

### 11.1 File Access Control
```csharp
public class FileAccessControl
{
    public bool CanUserAccessFile(User user, string filePath)
    {
        // Role-based: Admins can edit YAML, users can only edit CSV data
        if (filePath.EndsWith(".yaml"))
            return user.Role == UserRole.Administrator;
        
        if (filePath.EndsWith(".csv"))
            return user.Role >= UserRole.DataEditor;
        
        return false;
    }
}
```

### 11.2 User Roles
- **Viewer**: Read-only access (view documents, download PDFs)
- **DataEditor**: Can modify CSV data only
- **ConfigEditor**: Can edit YAML and CSV
- **Administrator**: Full access + user management

### 11.3 Input Validation
- Validate YAML structure before saving
- Sanitize CSV import (max rows, cell length limits)
- Prevent directory traversal in file paths
- Log all modifications for audit trail

---

## 12. Estimated Effort & Resources

| Component | Effort | Complexity |
|-----------|--------|-----------|
| Backend API | 60 hours | Medium |
| Blazor UI | 80 hours | Medium-High |
| File Services | 30 hours | Low |
| Validation Engine | 40 hours | Medium |
| Rendering Integration | 20 hours | Low |
| Change Tracking | 25 hours | Medium |
| Testing & QA | 50 hours | Medium |
| **Total** | **305 hours** | **~7-8 weeks** |

**Team:** 1-2 senior developers + 1 QA

---

## 13. Alternative Lightweight Approach

If full UI is too much overhead, consider:

### 13.1 VS Code Extension
- Leverage existing code editor workflow
- Add syntax highlighting and validation for YAML/CSV
- Add right-click menu to render sections
- Use VS Code's built-in preview panel

**Effort:** ~40 hours  
**Tooling:** VS Code Extension API (TypeScript)

### 13.2 PowerShell Script GUI
- Build simple WinForms wrapper around existing CLI
- Dropdown to select template and options
- Button to trigger render
- Show output location

**Effort:** ~15 hours  
**Tooling:** WinForms (C#)

---

## 14. Recommendation Summary

**Best Choice: ASP.NET Core Web UI (Blazor Server)**

**Why:**
1. ✅ Aligns with existing .NET 8.0 stack
2. ✅ Reuses all existing C# code (no duplication)
3. ✅ Supports multi-user access (future-proof)
4. ✅ Easier to maintain and extend
5. ✅ Can be deployed on-premises or cloud
6. ✅ Responsive design works on tablets
7. ✅ Built-in component model (Blazor)

**Architecture:**
- Blazor Server for UI (C# code runs server-side)
- ASP.NET Core WebAPI for backend logic
- File-based storage (YAML/CSV) with JSON for metadata
- Change tracking via custom service (no external VCS required)
- Integrated SchemaPdfRenderer for output generation

**Time to MVP:** 5-6 weeks  
**Full Feature Set:** 8-10 weeks

---

## 15. Next Steps

1. **Evaluate:** Review this analysis with stakeholders
2. **Decide:** Choose web (Blazor) vs desktop (WPF) approach
3. **Design:** Create detailed wireframes for each UI screen
4. **Prototype:** Build proof-of-concept for document structure editor
5. **Scope:** Define MVP features vs future enhancements
6. **Plan:** Create detailed sprint plan and assign resources

---

## Appendix: Technology Decision Matrix

| Criteria | Web (Blazor) | Desktop (WPF) | Lightweight (VS Code) |
|----------|--------------|---------------|-----------------------|
| Cross-Platform | ✅ Yes | ❌ Windows | ✅ Yes |
| Multi-User | ✅ Yes | ❌ No | ⚠ Complex |
| Offline Use | ❌ No | ✅ Yes | ✅ Yes |
| Collaboration | ✅ Yes | ❌ No | ⚠ Limited |
| Deployment Ease | ✅ Easy | ⚠ Moderate | ✅ Easy |
| Dev Time | ⚠ Moderate | ⚠ Moderate | ✅ Fast |
| Maintenance | ✅ Easy | ⚠ Moderate | ✅ Easy |
| User Experience | ⚠ Good | ✅ Excellent | ⚠ Good |
| **Overall Score** | **8/10** | **6/10** | **5/10** |

---

**Document Version:** 1.0  
**Last Updated:** May 24, 2026  
**Author:** Architecture Analysis
