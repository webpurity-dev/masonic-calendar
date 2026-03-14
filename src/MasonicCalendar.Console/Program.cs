using MasonicCalendar.Core.Renderers;
using MasonicCalendar.Core.Loaders;
using System.Linq;

// Get the project root directory
var projectRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));
var documentRoot = Path.Combine(projectRoot, "document");
var dataPath = Path.Combine(documentRoot, "data");
var outputDir = Path.Combine(projectRoot, "output");

if (!Directory.Exists(outputDir))
    Directory.CreateDirectory(outputDir);

// Check for -template and -output parameters for document rendering
string? templateName = null;
string? documentOutputFormat = null;
string? sectionId = null;
string? unitNumber = null;
bool debugMode = false;

var templateIndex = Array.IndexOf(args, "-template");
if (templateIndex != -1 && templateIndex + 1 < args.Length)
{
    templateName = args[templateIndex + 1];
}

var outputIndex = Array.IndexOf(args, "-output");
if (outputIndex != -1 && outputIndex + 1 < args.Length)
{
    documentOutputFormat = args[outputIndex + 1].ToUpper();
}

var sectionIndex = Array.IndexOf(args, "-section");
if (sectionIndex != -1 && sectionIndex + 1 < args.Length)
{
    sectionId = args[sectionIndex + 1];
}

var unitIndex = Array.IndexOf(args, "-unit");
if (unitIndex != -1 && unitIndex + 1 < args.Length)
{
    unitNumber = args[unitIndex + 1];
}

// Check for debug flag
debugMode = Array.IndexOf(args, "-debug") != -1;

// Check for showbleeds flag
bool showBleeds = Array.IndexOf(args, "-showbleeds") != -1;

// Document renderer mode
if (!string.IsNullOrWhiteSpace(templateName) && !string.IsNullOrWhiteSpace(documentOutputFormat))
{
    try
    {
        Console.WriteLine("📄 Masonic Calendar - Document Renderer");
        Console.WriteLine("=" + new string('=', 50));
        Console.WriteLine($"Template: {templateName}");
        Console.WriteLine($"Output:   {documentOutputFormat}");
        if (!string.IsNullOrWhiteSpace(sectionId))
        {
            Console.WriteLine($"Section:  {sectionId}");
        }
        if (!string.IsNullOrWhiteSpace(unitNumber))
        {
            Console.WriteLine($"Unit:     {unitNumber}");
        }
        Console.WriteLine();

        // Load data using schema
        var schemaLoader = new SchemaDataLoader(new DocumentLayoutLoader(documentRoot), dataPath);
        var schemaResult = await schemaLoader.LoadUnitsWithDataAsync(templateName);
        
        if (!schemaResult.Success)
        {
            Console.WriteLine($"❌ Error loading data: {schemaResult.Error}");
            return 1;
        }

        Console.WriteLine($"✓ Loaded {schemaResult.Data!.Count} units from CSV files");
        
        // Filter by unit number if specified
        var unitsToRender = schemaResult.Data;
        if (!string.IsNullOrWhiteSpace(unitNumber))
        {
            if (int.TryParse(unitNumber, out int unitNumberInt))
            {
                unitsToRender = unitsToRender.Where(u => u.Number == unitNumberInt).ToList();
                if (unitsToRender.Count == 0)
                {
                    Console.WriteLine($"❌ Error: Unit '{unitNumber}' not found in data");
                    return 1;
                }
                Console.WriteLine($"✓ Filtered to {unitsToRender.Count} unit(s) matching '{unitNumber}'");
            }
            else
            {
                Console.WriteLine($"❌ Error: Unit number '{unitNumber}' is not a valid integer");
                return 1;
            }
        }
        Console.WriteLine();
        
        // Load and display available sections and templates
        var layoutLoader = new DocumentLayoutLoader(documentRoot);
        var layoutResult = layoutLoader.LoadMasterLayout(templateName);
        if (layoutResult.Success && layoutResult.Data?.Sections?.Count > 0)
        {
            Console.WriteLine("Available Sections:");
            foreach (var section in layoutResult.Data.Sections)
            {
                if (!string.IsNullOrWhiteSpace(section.SectionId))
                {
                    var sectionType = (section.Type?.Equals("static", StringComparison.OrdinalIgnoreCase) ?? false) ? "(static)" : "(data-driven)";
                    Console.WriteLine($"  - {section.SectionId,-12} {sectionType,-15} → {section.Template}");
                }
            }
            Console.WriteLine();
        }

        // Render using Scriban template
        var renderer = new SchemaPdfRenderer(layoutLoader, schemaLoader, documentRoot, debugMode, showBleeds);
        
        // When rendering a specific unit, automatically render only the unit's section
        var targetSectionId = sectionId ?? null;  // null means render all sections
        
        if (!string.IsNullOrWhiteSpace(unitNumber) && string.IsNullOrWhiteSpace(sectionId))
        {
            // When unit is specified, render craft units by default
            // (For Royal Arch units, the user can specify -section royalarch_units)
            targetSectionId = "craft_units";
            Console.WriteLine($"📄 Rendering unit {unitNumber} from craft section");
            Console.WriteLine($"   (To render from royal arch section: add '-section royalarch_units')");
        }
        else if (targetSectionId != null)
        {
            Console.WriteLine($"📄 Rendering section: {targetSectionId}");
        }
        else
        {
            Console.WriteLine($"📄 Rendering all sections");
        }
        
        var renderResult = await renderer.RenderAsync(unitsToRender, templateName, targetSectionId, documentOutputFormat);
        
        if (!renderResult.Success)
        {
            Console.WriteLine($"❌ Error rendering: {renderResult.Error}");
            return 1;
        }

        // Save output file
        var fileExtension = documentOutputFormat.ToLower() == "pdf" ? "pdf" : "html";
        var sectionPart = targetSectionId ?? "all-sections";
        var unitPart = string.IsNullOrWhiteSpace(unitNumber) ? "" : $"-unit{unitNumber}";
        var outputPath = Path.Combine(outputDir, $"{templateName}-{sectionPart}{unitPart}.{fileExtension}");
        
        File.WriteAllBytes(outputPath, renderResult.Data!);

        var fileSize = new FileInfo(outputPath).Length;
        Console.WriteLine($"✅ Output generated successfully!");
        Console.WriteLine($"   Path: {outputPath}");
        Console.WriteLine($"   Size: {fileSize / 1024.0:F1}KB");
        Console.WriteLine();
        
        Console.WriteLine("✨ Document rendering completed successfully!");
        return 0;
    }
    catch (Exception ex)
    {
        Console.WriteLine($"❌ Error: {ex.Message}");
        return 1;
    }
}

