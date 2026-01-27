using QuestPDF.Fluent;
using QuestPDF.Infrastructure;
using QuestPDF.Drawing;
using MasonicCalendar.Core.Domain;
using HtmlAgilityPack;
using Unit = MasonicCalendar.Core.Domain.Unit;

namespace MasonicCalendar.Export.Pdf;

/// <summary>
/// Exports units as PDF pages using Scriban HTML templates.
/// </summary>
public class UnitPdfExporter
{
    private readonly SeribanTemplateRenderer _templateRenderer;
    private readonly string _pageSize;

    static UnitPdfExporter()
    {
        QuestPDF.Settings.License = LicenseType.Community;
    }

    public UnitPdfExporter(string? templatePath = null, string pageSize = "A4")
    {
        var defaultTemplatePath = templatePath ?? Path.Combine(
            AppContext.BaseDirectory, 
            "..", "..", "..", "..", "..", "data", "templates", "unit-page.html");
        
        _templateRenderer = new SeribanTemplateRenderer(defaultTemplatePath);
        _pageSize = pageSize;
    }

    /// <summary>
    /// Exports a list of units to a PDF file with one page per unit.
    /// Includes a table of contents at the beginning (if more than one unit).
    /// </summary>
    public void ExportUnitsToPdf(List<Unit> units, Dictionary<Guid, UnitLocation> locations, List<UnitOfficer> unitOfficers, Dictionary<Guid, Officer> officers, List<UnitPastMaster> pastMasters, List<UnitPMI> joiningPastMasters, List<UnitMember> members, List<UnitHonrary> honoraryMembers, string outputPath)
    {
        var orderedUnits = units.OrderBy(u => u.Number).ToList();
        
        var document = Document.Create(container =>
        {
            // Add table of contents if multiple units
            if (orderedUnits.Count > 1)
            {
                container.Page(page =>
                {
                    var (width, height, margin) = _pageSize switch
                    {
                        "A5" => (419.53f, 595.28f, 10f),
                        "A6" => (297.64f, 419.53f, 8f),
                        _ => (595.28f, 841.89f, 20f)
                    };
                    page.Size(width, height);
                    page.Margin(margin);

                    page.Content().Column(column =>
                    {
                        column.Item().Text("Table of Contents").FontSize(16).Bold().AlignCenter();
                        column.Item().PaddingBottom(20);

                        int pageNumber = 2; // TOC is page 1, units start at page 2
                        foreach (var unit in orderedUnits)
                        {
                            // Estimate unit content to approximate pages needed
                            // Most units fit on 1 page, some with many officers might need 2
                            var unitsOfficers = unitOfficers.Where(uo => uo.UnitId == unit.Id).Count();
                            var pastMastersCount = pastMasters.Where(pm => pm.UnitId == unit.Id).Count();
                            var estimatedPages = (unitsOfficers > 20 || pastMastersCount > 30) ? 2 : 1;

                            // Render TOC entry - format: "Number Name" with dotted line and page number
                            // Font size reduced by 40% (10pt → 6pt)
                            var unitLabel = $"{unit.Number} {unit.Name}";
                            var unitDestination = $"unit-{unit.Id:N}";
                            
                            column.Item().Hyperlink(unitDestination).Row(row =>
                            {
                                row.RelativeItem().Text(unitLabel).FontSize(6);
                                row.ConstantItem(40).Text(pageNumber.ToString()).FontSize(6).AlignRight();
                            });

                            pageNumber += estimatedPages;
                        }
                    });
                });
            }

            // Add unit pages
            foreach (var unit in orderedUnits)
            {
                locations.TryGetValue(unit.LocationId, out var location);
                
                // Get officers for this unit and enrich with Officer details
                var unitsOfficers = unitOfficers
                    .Where(uo => uo.UnitId == unit.Id)
                    .Select(uo =>
                    {
                        if (officers.TryGetValue(uo.OfficerId, out var officer))
                            uo.Officer = officer;
                        return uo;
                    })
                    .ToList();

                // Get past masters for this unit
                var unitsPastMasters = pastMasters
                    .Where(pm => pm.UnitId == unit.Id)
                    .ToList();

                // Get joining past masters for this unit
                var unitsJoiningPastMasters = joiningPastMasters
                    .Where(jpm => jpm.UnitId == unit.Id)
                    .ToList();

                // Get members for this unit
                var unitsMembers = members
                    .Where(m => m.UnitId == unit.Id)
                    .ToList();

                // Get honorary members for this unit
                var unitsHonoraryMembers = honoraryMembers
                    .Where(hm => hm.UnitId == unit.Id)
                    .ToList();

                container.Page(page =>
                {
                    // Set page size based on configuration
                    // Standard page sizes in points (1 inch = 72 points)
                    var (width, height, margin) = _pageSize switch
                    {
                        "A5" => (419.53f, 595.28f, 10f),   // A5: 148x210mm - smaller margins
                        "A6" => (297.64f, 419.53f, 8f),    // A6: 105x148mm - minimal margins
                        _ => (595.28f, 841.89f, 20f)       // A4: 210x297mm - standard margins
                    };
                    page.Size(width, height);
                    
                    page.Margin(margin);

                    // Render the unit page using the Scriban template
                    var html = _templateRenderer.RenderUnitPage(unit, location, unitsOfficers, unitsPastMasters, unitsJoiningPastMasters, unitsMembers, unitsHonoraryMembers);
                    
                    // Parse and display the rendered template content
                    page.Content().Column(column =>
                    {
                        RenderTemplateContent(column, html);
                    });
                });
            }
        });

        document.GeneratePdf(outputPath);
    }

