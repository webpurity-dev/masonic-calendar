namespace MasonicCalendar.Tests;

using MasonicCalendar.Core.Domain;
using MasonicCalendar.Core.Loaders;
using MasonicCalendar.Core.Renderers.Utilities;

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
        Assert.Equal(2, display.JoiningUnitsThreshold);
        Assert.True(display.HideExceededJoiningUnits);
        Assert.True(display.ConsolidateExceededJoiningUnits);
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

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory != null && !File.Exists(Path.Combine(directory.FullName, "MasonicCalendar.sln")))
            directory = directory.Parent;

        return directory?.FullName ?? throw new DirectoryNotFoundException("Could not locate repository root.");
    }
}