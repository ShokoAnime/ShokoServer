using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Shoko.Abstractions.Actions;

namespace Shoko.Server.Scheduling;

/// <summary>
/// What the scheduler needs of the scheduled action registry: the scheduled
/// actions, and how a run of one is queued.
/// </summary>
public interface IScheduledActionSource
{
    /// <summary>
    /// Lists the registered scheduled actions.
    /// </summary>
    /// <returns>The scheduled actions, by category, category name and name.</returns>
    IReadOnlyList<ScheduledActionDefinition> GetActions();

    /// <summary>
    /// Gets a registered scheduled action by its type.
    /// </summary>
    /// <param name="actionType">The scheduled action type.</param>
    /// <exception cref="ArgumentNullException"><paramref name="actionType"/> is <c>null</c>.</exception>
    /// <returns>The scheduled action, or <c>null</c> when the type is not a registered one.</returns>
    ScheduledActionDefinition? GetAction(Type actionType);

    /// <summary>
    /// Validates a scheduled action and queues a run of it. A run still
    /// waiting or running is not queued twice.
    /// </summary>
    /// <param name="actionId">The scheduled action ID.</param>
    /// <param name="token">Cancels the queuing.</param>
    /// <exception cref="KeyNotFoundException">No scheduled action has the ID.</exception>
    /// <returns>
    /// <c>null</c> when the run was queued, or the reason the scheduled action
    /// refused.
    /// </returns>
    Task<ActionValidationResult?> InvokeAsync(Guid actionId, CancellationToken token);
}
