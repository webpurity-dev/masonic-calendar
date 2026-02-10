# Document Renderer - Quick Start

## Simplest Usage

```bash
cd src/MasonicCalendar.Console
dotnet run -- -template master_v1 -output HTML
```

Output: `output/master_v1-complete.html` (11KB)

## Generate PDF

```bash
dotnet run -- -template master_v1 -output PDF
```

Output: `output/master_v1-complete.pdf` (5KB text-based)

## How It Works

1. Reads `document/master_v1.yaml` - master template
2. Loads included sections:
   - `document/sections/section-craft.yaml` 
   - `document/sections/section-royal-arch.yaml`
3. Loads all CSV data from `data/` directory
4. Renders filtered by unit type
5. Saves to `output/master_v1-complete.{html|pdf}`

## File Structure

```
master_v1.yaml
├─ document.title = "Masonic Calendar & Directory"
├─ sections[0] 
│  └─ section_id: "cover" (static section)
└─ sections[1]
   └─ section_id: "craft" (includes section-craft.yaml)
      ├─ section_name: "Craft Freemasonry"
      ├─ unit_type: "Craft"
      └─ pages: [toc, content]
```

## Output Includes

**HTML Output:**
- Valid HTML5 with CSS
- Cover page
- Craft section with TOC
- Craft lodges (137, 170, 386, 417, 472, 622, 665, 707, 708, 1037, etc.)
- Member lists for each lodge
- Royal Arch section (similar structure)

**PDF Output:**
- Text-based representation
- Same content as HTML
- Ready for enhancement with QuestPDF

## Key Classes

| Class | Purpose |
|-------|---------|
| `DocumentRenderer` | Main render engine |
| `DocumentLayoutLoader` | YAML config parser |
| `DocumentLayout` | Master template structure |
| `SectionConfig` | Section definition |
| `DocumentData` | Loaded CSV data |

## Adding New Sections

1. Create `document/sections/section-new.yaml`:
   ```yaml
   section_name: "New Section"
   unit_type: "SomeType"
   ```

2. Add to `master_v1.yaml`:
   ```yaml
   - section_id: "newsection"
     include: "section-new.yaml"
   ```

3. Run: `dotnet run -- -template master_v1 -output HTML`

## Troubleshooting

| Error | Solution |
|-------|----------|
| Template not found | Check template file exists in `document/` |
| No units rendered | Verify `unit_type` matches CSV data |
| Build fails | Run `dotnet build` to see errors |

## Next: Adding QuestPDF for Better PDFs

The PDF rendering is currently text-based. To add proper PDF layout:

1. Uncomment QuestPDF in `.csproj`
2. Implement proper `RenderPdf()` with QuestPDF layouts
3. Add page breaks, styling, images

Current PDF size: ~5KB (text)  
With QuestPDF: ~50-100KB (formatted)
