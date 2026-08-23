namespace MasonicCalendar.Tests;

using MasonicCalendar.Core.Domain;
using MasonicCalendar.Core.Loaders;
using MasonicCalendar.Core.Renderers.Utilities;
using Scriban;

public class UnitModelBuilderPageBreakTests
{
    [Theory]
    [InlineData("TRUE", true, true)]
    [InlineData("true", true, true)]
    [InlineData("FALSE", true, false)]
    [InlineData("", true, false)]
    [InlineData("invalid", true, false)]
    [InlineData("TRUE", false, false)]
    public async Task Loader_DefaultsMissingBlankFalseAndInvalidValuesToFalse(
        string csvValue,
        bool includeMapping,
        bool expected)
    {
        var root = Path.Combine(Path.GetTempPath(), $"masonic-calendar-{Guid.NewGuid():N}");
        var documentRoot = Path.Combine(root, "document");
        var dataRoot = Path.Combine(documentRoot, "data");
        var dataSourcesRoot = Path.Combine(documentRoot, "data_sources");

        Directory.CreateDirectory(dataRoot);
        Directory.CreateDirectory(dataSourcesRoot);

        try
        {
            await File.WriteAllTextAsync(
                Path.Combine(documentRoot, "master_v1.yaml"),
                """
                document:
                  version: "test"
                sections:
                  - section_id: "craft_units"
                    type: "data-driven"
                    data_mapping: "data_sources/test_data_source.yaml"
                """);

                        var mappingLines = new List<string>
                        {
                                "units:",
                                "  source: \"units.csv\"",
                                "  filter_field: \"Unit Type\"",
                                "  filter_value: \"Craft\"",
                                "  fields:",
                                "    - name: \"Number\"",
                                "      csv_column: \"Unit No\"",
                                "      type: \"int\"",
                                "    - name: \"Name\"",
                                "      csv_column: \"Unit Name\"",
                                "      type: \"string\""
                        };
                        if (includeMapping)
                        {
                                mappingLines.AddRange(
                                [
                                        "    - name: \"BreakBeforeMembers\"",
                                        "      csv_column: \"Break Before Members\"",
                                        "      type: \"bool\""
                                ]);
                        }
                        await File.WriteAllLinesAsync(
                                Path.Combine(dataSourcesRoot, "test_data_source.yaml"),
                                mappingLines);
            await File.WriteAllTextAsync(
                Path.Combine(dataRoot, "units.csv"),
                $"Unit Type,Unit No,Unit Name,Break Before Members{Environment.NewLine}Craft,2559,Test Unit,{csvValue}{Environment.NewLine}");

            var layoutLoader = new DocumentLayoutLoader(documentRoot);
            var dataLoader = new SchemaDataLoader(layoutLoader, dataRoot);
            var result = await dataLoader.LoadUnitsWithDataAsync("master_v1", "craft_units");

            Assert.True(result.Success, result.Error);
            Assert.Equal(expected, Assert.Single(result.Data!).BreakBeforeMembers);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public void Template_AppliesBreakClassOnlyWhenEnabled(bool grouped, bool enabled)
    {
        var unit = new SchemaUnit
        {
            Number = 2559,
            Name = "Test Unit",
            BreakBeforeMembers = enabled,
            Members =
            [
                new SchemaMember
                {
                    Name = "Test Member",
                    Grouping = grouped ? "First Group" : null
                }
            ]
        };
        var model = UnitModelBuilder.BuildModel(unit);
        var unitModel = Assert.IsType<Dictionary<string, object?>>(model["unit"]);
        Assert.Equal(enabled, unitModel["breakBeforeMembers"]);

        var templatePath = Path.Combine(FindRepositoryRoot(), "document", "templates", "_data-driven", "unit-page.html");
        var template = Template.Parse(File.ReadAllText(templatePath));
        Assert.False(template.HasErrors, string.Join(Environment.NewLine, template.Messages));

        var html = template.Render(model);

        if (enabled)
            Assert.Contains("class=\"page-break-before\"", html);
        else
            Assert.DoesNotContain("class=\"page-break-before\"", html);
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory != null && !File.Exists(Path.Combine(directory.FullName, "MasonicCalendar.sln")))
            directory = directory.Parent;

        return directory?.FullName ?? throw new DirectoryNotFoundException("Could not locate repository root.");
    }
}