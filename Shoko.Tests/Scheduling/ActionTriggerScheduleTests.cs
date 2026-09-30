using System;
using System.Linq;
using Shoko.Abstractions.ScheduledActions;
using Shoko.Server.Scheduling;
using Xunit;

namespace Shoko.Tests.Scheduling;

/// <summary>
/// Covers when the triggers of a scheduled action fire next, which are refused
/// and how they are described, in a time zone with daylight saving time: the
/// clock goes forward an hour at 02:00 on the last Sunday of March (29 March
/// 2026) and back an hour at 03:00 on the last Sunday of October (25 October
/// 2026), from UTC+1 to UTC+2 and back.
/// </summary>
public class ActionTriggerScheduleTests
{
    #region Fixture

    /// <summary>
    /// A time zone built in code, so the tests do not depend on the time zone
    /// data of the machine running them.
    /// </summary>
    internal static readonly TimeZoneInfo CentralEurope = TimeZoneInfo.CreateCustomTimeZone(
        "Test/Central Europe",
        TimeSpan.FromHours(1),
        "Test Central Europe",
        "Test Central Europe",
        "Test Central Europe Summer",
        [
            TimeZoneInfo.AdjustmentRule.CreateAdjustmentRule(
                DateTime.MinValue.Date,
                DateTime.MaxValue.Date,
                TimeSpan.FromHours(1),
                TimeZoneInfo.TransitionTime.CreateFloatingDateRule(new DateTime(1, 1, 1, 2, 0, 0), 3, 5, DayOfWeek.Sunday),
                TimeZoneInfo.TransitionTime.CreateFloatingDateRule(new DateTime(1, 1, 1, 3, 0, 0), 10, 5, DayOfWeek.Sunday)
            ),
        ]
    );

    private static DateTime Utc(int month, int day, int hour, int minute = 0, int second = 0)
        => new(2026, month, day, hour, minute, second, DateTimeKind.Utc);

    private static DateTime UtcIn(int year, int month, int day, int hour, int minute = 0)
        => new(year, month, day, hour, minute, 0, DateTimeKind.Utc);

    private static DateTime? Next(ActionTrigger trigger, DateTime lastRunAt)
        => ActionTriggerSchedule.GetNextRun(trigger, lastRunAt, CentralEurope);

    private static readonly TimeOnly FourAM = new(4, 0);

    #endregion

    #region Interval

    [Fact]
    public void AnInterval_CountsFromTheLastRun()
        => Assert.Equal(Utc(9, 28, 16, 30), Next(ActionTrigger.Every(TimeSpan.FromMinutes(90)), Utc(9, 28, 15)));

    [Fact]
    public void AnInterval_IgnoresTheClockChange()
        => Assert.Equal(Utc(10, 25, 2), Next(ActionTrigger.Every(TimeSpan.FromHours(2)), Utc(10, 25, 0)));

    #endregion

    #region Daily

    [Fact]
    public void ADailyTime_LaterToday_IsToday()
        // 12:00 UTC is 14:00 in summer, so 18:00 is 16:00 UTC the same day.
        => Assert.Equal(Utc(9, 28, 16), Next(ActionTrigger.DailyAt(new TimeOnly(18, 0)), Utc(9, 28, 12)));

    [Fact]
    public void ADailyTime_AlreadyPassedToday_IsTomorrow()
        => Assert.Equal(Utc(9, 29, 1), Next(ActionTrigger.DailyAt(new TimeOnly(3, 0)), Utc(9, 28, 12)));

    [Fact]
    public void ADailyTime_ExactlyAtTheLastRun_IsTomorrow()
        => Assert.Equal(Utc(9, 29, 1), Next(ActionTrigger.DailyAt(new TimeOnly(3, 0)), Utc(9, 28, 1)));

    [Fact]
    public void ADailyTime_KeepsItsWallClockTime_AcrossTheClockGoingBack()
    {
        // 03:00 in summer is 01:00 UTC; on the day the clock goes back it is 02:00 UTC.
        var trigger = ActionTrigger.DailyAt(new TimeOnly(3, 0));

        Assert.Equal(Utc(10, 24, 1), Next(trigger, Utc(10, 23, 12)));
        Assert.Equal(Utc(10, 25, 2), Next(trigger, Utc(10, 24, 1)));
        Assert.Equal(Utc(10, 26, 2), Next(trigger, Utc(10, 25, 2)));
    }

