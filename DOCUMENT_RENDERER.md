# Document Renderer - Master Template System

## Overview

The new `DocumentRenderer` system provides a simple, configuration-driven approach to generating documents in PDF or HTML format from a master YAML template.

## Usage

```bash
dotnet run -- -template <template_name> -output <format>
```

### Parameters

- **`-template`** (required): The name of the master YAML template file (without `.yaml` extension)
  - Example: `master_v1` → reads `document/master_v1.yaml`
  
- **`-output`** (required): Output format
  - `PDF` - generates a PDF file
  - `HTML` - generates an HTML file

### Examples

```bash
# Generate HTML from master_v1 template
dotnet run -- -template master_v1 -output HTML

# Generate PDF from master_v1 template
dotnet run -- -template master_v1 -output PDF
```

## Architecture

### Master Template Structure

The master template (`document/master_v1.yaml`) defines:

1. **Document Info**: Title, version, copyright, format, orientation
2. **Global Styling**: Fonts, colors, margins
3. **Page Numbering**: Configuration for page numbers
4. **Sections**: List of document sections

```yaml
document:
  title: "Masonic Calendar & Directory"
  version: "1.0"
  copyright: "© 2026 Masonic Publishing Co."

global_styling:
  fonts:
    default_family: "Helvetica"
    sizes:
      large_heading: 18pt
      body: 10pt

sections:
  - section_id: "cover"
    type: "static"
    template: "cover-page.html"
  
  - section_id: "craft"
    include: "section-craft.yaml"
```

### Section Configuration

Sections can either be:

1. **Static Sections**: Use an HTML template
   ```yaml
   - section_id: "cover"
     type: "static"
     template: "cover-page.html"
   ```

2. **Data-Driven Sections**: Include configuration from separate YAML file
   ```yaml
   - section_id: "craft"
     include: "section-craft.yaml"
   ```

Included section files (e.g., `document/sections/section-craft.yaml`) define:
- `section_name`: Display name for the section
- `unit_type`: Filter units by type (e.g., "Craft", "RoyalArch")
- `pages`: Page configurations for rendering
- `styling`: Section-specific styling

Example section file:
```yaml
section_name: "Craft Freemasonry"
unit_type: "Craft"

pages:
  - page_type: "toc"
    title: "Craft Table of Contents"
  
  - page_type: "content"
    title: "Craft Units"
    repeat_for: "each_unit"
```

## Data Loading

The `DocumentRenderer` automatically loads data from all available CSV files in the `data/` directory:

- `sample-units.csv` - Units and basic info
- `sample-unit-members.csv` - Unit members
- `sample-unit-meetings.csv` - Meeting schedules
- `sample-unit-officers.csv` - Officer assignments
- `sample-officers.csv` - Officer definitions
- `sample-unit-locations.csv` - Location details
- `sample-unit-honorary.csv` - Honorary members
- `sample-unit-pmi.csv` - Joining Past Masters

All data is automatically merged and filtered based on the section configuration.

## File Locations

```
document/
  master_v1.yaml                    # Master template
  sections/
    section-craft.yaml              # Craft section config
    section-royal-arch.yaml         # Royal Arch section config
  templates/
    cover-page.html                 # Static page template
    unit-page.html                  # Unit detail template

output/
  master_v1-complete.html           # Generated HTML output
  master_v1-complete.pdf            # Generated PDF output
```

## Rendering Process

1. **Load Master Template**: Reads YAML and deserializes to `DocumentLayout`
2. **Load Sections**: For included sections, loads their YAML and merges properties
3. **Load Data**: Reads all CSV files and creates `DocumentData` container
4. **Render Output**: 
   - For HTML: Creates formatted HTML with sections and filtered units
   - For PDF: Creates text-based representation (can be extended with QuestPDF)
5. **Save File**: Writes output to `output/{template-name}-complete.{format}`

## Output

### HTML Output

The HTML renderer produces:
- Valid HTML5 with embedded CSS
- Responsive styling
- Sections grouped by unit type
- Detailed unit information with members list
- Clickable email links
- Print-friendly styling

### PDF Output

Currently provides a text-based PDF representation. Can be extended to use QuestPDF for:
- Proper page breaks
- Layout control
- Page numbering
- Advanced styling

## Classes and Interfaces

### DocumentRenderer
Main class for rendering documents.

**Methods:**
- `Render(templateName: string, outputFormat: string): Result<byte[]>`
  - Returns the rendered document as bytes
  - Handles both PDF and HTML output formats

**Private Methods:**
- `RenderHtml()` - Generates HTML output
- `RenderPdf()` - Generates PDF output (text-based)
- `LoadAllDataSources()` - Loads CSV data

### DocumentLayoutLoader
Loads and parses YAML configuration files.

**Methods:**
- `LoadMasterLayout(templateName: string): Result<DocumentLayout>`
  - Loads master template and includes sections
  - Returns deserialized layout configuration

### Configuration Classes

- `DocumentLayout` - Top-level document structure
- `DocumentInfo` - Document metadata
- `GlobalStyling` - Global font and color configuration
- `GlobalMargins` - Global margin definitions
- `PageNumbering` - Page numbering configuration
- `SectionConfig` - Section definition
- `PageConfig` - Page definition within a section
- `SectionStyling` - Section-specific styling

## Error Handling

All operations return `Result<T>` for proper error handling:

```csharp
var result = renderer.Render("master_v1", "HTML");
if (!result.Success)
{
    Console.WriteLine($"Error: {result.Error}");
    return 1;
}
```

## Next Steps / Enhancements

1. **QuestPDF Integration**: Replace text-based PDF with proper QuestPDF rendering
2. **Template System**: Implement HTML template rendering for sections
3. **Advanced Styling**: Support more granular styling configuration
4. **Multi-Page Units**: Render units across multiple pages
5. **Custom Filters**: Support more complex data filtering in sections
6. **Style Inheritance**: Implement cascading styles from global to section to page level
7. **TOC Generation**: Automatic table of contents generation
8. **Repeating Sections**: Support for data-driven page generation

## Dependencies

- **YamlDotNet** (13.7.1): YAML parsing
- **CsvHelper** (30.0.0): CSV data loading
- **QuestPDF** (optional): For advanced PDF generation

## Building and Testing

```bash
# Build
cd src/MasonicCalendar.Console
dotnet build

# Test HTML output
dotnet run -- -template master_v1 -output HTML

# Test PDF output
dotnet run -- -template master_v1 -output PDF

# View output
# - HTML: output/master_v1-complete.html
# - PDF: output/master_v1-complete.pdf
```