// Show help if using document renderer mode but missing parameters
if (!string.IsNullOrWhiteSpace(templateName) || !string.IsNullOrWhiteSpace(documentOutputFormat))
{
    Console.WriteLine("📄 Masonic Calendar - Document Renderer");
    Console.WriteLine("=" + new string('=', 50));
    Console.WriteLine("\nUsage:");
    Console.WriteLine("  dotnet run -- -template <name> -output <format> [-section <id>] [-unit <number>] [-showbleeds] [-debug]");
    Console.WriteLine("\nParameters:");
    Console.WriteLine("  -template   Master template name (e.g., master_v1)");
    Console.WriteLine("  -output     Output format: PDF or HTML");
    Console.WriteLine("  -section    Section ID to render (optional, default: all sections)");
    Console.WriteLine("  -unit       Unit number to render (optional, default: all units)");
    Console.WriteLine("  -showbleeds Show page bleeds with border (optional, for debugging layout)");
    Console.WriteLine("  -debug      Enable debug output and HTML file generation (optional)");
    Console.WriteLine("\nAvailable Section IDs (from master_v1.yaml):");
    Console.WriteLine("  cover       Cover page");
    Console.WriteLine("  craft       Craft Freemasonry");
    Console.WriteLine("  royalarch   Royal Arch Chapters");
    Console.WriteLine("\nExamples:");
    Console.WriteLine("  dotnet run -- -template master_v1 -output PDF                     (renders all units)");
    Console.WriteLine("  dotnet run -- -template master_v1 -output PDF -unit 3366          (renders only unit 3366)");
    Console.WriteLine("  dotnet run -- -template master_v1 -output HTML -section craft     (renders only craft section)");
    Console.WriteLine("  dotnet run -- -template master_v1 -output HTML -unit 3366 -debug  (renders unit 3366 with debug)");
    return 1;
}

// No valid parameters provided
Console.WriteLine("📄 Masonic Calendar - Document Renderer");
Console.WriteLine("=" + new string('=', 50));
Console.WriteLine("\nUsage:");
Console.WriteLine("  dotnet run -- -template <name> -output <format> [-section <id>] [-unit <number>] [-showbleeds] [-debug]");
Console.WriteLine("\nExample (render all sections):");
Console.WriteLine("  dotnet run -- -template master_v1 -output PDF");
Console.WriteLine("\nExample (render specific unit):");
Console.WriteLine("  dotnet run -- -template master_v1 -output HTML -unit 3366");
Console.WriteLine("\nExample (render specific section with bleeds visible):");
Console.WriteLine("  dotnet run -- -template master_v1 -output HTML -section craft -showbleeds");
return 0;