    [Fact]
    public void ADailyTime_TheClockSkips_RunsWhenTheSkipEnds()
    {
        // 02:30 does not exist on 29 March: the clock goes from 02:00 to 03:00,
        // which is 01:00 UTC. The day after, 02:30 is back, in summer time.
        var trigger = ActionTrigger.DailyAt(new TimeOnly(2, 30));

        Assert.Equal(Utc(3, 29, 1), Next(trigger, Utc(3, 28, 12)));
        Assert.Equal(Utc(3, 30, 0, 30), Next(trigger, Utc(3, 29, 1)));
    }

    [Fact]
    public void ADailyTime_TheClockRunsTwice_RunsTheFirstTimeOnly()
    {
        // 02:30 happens twice on 25 October: at 00:30 UTC in summer time, and
        // at 01:30 UTC after the clock goes back.
        var trigger = ActionTrigger.DailyAt(new TimeOnly(2, 30));

        Assert.Equal(Utc(10, 25, 0, 30), Next(trigger, Utc(10, 24, 12)));
        Assert.Equal(Utc(10, 26, 1, 30), Next(trigger, Utc(10, 25, 0, 30, 1)));
    }

    [Fact]
    public void ADailyTime_ARunBetweenTheTwoOccurrences_DoesNotRunTheSecond()
        => Assert.Equal(Utc(10, 26, 1, 30), Next(ActionTrigger.DailyAt(new TimeOnly(2, 30)), Utc(10, 25, 1)));

    #endregion

    #region Weekly

    [Fact]
    public void AWeeklyTime_IsOnItsDay()
        // Wednesday 30 September to Monday 5 October, 09:00 summer time.
        => Assert.Equal(Utc(10, 5, 7), Next(ActionTrigger.WeeklyOn(DayOfWeek.Monday, new TimeOnly(9, 0)), Utc(9, 30, 12)));

    [Fact]
    public void AWeeklyTime_OnItsDayButPassed_IsNextWeek()
        => Assert.Equal(Utc(10, 12, 7), Next(ActionTrigger.WeeklyOn(DayOfWeek.Monday, new TimeOnly(9, 0)), Utc(10, 5, 8)));

    [Fact]
    public void AWeeklyTime_OnTheDayTheClockGoesBack_UsesWinterTime()
        => Assert.Equal(Utc(10, 25, 3), Next(ActionTrigger.WeeklyOn(DayOfWeek.Sunday, new TimeOnly(4, 0)), Utc(10, 19, 12)));

    [Fact]
    public void MondaysAtTwo_RunEveryMondayAtTwo()
    {
        // Monday 28 September, after the run: next Monday at 02:00 summer
        // time, then the Monday after the clock went back, in winter time.
        var trigger = ActionTrigger.WeeklyOn(DayOfWeek.Monday, new TimeOnly(2, 0));

        Assert.Equal(Utc(10, 5, 0), Next(trigger, Utc(9, 28, 12)));
        Assert.Equal(Utc(10, 12, 0), Next(trigger, Utc(10, 5, 0)));
        Assert.Equal(Utc(10, 26, 1), Next(trigger, Utc(10, 19, 0)));
    }

    [Fact]
    public void MondayAndFriday_RunOnBoth()
    {
        var trigger = ActionTrigger.WeeklyOn([DayOfWeek.Friday, DayOfWeek.Monday], new TimeOnly(2, 0));

        Assert.Equal(Utc(10, 2, 0), Next(trigger, Utc(9, 28, 12)));
        Assert.Equal(Utc(10, 5, 0), Next(trigger, Utc(10, 2, 0)));
        Assert.Equal(Utc(10, 9, 0), Next(trigger, Utc(10, 5, 0)));
    }

