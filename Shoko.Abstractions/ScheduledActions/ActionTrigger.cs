using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Shoko.Abstractions.Extensions;

namespace Shoko.Abstractions.ScheduledActions;

/// <summary>
///   When a scheduled action runs on its own: after an interval, daily, weekly or
///   monthly at a time of day in the server's time zone, at start-up, or when
///   the job queue is cleared. Build one through <see cref="Every"/>,
///   <see cref="DailyAt"/>, <see cref="WeeklyOn(System.DayOfWeek, TimeOnly)"/>,
///   <see cref="MonthlyOn(int, TimeOnly)"/>, <see cref="AtStartup"/> or
///   <see cref="OnQueueCleared"/>.
/// </summary>
/// <remarks>
///   Each type sets only its own fields. A run missed while the server was down
///   runs once at start-up. A time of day the clock skips runs when the skip
///   ends, and one that happens twice runs the first time. Days are compared as
///   sets, so the same days in another order are equal.
/// </remarks>
public sealed record ActionTrigger
{
    #region Constants

    /// <summary>
    ///   The shortest interval an <see cref="ActionTriggerType.Interval"/>
    ///   trigger may have.
    /// </summary>
    public static readonly TimeSpan MinimumInterval = TimeSpan.FromMinutes(1);

    /// <summary>
    ///   The longest interval an <see cref="ActionTriggerType.Interval"/>
    ///   trigger may have.
    /// </summary>
    public static readonly TimeSpan MaximumInterval = TimeSpan.FromDays(366);

    #endregion

    #region Properties

    /// <summary>
    ///   What makes the action run.
    /// </summary>
    public ActionTriggerType Type { get; init; }

    /// <summary>
    ///   How long after the action last ran it runs again, in whole minutes,
    ///   with no alignment to the clock. Set for
    ///   <see cref="ActionTriggerType.Interval"/> only.
    /// </summary>
    public TimeSpan? Interval { get; init; }

    /// <summary>
    ///   The time of day it runs, in whole minutes, in the server's time zone.
    ///   Set for <see cref="ActionTriggerType.Daily"/>,
    ///   <see cref="ActionTriggerType.Weekly"/> and
    ///   <see cref="ActionTriggerType.Monthly"/> only.
    /// </summary>
    public TimeOnly? TimeOfDay { get; init; }

    /// <summary>
    ///   The days of the week it runs, at least one, each once. Set for
    ///   <see cref="ActionTriggerType.Weekly"/> only.
    /// </summary>
    public IReadOnlyList<DayOfWeek>? DaysOfWeek { get; init; }

    /// <summary>
    ///   The days of the month it runs, at least one, each once: 1 to 31 from
    ///   the start of the month, or -1 to -31 from its end, where -1 is the
    ///   last day. A day the month does not have is skipped that month. Set
    ///   for <see cref="ActionTriggerType.Monthly"/> only.
    /// </summary>
    public IReadOnlyList<int>? DaysOfMonth { get; init; }

    #endregion

    #region Factories

    /// <summary>
    ///   A trigger that runs the action once the interval has passed since it
    ///   last ran.
    /// </summary>
    /// <param name="interval">
    ///   The interval, in whole minutes, from <see cref="MinimumInterval"/> to
    ///   <see cref="MaximumInterval"/>.
    /// </param>
    /// <exception cref="ArgumentOutOfRangeException">
    ///   <paramref name="interval"/> is out of range, or not whole minutes.
    /// </exception>
    /// <returns>The trigger.</returns>
    public static ActionTrigger Every(TimeSpan interval)
    {
        if (interval < MinimumInterval || interval > MaximumInterval)
            throw new ArgumentOutOfRangeException(nameof(interval), interval, $"An interval must be from {MinimumInterval} to {MaximumInterval}.");

        if (!IsWholeMinutes(interval))
            throw new ArgumentOutOfRangeException(nameof(interval), interval, "An interval must be a whole number of minutes.");

        return new() { Type = ActionTriggerType.Interval, Interval = interval };
    }

    /// <summary>
    ///   A trigger that runs the action every day at a time of day.
    /// </summary>
    /// <param name="timeOfDay">The time of day, in whole minutes, in the server's time zone.</param>
    /// <exception cref="ArgumentOutOfRangeException">
    ///   <paramref name="timeOfDay"/> is not whole minutes.
    /// </exception>
    /// <returns>The trigger.</returns>
    public static ActionTrigger DailyAt(TimeOnly timeOfDay)
    {
        CheckTimeOfDay(timeOfDay);
        return new() { Type = ActionTriggerType.Daily, TimeOfDay = timeOfDay };
    }

