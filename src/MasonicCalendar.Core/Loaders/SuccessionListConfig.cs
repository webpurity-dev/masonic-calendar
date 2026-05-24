namespace MasonicCalendar.Core.Loaders;

/// <summary>
/// Configuration for succession list section.
/// Contains table definitions for succession lists (PGM, DPGM, etc.)
/// </summary>
public class SuccessionListConfig
{
    public List<SuccessionTable>? Tables { get; set; }
}

/// <summary>
/// Defines a single succession table (e.g., Provincial Grand Masters)
/// </summary>
public class SuccessionTable
{
    public string? Title { get; set; }
    public string? Source { get; set; }
    public List<FieldMapping>? Fields { get; set; }
    public string? FontSize { get; set; }
    public List<string>? ColumnWidths { get; set; }
    public List<TableColumn>? Columns { get; set; }
    public string? TableCaption { get; set; }
}

/// <summary>
/// Defines a column in the succession table
/// </summary>
public class TableColumn
{
    public string? Name { get; set; }
    public string? CsvColumn { get; set; }
    public string? Width { get; set; }
    public string? Alignment { get; set; }
}
