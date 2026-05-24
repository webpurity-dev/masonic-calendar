namespace MasonicCalendar.Core.Services.Renderers.SectionRenderers;

using CsvHelper;
using MasonicCalendar.Core.Domain;
using MasonicCalendar.Core.Loaders;
using Scriban;
using System.Globalization;
using System.Text;

/// <summary>
/// Renders succession list sections with data from CSV files and YAML table definitions.
/// </summary>
public class SuccessionListSectionRenderer(string templateRoot, SchemaDataLoader? dataLoader, bool debugMode)
    : SectionRenderer(templateRoot, dataLoader, debugMode)
{
    public override async Task RenderAsync(
        SectionConfig section,
        int sectionIndex,
        List<SectionConfig> allSections,
        string masterTemplateKey,
        List<SchemaUnit> units,
        StringBuilder output)
    {
        if (string.IsNullOrWhiteSpace(section.Template))
            return;

        var template = LoadTemplate(section.Template);
        if (template == null)
            return;

        if (DebugMode)
            Console.WriteLine($"  ~ SuccessionListSectionRenderer: Rendering '{section.SectionId}'");

        var documentRoot = Path.GetDirectoryName(TemplateRoot) ?? ".";
        var metadata = await LoadSuccessionListDataAsync(section.DataMapping, documentRoot);

        // Build Scriban model
        var model = new Dictionary<string, object?>
        {
            { "tables", metadata["tables"] }
        };

        var html = template.Render(model);
        WrapWithPageBreakAndAnchor(output, $"section_{section.SectionId}", html, sectionIndex, section.ResetPageCounter, section.OverrideBreakBefore);

        if (DebugMode)
            Console.WriteLine($"  ✓ SuccessionListSectionRenderer: Completed '{section.SectionId}'");
    }

    private async Task<Dictionary<string, object?>> LoadSuccessionListDataAsync(
        string? dataMapping,
        string documentRoot
    )
    {
        var data = new Dictionary<string, object?>();

        if (string.IsNullOrWhiteSpace(dataMapping))
        {
            data["tables"] = new List<object>();
            return data;
        }

        // Load data source mapping from YAML
        var layoutLoader = new DocumentLayoutLoader(documentRoot);
        var mappingResult = layoutLoader.LoadDataSourceMapping(dataMapping);
        if (!mappingResult.Success)
        {
            if (DebugMode)
                Console.WriteLine($"    ❌ Failed to load succession mapping: {mappingResult.Error}");
            data["tables"] = new List<object>();
            return data;
        }

        var mapping = mappingResult.Data;
        if (mapping?.SuccessionList == null)
        {
            if (DebugMode)
                Console.WriteLine($"    ❌ SuccessionList config not found in mapping");
            data["tables"] = new List<object>();
            return data;
        }

        var config = mapping.SuccessionList;
        var tables = new List<object>();

        if (DebugMode)
            Console.WriteLine($"    ✓ Loaded succession list metadata with {config.Tables?.Count ?? 0} tables");

        // Process each table definition
        if (config.Tables != null)
        {
            foreach (var tableConfig in config.Tables)
            {
                if (string.IsNullOrWhiteSpace(tableConfig.Source))
                    continue;

                var csvPath = Path.Combine(documentRoot, tableConfig.Source);
                if (!File.Exists(csvPath))
                {
                    if (DebugMode)
                        Console.WriteLine($"    ⚠️ CSV file not found: {csvPath}");
                    continue;
                }

                // Read CSV data
                var rows = ReadCsvAsRecords(csvPath);

                var table = new Dictionary<string, object?>
                {
                    { "title", tableConfig.Title ?? "" },
                    { "font_size", tableConfig.FontSize ?? "8pt" },
                    { "columns", tableConfig.Columns ?? new List<TableColumn>() },
                    { "rows", rows }
                };

                tables.Add(table);
            }
        }

        data["tables"] = tables;
        return data;
    }

    private List<Dictionary<string, object?>> ReadCsvAsRecords(string csvPath)
    {
        var records = new List<Dictionary<string, object?>>();

        using var reader = new StreamReader(csvPath);
        using var csv = new CsvReader(reader, CultureInfo.InvariantCulture);

        csv.Read();
        csv.ReadHeader();

        while (csv.Read())
        {
            var record = new Dictionary<string, object?>();
            foreach (var header in csv.HeaderRecord ?? Array.Empty<string>())
            {
                record[header] = csv.GetField(header);
            }
            records.Add(record);
        }

        return records;
    }
}

