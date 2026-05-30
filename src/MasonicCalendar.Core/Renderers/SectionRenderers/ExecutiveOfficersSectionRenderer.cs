namespace MasonicCalendar.Core.Services.Renderers.SectionRenderers;

using CsvHelper;
using MasonicCalendar.Core.Domain;
using MasonicCalendar.Core.Loaders;
using Scriban;
using System.Globalization;
using System.Text;

/// <summary>
/// Renders executive officers sections with heads, deputy heads, and executive officers from YAML metadata and CSV.
/// Similar to ProvincialOfficersSectionRenderer but specialized for executive officers display.
/// </summary>
public class ExecutiveOfficersSectionRenderer(string templateRoot, SchemaDataLoader? dataLoader, bool debugMode)
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

        try
        {
            // Load YAML metadata and CSV officers
            var metadata = await LoadExecutiveOfficersDataAsync(section);

            if (DebugMode)
                Console.WriteLine($"  - Section '{section.SectionId}': Loaded executive officers metadata");

            // Build Scriban model - rename 'officers' to 'executive_officers' to distinguish from list_officers type
            var model = new Dictionary<string, object?>
            {
                { "heading1", metadata["heading1"] },
                { "heading2", metadata["heading2"] },
                { "website", metadata["website"] },
                { "crest", metadata["crest"] },
                { "heads", metadata["heads"] },
                { "deputy_heads", metadata["deputy_heads"] },
                { "executive_officers", metadata["executive_officers"] },
                { "override_break_before", section.OverrideBreakBefore }
            };

            var html = template.Render(model);
            WrapWithPageBreakAndAnchor(output, $"section_{section.SectionId}", html, sectionIndex, section.ResetPageCounter, section.OverrideBreakBefore);

            if (DebugMode)
                Console.WriteLine($"  ✓ Rendered executive officers section");
        }
        catch (Exception ex)
        {
            if (DebugMode)
                Console.WriteLine($"  ❌ Error rendering executive officers: {ex.Message}");
            throw;
        }
    }

    private async Task<Dictionary<string, object?>> LoadExecutiveOfficersDataAsync(SectionConfig section)
    {
        var data = new Dictionary<string, object?>();

        if (string.IsNullOrWhiteSpace(section.DataMapping))
            return data;

        // Get document root
        var documentRoot = Path.GetDirectoryName(TemplateRoot)?.TrimEnd(Path.DirectorySeparatorChar) 
            ?? TemplateRoot;

        // Load data source mapping from YAML
        var layoutLoader = new DocumentLayoutLoader(documentRoot);
        var mappingResult = layoutLoader.LoadDataSourceMapping(section.DataMapping);
        if (!mappingResult.Success)
        {
            if (DebugMode)
                Console.WriteLine($"    ❌ Failed to load mapping: {mappingResult.Error}");
            return data;
        }

        var mapping = mappingResult.Data;
        
        // v1.7: Use OrderExecutiveOfficers (order_ prefix for order-level data)
        var config = mapping?.OrderExecutiveOfficers;
        if (config == null)
        {
            if (DebugMode)
                Console.WriteLine($"    ❌ OrderExecutiveOfficers config not found in mapping");
            return data;
        }

        // v1.7: Load branding from order_summary (consolidated metadata)
        var orderSummary = mapping.OrderSummary;
        if (orderSummary != null)
        {
            data["heading1"] = orderSummary.Title ?? "";
            data["website"] = string.IsNullOrWhiteSpace(orderSummary.Website) ? null : orderSummary.Website;
            data["crest"] = orderSummary.Crest ?? "";
            data["heads"] = orderSummary.Heads ?? new List<OfficerGroup>();
            data["deputy_heads"] = orderSummary.DeputyHeads ?? new List<OfficerGroup>();
            if (DebugMode)
                Console.WriteLine($"    ✓ Loaded branding from order_summary");
        }
        else
        {
            // Fallback: load from executive_officers section itself (backward compatibility)
            data["heading1"] = config.Heading1 ?? "";
            data["website"] = string.IsNullOrWhiteSpace(config.Website) ? null : config.Website;
            data["crest"] = config.Crest ?? "";
            data["heads"] = config.Heads ?? new List<OfficerGroup>();
            data["deputy_heads"] = config.DeputyHeads ?? new List<OfficerGroup>();
            if (DebugMode)
                Console.WriteLine($"    ⚠ order_summary not found, using executive_officers fallback");
        }

        // Use section-specific heading2 (distinct from title)
        data["heading2"] = string.IsNullOrWhiteSpace(config.Heading2) ? null : config.Heading2;

        if (DebugMode)
            Console.WriteLine($"    ✓ Loaded executive officers metadata");

        // Load CSV executive officers (with Rank, Name, Contact columns)
        var officers = await LoadExecutiveOfficersFromCsvAsync(config.Source, documentRoot);
        data["executive_officers"] = officers;

        return data;
    }

    private async Task<List<Dictionary<string, object?>>> LoadExecutiveOfficersFromCsvAsync(string? csvSource, string documentRoot)
    {
        var officers = new List<Dictionary<string, object?>>();

        if (string.IsNullOrWhiteSpace(csvSource))
            return officers;

        var csvPath = Path.Combine(documentRoot, "data", csvSource);
        if (!File.Exists(csvPath))
        {
            if (DebugMode)
                Console.WriteLine($"    ❌ CSV file not found: {csvPath}");
            return officers;
        }

        try
        {
            using var reader = new StreamReader(csvPath);
            using var csv = new CsvReader(reader, CultureInfo.InvariantCulture);

            csv.Read();
            csv.ReadHeader();

            while (csv.Read())
            {
                var rank = csv.GetField("Rank")?.Trim();
                var name = csv.GetField("Name")?.Trim();
                var contact = csv.GetField("Contact")?.Trim();

                if (!string.IsNullOrWhiteSpace(name))
                {
                    officers.Add(new Dictionary<string, object?>
                    {
                        { "rank", rank },
                        { "name", name },
                        { "contact", string.IsNullOrWhiteSpace(contact) ? null : contact }
                    });
                }
            }

            if (DebugMode)
                Console.WriteLine($"    ✓ Loaded {officers.Count} executive officers from CSV");
        }
        catch (Exception ex)
        {
            if (DebugMode)
                Console.WriteLine($"    ❌ Error reading CSV: {ex.Message}");
            throw;
        }

        return await Task.FromResult(officers);
    }
}
