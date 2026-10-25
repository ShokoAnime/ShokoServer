using System;
using System.Collections.Generic;
using Shoko.Abstractions.Actions;

namespace Shoko.Abstractions.ScheduledActions;

/// <summary>
///   One action an <see cref="IScheduledActionProvider"/> hands the core, with
///   what an <see cref="IScheduledAction"/> declares about itself.
/// </summary>
public sealed record ScheduledActionDescriptor
{
    /// <summary>
    ///   The action's key within its provider: lowercase kebab-case, at most
    ///   64 characters, stable for as long as the action is the same one.
    /// </summary>
    public required string Key { get; init; }

    /// <summary>
    ///   The display name of the action.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    ///   The description of the action.
    /// </summary>
    public string? Description { get; init; }

    /// <summary>
    ///   The category the action is listed under. Defaults to
    ///   <see cref="ActionCategory.Miscellaneous"/>.
    /// </summary>
    public ActionCategory Category { get; init; } = ActionCategory.Miscellaneous;

    /// <summary>
    ///   Whether a person must confirm before running it by hand.
    /// </summary>
    public bool RequiresConfirmation { get; init; }

    /// <summary>
    ///   What to ask when <see cref="RequiresConfirmation"/> is set.
    /// </summary>
    public string? ConfirmationMessage { get; init; }

    /// <summary>
    ///   The triggers it runs on until an admin sets others, as
    ///   <see cref="IScheduledAction.DefaultTriggers"/>.
    /// </summary>
    public IReadOnlyList<ActionTrigger> DefaultTriggers { get; init; } = [];

    /// <summary>
    ///   The least time between two runs, as
    ///   <see cref="IScheduledAction.MinimumInterval"/>.
    /// </summary>
    public TimeSpan? MinimumInterval { get; init; }

    /// <summary>
    ///   Whether runs by hand count for the schedule, as
    ///   <see cref="IScheduledAction.ScheduleCountsManualRuns"/>.
    /// </summary>
    public bool ScheduleCountsManualRuns { get; init; }
}
