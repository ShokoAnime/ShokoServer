using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Shoko.Abstractions.Actions;

namespace Shoko.Abstractions.ScheduledActions;

/// <summary>
///   Work that runs on its own, on the triggers the admin set or on its
///   <see cref="DefaultTriggers"/>, and that an admin can run by hand. Core or
///   a plugin registers one by implementing this interface.
/// </summary>
/// <remarks>
///   Global and without a caller, so only an admin lists, runs or changes it,
///   through <see cref="Services.IScheduledActionService"/>. It takes no
///   parameters: a run with options is an <see cref="IExecutableAction"/>'s job.
///   Runs go through the job queue and are never queued twice. Its ID is a UUIDv5
///   of the class's full name under the plugin's ID, so renaming or moving the
///   class loses its stored triggers and last runs.
/// </remarks>
public interface IScheduledAction
{
    /// <summary>
    ///   The display name of the scheduled action.
    /// </summary>
    string Name { get; }

    /// <summary>
    ///   The description of the scheduled action.
    /// </summary>
    string? Description { get => null; }

    /// <summary>
    ///   The category the scheduled action is listed under. Defaults to
    ///   <see cref="ActionCategory.Miscellaneous"/>.
    /// </summary>
    ActionCategory Category { get => ActionCategory.Miscellaneous; }

    /// <summary>
    ///   Whether a client should ask before an admin runs it by hand. Defaults
    ///   to <see langword="false"/>.
    /// </summary>
    bool RequiresConfirmation { get => false; }

    /// <summary>
    ///   The question a client asks before a run by hand, or
    ///   <see langword="null"/> for a generic one. Only read when
    ///   <see cref="RequiresConfirmation"/> is set.
    /// </summary>
    string? ConfirmationMessage { get => null; }

    /// <summary>
    ///   When it runs on its own until the admin says otherwise. Defaults to
    ///   none, so it only runs by hand.
    /// </summary>
    /// <remarks>
    ///   An invalid trigger, one under <see cref="MinimumInterval"/>, or
    ///   daily, weekly and monthly triggers closer together than it, reject the
    ///   scheduled action at load time.
    /// </remarks>
    IReadOnlyList<ActionTrigger> DefaultTriggers { get => []; }

    /// <summary>
    ///   The shortest time after a run before it runs on its own again, or
    ///   <see langword="null"/> for <see cref="ActionTrigger.MinimumInterval"/>,
    ///   a minute.
    /// </summary>
    /// <remarks>
    ///   Set it when the work calls a service that bans clients asking too often.
    ///   Trigger times inside it are skipped and triggers closer together are
    ///   refused. It counts from the last run a trigger queued, or from any run
    ///   with <see cref="ScheduleCountsManualRuns"/>; a run by hand is never held
    ///   back. Whole minutes from <see cref="ActionTrigger.MinimumInterval"/> to
    ///   <see cref="ActionTrigger.MaximumInterval"/>, or it is rejected at load.
    /// </remarks>
    TimeSpan? MinimumInterval { get => null; }

    /// <summary>
    ///   Whether a run by hand counts for the schedule like a run a trigger
    ///   queued. Defaults to <see langword="false"/>.
    /// </summary>
    /// <remarks>
    ///   Left unset, a run by hand neither moves the next run nor opens a
    ///   <see cref="MinimumInterval"/> that skips one. Set it along with
    ///   <see cref="MinimumInterval"/> when the service minds how often it is
    ///   asked, whoever asks: a trigger time inside the minimum after a run by
    ///   hand is then skipped and logged at debug level.
    /// </remarks>
    bool ScheduleCountsManualRuns { get => false; }

    /// <summary>
    ///   Optional pre-check. Return a non-null result to refuse a run.
    ///   Default: always allowed.
    /// </summary>
    /// <remarks>
    ///   Asked twice, on two instances: before a run is queued, so a run by
    ///   hand is refused at once, and again in the queue right before
    ///   <see cref="Execute"/>, where a refusal skips the run rather than
    ///   failing it. Keep it cheap, free of side effects, and about the
    ///   present.
    /// </remarks>
    /// <param name="token">
    ///   Cancels the check: bound to the request or the scheduler on the first
    ///   call, and to the queue job on the second.
    /// </param>
    /// <returns>
    ///   A reason to refuse, or <see langword="null"/> to allow the run.
    /// </returns>
    Task<ActionValidationResult?> Validate(CancellationToken token)
        => Task.FromResult<ActionValidationResult?>(null);

    /// <summary>
    ///   Does the work, on a fresh instance resolved from DI.
    /// </summary>
    /// <remarks>
    ///   An exception is logged as a failure of the queue job.
    /// </remarks>
    /// <param name="progress">
    ///   Takes how far the run is, as a percentage from 0 to 100, shown on the
    ///   queue job. The job shows none until the first report, so report 0 as
    ///   soon as the work knows it will report. Values outside the range are
    ///   clamped.
    /// </param>
    /// <param name="token">
    ///   The queue job's token. It is cancelled when an admin cancels the run,
    ///   and when the worker pool stops. Stop by throwing an
    ///   <see cref="OperationCanceledException"/>: after a cancel the run ends
    ///   as cancelled, and after a shutdown it is queued again.
    /// </param>
    /// <returns>A task that completes when the work is done.</returns>
    Task Execute(IProgress<decimal> progress, CancellationToken token);
}