    /// <summary>
    /// Exports a list of units to an HTML file with one page per unit.
    /// Useful for verifying layout and content before PDF generation.
    /// Includes a table of contents with clickable links for multi-unit documents.
    /// </summary>
    public void ExportUnitsToHtml(List<Unit> units, Dictionary<Guid, UnitLocation> locations, List<UnitOfficer> unitOfficers, Dictionary<Guid, Officer> officers, List<UnitPastMaster> pastMasters, List<UnitPMI> joiningPastMasters, List<UnitMember> members, List<UnitHonrary> honoraryMembers, string outputPath)
    {
        var orderedUnits = units.OrderBy(u => u.Number).ToList();
        var html = new System.Text.StringBuilder();
        
        html.AppendLine("<!DOCTYPE html>");
        html.AppendLine("<html lang=\"en\">");
        html.AppendLine("<head>");
        html.AppendLine("    <meta charset=\"UTF-8\">");
        html.AppendLine("    <meta name=\"viewport\" content=\"width=device-width, initial-scale=1.0\">");
        html.AppendLine("    <title>Masonic Calendar Units</title>");
        html.AppendLine("    <style>");
        html.AppendLine("        body { font-family: Arial, sans-serif; margin: 0; padding: 10px; background: #f5f5f5; }");
        html.AppendLine("        .unit-page { page-break-after: always; background: white; padding: 40px; margin-bottom: 20px; box-shadow: 0 2px 4px rgba(0,0,0,0.1); }");
        html.AppendLine("        .toc-page { page-break-after: always; background: white; padding: 40px; margin-bottom: 20px; box-shadow: 0 2px 4px rgba(0,0,0,0.1); }");
        html.AppendLine("        .toc-title { font-size: 24px; font-weight: bold; text-align: center; margin-bottom: 30px; }");
        html.AppendLine("        .toc-entry { margin: 8px 0; display: flex; justify-content: space-between; border-bottom: 1px dotted #ccc; padding-bottom: 4px; }");
        html.AppendLine("        .toc-entry a { text-decoration: none; color: #0066cc; flex: 1; }");
        html.AppendLine("        .toc-entry a:hover { text-decoration: underline; }");
        html.AppendLine("        .toc-page-number { margin-left: 20px; text-align: right; min-width: 40px; }");
        html.AppendLine("        @media print { body { padding: 0; margin: 0; background: white; } .unit-page, .toc-page { margin-bottom: 0; box-shadow: none; } }");
        html.AppendLine("    </style>");
        html.AppendLine("</head>");
        html.AppendLine("<body>");

        // Add table of contents if multiple units
        if (orderedUnits.Count > 1)
        {
            html.AppendLine("    <div class=\"toc-page\">");
            html.AppendLine("        <div class=\"toc-title\">Table of Contents</div>");
            
            int pageNumber = 2; // TOC is page 1, units start at page 2
            foreach (var unit in orderedUnits)
            {
                // Estimate unit content to approximate pages needed
                var unitsOfficers = unitOfficers.Where(uo => uo.UnitId == unit.Id).Count();
                var pastMastersCount = pastMasters.Where(pm => pm.UnitId == unit.Id).Count();
                var estimatedPages = (unitsOfficers > 20 || pastMastersCount > 30) ? 2 : 1;

                var unitLabel = $"{unit.Number} {unit.Name}";
                var unitAnchor = $"unit-{unit.Id:N}";
                
                html.AppendLine($"        <div class=\"toc-entry\">");
                html.AppendLine($"            <a href=\"#{unitAnchor}\">{HtmlEncode(unitLabel)}</a>");
                html.AppendLine($"            <div class=\"toc-page-number\">{pageNumber}</div>");
                html.AppendLine($"        </div>");
                
                pageNumber += estimatedPages;
            }
            
            html.AppendLine("    </div>");
        }

        // Add unit pages with anchors
        foreach (var unit in orderedUnits)
        {
            locations.TryGetValue(unit.LocationId, out var location);
            
            // Get officers for this unit and enrich with Officer details
            var unitsOfficers = unitOfficers
                .Where(uo => uo.UnitId == unit.Id)
                .Select(uo =>
                {
                    if (officers.TryGetValue(uo.OfficerId, out var officer))
                        uo.Officer = officer;
                    return uo;
                })
                .ToList();

            // Get past masters for this unit
            var unitsPastMasters = pastMasters
                .Where(pm => pm.UnitId == unit.Id)
                .ToList();

            // Get joining past masters for this unit
            var unitsJoiningPastMasters = joiningPastMasters
                .Where(jpm => jpm.UnitId == unit.Id)
                .ToList();

            // Get members for this unit
            var unitsMembers = members
                .Where(m => m.UnitId == unit.Id)
                .ToList();

            // Get honorary members for this unit
            var unitsHonoraryMembers = honoraryMembers
                .Where(hm => hm.UnitId == unit.Id)
                .ToList();
            
            var rendered = _templateRenderer.RenderUnitPage(unit, location, unitsOfficers, unitsPastMasters, unitsJoiningPastMasters, unitsMembers, unitsHonoraryMembers);
            
            var unitAnchor = $"unit-{unit.Id:N}";
            html.AppendLine($"    <div class=\"unit-page\" id=\"{unitAnchor}\">");
            html.AppendLine(rendered);
            html.AppendLine("    </div>");
        }

        html.AppendLine("</body>");
        html.AppendLine("</html>");

        File.WriteAllText(outputPath, html.ToString());
    }

