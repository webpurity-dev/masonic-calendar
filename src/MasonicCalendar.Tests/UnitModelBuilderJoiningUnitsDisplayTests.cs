namespace MasonicCalendar.Tests;

using MasonicCalendar.Core.Domain;
using MasonicCalendar.Core.Loaders;
using MasonicCalendar.Core.Renderers.Utilities;
using Scriban;

public class UnitModelBuilderJoiningUnitsDisplayTests
{
    [Fact]
    public void MasterLayout_LoadsJoiningPastMastersDisplayConfiguration()
    {
        var repositoryRoot = FindRepositoryRoot();
        var loader = new DocumentLayoutLoader(Path.Combine(repositoryRoot, "document"));

        var result = loader.LoadMasterLayout("master_v1");

        Assert.True(result.Success, result.Error);
        var display = Assert.IsType<JoiningPastMastersDisplay>(result.Data?.UiPreferences?.JoiningPastMastersDisplay);
        Assert.Equal(46, display.RowsPerTable);
        Assert.Equal(1, display.JoiningUnitsThreshold);
        Assert.True(display.HideExceededJoiningUnits);
        Assert.False(display.ConsolidateExceededJoiningUnits);
        Assert.Equal("X unit(s)", display.ConsolidateText);
    }

    [Theory]
    [InlineData(3, false, true, "1,2,3")]
    [InlineData(3, false, true, "4 unit(s)")]
    [InlineData(3, true, true, "1,2,3")]
    [InlineData(3, true, false, "1,2,3")]
    [InlineData(3, false, false, "1,2,3,4")]
    public void JoiningUnits_ApplyConfiguredDisplayBehavior(
        int threshold,
        bool hide,
        bool consolidate,
        string? expected)
    {
        var previousDisplay = UnitModelBuilder.ConfiguredJoiningPastMastersDisplay;
        try
        {
            UnitModelBuilder.ConfiguredJoiningPastMastersDisplay = new JoiningPastMastersDisplay
            {
                JoiningUnitsThreshold = threshold,
                HideExceededJoiningUnits = hide,
                ConsolidateExceededJoiningUnits = consolidate,
                ConsolidateText = "X unit(s)"
            };
            var pastUnits = expected == "1,2,3" ? "1,2,3" : "1,2,3,4";

            Assert.Equal(expected, GetJoiningUnitsDisplay(pastUnits));
        }
        finally
        {
            UnitModelBuilder.ConfiguredJoiningPastMastersDisplay = previousDisplay;
        }
    }

    [Fact]
    public void JoiningUnits_ReplaceXInConfiguredConsolidationText()
    {
        var previousDisplay = UnitModelBuilder.ConfiguredJoiningPastMastersDisplay;
        try
        {
            UnitModelBuilder.ConfiguredJoiningPastMastersDisplay = new JoiningPastMastersDisplay
            {
                JoiningUnitsThreshold = 2,
                ConsolidateExceededJoiningUnits = true,
                ConsolidateText = "Joined from X lodges"
            };

            Assert.Equal("Joined from 4 lodges", GetJoiningUnitsDisplay("1,2,3,4"));
        }
        finally
        {
            UnitModelBuilder.ConfiguredJoiningPastMastersDisplay = previousDisplay;
        }
    }

    [Theory]
    [InlineData(46, 46)]
    [InlineData(47, 46, 1)]
    [InlineData(92, 46, 46)]
    [InlineData(93, 46, 46, 1)]
    public void JoiningPastMasters_ChunkAtConfiguredTableBoundary(int rowCount, params int[] expectedChunkSizes)
    {
        var previousDisplay = UnitModelBuilder.ConfiguredJoiningPastMastersDisplay;
        try
        {
            UnitModelBuilder.ConfiguredJoiningPastMastersDisplay = new JoiningPastMastersDisplay { RowsPerTable = 46 };

            var model = UnitModelBuilder.BuildModel(CreateUnitWithJoiningPastMasters(rowCount));
            var tables = Assert.IsType<List<List<Dictionary<string, object?>>>>(model["joiningPastMasterTables"]);

            Assert.Equal(expectedChunkSizes, tables.Select(table => table.Count));
            Assert.Equal(
                Enumerable.Range(1, rowCount).Select(index => $"JPM-{index:D3}"),
                tables.SelectMany(table => table).Select(row => row["dataId"] as string));
        }
        finally
        {
            UnitModelBuilder.ConfiguredJoiningPastMastersDisplay = previousDisplay;
        }
    }

    [Fact]
    public void JoiningPastMasters_DisabledChunkingRetainsSingleTable()
    {
        var previousDisplay = UnitModelBuilder.ConfiguredJoiningPastMastersDisplay;
        try
        {
            UnitModelBuilder.ConfiguredJoiningPastMastersDisplay = new JoiningPastMastersDisplay { RowsPerTable = 0 };

            var model = UnitModelBuilder.BuildModel(CreateUnitWithJoiningPastMasters(93));
            var tables = Assert.IsType<List<List<Dictionary<string, object?>>>>(model["joiningPastMasterTables"]);

            Assert.Equal(93, Assert.Single(tables).Count);
        }
        finally
        {
            UnitModelBuilder.ConfiguredJoiningPastMastersDisplay = previousDisplay;
        }
    }

    [Fact]
    public void JoiningPastMasters_TemplateStartsEveryAdditionalTableOnNewPage()
    {
        var previousDisplay = UnitModelBuilder.ConfiguredJoiningPastMastersDisplay;
        try
        {
            UnitModelBuilder.ConfiguredJoiningPastMastersDisplay = new JoiningPastMastersDisplay { RowsPerTable = 46 };
            var model = UnitModelBuilder.BuildModel(CreateUnitWithJoiningPastMasters(93));
            var templatePath = Path.Combine(FindRepositoryRoot(), "document", "templates", "_data-driven", "unit-page.html");
            var template = Template.Parse(File.ReadAllText(templatePath));

            Assert.False(template.HasErrors, string.Join(Environment.NewLine, template.Messages));
            var html = template.Render(model);

            Assert.Equal(3, System.Text.RegularExpressions.Regex.Matches(html, "<table ").Count);
            Assert.Equal(2, System.Text.RegularExpressions.Regex.Matches(html, "class=\"page-break-before\"").Count);
        }
        finally
        {
            UnitModelBuilder.ConfiguredJoiningPastMastersDisplay = previousDisplay;
        }
    }

    private static string? GetJoiningUnitsDisplay(string pastUnits)
    {
        var unit = new SchemaUnit
        {
            Number = 1,
            Name = "Test Unit",
            JoinPastMasters =
            [
                new SchemaJoinPastMaster { Name = "Joining Member", PastUnits = pastUnits }
            ]
        };
        var model = UnitModelBuilder.BuildModel(unit);
        var joiningPastMasters = Assert.IsType<List<Dictionary<string, object?>>>(model["joiningPastMasters"]);

        return Assert.Single(joiningPastMasters)["pastUnits"] as string;
    }

    private static SchemaUnit CreateUnitWithJoiningPastMasters(int count)
    {
        return new SchemaUnit
        {
            Number = 1,
            Name = "Test Unit",
            JoinPastMasters = Enumerable.Range(1, count)
                .Select(index => new SchemaJoinPastMaster
                {
                    Reference = $"JPM-{index:D3}",
                    Name = $"Joining Member {index:D3}"
                })
                .ToList()
        };
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory != null && !File.Exists(Path.Combine(directory.FullName, "MasonicCalendar.sln")))
            directory = directory.Parent;

        return directory?.FullName ?? throw new DirectoryNotFoundException("Could not locate repository root.");
    }
}