    /// <summary>
    ///   A trigger that runs the action every week on a day at a time of day.
    /// </summary>
    /// <param name="dayOfWeek">The day of the week.</param>
    /// <param name="timeOfDay">The time of day, in whole minutes, in the server's time zone.</param>
    /// <exception cref="ArgumentOutOfRangeException">
    ///   <paramref name="dayOfWeek"/> is not a day of the week, or
    ///   <paramref name="timeOfDay"/> is not whole minutes.
    /// </exception>
    /// <returns>The trigger.</returns>
    public static ActionTrigger WeeklyOn(DayOfWeek dayOfWeek, TimeOnly timeOfDay)
        => WeeklyOn([dayOfWeek], timeOfDay);

    /// <summary>
    ///   A trigger that runs the action every week on some days at a time of
    ///   day. A day given twice counts once.
    /// </summary>
    /// <param name="daysOfWeek">The days of the week, at least one.</param>
    /// <param name="timeOfDay">The time of day, in whole minutes, in the server's time zone.</param>
    /// <exception cref="ArgumentException">
    ///   <paramref name="daysOfWeek"/> is empty.
    /// </exception>
    /// <exception cref="ArgumentOutOfRangeException">
    ///   A day is not a day of the week, or <paramref name="timeOfDay"/> is not
    ///   whole minutes.
    /// </exception>
    /// <returns>The trigger.</returns>
    public static ActionTrigger WeeklyOn(IEnumerable<DayOfWeek> daysOfWeek, TimeOnly timeOfDay)
    {
        var days = daysOfWeek.Distinct().ToArray();
        if (days.Length is 0)
            throw new ArgumentException("A weekly trigger needs at least one day of the week.", nameof(daysOfWeek));

        foreach (var day in days)
        {
            if (!Enum.IsDefined(day))
                throw new ArgumentOutOfRangeException(nameof(daysOfWeek), day, "Not a day of the week.");
        }

        CheckTimeOfDay(timeOfDay);
        return new() { Type = ActionTriggerType.Weekly, DaysOfWeek = [.. days.OrderBy(MondayFirst)], TimeOfDay = timeOfDay };
    }

    /// <summary>
    ///   A trigger that runs the action every month on a day at a time of day.
    /// </summary>
    /// <param name="dayOfMonth">
    ///   The day of the month: 1 to 31, or -1 to -31 counting from the end,
    ///   where -1 is the last day.
    /// </param>
    /// <param name="timeOfDay">The time of day, in whole minutes, in the server's time zone.</param>
    /// <exception cref="ArgumentOutOfRangeException">
    ///   <paramref name="dayOfMonth"/> is out of range, or
    ///   <paramref name="timeOfDay"/> is not whole minutes.
    /// </exception>
    /// <returns>The trigger.</returns>
    public static ActionTrigger MonthlyOn(int dayOfMonth, TimeOnly timeOfDay)
        => MonthlyOn([dayOfMonth], timeOfDay);

    /// <summary>
    ///   A trigger that runs the action every month on some days at a time of
    ///   day. A day given twice counts once.
    /// </summary>
    /// <param name="daysOfMonth">
    ///   The days of the month, at least one: 1 to 31, or -1 to -31 counting
    ///   from the end, where -1 is the last day.
    /// </param>
    /// <param name="timeOfDay">The time of day, in whole minutes, in the server's time zone.</param>
    /// <exception cref="ArgumentException">
    ///   <paramref name="daysOfMonth"/> is empty.
    /// </exception>
    /// <exception cref="ArgumentOutOfRangeException">
    ///   A day is out of range, or <paramref name="timeOfDay"/> is not whole
    ///   minutes.
    /// </exception>
    /// <returns>The trigger.</returns>
    public static ActionTrigger MonthlyOn(IEnumerable<int> daysOfMonth, TimeOnly timeOfDay)
    {
        var days = daysOfMonth.Distinct().ToArray();
        if (days.Length is 0)
            throw new ArgumentException("A monthly trigger needs at least one day of the month.", nameof(daysOfMonth));

        foreach (var day in days)
        {
            if (!IsDayOfMonth(day))
                throw new ArgumentOutOfRangeException(nameof(daysOfMonth), day, "A day of the month must be from 1 to 31, or from -31 to -1.");
        }

        CheckTimeOfDay(timeOfDay);
        return new() { Type = ActionTriggerType.Monthly, DaysOfMonth = [.. days.OrderBy(day => day < 0).ThenBy(day => day)], TimeOfDay = timeOfDay };
    }