    [Fact]
    public void AWeeklyTime_TheClockSkips_RunsWhenTheSkipEnds_AndTheClockRunsTwice_RunsOnce()
    {
        var trigger = ActionTrigger.WeeklyOn(DayOfWeek.Sunday, new TimeOnly(2, 30));

        Assert.Equal(Utc(3, 29, 1), Next(trigger, Utc(3, 28, 12)));
        Assert.Equal(Utc(10, 25, 0, 30), Next(trigger, Utc(10, 24, 12)));
        Assert.Equal(Utc(11, 1, 1, 30), Next(trigger, Utc(10, 25, 0, 30)));
    }

    #endregion

    #region Monthly

    [Fact]
    public void TheSixteenthAtFour_RunsOnTheSixteenthOnTheMinute()
    {
        // 04:00 on 16 October is summer time, on 16 November winter time.
        var trigger = ActionTrigger.MonthlyOn(16, FourAM);

        var next = Next(trigger, Utc(9, 28, 12, 17, 43));
        Assert.Equal(Utc(10, 16, 2), next);
        Assert.Equal(0, next!.Value.Second);
        Assert.Equal(Utc(11, 16, 3), Next(trigger, Utc(10, 16, 2)));
    }

    [Fact]
    public void TheLastDay_IsTheLastDayOfEachMonth_FebruaryAndLeapYearsIncluded()
    {
        var trigger = ActionTrigger.MonthlyOn(-1, FourAM);

        Assert.Equal(Utc(1, 31, 3), Next(trigger, Utc(1, 15, 12)));
        Assert.Equal(Utc(2, 28, 3), Next(trigger, Utc(1, 31, 3)));
        Assert.Equal(Utc(3, 31, 2), Next(trigger, Utc(2, 28, 3)));
        Assert.Equal(Utc(4, 30, 2), Next(trigger, Utc(3, 31, 2)));
        Assert.Equal(UtcIn(2028, 2, 29, 3), Next(trigger, UtcIn(2028, 1, 31, 3)));
        Assert.Equal(UtcIn(2100, 2, 28, 3), Next(trigger, UtcIn(2100, 1, 31, 3)));
    }

    [Fact]
    public void TheSecondToLastDay_CountsFromTheEnd()
    {
        var trigger = ActionTrigger.MonthlyOn(-2, FourAM);

        Assert.Equal(Utc(2, 27, 3), Next(trigger, Utc(2, 1, 12)));
        Assert.Equal(UtcIn(2028, 2, 28, 3), Next(trigger, UtcIn(2028, 2, 1, 12)));
        Assert.Equal(Utc(4, 29, 2), Next(trigger, Utc(4, 1, 12)));
        Assert.Equal(Utc(5, 30, 2), Next(trigger, Utc(4, 29, 2)));
    }

    [Fact]
    public void TheThirtyFirst_IsSkippedInShorterMonths()
    {
        var trigger = ActionTrigger.MonthlyOn(31, FourAM);

        Assert.Equal(Utc(3, 31, 2), Next(trigger, Utc(1, 31, 3)));
        Assert.Equal(Utc(5, 31, 2), Next(trigger, Utc(3, 31, 2)));
        Assert.Equal(Utc(8, 31, 2), Next(trigger, Utc(7, 31, 2)));
    }

    [Fact]
    public void SeveralDaysOfTheMonth_RunOnEach_AndTwoLandingOnTheSameDayRunOnce()
    {
        var trigger = ActionTrigger.MonthlyOn([1, 15, -1], FourAM);

        Assert.Equal(Utc(4, 15, 2), Next(trigger, Utc(4, 1, 2)));
        Assert.Equal(Utc(4, 30, 2), Next(trigger, Utc(4, 15, 2)));
        Assert.Equal(Utc(5, 1, 2), Next(trigger, Utc(4, 30, 2)));

        // 31 and -1 are both 31 January, and one run.
        var lastDays = ActionTrigger.MonthlyOn([31, -1], FourAM);
        Assert.Equal(Utc(2, 28, 3), Next(lastDays, Utc(1, 31, 3)));
    }

