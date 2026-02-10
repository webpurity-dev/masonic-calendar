# Quick Commands Reference

## Build
```bash
cd src/MasonicCalendar.Console
dotnet build
```

## Run - Document Renderer

### Generate HTML Document
```bash
dotnet run -- -template master_v1 -output HTML
```
Output: `output/master_v1-complete.html` (10KB)

### Generate PDF Document  
```bash
dotnet run -- -template master_v1 -output PDF
```
Output: `output/master_v1-complete.pdf` (5KB)

### Show Help
```bash
dotnet run
```

## Run - Unit Exporter (Original Functionality)

### HTML Output
```bash
dotnet run -- --output html
```

### PDF Output  
```bash
dotnet run -- --output pdf --pagesize A6
```

### With Meetings Calendar
```bash
dotnet run -- --meetings-calendar --output html
```

## Files to Check

After running the document renderer:

```bash
# View generated HTML in browser
start output/master_v1-complete.html

# View generated PDF
start output/master_v1-complete.pdf
```

## Architecture Files

- `src/MasonicCalendar.Core/Services/DocumentRenderer.cs` - Main engine
- `src/MasonicCalendar.Core/Services/DocumentLayoutLoader.cs` - Config loader
- `src/MasonicCalendar.Console/Program.cs` - CLI entry point
- `document/master_v1.yaml` - Master template
- `document/sections/section-craft.yaml` - Craft section config
- `document/sections/section-royal-arch.yaml` - Royal Arch section config

## Data Files

All CSV data is automatically loaded from:
- `data/sample-units.csv`
- `data/sample-unit-members.csv`
- `data/sample-unit-meetings.csv`
- `data/sample-unit-officers.csv`
- `data/sample-officers.csv`
- `data/sample-unit-locations.csv`
- `data/sample-unit-honorary.csv`
- `data/sample-unit-pmi.csv`

## Testing

### Quick Test
```bash
# Build and run in one command
dotnet build && dotnet run -- -template master_v1 -output HTML
```

### Verify Output
```bash
# Check file was created
ls -la output/master_v1-complete.*

# Check file size
Get-Item output/master_v1-complete.html | Select-Object Length
```

## Troubleshooting

### Build fails
```bash
# Clean and rebuild
dotnet clean
dotnet build
```

### Template not found
- Check `document/master_v1.yaml` exists
- Verify spelling matches parameter

### No units rendered
- Check CSV files exist in `data/` directory
- Verify `unit_type` in section config matches CSV data
- Check member units have matching UnitId in CSV

### Parameter errors
```bash
# Run with no parameters to see help
dotnet run
```

Expected output:
```
Usage:
  dotnet run -- -template <name> -output <format>

Parameters:
  -template   Master template name (e.g., master_v1)
  -output     Output format: PDF or HTML

Example:
  dotnet run -- -template master_v1 -output PDF
```
