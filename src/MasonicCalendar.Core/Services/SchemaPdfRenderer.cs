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
    private readonly string _imagesRoot = !string.IsNullOrWhiteSpace(documentRoot)
        ? Path.Combine(documentRoot, "images")
        : Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "document", "images");

    public async Task<Result<byte[]>> RenderUnitsAsync(
        List<SchemaUnit> units,
        string masterTemplateKey,
        string? sectionId = null,
        string format = "HTML")
    {
        // If no specific section, render all sections
        if (string.IsNullOrWhiteSpace(sectionId))
        {
            return await RenderAllSectionsAsync(units, masterTemplateKey, format);
        }

        return await RenderSectionAsync(units, masterTemplateKey, sectionId, format);
    }

    private async Task<Result<byte[]>> RenderSectionAsync(
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
            output.AppendLine("body { background-color: #f0f0f0; }");
            output.AppendLine(".page-break { page-break-after: always; height: 10px; background-color: #f0f0f0; margin: 0; padding: 0; }");
            output.AppendLine(".page-break::before { content: \"\"; }");
            output.AppendLine(".unit-page { background-color: white; margin: 10px auto; padding: 20px; box-shadow: 0 2px 4px rgba(0,0,0,0.1); }");
            output.AppendLine("@media print {");
            output.AppendLine("  body { background-color: white; }");
            output.AppendLine("  .page-break { height: 0; background-color: transparent; margin: 0; padding: 0; }");
            output.AppendLine("  .unit-page { background-color: transparent; margin: 0; padding: 0; box-shadow: none; }");
            output.AppendLine("}");
            
            output.AppendLine("</style>");
            
            output.AppendLine("</head>");
            output.AppendLine("<body>");

            // Check section type
            var isToc = section.Type?.Equals("toc", StringComparison.OrdinalIgnoreCase) ?? false;
            var isStatic = section.Type?.Equals("static", StringComparison.OrdinalIgnoreCase) ?? false;

            if (isToc)
            {
                // Render table of contents
                var tocModel = new Dictionary<string, object?>
                {
                    { "toc_by_section", BuildTocData(units, layout?.Sections) }
                };
                var tocHtml = template.Render(tocModel);
                output.AppendLine("<div class='unit-page'>");
                output.Append(tocHtml);
                output.AppendLine("</div>");
            }
            else if (isStatic)
            {
                // Render static template (template handles all content)
                var staticModel = new Dictionary<string, object?>();
                var staticHtml = template.Render(staticModel);
                output.AppendLine("<div class='unit-page'>");
                output.Append(staticHtml);
                output.AppendLine("</div>");
            }
            else
            {
                // Filter units by section's unit type if configured
                var unitsToRender = units;
                if (!string.IsNullOrWhiteSpace(section.UnitType))
                {
                    unitsToRender = units.Where(u => u.UnitType == section.UnitType).ToList();
                }

                var unitIndex = 0;
                foreach (var unit in unitsToRender)
                {
                    var anchorId = GenerateAnchorId(unit);
                    var unitHtml = RenderUnitWithScriban(unit, template);
                    output.AppendLine("<div class='unit-page'>");
                    output.AppendLine($"<a id=\"{anchorId}\"></a>");
                    output.Append(unitHtml);
                    output.AppendLine("</div>");
                    
                    // Add page breaks based on pages_per_unit configuration
                    // Each unit typically uses N pages, add breaks between units
                    unitIndex++;
                    if (unitIndex < unitsToRender.Count)
                    {
                        output.AppendLine("<div class='page-break'></div>");
                    }
                }
            }

            output.AppendLine("</body>");
            output.AppendLine("</html>");

            var htmlContent = output.ToString();

            // Handle output format
            if (format.Equals("PDF", StringComparison.OrdinalIgnoreCase))
            {
                // Convert relative image paths to data URLs for PDF compatibility
                htmlContent = ConvertRelativeImagesToDataUrls(htmlContent);
                
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

    private async Task<Result<byte[]>> RenderAllSectionsAsync(List<SchemaUnit> units, string masterTemplateKey, string format)
    {
        try
        {
            var layoutResult = _layoutLoader.LoadMasterLayout(masterTemplateKey);
            if (!layoutResult.Success)
                return Result<byte[]>.Fail(layoutResult.Error ?? "Failed to load template");

            var layout = layoutResult.Data;
            if (layout?.Sections?.Count == 0)
                return Result<byte[]>.Fail("No sections found in template");

            var output = new StringBuilder();

            // Build complete HTML document once
            output.AppendLine("<!DOCTYPE html>");
            output.AppendLine("<html>");
            output.AppendLine("<head>");
            output.AppendLine("<meta charset='utf-8'/>");
            output.AppendLine("<title>Masonic Calendar</title>");
            
            var format_str = layout?.Document?.Format ?? "A6";
            var orientation = layout?.Document?.Orientation ?? "portrait";
            output.AppendLine($"<meta name='format' content='{format_str}'/>");
            output.AppendLine($"<meta name='orientation' content='{orientation}'/>");
            
            output.AppendLine("<style>");
            output.AppendLine($"@page {{ size: {format_str} {orientation}; ");
            
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
            output.AppendLine("body { background-color: #f0f0f0; }");
            output.AppendLine(".page-break { page-break-after: always; height: 10px; background-color: #f0f0f0; margin: 0; padding: 0; }");
            output.AppendLine(".page-break::before { content: \"\"; }");
            output.AppendLine(".unit-page { background-color: white; margin: 10px auto; padding: 20px; box-shadow: 0 2px 4px rgba(0,0,0,0.1); }");
            output.AppendLine("@media print {");
            output.AppendLine("  body { background-color: white; }");
            output.AppendLine("  .page-break { height: 0; background-color: transparent; margin: 0; padding: 0; }");
            output.AppendLine("  .unit-page { background-color: transparent; margin: 0; padding: 0; box-shadow: none; }");
            output.AppendLine("}");
            output.AppendLine("</style>");
            
            output.AppendLine("</head>");
            output.AppendLine("<body>");

            // Render each section
            if (layout?.Sections == null || layout.Sections.Count == 0)
                return Result<byte[]>.Fail("No sections found in template");
            
            var sectionIndex = 0;
            foreach (var section in layout.Sections)
            {
                if (string.IsNullOrWhiteSpace(section.Template))
                    continue;

                var templateFile = Path.Combine(_templateRoot, section.Template);
                if (!File.Exists(templateFile))
                    continue;

                var templateContent = File.ReadAllText(templateFile);
                var template = Template.Parse(templateContent);
                if (template.HasErrors)
                    continue;

                // Check section type
                var isToc = section.Type?.Equals("toc", StringComparison.OrdinalIgnoreCase) ?? false;
                var isStatic = section.Type?.Equals("static", StringComparison.OrdinalIgnoreCase) ?? false;
                var isDataDriven = section.Type?.Equals("data-driven", StringComparison.OrdinalIgnoreCase) ?? false;

                if (isToc)
                {
                    // Render table of contents
                    var tocModel = new Dictionary<string, object?>
                    {
                        { "toc_by_section", BuildTocData(units, layout.Sections) }
                    };
                    var tocHtml = template.Render(tocModel);
                    output.AppendLine("<div class='unit-page'>");
                    output.Append(tocHtml);
                    output.AppendLine("</div>");
                }
                else if (isStatic)
                {
                    // Render static template (template handles all content)
                    var staticModel = new Dictionary<string, object?>();
                    var staticHtml = template.Render(staticModel);
                    output.AppendLine("<div class='unit-page'>");
                    output.Append(staticHtml);
                    output.AppendLine("</div>");
                }
                else if (isDataDriven)
                {
                    // Render data-driven section with units
                    var unitsForSection = units;
                    if (!string.IsNullOrWhiteSpace(section.UnitType))
                    {
                        unitsForSection = units.Where(u => u.UnitType == section.UnitType).ToList();
                    }

                    var unitIndex = 0;
                    foreach (var unit in unitsForSection)
                    {
                        var anchorId = GenerateAnchorId(unit);
                        var unitHtml = RenderUnitWithScriban(unit, template);
                        output.AppendLine("<div class='unit-page'>");
                        output.AppendLine($"<a id=\"{anchorId}\"></a>");
                        output.Append(unitHtml);
                        output.AppendLine("</div>");
                        
                        unitIndex++;
                        if (unitIndex < unitsForSection.Count)
                        {
                            output.AppendLine("<div class='page-break'></div>");
                        }
                    }
                }

                // Add page break between sections
                sectionIndex++;
                if (sectionIndex < layout.Sections.Count)
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
                // Convert relative image paths to data URLs for PDF compatibility
                htmlContent = ConvertRelativeImagesToDataUrls(htmlContent);
                
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
                return Result<byte[]>.Ok(Encoding.UTF8.GetBytes(htmlContent));
            }
        }
        catch (Exception ex)
        {
            return Result<byte[]>.Fail($"Error rendering all sections: {ex.Message}");
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

    private string RenderUnitWithScriban(SchemaUnit unit, Template template)
    {
        // Build the model for Scriban
        var model = new Dictionary<string, object?>
        {
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

    private string ConvertImageToDataUrl(string fileName)
    {
        var imagePath = Path.Combine(_imagesRoot, fileName);
        if (!File.Exists(imagePath))
            return "";

        var fileExtension = Path.GetExtension(imagePath).ToLowerInvariant().TrimStart('.');
        var mimeType = fileExtension switch
        {
            "png" => "image/png",
            "jpg" or "jpeg" => "image/jpeg",
            "gif" => "image/gif",
            "svg" => "image/svg+xml",
            _ => "image/png"
        };

        var imageBytes = File.ReadAllBytes(imagePath);
        var base64 = Convert.ToBase64String(imageBytes);
        return $"data:{mimeType};base64,{base64}";
    }

    private string ConvertRelativeImagesToDataUrls(string htmlContent)
    {
        // Find all img src attributes with relative paths and convert to data URLs
        var regex = new System.Text.RegularExpressions.Regex(@"src=""\.\.\/images\/([^""]+)""", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        return regex.Replace(htmlContent, match =>
        {
            var fileName = match.Groups[1].Value;
            var dataUrl = ConvertImageToDataUrl(fileName);
            
            if (!string.IsNullOrEmpty(dataUrl))
                return $"src=\"{dataUrl}\"";
            
            return match.Value;
        });
    }

    private string GenerateAnchorId(SchemaUnit unit)
    {
        // Create a clean anchor ID from unit number and name
        var cleanName = System.Text.RegularExpressions.Regex.Replace(unit.Name ?? "", @"[^a-zA-Z0-9]", "_");
        return $"unit_{unit.Number}_{cleanName}".ToLower();
    }

    private List<Dictionary<string, object?>> BuildTocData(List<SchemaUnit> units, List<SectionConfig>? sections)
    {
        var tocSections = new List<Dictionary<string, object?>>();

        if (sections == null)
            return tocSections;

        // Calculate page numbers
        // Assume: Cover = 1 page, TOC = 1 page, Average 2.5 units per page
        const int unitsPerPage = 2;  // Conservative estimate
        int currentPageNumber = 3;   // Start after cover (1) and TOC (2)
        int unitIndexPerSection = 0;

        // Process each data-driven section
        foreach (var section in sections)
        {
            // Skip non-data-driven sections
            if (!section.Type?.Equals("data-driven", StringComparison.OrdinalIgnoreCase) ?? true)
                continue;

            // Use section_title from YAML if configured, otherwise fallback to section short names
            var sectionTitle = section.Title ?? section.SectionName ?? section.SectionId ?? "Unknown";

            // Get units for this section
            var unitsForSection = units;
            if (!string.IsNullOrWhiteSpace(section.UnitType))
            {
                unitsForSection = units.Where(u => u.UnitType == section.UnitType).ToList();
            }

            // Build items list for this section
            var items = new List<object?>();
            unitIndexPerSection = 0;

            foreach (var u in unitsForSection)
            {
                // Calculate page number: start at currentPageNumber, add 1 for every unitsPerPage units
                int pageNumber = currentPageNumber + (unitIndexPerSection / unitsPerPage);
                
                items.Add(new Dictionary<string, object?>
                {
                    { "unit_number", u.Number },
                    { "unit_name", CleanName(u.Name) },
                    { "anchor_id", GenerateAnchorId(u) },
                    { "page_number", pageNumber }
                });

                unitIndexPerSection++;
            }

            // Update page counter for next section
            currentPageNumber += (unitIndexPerSection + unitsPerPage - 1) / unitsPerPage; // Round up

            tocSections.Add(new Dictionary<string, object?>
            {
                { "section_title", sectionTitle },
                { "items", items }
            });
        }

        return tocSections;
    }
}
