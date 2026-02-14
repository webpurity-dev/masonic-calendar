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
public class SchemaPdfRenderer(DocumentLayoutLoader layoutLoader, SchemaDataLoader? dataLoader = null, string? documentRoot = null, bool debugMode = false)
{
    private readonly DocumentLayoutLoader _layoutLoader = layoutLoader;
    private readonly SchemaDataLoader? _dataLoader = dataLoader;
    private readonly bool _debugMode = debugMode;
    private readonly string _templateRoot = !string.IsNullOrWhiteSpace(documentRoot)
        ? Path.Combine(documentRoot, "templates")
        : Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "document", "templates");
    private readonly string _imagesRoot = !string.IsNullOrWhiteSpace(documentRoot)
        ? Path.Combine(documentRoot, "images")
        : Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "document", "images");
    private readonly string _outputRoot = !string.IsNullOrWhiteSpace(documentRoot)
        ? Path.Combine(documentRoot, "..", "output")
        : Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "output");
    
    // Track page numbers for each section as they are rendered
    private readonly Dictionary<string, int> _sectionStartPages = new();

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
            
            // Link to print-ready CSS with Paged.js styling
            var printCssPath = Path.Combine(_templateRoot, "print.css");
            if (File.Exists(printCssPath))
            {
                var printCssContent = File.ReadAllText(printCssPath);
                output.AppendLine("<style>");
                output.AppendLine(printCssContent);
                output.AppendLine("</style>");
            }
            
            // Load Paged.js from CDN (or local if offline)
            output.AppendLine("<script src='https://unpkg.com/pagedjs/dist/paged.polyfill.js'></script>");
            output.AppendLine("</head>");
            output.AppendLine("<body>");

            // Check section type
            var isToc = section.Type?.Equals("toc", StringComparison.OrdinalIgnoreCase) ?? false;
            var isStatic = section.Type?.Equals("static", StringComparison.OrdinalIgnoreCase) ?? false;

            if (isToc)
            {
                // Render table of contents
                List<Dictionary<string, object?>> tocData;
                if (section.ForSection?.Equals("all", StringComparison.OrdinalIgnoreCase) ?? false)
                {
                    tocData = BuildSectionsTocData(layout?.Sections, new Dictionary<string, int>());
                }
                else if (!string.IsNullOrWhiteSpace(section.ForSection))
                {
                    // Reload units for the specific section using its data mapping
                    var unitsForToc = new List<SchemaUnit>();
                    var targetSection = layout?.Sections?.FirstOrDefault(s => 
                        s.SectionId?.Equals(section.ForSection, StringComparison.OrdinalIgnoreCase) ?? false);
                    
                    if (_dataLoader != null && targetSection != null && !string.IsNullOrWhiteSpace(targetSection.DataMapping))
                    {
                        var reloadResult = await _dataLoader.LoadUnitsWithDataAsync(masterTemplateKey, targetSection.SectionId);
                        if (reloadResult.Success)
                        {
                            unitsForToc = reloadResult.Data ?? [];
                        }
                    }
                    else
                    {
                        unitsForToc = units;
                    }
                    
                    tocData = BuildTocData(unitsForToc, layout?.Sections, section.ForSection);
                }
                else
                {
                    tocData = BuildTocData(units, layout?.Sections);
                }
                
                var tocModel = new Dictionary<string, object?>
                {
                    { "section_title", section.SectionTitle },
                    { "toc_by_section", tocData }
                };
                var tocHtml = template.Render(tocModel);
                output.AppendLine(tocHtml);  // Paged.js handles page numbering via CSS
            }
            else if (isStatic)
            {
                // Render static template (template handles all content)
                var staticModel = new Dictionary<string, object?>();
                var staticHtml = template.Render(staticModel);
                output.AppendLine(staticHtml);  // Paged.js handles page numbering via CSS
            }
            else
            {
                // Add section anchor for TOC links
                output.AppendLine($"<a id=\"section_{section.SectionId}\"></a>");
                
                // Reload units for this section using its data mapping
                var unitsToRender = new List<SchemaUnit>();
                if (_dataLoader != null && !string.IsNullOrWhiteSpace(section.DataMapping))
                {
                    // Reload units from this section's specific data mapping
                    var reloadResult = await _dataLoader.LoadUnitsWithDataAsync(masterTemplateKey, section.SectionId);
                    if (reloadResult.Success)
                    {
                        unitsToRender = reloadResult.Data ?? [];
                    }
                }
                else
                {
                    // No data mapping, use all units
                    unitsToRender = units;
                }

                var unitIndex = 0;
                foreach (var unit in unitsToRender)
                {
                    var anchorId = GenerateAnchorId(unit);
                    var unitHtml = RenderUnitWithScriban(unit, template);
                    output.AppendLine($"<div id=\"{anchorId}\" class='unit-page'>");
                    output.Append(unitHtml);
                    output.AppendLine("</div>");
                    
                    // Paged.js handles page breaks automatically via CSS
                    unitIndex++;
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
                
                // Convert HTML to PDF using Puppeteer
                // Paged.js handles all margins, page numbers, and layout via CSS @page rules
                var paperFormat = MapToPaperFormat(format_str);
                var isLandscape = orientation?.Equals("landscape", StringComparison.OrdinalIgnoreCase) ?? false;
                
                var pdfOptions = new PdfOptions
                {
                    Format = paperFormat,
                    Landscape = isLandscape,
                    PrintBackground = true,
                    DisplayHeaderFooter = false
                    // Don't set Width/Height/MarginOptions - let Paged.js CSS @page rules handle all sizing and margins
                };
                var pdfBytes = await ConvertHtmlToPdf(htmlContent, pdfOptions);
                return Result<byte[]>.Ok(pdfBytes);
            }
            else
            {
                // Convert relative image paths to data URLs for HTML portability
                htmlContent = ConvertRelativeImagesToDataUrls(htmlContent);
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
            var format_str = layout?.Document?.Format ?? "A6";
            var orientation = layout?.Document?.Orientation ?? "portrait";

            // Build complete HTML document
            output.AppendLine("<!DOCTYPE html>");
            output.AppendLine("<html>");
            output.AppendLine("<head>");
            output.AppendLine("<meta charset='utf-8'/>");
            output.AppendLine("<title>Masonic Calendar</title>");
            output.AppendLine($"<meta name='format' content='{format_str}'/>");
            output.AppendLine($"<meta name='orientation' content='{orientation}'/>");
            
            // Link to print-ready CSS with Paged.js styling
            var printCssPath = Path.Combine(_templateRoot, "print.css");
            if (File.Exists(printCssPath))
            {
                var printCssContent = File.ReadAllText(printCssPath);
                output.AppendLine("<style>");
                output.AppendLine(printCssContent);
                output.AppendLine("</style>");
            }
            
            // Load Paged.js from CDN
            output.AppendLine("<script src='https://unpkg.com/pagedjs/dist/paged.polyfill.js'></script>");
            output.AppendLine("<script>");
            output.AppendLine("// Wait for Paged.js to be available");
            output.AppendLine("if (typeof Paged !== 'function' && typeof window.Paged !== 'function') {");
            output.AppendLine("  console.warn('Paged.js not loaded from CDN - PDF rendering will be basic');");
            output.AppendLine("}");
            output.AppendLine("</script>");
            output.AppendLine("</head>");
            output.AppendLine("<body>");

            // Render each section
            Console.WriteLine($"  - Processing {layout.Sections.Count} sections...");
            foreach (var section in layout.Sections)
            {
                Console.WriteLine($"    • {section.SectionId} ({section.Type})");

                if (string.IsNullOrWhiteSpace(section.Template))
                {
                    if (_debugMode)
                        Console.WriteLine($"    ⚠️ No template specified, skipping");
                    continue;
                }

                var templateFile = Path.Combine(_templateRoot, section.Template);
                if (!File.Exists(templateFile))
                {
                    if (_debugMode)
                        Console.WriteLine($"    ⚠️ Template file not found: {templateFile}");
                    continue;
                }

                var templateContent = File.ReadAllText(templateFile);
                var template = Template.Parse(templateContent);
                if (template.HasErrors)
                {
                    if (_debugMode)
                        Console.WriteLine($"    ⚠️ Template has parsing errors, skipping");
                    continue;
                }

                var isToc = section.Type?.Equals("toc", StringComparison.OrdinalIgnoreCase) ?? false;
                var isStatic = section.Type?.Equals("static", StringComparison.OrdinalIgnoreCase) ?? false;
                var isDataDriven = section.Type?.Equals("data-driven", StringComparison.OrdinalIgnoreCase) ?? false;

                if (isToc)
                {
                    // Render table of contents with Paged.js target-counter() for automatic page numbers
                    List<Dictionary<string, object?>> tocData;
                    if (section.ForSection?.Equals("all", StringComparison.OrdinalIgnoreCase) ?? false)
                    {
                        tocData = BuildSectionsTocData(layout.Sections, new Dictionary<string, int>());
                    }
                    else if (!string.IsNullOrWhiteSpace(section.ForSection))
                    {
                        var unitsForToc = new List<SchemaUnit>();
                        var targetSection = layout.Sections.FirstOrDefault(s => 
                            s.SectionId?.Equals(section.ForSection, StringComparison.OrdinalIgnoreCase) ?? false);
                        
                        if (_dataLoader != null && targetSection != null && !string.IsNullOrWhiteSpace(targetSection.DataMapping))
                        {
                            var reloadResult = await _dataLoader.LoadUnitsWithDataAsync(masterTemplateKey, targetSection.SectionId);
                            if (reloadResult.Success)
                                unitsForToc = reloadResult.Data ?? [];
                        }
                        else
                        {
                            unitsForToc = units;
                        }
                        
                        tocData = BuildTocData(unitsForToc, layout.Sections, section.ForSection);
                    }
                    else
                    {
                        tocData = BuildTocData(units, layout.Sections);
                    }
                    
                    var tocModel = new Dictionary<string, object?>
                    {
                        { "section_title", section.SectionTitle },
                        { "toc_by_section", tocData }
                    };
                    var tocHtml = template.Render(tocModel);
                    output.AppendLine(tocHtml);  // Paged.js handles page numbering via CSS
                }
                else if (isStatic)
                {
                    // Render static template
                    var staticModel = new Dictionary<string, object?>();
                    var staticHtml = template.Render(staticModel);
                    output.AppendLine(staticHtml);  // Paged.js handles page numbering via CSS
                }
                else if (isDataDriven)
                {
                    // Add section anchor for TOC links
                    output.AppendLine($"<a id=\"section_{section.SectionId}\"></a>");
                    
                    // Reload units for this section
                    var unitsForSection = new List<SchemaUnit>();
                    if (_dataLoader != null && !string.IsNullOrWhiteSpace(section.DataMapping))
                    {
                        var reloadResult = await _dataLoader.LoadUnitsWithDataAsync(masterTemplateKey, section.SectionId);
                        if (reloadResult.Success)
                            unitsForSection = reloadResult.Data ?? [];
                    }
                    else
                    {
                        unitsForSection = units;
                    }

                    if (_debugMode)
                        Console.WriteLine($"  - Section '{section.SectionId}' ({section.Type}): {unitsForSection.Count} units");

                    // Render each unit
                    if (unitsForSection.Count > 0)
                    {
                        Console.WriteLine($"      ✓ Rendering {unitsForSection.Count} units");
                    }
                    foreach (var unit in unitsForSection)
                    {
                        var anchorId = GenerateAnchorId(unit);
                        var unitHtml = RenderUnitWithScriban(unit, template);
                        output.AppendLine($"<div id=\"{anchorId}\" class='unit-page'>");
                        output.Append(unitHtml);
                        output.AppendLine("</div>");
                        // Paged.js handles page breaks automatically via CSS
                    }
                }
            }

            output.AppendLine("<script>");
            output.AppendLine(@"
// Function to inject TOC page numbers once Paged.js has rendered pages
function injectTocPageNumbers() {
    const tocLinks = document.querySelectorAll('.toc-item a');
    const pages = document.querySelectorAll('.pagedjs_page');
    
    console.log('[injectTocPageNumbers] TOC links found: ' + tocLinks.length);
    console.log('[injectTocPageNumbers] Paged.js pages found: ' + pages.length);
    
    if (pages.length === 0) {
        console.log('[injectTocPageNumbers] No Paged.js pages found - cannot inject page numbers');
        return false;
    }
    
    let injectedCount = 0;
    tocLinks.forEach((link, index) => {
        const href = link.getAttribute('href');
        if (!href || !href.startsWith('#')) {
            console.log(`[injectTocPageNumbers] Link ${index}: skipped (no href or invalid format)`);
            return;
        }
        
        const anchorId = href.substring(1);
        const targetElement = document.getElementById(anchorId);
        
        console.log(`[injectTocPageNumbers] Link ${index} (${href}): target element ${targetElement ? 'found' : 'NOT found'}`);
        
        if (targetElement) {
            // Find which page contains this element
            let pageNumber = 0;
            for (let i = 0; i < pages.length; i++) {
                if (pages[i].contains(targetElement)) {
                    pageNumber = i + 1;
                    console.log(`[injectTocPageNumbers] Link ${index}: Target found on page ${pageNumber}`);
                    break;
                }
            }
            
            if (pageNumber > 0 && !link.querySelector('span')) {
                // Create and append page number span
                const pageSpan = document.createElement('span');
                pageSpan.className = 'toc-page-number';
                pageSpan.textContent = pageNumber.toString();
                pageSpan.style.display = 'inline-block';
                pageSpan.style.marginLeft = '6pt';
                pageSpan.style.minWidth = '30px';
                pageSpan.style.textAlign = 'right';
                pageSpan.style.color = '#000';
                pageSpan.style.fontSize = '9pt';
                link.appendChild(pageSpan);
                injectedCount++;
                console.log(`[injectTocPageNumbers] Link ${index}: Span created and appended with page ${pageNumber}`);
            } else if (pageNumber === 0) {
                console.log(`[injectTocPageNumbers] Link ${index}: Target element not found in any page`);
            } else {
                console.log(`[injectTocPageNumbers] Link ${index}: Span already exists, skipping`);
            }
        }
    });
    
    console.log(`[injectTocPageNumbers] TOTAL INJECTED: ${injectedCount} page numbers`);
    return injectedCount > 0;
}

// Use Paged.js event if available, otherwise be called explicitly by Puppeteer
if (window.Paged && typeof window.Paged.on === 'function') {
    console.log('[injectTocPageNumbers] Paged.js detected, registering rendered event');
    window.Paged.on('rendered', () => {
        console.log('[injectTocPageNumbers] Paged.js rendered event fired');
        injectTocPageNumbers();
    });
} else {
    console.log('[injectTocPageNumbers] Paged.js not available - page numbers will be injected via external call (Puppeteer)');
}
            ");
            output.AppendLine("</script>");
            output.AppendLine("</body>");
            output.AppendLine("</html>");

            var htmlContent = output.ToString();

            // Save debug HTML if debug mode is enabled
            if (_debugMode)
            {
                var debugFile = Path.Combine(_outputRoot, "master_v1-all-sections-debug.html");
                File.WriteAllText(debugFile, htmlContent);
                Console.WriteLine($"\n  - Debug HTML saved: {debugFile}");
            }

            // Handle output format
            if (format.Equals("PDF", StringComparison.OrdinalIgnoreCase))
            {
                Console.WriteLine("  - Converting HTML to PDF (initializing Puppeteer)...");
                // Convert relative image paths to data URLs for PDF compatibility
                htmlContent = ConvertRelativeImagesToDataUrls(htmlContent);
                
                // Convert HTML to PDF using Puppeteer
                // Paged.js handles all margins, page numbers, and layout via CSS @page rules
                var paperFormat = MapToPaperFormat(format_str);
                var isLandscape = orientation?.Equals("landscape", StringComparison.OrdinalIgnoreCase) ?? false;
                
                var pdfOptions = new PdfOptions
                {
                    Format = paperFormat,
                    Landscape = isLandscape,
                    PrintBackground = true,
                    DisplayHeaderFooter = false
                    // Don't set Width/Height/MarginOptions - let Paged.js CSS @page rules handle all sizing and margins
                };
                
                var pdfBytes = await ConvertHtmlToPdf(htmlContent, pdfOptions);
                return Result<byte[]>.Ok(pdfBytes);
            }
            else
            {
                // Convert relative image paths to data URLs for HTML portability
                htmlContent = ConvertRelativeImagesToDataUrls(htmlContent);
                return Result<byte[]>.Ok(Encoding.UTF8.GetBytes(htmlContent));
            }
        }
        catch (Exception ex)
        {
            return Result<byte[]>.Fail($"Error rendering all sections: {ex.Message}");
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

    private static (string Width, string Height) GetPaperDimensions(string format, bool landscape)
    {
        // Return (width, height) in mm based on format and orientation
        var (portraitWidth, portraitHeight) = format.ToUpperInvariant() switch
        {
            "A0" => ("841mm", "1189mm"),
            "A1" => ("594mm", "841mm"),
            "A2" => ("420mm", "594mm"),
            "A3" => ("297mm", "420mm"),
            "A4" => ("210mm", "297mm"),
            "A5" => ("148mm", "210mm"),
            "A6" => ("105mm", "148mm"),
            "LETTER" => ("215.9mm", "279.4mm"),
            "LEGAL" => ("215.9mm", "355.6mm"),
            "TABLOID" => ("279.4mm", "431.8mm"),
            _ => ("210mm", "297mm") // Default to A4
        };

        // Swap dimensions if landscape
        if (landscape)
            return (portraitHeight, portraitWidth);

        return (portraitWidth, portraitHeight);
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
        // Assume: Cover = 1 page, TOC = 1 page, then calculate based on pages_per_unit from each section
        int currentPageNumber = 3;   // Start after cover (1) and TOC (2)
        int unitIndexPerSection = 0;

        // Process each data-driven section
        foreach (var section in sections)
        {
            // Skip non-data-driven sections
            if (!section.Type?.Equals("data-driven", StringComparison.OrdinalIgnoreCase) ?? true)
                continue;

            // Use section_title from YAML config, with fallbacks
            var sectionTitle = section.SectionTitle ?? section.Title ?? section.SectionName ?? section.SectionId ?? "Unknown";

            // Get units for this section
            var unitsForSection = units;
            if (!string.IsNullOrWhiteSpace(section.UnitType))
            {
                unitsForSection = units.Where(u => u.UnitType == section.UnitType).ToList();
            }

            // Build items list for this section
            var items = new List<object?>();
            unitIndexPerSection = 0;
            int pagesPerUnit = section.PagesPerUnit ?? 1;  // Use configured value or default to 1

            foreach (var u in unitsForSection)
            {
                // Calculate page number: each unit occupies pagesPerUnit pages
                int pageNumber = currentPageNumber + (unitIndexPerSection * pagesPerUnit);
                
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
            currentPageNumber += unitIndexPerSection * pagesPerUnit;

            tocSections.Add(new Dictionary<string, object?>
            {
                { "section_title", sectionTitle },
                { "items", items }
            });
        }

        return tocSections;
    }

    /// <summary>
    /// Builds TOC data for a specific section only.
    /// </summary>
    private List<Dictionary<string, object?>> BuildTocData(List<SchemaUnit> units, List<SectionConfig>? sections, string forSection)
    {
        var tocSections = new List<Dictionary<string, object?>>();

        if (sections == null)
            return tocSections;

        // Calculate page numbers
        int currentPageNumber = 3;
        int unitIndexPerSection = 0;

        // Find the target section
        var targetSection = sections.FirstOrDefault(s => 
            s.SectionId?.Equals(forSection, StringComparison.OrdinalIgnoreCase) ?? false);

        if (targetSection == null || targetSection.Type?.Equals("data-driven", StringComparison.OrdinalIgnoreCase) != true)
            return tocSections;

        // Use section_title from YAML config
        var sectionTitle = targetSection.SectionTitle ?? targetSection.Title ?? targetSection.SectionName ?? targetSection.SectionId ?? "Unknown";

        // Get units for this section
        var unitsForSection = units;
        if (!string.IsNullOrWhiteSpace(targetSection.UnitType))
        {
            unitsForSection = units.Where(u => u.UnitType == targetSection.UnitType).ToList();
        }

        // Build items list for this section
        var items = new List<object?>();
        unitIndexPerSection = 0;
        int pagesPerUnit = targetSection.PagesPerUnit ?? 1;  // Use configured value or default to 1

        foreach (var u in unitsForSection)
        {
            // Calculate page number: each unit occupies pagesPerUnit pages
            int pageNumber = currentPageNumber + (unitIndexPerSection * pagesPerUnit);
            
            items.Add(new Dictionary<string, object?>
            {
                { "unit_number", u.Number },
                { "unit_name", CleanName(u.Name) },
                { "anchor_id", GenerateAnchorId(u) },
                { "page_number", pageNumber }
            });

            unitIndexPerSection++;
        }

        tocSections.Add(new Dictionary<string, object?>
        {
            { "section_title", sectionTitle },
            { "items", items }
        });

        return tocSections;
    }

    /// <summary>
    /// Builds TOC data for section titles only (for_section: "all").
    /// </summary>
    private List<Dictionary<string, object?>> BuildSectionsTocData(List<SectionConfig>? sections, Dictionary<string, int> sectionStartPages)
    {
        var tocSections = new List<Dictionary<string, object?>>();

        if (sections == null)
            return tocSections;

        // Process only data-driven sections (skip cover, toc, and static sections)
        var dataDrivenSections = sections.Where(s => 
            s.Type?.Equals("data-driven", StringComparison.OrdinalIgnoreCase) ?? false).ToList();

        int estimatedPageNumber = 2;  // Start after cover (1) and TOC (2)
        
        for (int i = 0; i < dataDrivenSections.Count; i++)
        {
            var section = dataDrivenSections[i];
            var sectionTitle = section.SectionTitle ?? section.Title ?? section.SectionName ?? section.SectionId ?? "Unknown";
            
            // Use tracked page numbers if available, otherwise estimate
            int pageNumber = estimatedPageNumber;
            if (!string.IsNullOrWhiteSpace(section.SectionId) && sectionStartPages.TryGetValue(section.SectionId, out var startPage))
            {
                pageNumber = startPage;
            }

            tocSections.Add(new Dictionary<string, object?>
            {
                { "section_id", section.SectionId },
                { "section_title", sectionTitle },
                { "page_number", pageNumber },
                { "items", new List<object?>() }  // Empty items list for section-only TOC
            });
            
            // Update estimated page for next section (used if actual tracking isn't available)
            estimatedPageNumber = pageNumber + 1;
        }

        return tocSections;
    }

    private async Task<byte[]> ConvertHtmlToPdf(string htmlContent, PdfOptions pdfOptions)
    {
        try
        {
            // Download/ensure Chromium is available
            Console.WriteLine("  - Downloading Chromium if needed...");
            var browserFetcher = new BrowserFetcher();
            await browserFetcher.DownloadAsync();

            // Launch Chrome and render PDF
            Console.WriteLine("  - Launching Puppeteer browser...");
            await using var browser = await Puppeteer.LaunchAsync(new LaunchOptions
            {
                Headless = true,
                Args = new[] { "--no-sandbox" }
            });

            Console.WriteLine("  - Creating new page...");
            await using var page = await browser.NewPageAsync();
            
            // Capture console messages from the page
            page.Console += (sender, args) => {
                var message = args.Message.Text;
                if (message.Contains("[injectTocPageNumbers]"))
                {
                    Console.WriteLine($"    [JS] {message}");
                }
            };
            
            // Set page content
            Console.WriteLine("  - Loading HTML content (this triggers Paged.js rendering)...");
            await page.SetContentAsync(htmlContent);
            
            // Wait for Paged.js to complete pagination before generating PDF
            try
            {
                Console.WriteLine("  - Waiting for Paged.js to complete pagination (max 90 seconds)...");
                
                // First, wait for any initial pages to be created
                await page.WaitForFunctionAsync(@"() => {
                    return document.querySelector('.pagedjs_pages') && 
                           document.querySelectorAll('.pagedjs_page').length > 0;
                }", new WaitForFunctionOptions { Timeout = 30000 });
                
                // Now wait for pages to stabilize by checking if page count stays the same
                // This ensures all content has been paginated
                var previousPageCount = -1;
                var stableCount = 0;
                var maxWaitTime = 60000; // 60 seconds max
                var checkInterval = 500; // Check every 500ms
                var elapsedTime = 0;
                
                while (elapsedTime < maxWaitTime && stableCount < 3)
                {
                    await Task.Delay(checkInterval);
                    var currentPageCount = await page.EvaluateFunctionAsync<int>(
                        "() => document.querySelectorAll('.pagedjs_page').length"
                    );
                    
                    if (currentPageCount == previousPageCount)
                    {
                        stableCount++;
                    }
                    else
                    {
                        stableCount = 0;
                    }
                    
                    previousPageCount = currentPageCount;
                    elapsedTime += checkInterval;
                }
                
                Console.WriteLine("  - Paged.js pagination complete");
                var finalPageCount = await page.EvaluateFunctionAsync<int>("() => document.querySelectorAll('.pagedjs_page').length");
                Console.WriteLine($"  - Total pages: {finalPageCount}");
                
                // Inject TOC page numbers using JavaScript
                try
                {
                    Console.WriteLine("  - Calling injectTocPageNumbers function...");
                    var injectedCount = await page.EvaluateFunctionAsync<int>(@"() => {
                        if (typeof injectTocPageNumbers === 'function') {
                            console.log('[injectTocPageNumbers] Function found, executing...');
                            const result = injectTocPageNumbers();
                            console.log('[injectTocPageNumbers] Function returned: ' + result);
                            return result ? 1 : 0;
                        } else {
                            console.log('[injectTocPageNumbers] ERROR: injectTocPageNumbers function NOT found');
                            return -1;
                        }
                    }");
                    
                    if (injectedCount > 0)
                    {
                        Console.WriteLine("  ✓ TOC page numbers injected successfully");
                    }
                    else if (injectedCount == 0)
                    {
                        Console.WriteLine("⚠️  No TOC page numbers were injected (function returned false)");
                    }
                    else
                    {
                        Console.WriteLine("⚠️  injectTocPageNumbers function not defined in page context");
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"⚠️  Error calling injectTocPageNumbers: {ex.Message}");
                }

            }
            catch (WaitTaskTimeoutException)
            {
                Console.WriteLine("⚠️  Paged.js timeout - proceeding with available pages");
                var pageCount = await page.EvaluateFunctionAsync<int>("() => document.querySelectorAll('.pagedjs_page').length");
                Console.WriteLine($"⚠️  Pages available: {pageCount}");
            }
            
            // Generate PDF with specified options
            Console.WriteLine("  - Generating PDF from rendered pages...");
            if (_debugMode)
            {
                Console.WriteLine($"  - Format: {pdfOptions.Format}");
                Console.WriteLine($"  - Landscape: {pdfOptions.Landscape}");
                Console.WriteLine($"  - DisplayHeaderFooter: {pdfOptions.DisplayHeaderFooter}");
            }

            var pdfStream = await page.PdfStreamAsync(pdfOptions);
            using (var memoryStream = new MemoryStream())
            {
                await pdfStream.CopyToAsync(memoryStream);
                Console.WriteLine("  - PDF generation complete");
                return memoryStream.ToArray();
            }
        }
        catch (Exception ex)
        {
            throw new Exception($"PDF conversion failed: {ex.GetType().Name}: {ex.Message}\nStack trace: {ex.StackTrace}", ex);
        }
    }
}

