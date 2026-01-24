namespace MasonicCalendar.Export.Pdf;

/// <summary>
/// Represents the layout configuration for a unit page.
/// </summary>
public class UnitPageLayout
{
    public PageConfig Page { get; set; } = new();
    public HeaderConfig Header { get; set; } = new();
    public SummaryConfig Summary { get; set; } = new();
    public FooterConfig Footer { get; set; } = new();

    public static UnitPageLayout CreateDefault()
    {
        return new UnitPageLayout
        {
            Page = new PageConfig
            {
                Size = "A6",
                Margins = new Margins { Top = 20, Right = 20, Bottom = 20, Left = 20 }
            },
            Header = new HeaderConfig
            {
                Height = 120,
                UnitNumberFontSize = 10,
                UnitNumberColor = "gray",
                UnitNameFontSize = 24,
                UnitNameBold = true
            },
            Summary = new SummaryConfig
            {
                Height = 100,
                LocationNameFontSize = 14,
                LocationNameBold = true,
                AddressFontSize = 14
            },
            Footer = new FooterConfig
            {
                IncludePageNumber = true,
                IncludeDateGenerated = true
            }
        };
    }
}

public class PageConfig
{
    public string Size { get; set; } = "A6"; // A4, A5, A6, etc.
    public string Font { get; set; } = "Helvetica";
    public Margins Margins { get; set; } = new();
}

public class Margins
{
    public float Top { get; set; }
    public float Right { get; set; }
    public float Bottom { get; set; }
    public float Left { get; set; }
}

public class HeaderConfig
{
    public float Height { get; set; } = 120;
    public float UnitNumberFontSize { get; set; } = 10;
    public string UnitNumberColor { get; set; } = "gray";
    public float UnitNameFontSize { get; set; } = 24;
    public bool UnitNameBold { get; set; } = true;
}

public class SummaryConfig
{
    public float Height { get; set; } = 100;
    public float LocationNameFontSize { get; set; } = 14;
    public bool LocationNameBold { get; set; } = true;
    public float AddressFontSize { get; set; } = 14;
}

public class FooterConfig
{
    public bool IncludePageNumber { get; set; } = true;
    public bool IncludeDateGenerated { get; set; } = true;
    public float FontSize { get; set; } = 8;
}
