using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Shoko.Abstractions.Actions;

namespace Shoko.Abstractions.ScheduledActions.Services;

/// <summary>
///   Lists the scheduled actions, sets when they run, and runs or cancels
///   them. Every member is meant for an admin: a scheduled action has no
///   permission of its own.
/// </summary>
/// <remarks>
///   A trigger queues a run through the job queue, never twice. No trigger runs
///   an action sooner than its <see cref="ScheduledActionInfo.MinimumInterval"/>
///   after its last counted run: a firing inside it, start-up and queue-cleared
///   included, is skipped and logged. A run whose job started late delays the next one. A run
///   by hand is never held back. Usable once the server has started.
/// </remarks>
public interface IScheduledActionService
{
    /// <summary>
    ///   Lists every scheduled action, by category, category name and name.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    ///   The server has not started yet.
    /// </exception>
    /// <returns>The scheduled actions.</returns>
    IReadOnlyList<ScheduledActionInfo> GetScheduledActions();

    /// <summary>
    ///   Gets one scheduled action by its ID.
    /// </summary>
    /// <param name="actionId">The scheduled action ID.</param>
    /// <exception cref="InvalidOperationException">
    ///   The server has not started yet.
    /// </exception>
    /// <returns>
    ///   The scheduled action, or <see langword="null"/> when none has the ID.
    /// </returns>
    ScheduledActionInfo? GetScheduledAction(Guid actionId);

    /// <summary>
    ///   Gets one scheduled action by its type, so a plugin can find the ID of
    ///   its own without deriving it.
    /// </summary>
    /// <typeparam name="TAction">The scheduled action type.</typeparam>
    /// <exception cref="InvalidOperationException">
    ///   The server has not started yet.
    /// </exception>
    /// <returns>
    ///   The scheduled action, or <see langword="null"/> when the type is not
    ///   a registered one.
    /// </returns>
    ScheduledActionInfo? GetScheduledAction<TAction>() where TAction : class, IScheduledAction;

    /// <summary>
    ///   Sets the triggers of a scheduled action, replacing the ones in
    ///   effect.
    /// </summary>
    /// <param name="actionId">The scheduled action ID.</param>
    /// <param name="triggers">The new triggers, empty for none.</param>
    /// <exception cref="ArgumentNullException">
    ///   <paramref name="triggers"/> is <see langword="null"/>.
    /// </exception>
    /// <exception cref="KeyNotFoundException">
    ///   No scheduled action has the ID.
    /// </exception>
    /// <exception cref="ArgumentException">
    ///   A trigger is invalid, or its interval is under the scheduled action's
    ///   <see cref="ScheduledActionInfo.MinimumInterval"/>, see
    ///   <see cref="ActionTrigger.GetValidationError(TimeSpan)"/>; or the
    ///   daily, weekly and monthly triggers run closer together than that
    ///   minimum.
    /// </exception>
    /// <exception cref="InvalidOperationException">
    ///   The server has not started yet.
    /// </exception>
    /// <returns>The scheduled action with its new triggers.</returns>
    ScheduledActionInfo SetTriggers(Guid actionId, IReadOnlyList<ActionTrigger> triggers);

    /// <summary>
    ///   Puts the scheduled action's own default triggers back in effect.
    /// </summary>
    /// <param name="actionId">The scheduled action ID.</param>
    /// <exception cref="KeyNotFoundException">
    ///   No scheduled action has the ID.
    /// </exception>
    /// <exception cref="InvalidOperationException">
    ///   The server has not started yet.
    /// </exception>
    /// <returns>The scheduled action with its default triggers.</returns>
    ScheduledActionInfo ResetTriggers(Guid actionId);

    /// <summary>
    ///   Queues a run of a scheduled action now, recorded as its last run by
    ///   hand, which the schedule only counts when it sets
    ///   <see cref="IScheduledAction.ScheduleCountsManualRuns"/>. It is not
    ///   held back by the <see cref="ScheduledActionInfo.MinimumInterval"/>,
    ///   but a run still waiting or running is not queued twice, nor recorded
    ///   again.
    /// </summary>
    /// <param name="actionId">The scheduled action ID.</param>
    /// <param name="token">Cancels the queuing, not the run.</param>
    /// <exception cref="KeyNotFoundException">
    ///   No scheduled action has the ID.
    /// </exception>
    /// <exception cref="InvalidOperationException">
    ///   The server has not started yet.
    /// </exception>
    /// <returns>
    ///   <see langword="null"/> when the run was queued, or already was, or
    ///   the reason the scheduled action refused to run.
    /// </returns>
    Task<ActionValidationResult?> InvokeAsync(Guid actionId, CancellationToken token = default);

    /// <summary>
    ///   Cancels the current runs of a scheduled action: a waiting one is taken
    ///   out of the queue, and a running one is asked to stop, which cannot be
    ///   undone.
    /// </summary>
    /// <param name="actionId">The scheduled action ID.</param>
    /// <param name="token">Cancels the bookkeeping of a removal, never the run.</param>
    /// <exception cref="KeyNotFoundException">
    ///   No scheduled action has the ID.
    /// </exception>
    /// <exception cref="InvalidOperationException">
    ///   The server has not started yet.
    /// </exception>
    /// <returns>
    ///   The scheduled action afterwards. Its
    ///   <see cref="ScheduledActionInfo.State"/> is
    ///   <see cref="ScheduledActionState.CancellationRequested"/> until a
    ///   running job stops, and stays <see cref="ScheduledActionState.Running"/>
    ///   when the job does not observe cancellation.
    /// </returns>
    Task<ScheduledActionInfo> Cancel(Guid actionId, CancellationToken token = default);
}