    /// <summary>
    ///   A trigger that runs the action every time the server has started.
    /// </summary>
    public static ActionTrigger AtStartup { get; } = new() { Type = ActionTriggerType.Startup };

    /// <summary>
    ///   A trigger that runs the action every time the job queue is cleared.
    /// </summary>
    public static ActionTrigger OnQueueCleared { get; } = new() { Type = ActionTriggerType.QueueCleared };

    #endregion

    #region Validation

    /// <summary>
    ///   Why the trigger cannot be used, or <c>null</c> when it
    ///   can: it must set exactly the fields its <see cref="Type"/> takes, an
    ///   interval must be whole minutes and in range, a time of day whole
    ///   minutes, and each day set once and in range.
    /// </summary>
    /// <returns>The reason, or <c>null</c>.</returns>
    public string? GetValidationError()
    {
        if (!Enum.IsDefined(Type))
            return "Unknown trigger type.";

        var (subject, takes) = GetShape(Type);
        var set = GetSetFields();
        var extra = set.Where(field => !takes.Contains(field)).ToList();
        if (extra.Count > 0)
        {
            var allowed = takes.Length is 0 ? "no fields" : $"only {JoinAnd(takes)}";
            return $"{subject} takes {allowed}, but {JoinAnd(extra)} {(extra.Count is 1 ? "is" : "are")} set.";
        }

        var missing = takes.Where(field => !set.Contains(field)).ToList();
        if (missing.Count > 0)
            return $"{subject} needs {JoinAnd(missing)}.";

        if (Interval is { } interval)
        {
            if (interval < MinimumInterval || interval > MaximumInterval)
                return $"An interval must be from {MinimumInterval} to {MaximumInterval}.";

            if (!IsWholeMinutes(interval))
                return "An interval must be a whole number of minutes.";
        }

        if (TimeOfDay is { } timeOfDay && !IsWholeMinutes(timeOfDay))
            return "TimeOfDay must be whole minutes, with no seconds.";

        if (DaysOfWeek is { } daysOfWeek)
        {
            if (daysOfWeek.Count is 0)
                return "DaysOfWeek must name at least one day of the week.";

            foreach (var day in daysOfWeek)
            {
                if (!Enum.IsDefined(day))
                    return string.Create(CultureInfo.InvariantCulture, $"DaysOfWeek holds {(int)day}, which is not a day of the week.");
            }

            if (daysOfWeek.GroupBy(day => day).FirstOrDefault(group => group.Count() > 1) is { } repeated)
                return $"DaysOfWeek names {repeated.Key} more than once.";
        }

        if (DaysOfMonth is { } daysOfMonth)
        {
            if (daysOfMonth.Count is 0)
                return "DaysOfMonth must hold at least one day of the month.";

            foreach (var day in daysOfMonth)
            {
                if (!IsDayOfMonth(day))
                    return string.Create(CultureInfo.InvariantCulture, $"DaysOfMonth holds {day}, but a day of the month must be from 1 to 31, or from -31 to -1 to count from the month's end.");
            }

            if (daysOfMonth.GroupBy(day => day).FirstOrDefault(group => group.Count() > 1) is { } repeated)
                return string.Create(CultureInfo.InvariantCulture, $"DaysOfMonth holds {repeated.Key} more than once.");
        }

        return null;
    }

    /// <summary>
    ///   Why the trigger cannot be used for an action, or
    ///   <c>null</c> when it can: as
    ///   <see cref="GetValidationError()"/>, and an interval must not be
    ///   under the action's minimum.
    /// </summary>
    /// <remarks>
    ///   Whether several triggers together run the action more often than its
    ///   minimum is checked when they are set, through
    ///   <see cref="Services.IScheduledActionService.SetTriggers"/>.
    /// </remarks>
    /// <param name="minimumInterval">
    ///   The action's minimum interval, as
    ///   <see cref="ScheduledActionInfo.MinimumInterval"/> gives it.
    /// </param>
    /// <returns>The reason, or <c>null</c>.</returns>
    public string? GetValidationError(TimeSpan minimumInterval)
        => GetValidationError() ?? (Type is ActionTriggerType.Interval && Interval < minimumInterval
            ? $"An interval must be at least {minimumInterval.ToDurationString()} for this action, which may not run more often."
            : null);

