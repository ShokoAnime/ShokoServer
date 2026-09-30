using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Shoko.Abstractions.Extensions;
using Shoko.Abstractions.ScheduledActions;

#nullable enable
namespace Shoko.Server.Scheduling;

/// <summary>
/// When the triggers of a scheduled action fire next.
/// </summary>
public static class ActionTriggerSchedule
{
    #region Constants

    /// <summary>
    /// How soon after a run a daily, weekly or monthly time still counts as
    /// that run, so a timer that fires a hair early does not run it twice.
    /// </summary>
    internal static readonly TimeSpan SameRunTolerance = TimeSpan.FromSeconds(1);

    /// <summary>
    /// How many days ahead a daily, weekly or monthly time is looked for. Every
    /// valid trigger fires within about two months.
    /// </summary>
    private const int MaximumSearchDays = 400;

    #endregion

    #region Next Run

    /// <summary>
    /// When the earliest of the triggers fires after the action last ran, at
    /// or after a point in time. A time any trigger fires before that point is
    /// skipped, and the trigger moves on to its next time.
    /// </summary>
    /// <param name="triggers">The triggers.</param>
    /// <param name="lastRunAt">
    /// When the action last ran, in UTC, or when the scheduler first saw it if
    /// it never ran.
    /// </param>
    /// <param name="timeZone">The time zone the times of day are in.</param>
    /// <param name="notBefore">
    /// The earliest the action may run, in UTC, as the end of its minimum
    /// interval after its last run, or <c>null</c> for no such bound.
    /// </param>
    /// <exception cref="ArgumentException">A trigger is invalid.</exception>
    /// <returns>
    /// The time in UTC, or <c>null</c> when only a start-up trigger, or none,
    /// is set. It may lie in the past, when a run was missed.
    /// </returns>
    public static DateTime? GetNextRun(IEnumerable<ActionTrigger> triggers, DateTime lastRunAt, TimeZoneInfo timeZone, DateTime? notBefore = null)
        => triggers
            .Select(trigger => GetNextRun(trigger, lastRunAt, timeZone, notBefore))
            .Where(next => next.HasValue)
            .Min();

    /// <summary>
    /// When one trigger fires after the action last ran, at or after a point in
    /// time. A time before that point is skipped: an interval trigger moves on
    /// by whole intervals, and a daily, weekly or monthly one to its next
    /// wall-clock time.
    /// </summary>
    /// <remarks>
    /// A daily, weekly or monthly trigger fires on its wall-clock time, to the
    /// minute. A time of day the clock skips on a day runs when the clock comes
    /// out of the skip, and one that happens twice runs the first time. One
    /// that falls within <see cref="SameRunTolerance"/> after the last run is
    /// that run, fired a hair early, so the next day's counts.
    /// </remarks>
    /// <param name="trigger">The trigger.</param>
    /// <param name="lastRunAt">When the action last ran, in UTC.</param>
    /// <param name="timeZone">The time zone the times of day are in.</param>
    /// <param name="notBefore">
    /// The earliest the action may run, in UTC, or <c>null</c> for no such
    /// bound.
    /// </param>
    /// <exception cref="ArgumentException">The trigger is invalid.</exception>
    /// <returns>
    /// The time in UTC, always after <paramref name="lastRunAt"/> and not
    /// before <paramref name="notBefore"/>, or <c>null</c> for a start-up
    /// trigger.
    /// </returns>
    public static DateTime? GetNextRun(ActionTrigger trigger, DateTime lastRunAt, TimeZoneInfo timeZone, DateTime? notBefore = null)
    {
        if (trigger.GetValidationError() is { } error)
            throw new ArgumentException(error, nameof(trigger));

        lastRunAt = DateTime.SpecifyKind(lastRunAt, DateTimeKind.Utc);
        var next = GetFirstRun(trigger, lastRunAt, timeZone);
        if (next is null || notBefore is not { } bound || next >= bound)
            return next;

        bound = DateTime.SpecifyKind(bound, DateTimeKind.Utc);
        return trigger.Type is ActionTriggerType.Interval
            ? AfterIntervals(lastRunAt, trigger.Interval!.Value, bound, inclusive: true)
            : NextAt(bound - SameRunTolerance - TimeSpan.FromTicks(1), timeZone, trigger);
    }