    /// <summary>
    /// HTML encodes a string to safely display in HTML.
    /// </summary>
    private static string HtmlEncode(string text)
    {
        return System.Net.WebUtility.HtmlEncode(text);
    }

    /// <summary>
    /// Renders the Scriban template output to PDF by parsing HTML elements and applying styles.
    /// Uses HtmlAgilityPack for proper HTML parsing and style extraction.
    /// </summary>
    private void RenderTemplateContent(ColumnDescriptor column, string html)
    {
        var doc = new HtmlDocument();
        doc.LoadHtml(html);

        // Process all elements, starting with the root body or top-level content
        // Look for h1, h2, h3, h4, h5, h6, p, div, table elements
        ProcessHtmlElements(column, doc.DocumentNode);
    }

    /// <summary>
    /// Recursively process HTML elements and render appropriate content.
    /// </summary>
    private void ProcessHtmlElements(ColumnDescriptor column, HtmlNode parentNode)
    {
        foreach (var element in parentNode.ChildNodes)
        {
            // Skip text nodes and comments
            if (element.NodeType == HtmlNodeType.Text || element.NodeType == HtmlNodeType.Comment)
                continue;

            var tagName = element.Name.ToLower();

            // Handle heading and paragraph elements
            if (tagName == "h1" || tagName == "h2" || tagName == "h3" || tagName == "h4" || tagName == "h5" || tagName == "h6" || tagName == "p")
            {
                var text = ExtractTextFromElement(element);
                if (string.IsNullOrWhiteSpace(text))
                    continue;

                var styleAttr = element.GetAttributeValue("style", "");

                // Extract font size (default 12pt)
                float fontSize = 12;
                var fontSizeMatch = System.Text.RegularExpressions.Regex.Match(styleAttr, @"font-size:\s*(\d+(?:\.\d+)?)(pt|px|em)?", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                if (fontSizeMatch.Success && float.TryParse(fontSizeMatch.Groups[1].Value, out var size))
                {
                    fontSize = size;
                }

                // Extract styles
                var isBold = styleAttr.Contains("font-weight");
                var isCentered = styleAttr.Contains("text-align: center");
                var color = ExtractColor(styleAttr);

                // Render the text item
                var item = column.Item();
                if (isCentered)
                    item = item.AlignCenter();

                var textItem = item.Text(text).FontSize(fontSize);

                if (isBold)
                    textItem = textItem.Bold();

                if (color != null)
                    textItem = textItem.FontColor(color);

                // Add spacing after elements
                if (tagName.StartsWith("h"))
                {
                    column.Item().PaddingBottom(10);
                }
                else if (tagName == "p")
                {
                    column.Item().PaddingBottom(5);
                }
            }
            // Handle table elements
            else if (tagName == "table")
            {
                RenderTable(column, element);
            }
            // Handle flex container divs with tables inside
            else if (tagName == "div")
            {
                var styleAttr = element.GetAttributeValue("style", "");
                
                // Extract margin-top if present
                var marginTop = ExtractPixelValue(styleAttr, "margin-top");
                if (marginTop > 0)
                {
                    column.Item().PaddingTop(marginTop);
                }
                
                if (styleAttr.Contains("display: flex"))
                {
                    RenderFlexContainer(column, element);
                }
                else
                {
                    // Recurse into nested divs
                    ProcessHtmlElements(column, element);
                }
            }
            // Recurse into other containers
            else if (tagName == "body" || tagName == "html" || tagName == "tbody")
            {
                ProcessHtmlElements(column, element);
            }
        }
    }

    /// <summary>
    /// Renders a flex container with tables inside (for officers layout).
    /// </summary>
    private void RenderFlexContainer(ColumnDescriptor column, HtmlNode flexDiv)
    {
        var tables = flexDiv.SelectNodes(".//table");
        if (tables == null || tables.Count == 0)
            return;

        // Center the table container with natural width (not full width)
        column.Item().AlignCenter().Element(container =>
        {
            container.Column(col =>
            {
                col.Item().Row(row =>
                {
                    foreach (var table in tables)
                    {
                        // Render tables with padding between them, allowing natural width
                        row.RelativeItem().PaddingHorizontal(5).Element(tableContainer =>
                        {
                            tableContainer.Column(innerCol =>
                            {
                                RenderTableWithColumn(innerCol, table);
                            });
                        });
                    }
                });
            });
        });
    }

    /// <summary>
    /// Renders an HTML table to PDF as formatted text rows using a column.
    /// </summary>
    private void RenderTableWithColumn(ColumnDescriptor column, HtmlNode tableNode)
    {
        var rows = tableNode.SelectNodes(".//tr");
        if (rows == null || rows.Count == 0)
            return;

        // Extract font size from table style (default 9pt)
        var tableStyle = tableNode.GetAttributeValue("style", "");
        float fontSize = 9;
        var fontSizeMatch = System.Text.RegularExpressions.Regex.Match(tableStyle, @"font-size:\s*(\d+(?:\.\d+)?)(pt|px|em)?", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        if (fontSizeMatch.Success && float.TryParse(fontSizeMatch.Groups[1].Value, out var size))
        {
            // Reduce font size by 27.1% for PDF (triple 10% reduction: 0.9 * 0.9 * 0.9 = 0.729)
            fontSize = size * 0.729f;
        }

        foreach (var row in rows)
        {
            var rowCells = row.SelectNodes(".//td | .//th");
            if (rowCells != null && rowCells.Count > 0)
            {
                RenderTableRow(column, rowCells, fontSize);
            }
        }
    }

    /// <summary>
    /// Renders an HTML table to PDF as formatted text rows.
    /// </summary>
    private void RenderTable(ColumnDescriptor column, HtmlNode tableNode)
    {
        RenderTableWithColumn(column, tableNode);
    }

    /// <summary>
    /// Renders a single table row as formatted text with proper column layout.
    /// </summary>
    private void RenderTableRow(ColumnDescriptor column, HtmlNodeCollection cells, float fontSize = 9)
    {
        column.Item().Row(row =>
        {
            for (int i = 0; i < cells.Count; i++)
            {
                var cell = cells[i];
                var cellText = ExtractTextFromElement(cell);
                var cellStyle = cell.GetAttributeValue("style", "");
                var isBold = cellStyle.Contains("font-weight");
                var isRightAlign = cellStyle.Contains("text-align: right");

                var cellColumn = row.RelativeItem();
                
                // Render the cell content
                cellColumn.Element(container =>
                {
                    var textItem = container.PaddingHorizontal(2).PaddingVertical(1).Text(cellText);
                    textItem.FontSize(fontSize);
                    
                    if (isBold)
                        textItem.Bold();
                    
                    if (isRightAlign)
                        textItem.AlignRight();
                });
            }
        });
    }

    /// <summary>
    /// Extracts text content from an HTML element, handling nested elements.
    /// </summary>
    private string ExtractTextFromElement(HtmlNode element)
    {
        // Get inner text and decode HTML entities
        var text = System.Net.WebUtility.HtmlDecode(element.InnerText);
        
        // Clean up whitespace
        text = System.Text.RegularExpressions.Regex.Replace(text, @"\s+", " ").Trim();

        return text;
    }

    /// <summary>
    /// Extracts hex color from CSS style attribute.
    /// </summary>
    private string? ExtractColor(string styleAttr)
    {
        var colorMatch = System.Text.RegularExpressions.Regex.Match(
            styleAttr,
            @"color:\s*(#[0-9a-fA-F]{6}|#[0-9a-fA-F]{3})",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);

        return colorMatch.Success ? colorMatch.Groups[1].Value : null;
    }

    /// <summary>
    /// Extracts a pixel value from CSS style attribute (e.g., margin-top, padding-top).
    /// </summary>
    private float ExtractPixelValue(string styleAttr, string property)
    {
        var pattern = $@"{property}:\s*(\d+(?:\.\d+)?)(px)?";
        var match = System.Text.RegularExpressions.Regex.Match(styleAttr, pattern, System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        
        if (match.Success && float.TryParse(match.Groups[1].Value, out var value))
        {
            return value;
        }
        
        return 0;
    }
}

