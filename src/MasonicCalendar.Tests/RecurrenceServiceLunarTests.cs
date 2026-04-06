namespace MasonicCalendar.Tests;

using MasonicCalendar.Core.Domain;
using MasonicCalendar.Core.Services;
using Xunit;

/// <summary>
/// Guards the LunarSeason and LunarSeasonBefore recurrence strategies against regressions.
///
/// Ground-truth dates verified against actual lodge meeting schedules and the
/// Royal Observatory Greenwich full moon calendar for 2026 (UK local time):
///
///   3 Jan (10:03am)  Wolf Moon        1 Feb (10:09pm)  Snow Moon
///   3 Mar (11:38am)  Worm Moon        2 Apr (03:12am)  Pink Moon   ← first of two in Apr
///   1 May (06:23pm)  Flower Moon     31 May (09:45am)  Blue Moon
///  30 Jun (12:57am)  Strawberry Moon 29 Jul (03:36pm)  Buck Moon
///  28 Aug (05:18am)  Sturgeon Moon   26 Sep (05:49pm)  Harvest Moon
///  26 Oct (04:12am)  Hunter's Moon   24 Nov (02:53pm)  Beaver Moon
///  24 Dec (01:28am)  Cold Moon
///
/// Key note: 30 Jun 12:57am BST = 29 Jun 23:57 UTC — the algorithm uses UTC, so
/// "last Tuesday on or before Jun 29 UTC" = Jun 23, which is correct.
/// </summary>
public class RecurrenceServiceLunarTests
{
    private readonly RecurrenceService _svc = new();

    // -----------------------------------------------------------------------
    // Helper: build a CalendarEvent with the fields used by LunarSeason logic
    // -----------------------------------------------------------------------
    private static CalendarEvent MakeEvent(
        string unitId,
        string strategy,
        string dayOfWeek,
        string? installationMonth = null,
        string? startMonth = null,
        string? endMonth = null)
    {
        return new CalendarEvent
        {
            Id = Guid.NewGuid().ToString(),
            UnitId = unitId,
            UnitType = "Craft",
            Title = "Meeting",
            RecurrenceType = "Monthly",
            RecurrenceStrategy = strategy,
            DayOfWeek = dayOfWeek,
            InstallationMonth = installationMonth,
            StartMonth = startMonth,
            EndMonth = endMonth
        };
    }

    // -----------------------------------------------------------------------
    // Unit 472 — Lodge of Friendship and Sincerity
    // Strategy: LunarSeason (nearest Thursday after 2nd Thursday of month)
    // Active months: Feb–Nov (LunarSeason row); Dec and Jan from a Default row.
    // -----------------------------------------------------------------------

    [Theory]
    [InlineData(2026, 4,  "2026-04-30")]  // April  — blue-moon month, correct moon is Apr 30
    [InlineData(2026, 5,  "2026-05-28")]  // May
    [InlineData(2026, 6,  "2026-06-25")]  // June
    [InlineData(2026, 7,  "2026-07-30")]  // July
    [InlineData(2026, 8,  "2026-08-27")]  // August
    [InlineData(2026, 9,  "2026-09-24")]  // September
    [InlineData(2026, 10, "2026-10-22")]  // October
    [InlineData(2026, 11, "2026-11-26")]  // November
    public void Unit472_LunarSeason_Thursday_MatchesActual(int year, int month, string expectedDate)
    {
        var evt = MakeEvent("472", "LunarSeason", "Thursday");
        var instances = _svc.ExpandEvent(evt, year, month, year, month);

        Assert.Single(instances);
        Assert.Equal(DateOnly.Parse(expectedDate), instances[0].Date);
    }

    // -----------------------------------------------------------------------
    // Unit 1266 — Lodge of Honour and Friendship
    // Strategy: LunarSeasonBefore (last Tuesday on or before end-of-month full moon)
    //           Installation month (November): 4th Tuesday instead.
    // Active months: Jan–Dec.
    // -----------------------------------------------------------------------

    [Theory]
    [InlineData(2026, 4,  "2026-04-28", false)]  // Apr 30 moon → last Tue ≤ Apr 30 = Apr 28
    [InlineData(2026, 5,  "2026-05-26", false)]  // May 31 blue moon → last Tue ≤ May 31 = May 26
    [InlineData(2026, 6,  "2026-06-23", false)]  // Jun 30 12:57am BST = Jun 29 UTC → last Tue ≤ Jun 29 = Jun 23
    [InlineData(2026, 7,  "2026-07-28", false)]  // Jul 29 moon → last Tue ≤ Jul 29 = Jul 28
    [InlineData(2026, 8,  "2026-08-25", false)]  // Aug 28 moon → last Tue ≤ Aug 28 = Aug 25
    [InlineData(2026, 9,  "2026-09-22", false)]  // Sep 26 moon → last Tue ≤ Sep 26 = Sep 22
    [InlineData(2026, 10, "2026-10-20", false)]  // Oct 26 moon → last Tue ≤ Oct 26 = Oct 20
    [InlineData(2026, 11, "2026-11-24", true)]   // Installation month → 4th Tuesday = Nov 24
    [InlineData(2026, 12, "2026-12-22", false)]  // Dec 24 moon → last Tue ≤ Dec 24 = Dec 22
    public void Unit1266_LunarSeasonBefore_Tuesday_MatchesActual(
        int year, int month, string expectedDate, bool isInstallation)
    {
        var installationMonth = isInstallation ? "Nov" : null;
        var evt = MakeEvent("1266", "LunarSeasonBefore", "Tuesday", installationMonth);
        var instances = _svc.ExpandEvent(evt, year, month, year, month);

        Assert.Single(instances);
        Assert.Equal(DateOnly.Parse(expectedDate), instances[0].Date);
        if (isInstallation)
            Assert.True(instances[0].IsInstallation);
    }

    // -----------------------------------------------------------------------
    // Edge case: blue-moon April 2026 must NOT use the early Apr 2 full moon
    // (which would yield no valid Tuesday before it, causing a null/wrong date).
    // -----------------------------------------------------------------------
    [Fact]
    public void LunarSeasonBefore_BlueMoonApril2026_UsesEndOfMonthMoon()
    {
        // Apr 2026 has two full moons: Apr 2 (irrelevant early moon) and Apr 30.
        // The algorithm must use the Apr 30 moon → last Tuesday ≤ Apr 30 = Apr 28.
        var evt = MakeEvent("1266", "LunarSeasonBefore", "Tuesday");
        var instances = _svc.ExpandEvent(evt, 2026, 4, 2026, 4);

        Assert.Single(instances);
        Assert.Equal(new DateOnly(2026, 4, 28), instances[0].Date);
    }

    // -----------------------------------------------------------------------
    // Edge case: June 2026 full moon at 12:57am BST = 11:57pm Jun 29 UTC.
    // Algorithm uses UTC, so the full moon date is Jun 29, not Jun 30.
    // Result must be Jun 23 (last Tuesday ≤ Jun 29), NOT Jun 30.
    // -----------------------------------------------------------------------
    [Fact]
    public void LunarSeasonBefore_JuneMoon_CorrectlyUsesUtcDate()
    {
        var evt = MakeEvent("1266", "LunarSeasonBefore", "Tuesday");
        var instances = _svc.ExpandEvent(evt, 2026, 6, 2026, 6);

        Assert.Single(instances);
        Assert.Equal(new DateOnly(2026, 6, 23), instances[0].Date);
        Assert.NotEqual(new DateOnly(2026, 6, 30), instances[0].Date);
    }
}