    [Fact]
    public void AMonthlyTime_OnTheDaysTheClockChanges_KeepsTheDaylightSavingRules()
    {
        // 29 March 02:30 does not exist and runs at 03:00 summer time; 25
        // October 02:30 happens twice and runs the first time only.
        var trigger = ActionTrigger.MonthlyOn([25, 29], new TimeOnly(2, 30));

        Assert.Equal(Utc(3, 29, 1), Next(trigger, Utc(3, 26, 12)));
        Assert.Equal(Utc(10, 25, 0, 30), Next(trigger, Utc(10, 24, 12)));
        Assert.Equal(Utc(10, 29, 1, 30), Next(trigger, Utc(10, 25, 0, 30)));
    }

    #endregion

    #region Same Run

    [Fact]
    public void AWallClockTime_JustAfterTheLastRun_IsThatRun()
    {
        // A timer firing a hair before 04:00 ran it; 04:00 itself is not run again.
        var trigger = ActionTrigger.DailyAt(FourAM);

        Assert.Equal(Utc(9, 30, 2), Next(trigger, Utc(9, 29, 2) - TimeSpan.FromMilliseconds(3)));
    }

    #endregion

    #region Several Triggers

    [Fact]
    public void SeveralTriggers_TheEarliestWins()
        => Assert.Equal(
            Utc(9, 28, 13),
            ActionTriggerSchedule.GetNextRun(
                [ActionTrigger.Every(TimeSpan.FromHours(6)), ActionTrigger.DailyAt(new TimeOnly(15, 0)), ActionTrigger.AtStartup],
                Utc(9, 28, 12),
                CentralEurope
            )
        );

    [Fact]
    public void OnlyAStartupTrigger_HasNoNextRun()
    {
        Assert.Null(Next(ActionTrigger.AtStartup, Utc(9, 28, 12)));
        Assert.Null(ActionTriggerSchedule.GetNextRun([ActionTrigger.AtStartup], Utc(9, 28, 12), CentralEurope));
        Assert.Null(ActionTriggerSchedule.GetNextRun([], Utc(9, 28, 12), CentralEurope));
    }

    [Fact]
    public void AMissedRun_IsInThePast()
        => Assert.True(Next(ActionTrigger.Every(TimeSpan.FromHours(1)), Utc(9, 25, 12)) < Utc(9, 28, 12));

    #endregion

    #region Minimum Interval

    [Fact]
    public void AWallClockTimeInsideTheMinimum_IsSkipped_ForItsNextTime()
    {
        // 03:00 and 04:00 are 01:00 and 02:00 UTC in summer; ran at 03:00, may not run again before 09:00.
        var triggers = new[] { ActionTrigger.DailyAt(new(3, 0)), ActionTrigger.DailyAt(FourAM) };
        Assert.Equal(Utc(9, 28, 2), ActionTriggerSchedule.GetNextRun(triggers, Utc(9, 28, 1), CentralEurope));
        Assert.Equal(Utc(9, 29, 1), ActionTriggerSchedule.GetNextRun(triggers, Utc(9, 28, 1), CentralEurope, Utc(9, 28, 7)));

        // A time on the end of the minimum is not inside it.
        Assert.Equal(Utc(9, 28, 2), ActionTriggerSchedule.GetNextRun(triggers, Utc(9, 28, 1), CentralEurope, Utc(9, 28, 2)));
    }

    [Fact]
    public void AWeeklyOrMonthlyTimeInsideTheMinimum_MovesOnToItsNextDay()
    {
        var weekly = ActionTrigger.WeeklyOn([DayOfWeek.Monday, DayOfWeek.Tuesday], FourAM);
        Assert.Equal(Utc(10, 5, 2), ActionTriggerSchedule.GetNextRun(weekly, Utc(9, 28, 2), CentralEurope, Utc(9, 30, 2)));

        var monthly = ActionTrigger.MonthlyOn([1, 2], FourAM);
        Assert.Equal(Utc(11, 1, 3), ActionTriggerSchedule.GetNextRun(monthly, Utc(10, 1, 2), CentralEurope, Utc(10, 5, 2)));
    }

