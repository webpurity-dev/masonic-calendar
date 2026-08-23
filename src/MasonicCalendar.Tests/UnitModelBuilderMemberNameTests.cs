namespace MasonicCalendar.Tests;

using MasonicCalendar.Core.Domain;
using MasonicCalendar.Core.Loaders;
using MasonicCalendar.Core.Renderers.Utilities;

public class UnitModelBuilderMemberNameTests
{
    [Fact]
    public void MasterLayout_LoadsMemberNameInitialsCompactThreshold()
    {
        var repositoryRoot = FindRepositoryRoot();
        var loader = new DocumentLayoutLoader(Path.Combine(repositoryRoot, "document"));

        var result = loader.LoadMasterLayout("master_v1");

        Assert.True(result.Success, result.Error);
        Assert.True(result.Data?.UiPreferences?.MemberNameInitialsCompactThreshold is > 0);
    }

    [Fact]
    public void RegularMembers_CompactInitialsOnlyWhenConfiguredThresholdIsExceeded()
    {
        var previousThreshold = UnitModelBuilder.ConfiguredMemberNameInitialsCompactThreshold;

        try
        {
            UnitModelBuilder.ConfiguredMemberNameInitialsCompactThreshold = 22;
            Assert.Equal("Verylongsurnamehere, AB", GetRegularMemberName("Verylongsurnamehere, A B"));
            Assert.Equal("Longsurnamehere, A B C", GetRegularMemberName("Longsurnamehere, A B C"));

            UnitModelBuilder.ConfiguredMemberNameInitialsCompactThreshold = null;
            Assert.Equal("Verylongsurnamehere, A B", GetRegularMemberName("Verylongsurnamehere, A B"));
        }
        finally
        {
            UnitModelBuilder.ConfiguredMemberNameInitialsCompactThreshold = previousThreshold;
        }
    }

    [Fact]
    public void GroupedMembers_DoNotCompactInitials()
    {
        var previousThreshold = UnitModelBuilder.ConfiguredMemberNameInitialsCompactThreshold;

        try
        {
            UnitModelBuilder.ConfiguredMemberNameInitialsCompactThreshold = 22;
            var unit = CreateUnit("Verylongsurnamehere, A B", "Group");
            var model = UnitModelBuilder.BuildModel(unit);
            var groups = Assert.IsType<List<Dictionary<string, object?>>>(model["groupedMembers"]);
            var columns = Assert.IsType<List<List<Dictionary<string, object?>>>>(Assert.Single(groups)["columns"]);

            Assert.Equal("Verylongsurnamehere, A B", Assert.Single(columns[0])["name"]);
        }
        finally
        {
            UnitModelBuilder.ConfiguredMemberNameInitialsCompactThreshold = previousThreshold;
        }
    }

    private static string GetRegularMemberName(string name)
    {
        var model = UnitModelBuilder.BuildModel(CreateUnit(name));
        var columns = Assert.IsType<List<List<Dictionary<string, object?>>>>(model["memberColumns"]);
        return Assert.IsType<string>(Assert.Single(columns[0])["name"]);
    }

    private static SchemaUnit CreateUnit(string memberName, string? grouping = null) => new()
    {
        Number = 1,
        Name = "Test Unit",
        Members = [new SchemaMember { Name = memberName, Grouping = grouping }]
    };

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory != null && !File.Exists(Path.Combine(directory.FullName, "MasonicCalendar.sln")))
            directory = directory.Parent;

        return directory?.FullName ?? throw new DirectoryNotFoundException("Could not locate repository root.");
    }
}