    /// <summary>
    /// The times the triggers fire after the action last ran but before it may
    /// run again, which are skipped, within a stretch of time. Two triggers
    /// firing at the same time are one time, given with the first of them.
    /// </summary>
    /// <param name="triggers">The triggers, each a valid one.</param>
    /// <param name="lastRunAt">When the action last ran, in UTC.</param>
    /// <param name="notBefore">
    /// The earliest the action may run, in UTC, as the end of its minimum
    /// interval after its last run.
    /// </param>
    /// <param name="after">Where the stretch starts, in UTC, not included.</param>
    /// <param name="until">Where the stretch ends, in UTC, included.</param>
    /// <param name="timeZone">The time zone the times of day are in.</param>
    /// <returns>The skipped times in UTC, in order, each with its trigger.</returns>
    public static List<(DateTime At, ActionTrigger Trigger)> GetSkippedRuns(
        IReadOnlyList<ActionTrigger> triggers,
        DateTime lastRunAt,
        DateTime notBefore,
        DateTime after,
        DateTime until,
        TimeZoneInfo timeZone
    )
    {
        lastRunAt = DateTime.SpecifyKind(lastRunAt, DateTimeKind.Utc);
        notBefore = DateTime.SpecifyKind(notBefore, DateTimeKind.Utc);
        after = DateTime.SpecifyKind(after, DateTimeKind.Utc);
        until = DateTime.SpecifyKind(until, DateTimeKind.Utc);
        var skipped = new List<(DateTime At, int Index)>();
        for (var index = 0; index < triggers.Count; index++)
        {
            var trigger = triggers[index];
            if (GetFirstRun(trigger, lastRunAt, timeZone) is not { } at)
                continue;

            if (at <= after)
            {
                at = trigger.Type is ActionTriggerType.Interval
                    ? AfterIntervals(lastRunAt, trigger.Interval!.Value, after, inclusive: false)
                    : NextAt(after - SameRunTolerance, timeZone, trigger);
            }

            while (at < notBefore && at <= until)
            {
                skipped.Add((at, index));
                at = trigger.Type is ActionTriggerType.Interval ? at + trigger.Interval!.Value : NextAt(at, timeZone, trigger);
            }
        }

        return skipped
            .OrderBy(entry => entry.At)
            .ThenBy(entry => entry.Index)
            .DistinctBy(entry => entry.At)
            .Select(entry => (entry.At, triggers[entry.Index]))
            .ToList();
    }

    /// <summary>
    /// When a valid trigger first fires after the action last ran, with no
    /// bound.
    /// </summary>
    /// <param name="trigger">The trigger, a valid one.</param>
    /// <param name="lastRunAt">When the action last ran, in UTC.</param>
    /// <param name="timeZone">The time zone the times of day are in.</param>
    /// <returns>The time in UTC, or <c>null</c> for a start-up trigger.</returns>
    private static DateTime? GetFirstRun(ActionTrigger trigger, DateTime lastRunAt, TimeZoneInfo timeZone)
        => trigger.Type switch
        {
            ActionTriggerType.Interval => lastRunAt + trigger.Interval!.Value,
            ActionTriggerType.Daily or ActionTriggerType.Weekly or ActionTriggerType.Monthly => NextAt(lastRunAt, timeZone, trigger),
            _ => null,
        };

    /// <summary>
    /// The first time a whole number of intervals, at least one, after a start
    /// that lies past a bound.
    /// </summary>
    /// <param name="start">Where the intervals count from, in UTC.</param>
    /// <param name="interval">The interval, above zero.</param>
    /// <param name="bound">The bound, in UTC.</param>
    /// <param name="inclusive">Whether a time on the bound itself counts.</param>
    /// <returns>The time in UTC.</returns>
    private static DateTime AfterIntervals(DateTime start, TimeSpan interval, DateTime bound, bool inclusive)
    {
        var ticks = (bound - start).Ticks;
        var count = ticks <= 0 ? 1 : inclusive ? (ticks + interval.Ticks - 1) / interval.Ticks : ticks / interval.Ticks + 1;
        return start + TimeSpan.FromTicks(Math.Max(count, 1) * interval.Ticks);
    }