    [Fact]
    public void AnIntervalInsideTheMinimum_MovesOnByWholeIntervals()
    {
        var fourHours = ActionTrigger.Every(TimeSpan.FromHours(4));
        Assert.Equal(Utc(9, 28, 20), ActionTriggerSchedule.GetNextRun(fourHours, Utc(9, 28, 12), CentralEurope, Utc(9, 28, 18)));
        Assert.Equal(Utc(9, 28, 16), ActionTriggerSchedule.GetNextRun(fourHours, Utc(9, 28, 12), CentralEurope, Utc(9, 28, 16)));
        Assert.Equal(Utc(9, 28, 16), ActionTriggerSchedule.GetNextRun(fourHours, Utc(9, 28, 12), CentralEurope, Utc(9, 28, 13)));
        Assert.Null(ActionTriggerSchedule.GetNextRun(ActionTrigger.AtStartup, Utc(9, 28, 12), CentralEurope, Utc(9, 28, 18)));
    }

    [Fact]
    public void TheSkippedTimes_AreListedInOrder_WithinTheirStretch_AndOnceEach()
    {
        var triggers = new[] { ActionTrigger.Every(TimeSpan.FromHours(1)), ActionTrigger.DailyAt(new(16, 0)), ActionTrigger.AtStartup };

        // Ran at 12:00 UTC, may not run again before 18:00; 16:00 local is 14:00 UTC, when the interval fires too.
        var skipped = ActionTriggerSchedule.GetSkippedRuns(triggers, Utc(9, 28, 12), Utc(9, 28, 18), Utc(9, 28, 12), Utc(9, 29, 0), CentralEurope);
        Assert.Equal([Utc(9, 28, 13), Utc(9, 28, 14), Utc(9, 28, 15), Utc(9, 28, 16), Utc(9, 28, 17)], skipped.Select(entry => entry.At));
        Assert.All(skipped, entry => Assert.Equal(triggers[0], entry.Trigger));

        var stretch = ActionTriggerSchedule.GetSkippedRuns(triggers, Utc(9, 28, 12), Utc(9, 28, 18), Utc(9, 28, 13), Utc(9, 28, 15), CentralEurope);
        Assert.Equal([Utc(9, 28, 14), Utc(9, 28, 15)], stretch.Select(entry => entry.At));

        var daily = ActionTriggerSchedule.GetSkippedRuns([triggers[1]], Utc(9, 28, 12), Utc(9, 28, 18), Utc(9, 28, 12), Utc(9, 29, 0), CentralEurope);
        Assert.Equal([(Utc(9, 28, 14), triggers[1])], daily);
    }

    [Fact]
    public void TheMinimum_CountsFromTheMinuteOfTheRun_AndAClockChangeShortensIt()
    {
        var sixHours = TimeSpan.FromHours(6);
        Assert.Equal(Utc(9, 28, 7), ActionTriggerSchedule.GetMinimumIntervalEnd(Utc(9, 28, 1, 0, 40), sixHours, CentralEurope));

        // 00:00 on the day the clock goes forward is 23:00 UTC; 06:00 that day is 04:00 UTC, five hours on.
        Assert.Equal(Utc(3, 29, 4), ActionTriggerSchedule.GetMinimumIntervalEnd(Utc(3, 28, 23), sixHours, CentralEurope));

        // 00:00 on the day the clock goes back is 22:00 UTC; six hours on is 04:00 UTC, 05:00 on the wall clock.
        Assert.Equal(Utc(10, 25, 4), ActionTriggerSchedule.GetMinimumIntervalEnd(Utc(10, 24, 22), sixHours, CentralEurope));
    }

    #endregion

    #region Validation

    [Fact]
    public void AnInvalidTrigger_IsRefused()
        => Assert.Throws<ArgumentException>(() => Next(new ActionTrigger { Type = ActionTriggerType.Daily }, Utc(9, 28, 12)));

