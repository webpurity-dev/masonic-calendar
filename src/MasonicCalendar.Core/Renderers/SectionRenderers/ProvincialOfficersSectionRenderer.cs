namespace MasonicCalendar.Core.Services.Renderers.SectionRenderers;

using CsvHelper;
using MasonicCalendar.Core.Domain;
using MasonicCalendar.Core.Loaders;
using Scriban;
using System.Globalization;
using System.Text;

/// <summary>
/// Renders provincial/grand order officers sections with data from YAML metadata and CSV officers.
/// </summary>
public class ProvincialOfficersSectionRenderer(string templateRoot, SchemaDataLoader? dataLoader, bool debugMode)
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
            var metadata = await LoadProvinceOfficersDataAsync(section);

            if (DebugMode)
                Console.WriteLine($"  - Section '{section.SectionId}': Loaded officers metadata");

            // Build Scriban model
            var model = new Dictionary<string, object?>
            {
                { "heading1", metadata["heading1"] },
                { "heading2", metadata["heading2"] },
                { "website", metadata["website"] },
                { "district_heading", metadata["district_heading"] },
                { "officers_heading", metadata["officers_heading"] },
                { "crest", metadata["crest"] },
                { "heads", metadata["heads"] },
                { "deputy_heads", metadata["deputy_heads"] },
                { "district_heads", metadata["district_heads"] },
                { "officers", metadata["officers"] },
                { "override_break_before", section.OverrideBreakBefore }
            };

            var html = template.Render(model);
            WrapWithPageBreakAndAnchor(output, $"section_{section.SectionId}", html, sectionIndex, section.ResetPageCounter, section.OverrideBreakBefore);

            if (DebugMode)
                Console.WriteLine($"  ✓ Rendered provincial officers section");
        }
        catch (Exception ex)
        {
            if (DebugMode)
                Console.WriteLine($"  ❌ Error rendering provincial officers: {ex.Message}");
            throw;
        }
    }

    private async Task<Dictionary<string, object?>> LoadProvinceOfficersDataAsync(SectionConfig section)
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
        if (mapping?.ProvincialOfficers == null)
        {
            if (DebugMode)
                Console.WriteLine($"    ❌ ProvincialOfficers config not found in mapping");
            return data;
        }

        var config = mapping.ProvincialOfficers;

        // Populate metadata directly from config
        data["heading1"] = config.Heading1 ?? "";
        data["heading2"] = string.IsNullOrWhiteSpace(config.Heading2) ? null : config.Heading2;
        data["website"] = string.IsNullOrWhiteSpace(config.Website) ? null : config.Website;
        data["district_heading"] = string.IsNullOrWhiteSpace(config.DistrictHeading) ? null : config.DistrictHeading;
        data["officers_heading"] = string.IsNullOrWhiteSpace(config.OfficersHeading) ? null : config.OfficersHeading;
        data["crest"] = config.Crest ?? "";
        data["heads"] = config.Heads ?? new List<OfficerGroup>();
        data["deputy_heads"] = config.DeputyHeads ?? new List<OfficerGroup>();
        data["district_heads"] = config.DistrictHeads ?? new List<OfficerGroup>();

        if (DebugMode)
            Console.WriteLine($"    ✓ Loaded provincial officers metadata");

        // Load CSV officers
        var officers = await LoadOfficersFromCsvAsync(config.Source, documentRoot);
        data["officers"] = officers;

        return data;
    }

    private async Task<List<ProvinceOfficer>> LoadOfficersFromCsvAsync(string? csvSource, string documentRoot)
    {
        var officers = new List<ProvinceOfficer>();

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
                var officer = new ProvinceOfficer
                {
                    Office = csv.GetField("Office"),
                    Name = csv.GetField("Name"),
                    Unit = csv.GetField("Unit")
                };

                if (!string.IsNullOrWhiteSpace(officer.Name))
                {
                    officers.Add(officer);
                }
            }

            if (DebugMode)
                Console.WriteLine($"    ✓ Loaded {officers.Count} officers from CSV");
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
