namespace MasonicCalendar.Tests;

using MasonicCalendar.Core.Domain;
using MasonicCalendar.Core.Loaders;
using MasonicCalendar.Core.Renderers.Utilities;

public class UnitModelBuilderRowCountTests
{
    [Theory]
    [InlineData(0, 0)]
    [InlineData(1, 1)]
    [InlineData(3, 1)]
    [InlineData(4, 2)]
    [InlineData(6, 2)]
    [InlineData(7, 3)]
    public void RegularMembers_UseTallestOfThreeColumns(int memberCount, int expectedRows)
    {
        var unit = CreateUnit();
        unit.Members = CreateMembers(memberCount);

        var result = UnitModelBuilder.CalculateDisplayRowCount(unit);

        Assert.Equal(expectedRows, result.MemberRows);
        Assert.Equal("Regular (3 columns)", result.MemberLayout);
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(1, 1)]
    [InlineData(3, 1)]
    [InlineData(4, 2)]
    [InlineData(6, 2)]
    [InlineData(7, 3)]
    public void HonoraryMembers_RenderThreePerRow(int honoraryCount, int expectedRows)
    {
        var unit = CreateUnit();
        unit.HonoraryMembers = Enumerable.Range(0, honoraryCount)
            .Select(index => new SchemaHonoraryMember { Name = $"Honorary {index}" })
            .ToList();

        var result = UnitModelBuilder.CalculateDisplayRowCount(unit);

        Assert.Equal(expectedRows, result.HonoraryMemberRows);
    }

    [Fact]
    public void Officers_ExcludePlaceholdersAndConfiguredHiddenVacancies()
    {
        var unit = CreateUnit();
        unit.Officers =
        [
            new SchemaOfficer { Name = "Appointed", Office = "WM", Position = "Master" },
            new SchemaOfficer { Name = "Vacant", Office = "Stwd", Position = "Steward" },
            new SchemaOfficer { Name = "Vacant", Office = "Stwd", Position = "Steward" },
            new SchemaOfficer { Name = "Placeholder", Office = "0", Position = "Unused" }
        ];
        var rules = new List<HideNotAppointedRule>
        {
            new() { Position = "Steward", Count = 1 }
        };

        var result = UnitModelBuilder.CalculateDisplayRowCount(unit, rules);

        Assert.Equal(2, result.OfficerCount);
        Assert.Equal(1, result.OfficerRows);
    }

    [Fact]
    public void Officers_ExcludeExactConfiguredHiddenNameFromModelAndRowCount()
    {
        var previousHiddenName = UnitModelBuilder.ConfiguredHiddenOfficerName;
        try
        {
            UnitModelBuilder.ConfiguredHiddenOfficerName = "Ignore";
            var unit = CreateUnit();
            unit.Officers =
            [
                new SchemaOfficer { Name = " ignore ", Office = "Org", Position = "Organist" },
                new SchemaOfficer { Name = "Ignore Smith", Office = "Gst Org", Position = "Guest Organist" }
            ];

            var model = UnitModelBuilder.BuildModel(unit);
            var officers = Assert.IsType<List<Dictionary<string, object?>>>(model["officers"]);
            var rowCount = UnitModelBuilder.CalculateDisplayRowCount(unit);

            var officer = Assert.Single(officers);
            Assert.Equal("Ignore Smith", officer["name"]);
            Assert.Equal(1, rowCount.OfficerCount);
            Assert.Equal(1, rowCount.OfficerRows);
        }
        finally
        {
            UnitModelBuilder.ConfiguredHiddenOfficerName = previousHiddenName;
        }
    }

    [Fact]
    public void Officers_HideConfiguredOfficeOnlyWhenVacantAndUnitTypeMatches()
    {
        var previousRules = UnitModelBuilder.ConfiguredHideOfficerIfVacantRules;
        try
        {
            UnitModelBuilder.ConfiguredHideOfficerIfVacantRules =
            [
                new HideOfficerIfVacantRule { UnitType = "Craft", Officers = ["Org"] }
            ];
            var craftUnit = CreateUnit();
            craftUnit.UnitType = "Craft";
            craftUnit.Officers =
            [
                new SchemaOfficer { Name = "Vacant", Office = "Org", Position = "Organist" },
                new SchemaOfficer { Name = "Appointed Organist", Office = "Org", Position = "Organist" },
                new SchemaOfficer { Name = "Vacant", Office = "Gst Org", Position = "Guest Organist" }
            ];
            var raUnit = CreateUnit();
            raUnit.UnitType = "RA";
            raUnit.Officers = [new SchemaOfficer { Name = "Vacant", Office = "Org", Position = "Organist" }];

            var craftModel = UnitModelBuilder.BuildModel(craftUnit);
            var craftOfficers = Assert.IsType<List<Dictionary<string, object?>>>(craftModel["officers"]);
            var craftRowCount = UnitModelBuilder.CalculateDisplayRowCount(craftUnit);
            var raModel = UnitModelBuilder.BuildModel(raUnit);
            var raOfficers = Assert.IsType<List<Dictionary<string, object?>>>(raModel["officers"]);

            Assert.Equal(2, craftOfficers.Count);
            Assert.Contains(craftOfficers, officer => Equals(officer["name"], "Appointed Organist"));
            Assert.Contains(craftOfficers, officer => Equals(officer["position"], "Guest Organist"));
            Assert.Equal(2, craftRowCount.OfficerCount);
            Assert.Single(raOfficers);
        }
        finally
        {
            UnitModelBuilder.ConfiguredHideOfficerIfVacantRules = previousRules;
        }
    }

    [Fact]
    public void GroupedMembers_SumTallestColumnInEachGroup()
    {
        var unit = CreateUnit();
        unit.Members =
        [
            .. CreateMembers(3, "First"),
            .. CreateMembers(4, "Second"),
            new SchemaMember { Name = "Not rendered", Grouping = null }
        ];

        var result = UnitModelBuilder.CalculateDisplayRowCount(unit);

        Assert.Equal(7, result.MemberCount);
        Assert.Equal(2, result.MemberGroupCount);
        Assert.Equal(4, result.MemberRows);
        Assert.Equal("Grouped (2 columns per group)", result.MemberLayout);
    }

    [Fact]
    public void TotalRows_SumAllDisplayedCategories()
    {
        var unit = CreateUnit();
        unit.Officers = Enumerable.Range(0, 5)
            .Select(index => new SchemaOfficer { Name = $"Officer {index}", Office = $"O{index}" })
            .ToList();
        unit.PastMasters = Enumerable.Range(0, 2)
            .Select(index => new SchemaPastMaster { Name = $"Past {index}" })
            .ToList();
        unit.JoinPastMasters = [new SchemaJoinPastMaster { Name = "Joining" }];
        unit.Members = CreateMembers(7);
        unit.HonoraryMembers = Enumerable.Range(0, 4)
            .Select(index => new SchemaHonoraryMember { Name = $"Honorary {index}" })
            .ToList();

        var result = UnitModelBuilder.CalculateDisplayRowCount(unit);

        Assert.Equal(19, result.TotalPersonCount);
        Assert.Equal(11, result.TotalDisplayedRows);
    }

    private static SchemaUnit CreateUnit() => new() { Number = 1, Name = "Test Unit" };

    private static List<SchemaMember> CreateMembers(int count, string? grouping = null) =>
        Enumerable.Range(0, count)
            .Select(index => new SchemaMember { Name = $"Member {grouping} {index}", Grouping = grouping })
            .ToList();
}