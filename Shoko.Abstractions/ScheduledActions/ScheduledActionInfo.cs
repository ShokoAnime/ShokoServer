using System;
using System.Collections.Generic;
using Shoko.Abstractions.Actions;

namespace Shoko.Abstractions.ScheduledActions;

/// <summary>
///   A scheduled action with its triggers, when it last ran and runs next, and
///   where its current run is in the queue.
/// </summary>
public sealed record ScheduledActionInfo
{
    /// <summary>
    ///   The scheduled action's stable UUIDv5 identifier, derived from its
    ///   type's fully-qualified name namespaced by the owning plugin's ID.
    /// </summary>
    public required Guid ID { get; init; }

    /// <summary>
    ///   The display name.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    ///   The description, if any.
    /// </summary>
    public string? Description { get; init; }

    /// <summary>
    ///   The category it is listed under.
    /// </summary>
    public ActionCategory Category { get; init; } = ActionCategory.Miscellaneous;

    /// <summary>
    ///   The display name of the category: the owning plugin's name for
    ///   <see cref="ActionCategory.PluginInferred"/>, otherwise the category's
    ///   own name.
    /// </summary>
    public required string CategoryName { get; init; }

    /// <summary>
    ///   Whether a client should ask before an admin runs it by hand.
    /// </summary>
    public bool RequiresConfirmation { get; init; }

    /// <summary>
    ///   The question to ask before a run by hand, or <c>null</c>
    ///   for a generic one.
    /// </summary>
    public string? ConfirmationMessage { get; init; }

    /// <summary>
    ///   The ID of the plugin that owns it.
    /// </summary>
    public required Guid PluginID { get; init; }

    /// <summary>
    ///   The triggers in effect: the admin's, or the defaults when the admin
    ///   set none. Empty when it never runs on its own.
    /// </summary>
    public required IReadOnlyList<ActionTrigger> Triggers { get; init; }

    /// <summary>
    ///   The triggers it declares for itself.
    /// </summary>
    public required IReadOnlyList<ActionTrigger> DefaultTriggers { get; init; }

    /// <summary>
    ///   The shortest time after a run before it runs on its own again: its
    ///   <see cref="IScheduledAction.MinimumInterval"/>, or
    ///   <see cref="ActionTrigger.MinimumInterval"/> when it declares none.
    /// </summary>
    /// <remarks>
    ///   Counted from the start of the minute of <see cref="LastScheduledRunAt"/>,
    ///   or of <see cref="LastRunAt"/> when <see cref="ScheduleCountsManualRuns"/>
    ///   is set. Triggers whose times come closer together are refused.
    /// </remarks>
    public TimeSpan MinimumInterval { get; init; } = ActionTrigger.MinimumInterval;

    /// <summary>
    ///   Whether a run by hand counts for the schedule like a run a trigger
    ///   queued, as its
    ///   <see cref="IScheduledAction.ScheduleCountsManualRuns"/> says: the
    ///   triggers then count from <see cref="LastRunAt"/> rather than from
    ///   <see cref="LastScheduledRunAt"/>.
    /// </summary>
    public bool ScheduleCountsManualRuns { get; init; }

    /// <summary>
    ///   Whether the admin set the triggers, rather than the defaults being in
    ///   effect.
    /// </summary>
    public required bool HasCustomTriggers { get; init; }

    /// <summary>
    ///   When it was last queued, by a trigger or by hand, or its
    ///   run's queue job last started, whichever is later, in UTC, or
    ///   <c>null</c> when it never was.
    /// </summary>
    public required DateTime? LastRunAt { get; init; }

    /// <summary>
    ///   When a trigger last queued it, or the queue job of a run a
    ///   trigger queued last started, whichever is later, in UTC, or
    ///   <c>null</c> when no trigger ever did.
    /// </summary>
    public DateTime? LastScheduledRunAt { get; init; }

    /// <summary>
    ///   When a trigger queues it next, in UTC, or <c>null</c> when
    ///   only start-up and queue-cleared triggers, or none, would.
    /// </summary>
    /// <remarks>
    ///   The first time one fires at least <see cref="MinimumInterval"/> after
    ///   the last run the schedule counts was due, held back further when that
    ///   run's job started late. A daily, weekly or monthly run falls on a
    ///   whole minute.
    /// </remarks>
    public required DateTime? NextRunAt { get; init; }

    /// <summary>
    ///   Where the queue job of its run is.
    /// </summary>
    public required ScheduledActionState State { get; init; }

    /// <summary>
    ///   How far the running job is, as a percentage from 0 to 100, or
    ///   <c>null</c> when it is not running or does not report.
    /// </summary>
    public required decimal? Progress { get; init; }

    /// <summary>
    ///   Whether the job can be cancelled now: a waiting one always can, and a
    ///   running one when it observes cancellation.
    /// </summary>
    public required bool IsCancellable { get; init; }

    /// <summary>
    ///   The queue key of the job a run uses.
    /// </summary>
    public required string JobKey { get; init; }
}
