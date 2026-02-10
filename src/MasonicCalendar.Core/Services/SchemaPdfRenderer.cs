namespace MasonicCalendar.Core.Services;

using MasonicCalendar.Core.Domain;
using Scriban;
using System.Text;
using PuppeteerSharp;
using PuppeteerSharp.Media;

/// <summary>
/// Schema-driven HTML/PDF renderer that uses Scriban template engine.
/// Supports rendering to HTML or converting HTML to PDF using Puppeteer/Chromium.
/// </summary>
public class SchemaPdfRenderer(DocumentLayoutLoader layoutLoader, string? documentRoot = null)
{
    private readonly DocumentLayoutLoader _layoutLoader = layoutLoader;
    private readonly string _templateRoot = !string.IsNullOrWhiteSpace(documentRoot)
        ? Path.Combine(documentRoot, "templates")
        : Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "document", "templates");

    public async Task<Result<byte[]>> RenderUnitsAsync(
        List<SchemaUnit> units,
        string masterTemplateKey,
        string sectionId,
        string format = "HTML")
    {
        try
        {
            var layoutResult = _layoutLoader.LoadMasterLayout(masterTemplateKey);
            if (!layoutResult.Success)
                return Result<byte[]>.Fail(layoutResult.Error ?? "Failed to load template");

            var layout = layoutResult.Data;
            var section = layout?.Sections?.FirstOrDefault(s =>
                s.SectionId?.Equals(sectionId, StringComparison.OrdinalIgnoreCase) ?? false);

            if (section == null)
                return Result<byte[]>.Fail($"Section '{sectionId}' not found in template");

            // Load the template specified in the section config
            if (string.IsNullOrWhiteSpace(section.Template))
                return Result<byte[]>.Fail($"No template specified in section '{sectionId}'");

            var templateFile = Path.Combine(_templateRoot, section.Template);
            if (!File.Exists(templateFile))
                return Result<byte[]>.Fail($"Template not found: {templateFile}");

            var templateContent = File.ReadAllText(templateFile);
            var template = Template.Parse(templateContent);

            var output = new StringBuilder();

            // Always build HTML internally
            output.AppendLine("<!DOCTYPE html>");
            output.AppendLine("<html>");
            output.AppendLine("<head>");
            output.AppendLine("<meta charset='utf-8'/>");
            output.AppendLine("<title>Masonic Calendar</title>");
            
            // Add page sizing and margins for PDF output (CSS for HTML viewers too)
            var format_str = layout?.Document?.Format ?? "A6";
            var orientation = layout?.Document?.Orientation ?? "portrait";
            output.AppendLine($"<meta name='format' content='{format_str}'/>");
            output.AppendLine($"<meta name='orientation' content='{orientation}'/>");
            
            // CSS for page styling and fonts
            output.AppendLine("<style>");
            output.AppendLine($"@page {{ size: {format_str} {orientation}; ");
            
            // Apply global margins if available
            if (layout?.GlobalMargins != null)
            {
                var margins = layout.GlobalMargins;
                var top = margins.PageTop ?? "5mm";
                var bottom = margins.PageBottom ?? "5mm";
                var left = margins.PageLeft ?? "5mm";
                var right = margins.PageRight ?? "5mm";
                output.AppendLine($"margin: {top} {right} {bottom} {left}; ");
            }
            else
            {
                output.AppendLine("margin: 5mm; ");
            }
            
            output.AppendLine("}");
            output.AppendLine(".page-break { page-break-after: always; }");
            
            // Add font styling from global_styling config
            if (layout?.GlobalStyling?.Fonts?.Sizes != null)
            {
                var sizes = layout.GlobalStyling.Fonts.Sizes;
                output.AppendLine(":root {");
                
                if (sizes.TryGetValue("large_heading", out var largeHeading))
                    output.AppendLine($"  --font-large-heading: {largeHeading};");
                if (sizes.TryGetValue("section_heading", out var sectionHeading))
                    output.AppendLine($"  --font-section-heading: {sectionHeading};");
                if (sizes.TryGetValue("subsection_heading", out var subsectionHeading))
                    output.AppendLine($"  --font-subsection-heading: {subsectionHeading};");
                if (sizes.TryGetValue("body", out var body))
                    output.AppendLine($"  --font-body: {body};");
                if (sizes.TryGetValue("small", out var small))
                    output.AppendLine($"  --font-small: {small};");
                if (sizes.TryGetValue("tiny", out var tiny))
                    output.AppendLine($"  --font-tiny: {tiny};");
                
                output.AppendLine("}");
            }
            
            output.AppendLine("</style>");
            
            output.AppendLine("</head>");
            output.AppendLine("<body>");

            // Filter units by section's unit type if configured
            var unitsToRender = units;
            if (!string.IsNullOrWhiteSpace(section.UnitType))
            {
                unitsToRender = units.Where(u => u.UnitType == section.UnitType).ToList();
            }

            var unitIndex = 0;
            foreach (var unit in unitsToRender)
            {
                var unitHtml = RenderUnitWithScriban(unit, template, layout?.GlobalStyling);
                output.Append(unitHtml);
                
                // Add page breaks based on pages_per_unit configuration
                // Each unit typically uses N pages, add breaks between units
                unitIndex++;
                if (unitIndex < unitsToRender.Count)
                {
                    output.AppendLine("<div class='page-break'></div>");
                }
            }

            output.AppendLine("</body>");
            output.AppendLine("</html>");

            var htmlContent = output.ToString();

            // Handle output format
            if (format.Equals("PDF", StringComparison.OrdinalIgnoreCase))
            {
                // Convert HTML to PDF using Puppeteer with layout settings
                // Map format string to PuppeteerSharp PaperFormat enum
                var paperFormat = MapToPaperFormat(format_str);
                
                var pdfOptions = new PdfOptions
                {
                    Format = paperFormat,
                    Landscape = orientation?.Equals("landscape", StringComparison.OrdinalIgnoreCase) ?? false,
                    MarginOptions = new MarginOptions
                    {
                        Top = layout?.GlobalMargins?.PageTop ?? "5mm",
                        Bottom = layout?.GlobalMargins?.PageBottom ?? "5mm",
                        Left = layout?.GlobalMargins?.PageLeft ?? "5mm",
                        Right = layout?.GlobalMargins?.PageRight ?? "5mm"
                    },
                    PrintBackground = true
                };
                var pdfBytes = await ConvertHtmlToPdf(htmlContent, pdfOptions);
                return Result<byte[]>.Ok(pdfBytes);
            }
            else
            {
                // Return as HTML
                return Result<byte[]>.Ok(Encoding.UTF8.GetBytes(htmlContent));
            }
        }
        catch (Exception ex)
        {
            return Result<byte[]>.Fail($"Error rendering units: {ex.Message}");
        }
    }

    private async Task<byte[]> ConvertHtmlToPdf(string htmlContent, PdfOptions pdfOptions)
    {
        try
        {
            // Download Chrome if not already present
            var browserFetcher = new BrowserFetcher();
            await browserFetcher.DownloadAsync();

            // Launch Chrome and convert HTML to PDF
            await using var browser = await Puppeteer.LaunchAsync(new LaunchOptions
            {
                Headless = true,
                Args = new[] { "--no-sandbox" }
            });

            await using var page = await browser.NewPageAsync();
            
            // Set content and render
            await page.SetContentAsync(htmlContent);
            
            // Generate PDF with specified options
            var pdfStream = await page.PdfStreamAsync(pdfOptions);
            using (var memoryStream = new MemoryStream())
            {
                await pdfStream.CopyToAsync(memoryStream);
                return memoryStream.ToArray();
            }
        }
        catch (Exception ex)
        {
            throw new Exception($"PDF conversion failed: {ex.Message}", ex);
        }
    }

    private static PaperFormat MapToPaperFormat(string format)
    {
        // Map common paper format strings to PuppeteerSharp PaperFormat enum
        return format.ToUpperInvariant() switch
        {
            "A0" => PaperFormat.A0,
            "A1" => PaperFormat.A1,
            "A2" => PaperFormat.A2,
            "A3" => PaperFormat.A3,
            "A4" => PaperFormat.A4,
            "A5" => PaperFormat.A5,
            "A6" => PaperFormat.A6,
            "LETTER" => PaperFormat.Letter,
            "LEGAL" => PaperFormat.Legal,
            "TABLOID" => PaperFormat.Tabloid,
            _ => PaperFormat.A4 // Default to A4
        };
    }

    private static decimal ConvertMarginToUnit(string? margin)
    {
        if (string.IsNullOrWhiteSpace(margin))
            return 0.196m; // 5mm default

        // Convert common CSS units to PuppeteerSharp units (mm)
        if (margin.EndsWith("mm"))
        {
            if (decimal.TryParse(margin[..^2], out var value))
                return value;
        }
        else if (margin.EndsWith("cm"))
        {
            if (decimal.TryParse(margin[..^2], out var value))
                return value * 10;
        }
        else if (margin.EndsWith("in"))
        {
            if (decimal.TryParse(margin[..^2], out var value))
                return value * 25.4m;
        }
        else if (decimal.TryParse(margin, out var value))
        {
            return value; // Assume mm if no unit specified
        }

        return 0.196m; // 5mm default
    }

    private string RenderUnitWithScriban(SchemaUnit unit, Template template, dynamic? globalStyling = null)
    {
        // Build font sizes for Scriban
        var fontSizes = new Dictionary<string, object?>();
        if (globalStyling?.Fonts?.Sizes is Dictionary<string, object?> sizes)
        {
            fontSizes["large_heading"] = sizes.TryGetValue("large_heading", out var lh) ? lh?.ToString() : "14pt";
            fontSizes["section_heading"] = sizes.TryGetValue("section_heading", out var sh) ? sh?.ToString() : "12pt";
            fontSizes["subsection_heading"] = sizes.TryGetValue("subsection_heading", out var ssh) ? ssh?.ToString() : "10pt";
            fontSizes["body"] = sizes.TryGetValue("body", out var b) ? b?.ToString() : "9pt";
            fontSizes["small"] = sizes.TryGetValue("small", out var s) ? s?.ToString() : "8pt";
            fontSizes["tiny"] = sizes.TryGetValue("tiny", out var t) ? t?.ToString() : "6pt";
        }
        else
        {
            fontSizes["large_heading"] = "14pt";
            fontSizes["section_heading"] = "12pt";
            fontSizes["subsection_heading"] = "10pt";
            fontSizes["body"] = "9pt";
            fontSizes["small"] = "8pt";
            fontSizes["tiny"] = "6pt";
        }
        
        // Build the model for Scriban
        var model = new Dictionary<string, object?>
        {
            { "fonts", fontSizes },
            {
                "unit", new Dictionary<string, object?>
                {
                    { "name", CleanName(unit.Name) },
                    { "number", unit.Number },
                    { "email", unit.Email },
                    { "established", unit.Established?.ToString("d MMMM yyyy") ?? "" },
                    { "lastInstallationDate", unit.LastInstallationDate?.ToString("d MMMM yyyy") ?? "" }
                }
            },
            { "location", null }, // No location data in schema
            {
                "officers", unit.Officers
                    .OrderBy(o => o.DisplayOrder ?? 999)
                    .Select(o => new Dictionary<string, object?>
                    {
                        { "name", CleanName(o.Name) },
                        { "lastName", ExtractLastName(CleanName(o.Name)) },
                        { "initials", ExtractInitials(CleanName(o.Name)) },
                        { "position", o.Position },
                        { "posNo", o.DisplayOrder ?? 0 }
                    })
                    .ToList()
            },
            {
                "pastMasters", unit.PastMasters
                    .Select(pm => new Dictionary<string, object?>
                    {
                        { "name", CleanName(pm.Name) },
                        { "lastName", ExtractLastName(CleanName(pm.Name)) },
                        { "initials", ExtractInitials(CleanName(pm.Name)) },
                        { "installed", pm.YearInstalled },
                        { "provRank", CleanProvincialRank(pm.ProvincialRank) },
                        { "provRankIssued", CleanProvincialRank(pm.RankYear) }
                    })
                    .ToList()
            },
            {
                "joiningPastMasters", unit.JoinPastMasters
                    .Select(jpm => new Dictionary<string, object?>
                    {
                        { "name", CleanName(jpm.Name) },
                        { "lastName", ExtractLastName(CleanName(jpm.Name)) },
                        { "initials", ExtractInitials(CleanName(jpm.Name)) },
                        { "provRank", CleanProvincialRank(jpm.ProvincialRank) },
                        { "provRankIssued", CleanProvincialRank(jpm.RankYear) }
                    })
                    .ToList()
            },
            {
                "members", unit.Members
                    .Select(m => new Dictionary<string, object?>
                    {
                        { "name", CleanName(m.Name) },
                        { "lastName", ExtractLastName(CleanName(m.Name)) },
                        { "initials", ExtractInitials(CleanName(m.Name)) },
                        { "joined", m.YearInitiated }
                    })
                    .ToList()
            },
            {
                "memberColumns", SplitMembersIntoColumns(unit.Members)
            },
            {
                "honoraryMembers", unit.HonoraryMembers
                    .Select(hm => new Dictionary<string, object?>
                    {
                        { "name", CleanName(hm.Name) },
                        { "lastName", ExtractLastName(CleanName(hm.Name)) },
                        { "initials", ExtractInitials(CleanName(hm.Name)) },
                        { "grandRank", "" },
                        { "provRank", "" }
                    })
                    .ToList()
            }
        };

        return template.Render(model);
    }

    private string CleanName(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return "";
        
        // Remove corruption characters and any non-printable characters
        // Keep only letters, numbers, spaces, hyphens, apostrophes, periods, and commas
        var cleaned = System.Text.RegularExpressions.Regex.Replace(name, @"[^\w\s\-'\.,]", "");
        cleaned = cleaned.Trim();
        
        // Remove extra spaces between words
        while (cleaned.Contains("  "))
            cleaned = cleaned.Replace("  ", " ");
        
        return cleaned;
    }

    private string CleanProvincialRank(string? rank)
    {
        if (string.IsNullOrWhiteSpace(rank))
            return "";
        
        // Remove commas, brackets, and extra whitespace
        var cleaned = System.Text.RegularExpressions.Regex.Replace(rank, @"[,\(\)\[\]\{\}]", "");
        cleaned = cleaned.Trim();
        
        // Remove extra spaces between words
        while (cleaned.Contains("  "))
            cleaned = cleaned.Replace("  ", " ");
        
        return cleaned;
    }

    private string ExtractLastName(string fullName)
    {
        if (string.IsNullOrWhiteSpace(fullName))
            return "";

        // Assume last word is last name
        var parts = fullName.Trim().Split(' ');
        return parts.Length > 0 ? parts[^1] : fullName;
    }

    private string ExtractInitials(string fullName)
    {
        if (string.IsNullOrWhiteSpace(fullName))
            return "";

        var parts = fullName.Trim().Split(' ');
        if (parts.Length <= 1)
            return "";

        // All parts except the last one (which is last name)
        return string.Join(" ", parts.Take(parts.Length - 1));
    }

    private List<List<Dictionary<string, object?>>> SplitMembersIntoColumns(List<SchemaMember> members)
    {
        // Split members into 3 roughly equal columns
        var membersData = members
            .Select(m => new Dictionary<string, object?>
            {
                { "lastName", ExtractLastName(CleanName(m.Name)) },
                { "initials", ExtractInitials(CleanName(m.Name)) },
                { "joined", m.YearInitiated }
            })
            .ToList();

        if (membersData.Count == 0)
            return new List<List<Dictionary<string, object?>>> { new(), new(), new() };

        var itemsPerColumn = (int)Math.Ceiling(membersData.Count / 3.0);
        var col1 = membersData.Take(itemsPerColumn).ToList();
        var col2 = membersData.Skip(itemsPerColumn).Take(itemsPerColumn).ToList();
        var col3 = membersData.Skip(itemsPerColumn * 2).ToList();

        return new List<List<Dictionary<string, object?>>> { col1, col2, col3 };
    }
}
