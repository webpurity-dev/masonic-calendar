namespace MasonicCalendar.Core.Domain;

/// <summary>
/// Represents Paged.js CSS @page margin configuration for PDF rendering.
/// Separates left/right pages for mirror margin support (gutter vs outer).
/// </summary>
public class PageMargins
{
    public string? PageSize { get; set; }  // e.g., "105mm 148mm" for A6
    public PageSideMargins? RightPage { get; set; }  // Odd pages (Recto)
    public PageSideMargins? LeftPage { get; set; }   // Even pages (Verso)
    public PageSideMargins? FirstPage { get; set; }  // Cover page (no page number)
    public FooterMargins? Footer { get; set; }
}

/// <summary>
/// Margins for one side of a page (left, right, top, bottom).
/// </summary>
public class PageSideMargins
{
    public string? Top { get; set; }
    public string? Bottom { get; set; }
    public string? Left { get; set; }
    public string? Right { get; set; }
}

/// <summary>
/// Configuration for page footer (margin box) styling.
/// </summary>
public class FooterMargins
{
    public string? FontFamily { get; set; }
    public string? FontSize { get; set; }
    public string? TextAlign { get; set; }
}