    /// <summary>
    ///   How a trigger type is named in a reason, and the fields it takes.
    /// </summary>
    /// <param name="type">The trigger type, a defined one.</param>
    /// <returns>The name and the fields.</returns>
    private static (string Subject, string[] Takes) GetShape(ActionTriggerType type)
        => type switch
        {
            ActionTriggerType.Interval => ("An interval trigger", [nameof(Interval)]),
            ActionTriggerType.Daily => ("A daily trigger", [nameof(TimeOfDay)]),
            ActionTriggerType.Weekly => ("A weekly trigger", [nameof(DaysOfWeek), nameof(TimeOfDay)]),
            ActionTriggerType.Monthly => ("A monthly trigger", [nameof(DaysOfMonth), nameof(TimeOfDay)]),
            ActionTriggerType.QueueCleared => ("A queue-cleared trigger", []),
            _ => ("A start-up trigger", []),
        };

    /// <summary>
    ///   The fields the trigger sets.
    /// </summary>
    /// <returns>Their names.</returns>
    private List<string> GetSetFields()
    {
        var fields = new List<string>(4);
        if (Interval is not null)
            fields.Add(nameof(Interval));
        if (DaysOfWeek is not null)
            fields.Add(nameof(DaysOfWeek));
        if (DaysOfMonth is not null)
            fields.Add(nameof(DaysOfMonth));
        if (TimeOfDay is not null)
            fields.Add(nameof(TimeOfDay));
        return fields;
    }

    /// <summary>
    ///   Joins words as "a", "a and b" or "a, b and c".
    /// </summary>
    /// <param name="words">The words, at least one.</param>
    /// <returns>The text.</returns>
    private static string JoinAnd(IReadOnlyList<string> words)
        => words.Count is 1 ? words[0] : $"{string.Join(", ", words.Take(words.Count - 1))} and {words[^1]}";

    /// <summary>
    ///   Refuses a time of day that is not whole minutes.
    /// </summary>
    /// <param name="timeOfDay">The time of day.</param>
    /// <exception cref="ArgumentOutOfRangeException">It is not whole minutes.</exception>
    private static void CheckTimeOfDay(TimeOnly timeOfDay)
    {
        if (!IsWholeMinutes(timeOfDay))
            throw new ArgumentOutOfRangeException(nameof(timeOfDay), timeOfDay, "A time of day must be whole minutes, with no seconds.");
    }

    /// <summary>
    ///   Whether a time span is a whole number of minutes.
    /// </summary>
    /// <param name="interval">The time span.</param>
    /// <returns>Whether it is.</returns>
    private static bool IsWholeMinutes(TimeSpan interval)
        => interval.Ticks % TimeSpan.TicksPerMinute is 0;

    /// <summary>
    ///   Whether a time of day is on a whole minute.
    /// </summary>
    /// <param name="timeOfDay">The time of day.</param>
    /// <returns>Whether it is.</returns>
    private static bool IsWholeMinutes(TimeOnly timeOfDay)
        => timeOfDay.Ticks % TimeSpan.TicksPerMinute is 0;

    /// <summary>
    ///   Whether a number is a day of the month a monthly trigger takes.
    /// </summary>
    /// <param name="day">The number.</param>
    /// <returns>Whether it is.</returns>
    private static bool IsDayOfMonth(int day)
        => day is >= 1 and <= 31 or >= -31 and <= -1;

    /// <summary>
    ///   Orders the days of the week from Monday.
    /// </summary>
    /// <param name="day">The day.</param>
    /// <returns>Its place, from 0 for Monday to 6 for Sunday.</returns>
    private static int MondayFirst(DayOfWeek day)
        => ((int)day + 6) % 7;

    #endregion

    #region Description

