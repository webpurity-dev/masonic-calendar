namespace MasonicCalendar.Tests;

using MasonicCalendar.Core.Loaders;
using MasonicCalendar.Core.Services.Renderers.SectionRenderers;
using Scriban;
using System.Text.RegularExpressions;

public class ProvincialOfficersSectionRendererTests
{
    [Fact]
    public void MasterLayout_LoadsOrderOfficersDisplayConfiguration()
    {
        var repositoryRoot = FindRepositoryRoot();
        var loader = new DocumentLayoutLoader(Path.Combine(repositoryRoot, "document"));

        var result = loader.LoadMasterLayout("master_v1");

        Assert.True(result.Success, result.Error);
        var display = Assert.IsType<OrderOfficersDisplay>(result.Data?.UiPreferences?.OrderOfficersDisplay);
        Assert.Equal(30, display.RowsPerTableWithHeading);
        Assert.Equal(36, display.RowsPerTableWithoutHeading);
    }

    [Theory]
    [InlineData(30, 30)]
    [InlineData(31, 30, 1)]
    [InlineData(66, 30, 36)]
    [InlineData(67, 30, 36, 1)]
    public void Officers_WithHeadingUseSmallerFirstTable(int rowCount, params int[] expectedChunkSizes)
    {
        var officers = CreateOfficers(rowCount);

        var tables = ProvincialOfficersSectionRenderer.BuildOfficerTables(officers, 30, 36);

        Assert.Equal(expectedChunkSizes, tables.Select(table => table.Count));
        Assert.Equal(
            Enumerable.Range(1, rowCount).Select(index => $"Officer {index:D3}"),
            tables.SelectMany(table => table).Select(row => row["name"] as string));
    }

    [Theory]
    [InlineData(36, 36)]
    [InlineData(37, 36, 1)]
    [InlineData(72, 36, 36)]
    [InlineData(73, 36, 36, 1)]
    public void Officers_WithoutHeadingUseFullTableSize(int rowCount, params int[] expectedChunkSizes)
    {
        var tables = ProvincialOfficersSectionRenderer.BuildOfficerTables(CreateOfficers(rowCount), 36, 36);

        Assert.Equal(expectedChunkSizes, tables.Select(table => table.Count));
    }

    [Theory]
    [InlineData("Grand Officers", false, false, 30)]
    [InlineData("Provincial Grand Lodge", false, true, 36)]
    [InlineData("Provincial Grand Lodge", true, false, 36)]
    [InlineData("", false, false, 36)]
    public void Officers_SelectFirstTableSizeFromRenderedHeadingPage(
        string heading1,
        bool displayOfficersOnly,
        bool breakBeforeOfficers,
        int expectedRows)
    {
        var display = new OrderOfficersDisplay
        {
            RowsPerTableWithHeading = 30,
            RowsPerTableWithoutHeading = 36
        };

        var rows = ProvincialOfficersSectionRenderer.ResolveFirstTableRows(
            heading1,
            displayOfficersOnly,
            breakBeforeOfficers,
            display);

        Assert.Equal(expectedRows, rows);
    }

    [Theory]
    [InlineData(0, 36)]
    [InlineData(30, 0)]
    [InlineData(-1, 36)]
    [InlineData(30, -1)]
    public void Officers_DisabledChunkingRetainsSingleTable(int firstTableRows, int continuationTableRows)
    {
        var tables = ProvincialOfficersSectionRenderer.BuildOfficerTables(CreateOfficers(73), firstTableRows, continuationTableRows);

        Assert.Equal(73, Assert.Single(tables).Count);
    }

    [Fact]
    public void Officers_TemplateStartsEveryAdditionalTableOnNewPage()
    {
        var officers = CreateOfficers(73);
        var model = new Dictionary<string, object?>
        {
            ["officers"] = officers,
            ["officer_tables"] = ProvincialOfficersSectionRenderer.BuildOfficerTables(officers, 30, 36),
            ["heads"] = new List<object>(),
            ["deputy_heads"] = new List<object>(),
            ["district_heads"] = new List<object>(),
            ["display_officers_only"] = true,
            ["break_before_officers"] = false,
            ["override_break_before"] = false
        };
        var templatePath = Path.Combine(FindRepositoryRoot(), "document", "templates", "_data-driven", "list-officers.html");
        var template = Template.Parse(File.ReadAllText(templatePath));

        Assert.False(template.HasErrors, string.Join(Environment.NewLine, template.Messages));
        var html = template.Render(model);

        Assert.Equal(3, Regex.Matches(html, "data-id=\"officers").Count);
        Assert.Equal(2, Regex.Matches(html, "class=\"page-break-before\"").Count);
    }

    private static List<Dictionary<string, object?>> CreateOfficers(int count)
    {
        return Enumerable.Range(1, count)
            .Select(index => new Dictionary<string, object?>
            {
                ["office"] = $"Office {index:D3}",
                ["name"] = $"Officer {index:D3}",
                ["name_suffix"] = null,
                ["unit"] = index.ToString()
            })
            .ToList();
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory != null && !File.Exists(Path.Combine(directory.FullName, "MasonicCalendar.sln")))
            directory = directory.Parent;

        return directory?.FullName ?? throw new DirectoryNotFoundException("Could not locate repository root.");
    }
}