    [Theory]
    [InlineData(0)]
    [InlineData(59)]
    public void AnIntervalUnderAMinute_IsRefused(int seconds)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => ActionTrigger.Every(TimeSpan.FromSeconds(seconds)));
        Assert.NotNull(new ActionTrigger { Type = ActionTriggerType.Interval, Interval = TimeSpan.FromSeconds(seconds) }.GetValidationError());
    }

    [Theory]
    [InlineData(90)]
    [InlineData(3601)]
    public void AnIntervalThatIsNotWholeMinutes_IsRefused(int seconds)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => ActionTrigger.Every(TimeSpan.FromSeconds(seconds)));
        Assert.NotNull(new ActionTrigger { Type = ActionTriggerType.Interval, Interval = TimeSpan.FromSeconds(seconds) }.GetValidationError());
    }

    [Fact]
    public void AnIntervalUnderTheActionsMinimum_IsRefused_AndTheReasonNamesIt()
    {
        var minimum = TimeSpan.FromHours(6);

        Assert.Contains("6 hours", ActionTrigger.Every(TimeSpan.FromMinutes(359)).GetValidationError(minimum));
        Assert.Contains("1 hour 30 minutes", ActionTrigger.Every(TimeSpan.FromHours(1)).GetValidationError(TimeSpan.FromMinutes(90)));
        Assert.Null(ActionTrigger.Every(minimum).GetValidationError(minimum));
        Assert.Null(ActionTrigger.DailyAt(new TimeOnly(3, 0)).GetValidationError(TimeSpan.FromDays(2)));
        Assert.Null(ActionTrigger.AtStartup.GetValidationError(minimum));
        Assert.NotNull(new ActionTrigger { Type = ActionTriggerType.Daily }.GetValidationError(minimum));
    }

    [Fact]
    public void TheFactories_MakeValidTriggers()
    {
        Assert.Null(ActionTrigger.Every(TimeSpan.FromMinutes(1)).GetValidationError());
        Assert.Null(ActionTrigger.DailyAt(new TimeOnly(3, 0)).GetValidationError());
        Assert.Null(ActionTrigger.WeeklyOn(DayOfWeek.Friday, new TimeOnly(3, 0)).GetValidationError());
        Assert.Null(ActionTrigger.WeeklyOn([DayOfWeek.Friday, DayOfWeek.Monday, DayOfWeek.Friday], new TimeOnly(3, 0)).GetValidationError());
        Assert.Null(ActionTrigger.MonthlyOn([1, -1, 31], new TimeOnly(3, 0)).GetValidationError());
        Assert.Null(ActionTrigger.AtStartup.GetValidationError());
    }

    [Fact]
    public void TheFactories_RefuseWhatTheTypeCannotHold()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => ActionTrigger.DailyAt(new TimeOnly(3, 0, 30)));
        Assert.Throws<ArgumentException>(() => ActionTrigger.WeeklyOn([], FourAM));
        Assert.Throws<ArgumentOutOfRangeException>(() => ActionTrigger.WeeklyOn((DayOfWeek)7, FourAM));
        Assert.Throws<ArgumentException>(() => ActionTrigger.MonthlyOn([], FourAM));
        Assert.Throws<ArgumentOutOfRangeException>(() => ActionTrigger.MonthlyOn(0, FourAM));
        Assert.Throws<ArgumentOutOfRangeException>(() => ActionTrigger.MonthlyOn(32, FourAM));
        Assert.Throws<ArgumentOutOfRangeException>(() => ActionTrigger.MonthlyOn(-32, FourAM));
    }

    [Fact]
    public void TheDays_AreASet()
    {
        var built = ActionTrigger.WeeklyOn([DayOfWeek.Friday, DayOfWeek.Monday, DayOfWeek.Friday], FourAM);
        var given = new ActionTrigger { Type = ActionTriggerType.Weekly, DaysOfWeek = [DayOfWeek.Monday, DayOfWeek.Friday], TimeOfDay = FourAM };

        Assert.Equal([DayOfWeek.Monday, DayOfWeek.Friday], built.DaysOfWeek);
        Assert.Equal(given, built);
        Assert.Equal(given.GetHashCode(), built.GetHashCode());
        Assert.Equal([1, 15, -2, -1], ActionTrigger.MonthlyOn([-1, 15, 1, -2], FourAM).DaysOfMonth);
        Assert.Equal(ActionTrigger.MonthlyOn([-1, 15], FourAM), ActionTrigger.MonthlyOn([15, -1], FourAM));
        Assert.NotEqual(ActionTrigger.MonthlyOn([-1, 15], FourAM), ActionTrigger.MonthlyOn([15, 31], FourAM));
    }

    /// <summary>
    /// Triggers that are not valid, each with what the reason must name: the
    /// fields missing, the fields the type does not take together with the
    /// ones it does, or the bad day or time.
    /// </summary>
    public static TheoryData<ActionTrigger, string[]> InvalidTriggers => new()
    {
        { new() { Type = ActionTriggerType.Interval }, ["Interval"] },
        { new() { Type = ActionTriggerType.Daily }, ["TimeOfDay"] },
        { new() { Type = ActionTriggerType.Weekly }, ["DaysOfWeek", "TimeOfDay"] },
        { new() { Type = ActionTriggerType.Monthly }, ["DaysOfMonth", "TimeOfDay"] },
        { new() { Type = ActionTriggerType.Daily, TimeOfDay = FourAM, DaysOfWeek = [DayOfWeek.Monday] }, ["DaysOfWeek", "TimeOfDay"] },
        { new() { Type = ActionTriggerType.Weekly, Interval = TimeSpan.FromHours(1), DaysOfWeek = [DayOfWeek.Monday], DaysOfMonth = [1], TimeOfDay = FourAM }, ["Interval", "DaysOfMonth", "DaysOfWeek", "TimeOfDay"] },
        { new() { Type = ActionTriggerType.Monthly, DaysOfMonth = [1], DaysOfWeek = [DayOfWeek.Monday], TimeOfDay = FourAM }, ["DaysOfWeek", "DaysOfMonth", "TimeOfDay"] },
        { new() { Type = ActionTriggerType.Interval, Interval = TimeSpan.FromHours(1), TimeOfDay = FourAM }, ["TimeOfDay", "Interval"] },
        { new() { Type = ActionTriggerType.Startup, TimeOfDay = FourAM }, ["TimeOfDay"] },
        { new() { Type = ActionTriggerType.Startup, Interval = TimeSpan.FromHours(1) }, ["Interval"] },
        { new() { Type = (ActionTriggerType)42 }, [] },
        { new() { Type = ActionTriggerType.Weekly, DaysOfWeek = [], TimeOfDay = FourAM }, ["DaysOfWeek"] },
        { new() { Type = ActionTriggerType.Weekly, DaysOfWeek = [(DayOfWeek)9], TimeOfDay = FourAM }, ["9"] },
        { new() { Type = ActionTriggerType.Weekly, DaysOfWeek = [DayOfWeek.Monday, DayOfWeek.Monday], TimeOfDay = FourAM }, ["Monday"] },
        { new() { Type = ActionTriggerType.Monthly, DaysOfMonth = [], TimeOfDay = FourAM }, ["DaysOfMonth"] },
        { new() { Type = ActionTriggerType.Monthly, DaysOfMonth = [0], TimeOfDay = FourAM }, ["0"] },
        { new() { Type = ActionTriggerType.Monthly, DaysOfMonth = [32], TimeOfDay = FourAM }, ["32"] },
        { new() { Type = ActionTriggerType.Monthly, DaysOfMonth = [-32], TimeOfDay = FourAM }, ["-32"] },
        { new() { Type = ActionTriggerType.Monthly, DaysOfMonth = [-1, -1], TimeOfDay = FourAM }, ["-1"] },
        { new() { Type = ActionTriggerType.Daily, TimeOfDay = new(4, 0, 30) }, ["TimeOfDay"] },
    };

    [Theory]
    [MemberData(nameof(InvalidTriggers))]
    public void AnInvalidTrigger_IsRefused_WithAReasonNamingWhatIsWrong(ActionTrigger trigger, string[] named)
    {
        var error = trigger.GetValidationError();

        Assert.NotNull(error);
        Assert.All(named, name => Assert.Contains(name, error));
    }

    #endregion

    #region Spacing

    private static readonly TimeSpan SixHours = TimeSpan.FromHours(6);

    /// <summary>
    /// Asserts <paramref name="triggers"/> run <paramref name="gap"/> apart at
    /// the closest: accepted for a minimum of that gap, refused for a minute
    /// more.
    /// </summary>
    /// <param name="gap">The closest two runs of the triggers.</param>
    /// <param name="triggers">The triggers.</param>
    private static void AssertClosestRunsApart(TimeSpan gap, params ActionTrigger[] triggers)
    {
        Assert.Null(ActionTriggerSchedule.GetSpacingError(triggers, gap));
        Assert.NotNull(ActionTriggerSchedule.GetSpacingError(triggers, gap + TimeSpan.FromMinutes(1)));
    }

    [Fact]
    public void TwoDailyTimesCloserThanTheMinimum_AreRefused()
        => AssertClosestRunsApart(TimeSpan.FromHours(1), ActionTrigger.DailyAt(new(3, 0)), ActionTrigger.DailyAt(FourAM));

    [Fact]
    public void OneTriggerRunningTwiceWithinTheMinimum_IsRefused()
    {
        AssertClosestRunsApart(TimeSpan.FromDays(1), ActionTrigger.AtStartup, ActionTrigger.WeeklyOn([DayOfWeek.Monday, DayOfWeek.Tuesday], FourAM));

        // The last day of one month and the first of the next.
        AssertClosestRunsApart(TimeSpan.FromDays(1), ActionTrigger.MonthlyOn([1, -1], FourAM));
    }

    [Fact]
    public void TriggersFarEnoughApart_OrThatCannotBeTold_AreAccepted()
    {
        Assert.Null(ActionTriggerSchedule.GetSpacingError([ActionTrigger.DailyAt(new(3, 0)), ActionTrigger.DailyAt(new(9, 0))], SixHours));
        Assert.Null(ActionTriggerSchedule.GetSpacingError([ActionTrigger.DailyAt(new(3, 0)), ActionTrigger.Every(SixHours), ActionTrigger.AtStartup], SixHours));
        Assert.Null(ActionTriggerSchedule.GetSpacingError([ActionTrigger.DailyAt(FourAM), ActionTrigger.WeeklyOn(DayOfWeek.Monday, FourAM)], SixHours));
        Assert.Null(ActionTriggerSchedule.GetSpacingError([ActionTrigger.MonthlyOn([31, -1], FourAM)], TimeSpan.FromDays(7)));
        Assert.Null(ActionTriggerSchedule.GetSpacingError([], SixHours));
    }

    [Fact]
    public void WeekdaysAcrossTheWeekBoundary_AreMeasuredAcrossIt()
    {
        AssertClosestRunsApart(TimeSpan.FromDays(3), ActionTrigger.WeeklyOn([DayOfWeek.Monday, DayOfWeek.Friday], FourAM));
        AssertClosestRunsApart(new TimeSpan(2, 2, 0, 0), ActionTrigger.WeeklyOn(DayOfWeek.Friday, new TimeOnly(23, 0)), ActionTrigger.WeeklyOn(DayOfWeek.Monday, new TimeOnly(1, 0)));
        AssertClosestRunsApart(TimeSpan.FromDays(7), ActionTrigger.WeeklyOn(DayOfWeek.Wednesday, FourAM));
    }

    [Fact]
    public void ADailyTimeAcrossMidnight_IsMeasuredAcrossIt()
        => AssertClosestRunsApart(TimeSpan.FromHours(2), ActionTrigger.DailyAt(new(1, 0)), ActionTrigger.DailyAt(new(23, 0)));

    #endregion

    #region Description

    [Theory]
    [InlineData(new[] { 22, 2, 21, 3, 11, 1 }, "1st, 2nd, 3rd, 11th, 21st and 22nd")]
    [InlineData(new[] { -1, -2 }, "2nd to last day and last day")]
    public void DaysOfTheMonth_AreDescribedInOrderAsOrdinals(int[] days, string expected)
        => Assert.Contains(expected, ActionTrigger.MonthlyOn(days, FourAM).Describe());

    #endregion
}
