using QuestPDF.Fluent;
using QuestPDF.Infrastructure;
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

    static UnitPdfExporter()
    {
        QuestPDF.Settings.License = LicenseType.Community;
    }

    public UnitPdfExporter(string? templatePath = null)
    {
        var defaultTemplatePath = templatePath ?? Path.Combine(
            Directory.GetCurrentDirectory(), 
            "..", "..", "data", "templates", "unit-page.html");
        
        _templateRenderer = new SeribanTemplateRenderer(defaultTemplatePath);
    }

    /// <summary>
    /// Exports a list of units to a PDF file with one page per unit.
    /// </summary>
    public void ExportUnitsToPdf(List<Unit> units, Dictionary<Guid, UnitLocation> locations, string outputPath)
    {
        var document = Document.Create(container =>
        {
            foreach (var unit in units.OrderBy(u => u.Number))
            {
                locations.TryGetValue(unit.LocationId, out var location);

                container.Page(page =>
                {
                    page.Margin(20);

                    // Render the unit page using the Scriban template
                    var html = _templateRenderer.RenderUnitPage(unit, location);
                    
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
    /// </summary>
    public void ExportUnitsToHtml(List<Unit> units, Dictionary<Guid, UnitLocation> locations, string outputPath)
    {
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
        html.AppendLine("        @media print { body { padding: 0; margin: 0; background: white; } .unit-page { margin-bottom: 0; box-shadow: none; } }");
        html.AppendLine("    </style>");
        html.AppendLine("</head>");
        html.AppendLine("<body>");

        foreach (var unit in units.OrderBy(u => u.Number))
        {
            locations.TryGetValue(unit.LocationId, out var location);
            var rendered = _templateRenderer.RenderUnitPage(unit, location);
            
            html.AppendLine("    <div class=\"unit-page\">");
            html.AppendLine(rendered);
            html.AppendLine("    </div>");
        }

        html.AppendLine("</body>");
        html.AppendLine("</html>");

        File.WriteAllText(outputPath, html.ToString());
    }

    /// <summary>
    /// Renders the Scriban template output to PDF by parsing HTML elements and applying styles.
    /// Uses HtmlAgilityPack for proper HTML parsing and style extraction.
    /// </summary>
    private void RenderTemplateContent(ColumnDescriptor column, string html)
    {
        var doc = new HtmlDocument();
        doc.LoadHtml(html);

        // Process only meaningful content elements (headers and paragraphs), not wrapper divs
        var elements = doc.DocumentNode.SelectNodes("//h1 | //h2 | //h3 | //h4 | //h5 | //h6 | //p");
        
        if (elements == null || elements.Count == 0)
            return;

        foreach (var element in elements)
        {
            // Skip comment nodes and empty elements
            if (element.NodeType == HtmlNodeType.Comment)
                continue;

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
            var isBold = styleAttr.Contains("font-weight: bold");
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
            var tagName = element.Name.ToLower();
            if (tagName == "h1" || tagName == "h2" || tagName == "h3")
            {
                column.Item().PaddingBottom(10);
            }
            else if (tagName == "p")
            {
                column.Item().PaddingBottom(5);
            }
        }
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
}