    /// <summary>
    /// The first wall-clock time of a daily, weekly or monthly trigger after a
    /// point in time.
    /// </summary>
    /// <param name="after">The point in time, in UTC.</param>
    /// <param name="timeZone">The time zone the time of day is in.</param>
    /// <param name="trigger">The trigger, a valid one.</param>
    /// <returns>The time in UTC.</returns>
    private static DateTime NextAt(DateTime after, TimeZoneInfo timeZone, ActionTrigger trigger)
    {
        // A day earlier than the local date, since a time of day the clock
        // runs twice can land before `after` on its own day.
        var timeOfDay = trigger.TimeOfDay!.Value;
        var date = DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(after, timeZone)).AddDays(-1);
        var last = date.AddDays(MaximumSearchDays);
        for (; date <= last; date = date.AddDays(1))
        {
            if (!FiresOn(trigger, date))
                continue;

            var candidate = ToUtc(date.ToDateTime(timeOfDay), timeZone);
            if (candidate > after + SameRunTolerance)
                return candidate;
        }

        throw new InvalidOperationException($"The trigger does not fire within {MaximumSearchDays} days.");
    }

    /// <summary>
    /// Whether a daily, weekly or monthly trigger fires on a date. A day of the
    /// month the month does not have is skipped, whether it counts from the
    /// start or from the end.
    /// </summary>
    /// <param name="trigger">The trigger, a valid one.</param>
    /// <param name="date">The date.</param>
    /// <returns>Whether it fires.</returns>
    internal static bool FiresOn(ActionTrigger trigger, DateOnly date)
    {
        switch (trigger.Type)
        {
            case ActionTriggerType.Daily:
                return true;

            case ActionTriggerType.Weekly:
                return trigger.DaysOfWeek!.Contains(date.DayOfWeek);

            case ActionTriggerType.Monthly:
                var daysInMonth = DateTime.DaysInMonth(date.Year, date.Month);
                foreach (var day in trigger.DaysOfMonth!)
                {
                    if ((day > 0 ? day : daysInMonth + day + 1) == date.Day)
                        return true;
                }

                return false;

            default:
                return false;
        }
    }

    #endregion

    #region Minimum Interval

    /// <summary>
    /// When the minimum interval after a run ends: that long after the start of
    /// the minute the run was in, by the clock or by the server's wall clock,
    /// whichever comes first. A clock change shortens it by the hour it skips,
    /// as the wall-clock times of the triggers were checked against it with no
    /// clock changes.
    /// </summary>
    /// <param name="from">When the run was, in UTC.</param>
    /// <param name="minimumInterval">The action's minimum interval.</param>
    /// <param name="timeZone">The time zone of the wall clock.</param>
    /// <returns>The time in UTC, on a whole minute.</returns>
    public static DateTime GetMinimumIntervalEnd(DateTime from, TimeSpan minimumInterval, TimeZoneInfo timeZone)
    {
        from = DateTime.SpecifyKind(from, DateTimeKind.Utc);
        from = from.AddTicks(-(from.Ticks % TimeSpan.TicksPerMinute));
        var elapsed = from + minimumInterval;
        var wallClock = ToUtc(TimeZoneInfo.ConvertTimeFromUtc(from, timeZone) + minimumInterval, timeZone);
        return wallClock < elapsed ? wallClock : elapsed;
    }

    #endregion

    #region Spacing

    /// <summary>
    /// Why a set of triggers would run an action more often than its minimum
    /// interval on their own, or <c>null</c> when they would not or it cannot be
    /// told. Only the daily, weekly and monthly triggers can be told, by their
    /// wall-clock times with no clock changes: how close an interval or a
    /// start-up run comes to them depends on when the action ran.
    /// </summary>
    /// <param name="triggers">The triggers, each a valid one.</param>
    /// <param name="minimumInterval">The action's minimum interval.</param>
    /// <returns>The reason, or <c>null</c>.</returns>
    public static string? GetSpacingError(IReadOnlyList<ActionTrigger> triggers, TimeSpan minimumInterval)
    {
        var aligned = triggers
            .Select((trigger, index) => (Trigger: trigger, Index: index))
            .Where(entry => entry.Trigger.Type is ActionTriggerType.Daily or ActionTriggerType.Weekly or ActionTriggerType.Monthly)
            .ToList();
        if (aligned.Count is 0)
            return null;

        // The calendar repeats every 28 years (2000 to 2099) and a week every 7 days; one more month
        // or week past a whole cycle also measures the gap wrapping from its last run to its first.
        var first = new DateOnly(2000, 1, 1);
        var end = aligned.Any(entry => entry.Trigger.Type is ActionTriggerType.Monthly) ? first.AddYears(28).AddMonths(1) : first.AddDays(14);
        var shortest = TimeSpan.MaxValue;
        (int First, int Second) pair = default;
        DateTime? previous = null;
        var previousIndex = -1;
        var today = new List<(DateTime At, int Index)>(aligned.Count);
        for (var date = first; date <= end; date = date.AddDays(1))
        {
            today.Clear();
            foreach (var (trigger, index) in aligned)
            {
                if (FiresOn(trigger, date))
                    today.Add((date.ToDateTime(trigger.TimeOfDay!.Value), index));
            }

            foreach (var (at, index) in today.OrderBy(entry => entry.At).ThenBy(entry => entry.Index))
            {
                // Two triggers at the same time are one run.
                if (previous == at)
                    continue;

                if (previous is { } before && at - before < shortest)
                {
                    shortest = at - before;
                    pair = (previousIndex, index);
                }

                previous = at;
                previousIndex = index;
            }
        }

        if (shortest >= minimumInterval)
            return null;

        var gap = shortest.ToDurationString();
        var minimum = minimumInterval.ToDurationString();
        var which = pair.First == pair.Second
            ? string.Create(CultureInfo.InvariantCulture, $"Trigger {pair.First} runs twice within {gap}")
            : string.Create(CultureInfo.InvariantCulture, $"Triggers {Math.Min(pair.First, pair.Second)} and {Math.Max(pair.First, pair.Second)} run {gap} apart");
        return $"{which}, but this action may not run more often than every {minimum}.";
    }

    #endregion

    #region Wall Clock

    /// <summary>
    /// Turns a wall-clock time into UTC.
    /// </summary>
    /// <param name="local">The wall-clock time, of an unspecified kind.</param>
    /// <param name="timeZone">The time zone of the wall clock.</param>
    /// <returns>
    /// The time in UTC. A time the clock skips is moved to the end of the skip,
    /// and a time the clock runs twice is the first of the two.
    /// </returns>
    internal static DateTime ToUtc(DateTime local, TimeZoneInfo timeZone)
    {
        local = DateTime.SpecifyKind(local, DateTimeKind.Unspecified);
        if (timeZone.IsInvalidTime(local))
        {
            // The clock jumps over this time. Move on a minute at a time to the
            // first time it shows again, which is where the jump lands.
            var moved = local;
            do
                moved = moved.AddMinutes(1);
            while (timeZone.IsInvalidTime(moved));

            local = new DateTime(moved.Year, moved.Month, moved.Day, moved.Hour, moved.Minute, 0, DateTimeKind.Unspecified);
        }

        if (timeZone.IsAmbiguousTime(local))
        {
            // The first of the two is the one still at the larger offset.
            var offset = timeZone.GetAmbiguousTimeOffsets(local).Max();
            return new DateTimeOffset(local, offset).UtcDateTime;
        }

        return TimeZoneInfo.ConvertTimeToUtc(local, timeZone);
    }

    #endregion
}
