namespace MasonicCalendar.Tests;

using MasonicCalendar.Core.Domain;
using MasonicCalendar.Core.Loaders;
using MasonicCalendar.Core.Renderers.Utilities;

public class UnitModelBuilderRankDisplayTests
{
    [Fact]
    public void ConfiguredRoyalArchRanks_DisplayProvincialThenGrandRank()
    {
        var previousConfig = UnitModelBuilder.ConfiguredRankDisplay;
        try
        {
            UnitModelBuilder.ConfiguredRankDisplay = CreateRankDisplayConfig();
            var unit = CreateUnit("RA");
            unit.PastMasters =
            [
                new SchemaPastMaster
                {
                    Name = "Carter, D E",
                    ProvincialRank = "P3rdProvGPrin",
                    DateRankAccorded = 2012,
                    GrandRank = "PAGSoj",
                    GrandRankDateAccorded = 2010
                }
            ];
            unit.JoinPastMasters =
            [
                new SchemaJoinPastMaster
                {
                    Name = "Deakin, C W",
                    ProvincialRank = "P2ndProvGPrin",
                    DateRankAccorded = 2021,
                    GrandRank = "PGStB",
                    GrandRankDateAccorded = 2019
                }
            ];
            unit.HonoraryMembers =
            [
                new SchemaHonoraryMember
                {
                    Name = "Jowett, R B",
                    ProvincialRank = "P3rdProvGPrin",
                    GrandRank = "PGStB"
                }
            ];

            var model = UnitModelBuilder.BuildModel(unit);

            Assert.Equal("P3rdProvGPrin [2012], PAGSoj [2010]", GetDisplayRank(model, "pastMasters"));
            Assert.Equal("P2ndProvGPrin [2021], PGStB [2019]", GetDisplayRank(model, "joiningPastMasters"));
            Assert.Equal(", P3rdProvGPrin, PGStB", GetDisplayRank(model, "honoraryMembers"));
        }
        finally
        {
            UnitModelBuilder.ConfiguredRankDisplay = previousConfig;
        }
    }

    [Theory]
    [InlineData("RA", "ProvGChap", "PGStB [2019]")]
    [InlineData("Craft", "P3rdProvGPrin", "PGStB [2019]")]
    public void UnconfiguredRankOrUnitType_RetainsGrandRankPriority(
        string unitType, string provincialRank, string expectedDisplayRank)
    {
        var previousConfig = UnitModelBuilder.ConfiguredRankDisplay;
        try
        {
            UnitModelBuilder.ConfiguredRankDisplay = CreateRankDisplayConfig();
            var unit = CreateUnit(unitType);
            unit.PastMasters =
            [
                new SchemaPastMaster
                {
                    Name = "Test Member",
                    ProvincialRank = provincialRank,
                    DateRankAccorded = 2021,
                    GrandRank = "PGStB",
                    GrandRankDateAccorded = 2019
                }
            ];

            var model = UnitModelBuilder.BuildModel(unit);

            Assert.Equal(expectedDisplayRank, GetDisplayRank(model, "pastMasters"));
        }
        finally
        {
            UnitModelBuilder.ConfiguredRankDisplay = previousConfig;
        }
    }

    private static RankDisplay CreateRankDisplayConfig() => new()
    {
        PriorityOrder = ["grand_rank", "provincial_rank", "prov_rank_other_prov", "london_rank"],
        DisplayProvincialAndGrandRank =
        [
            new ProvincialAndGrandRankDisplayRule
            {
                UnitType = "RA",
                ProvincialRanks = ["P2ndProvGPrin", "P3rdProvGPrin"]
            }
        ],
        ShowDates = new RankDisplayShowDates
        {
            PastMasters = true,
            JoiningPastMasters = true,
            HonoraryMembers = false
        }
    };

    private static SchemaUnit CreateUnit(string unitType) => new()
    {
        Number = 5331,
        Name = "Kinson Chapter",
        UnitType = unitType
    };

    private static string? GetDisplayRank(Dictionary<string, object?> model, string collectionName)
    {
        var rows = Assert.IsType<List<Dictionary<string, object?>>>(model[collectionName]);
        return Assert.IsType<string>(rows[0]["display_rank"]);
    }
}