    /// <summary>
    ///   Describes the trigger in English, for logs and UIs: "every 30 minutes
    ///   trigger", "daily 04:00 trigger", "Monday and Friday 02:00 trigger",
    ///   "monthly 1st and last day 04:00 trigger", "start-up trigger" or
    ///   "queue-cleared trigger".
    /// </summary>
    /// <remarks>
    ///   Times of day are on a 24 hour clock, in the server's time zone, and
    ///   the days are listed from Monday or from the start of the month. An
    ///   invalid trigger is described as far as its fields allow, and never
    ///   throws.
    /// </remarks>
    /// <returns>The description, starting in lower case unless it starts with a day of the week.</returns>
    public string Describe()
    {
        var at = TimeOfDay is { } timeOfDay ? $" {timeOfDay.ToString("HH:mm", CultureInfo.InvariantCulture)}" : string.Empty;
        return Type switch
        {
            ActionTriggerType.Interval => Interval is { } interval ? $"every {interval.ToDurationString()} trigger" : "interval trigger",
            ActionTriggerType.Daily => $"daily{at} trigger",
            ActionTriggerType.Weekly => DaysOfWeek is { Count: > 0 } daysOfWeek
                ? $"{JoinAnd([.. daysOfWeek.Distinct().OrderBy(MondayFirst).Select(day => day.ToString())])}{at} trigger"
                : $"weekly{at} trigger",
            ActionTriggerType.Monthly => DaysOfMonth is { Count: > 0 } daysOfMonth
                ? $"monthly {JoinAnd([.. daysOfMonth.Distinct().OrderBy(day => day < 0).ThenBy(day => day).Select(DescribeDayOfMonth)])}{at} trigger"
                : $"monthly{at} trigger",
            ActionTriggerType.Startup => "start-up trigger",
            ActionTriggerType.QueueCleared => "queue-cleared trigger",
            _ => "unknown trigger",
        };
    }

    /// <summary>
    ///   Names a day of the month a monthly trigger takes, as "1st", "15th",
    ///   "last day" or "2nd to last day".
    /// </summary>
    /// <param name="day">The day: 1 to 31, or -1 to -31 counting from the end.</param>
    /// <returns>The name.</returns>
    private static string DescribeDayOfMonth(int day)
        => day switch
        {
            -1 => "last day",
            < 0 => $"{ToOrdinal(-day)} to last day",
            _ => ToOrdinal(day),
        };

    /// <summary>
    ///   Writes a number out as an English ordinal, as "1st", "2nd", "11th" or
    ///   "23rd".
    /// </summary>
    /// <param name="number">The number, at least 0.</param>
    /// <returns>The ordinal.</returns>
    private static string ToOrdinal(int number)
    {
        var suffix = (number % 100) is 11 or 12 or 13 ? "th" : (number % 10) switch
        {
            1 => "st",
            2 => "nd",
            3 => "rd",
            _ => "th",
        };
        return string.Create(CultureInfo.InvariantCulture, $"{number}{suffix}");
    }

    #endregion

    #region Equality

    /// <summary>
    ///   Whether two triggers are the same, comparing the days as sets.
    /// </summary>
    /// <param name="other">The other trigger.</param>
    /// <returns>Whether they are.</returns>
    public bool Equals(ActionTrigger? other)
        => other is not null
            && Type == other.Type
            && Interval == other.Interval
            && TimeOfDay == other.TimeOfDay
            && SetEquals(DaysOfWeek, other.DaysOfWeek)
            && SetEquals(DaysOfMonth, other.DaysOfMonth);

    /// <summary>
    ///   A hash code that agrees with <see cref="Equals(ActionTrigger?)"/>.
    /// </summary>
    /// <returns>The hash code.</returns>
    public override int GetHashCode()
        => HashCode.Combine(Type, Interval, TimeOfDay, SetHash(DaysOfWeek), SetHash(DaysOfMonth));

    /// <summary>
    ///   Whether two lists hold the same values, ignoring order and repeats.
    /// </summary>
    /// <typeparam name="T">The type of the values.</typeparam>
    /// <param name="left">The one list, or <c>null</c>.</param>
    /// <param name="right">The other list, or <c>null</c>.</param>
    /// <returns>Whether they do.</returns>
    private static bool SetEquals<T>(IReadOnlyList<T>? left, IReadOnlyList<T>? right)
        => left is null || right is null ? left is null && right is null : left.ToHashSet().SetEquals(right);

    /// <summary>
    ///   A hash code of a list that ignores order and repeats.
    /// </summary>
    /// <typeparam name="T">The type of the values.</typeparam>
    /// <param name="values">The list, or <c>null</c>.</param>
    /// <returns>The hash code.</returns>
    private static int SetHash<T>(IReadOnlyList<T>? values)
        => values is null ? 0 : values.Distinct().Aggregate(1, (hash, value) => hash ^ (value?.GetHashCode() ?? 0));

    #endregion
}
