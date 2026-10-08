using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Shoko.Abstractions.Actions;
using Shoko.Abstractions.Plugin;
using Shoko.Abstractions.ScheduledActions;
using Shoko.Abstractions.Utilities;
using Shoko.QueueProcessor.Abstractions;
using Shoko.QueueProcessor.Builder;
using Shoko.Server.Actions;
using Shoko.Server.Scheduling;
using Shoko.Server.Scheduling.Jobs.Actions;

namespace Shoko.Server.Services;

/// <summary>
/// Keeps the scheduled actions of the core and the plugins, checks what they
/// declare when they are loaded, and queues their runs.
/// </summary>
/// <param name="scheduler">The queue.</param>
/// <param name="pluginManager">Names the category of a plugin's own group.</param>
/// <param name="services">Resolves a fresh instance of a scheduled action.</param>
public sealed class ScheduledActionRegistry(IQueueScheduler scheduler, IPluginManager pluginManager, IServiceProvider services) : IScheduledActionSource
{
    #region Fields

    /// <summary>
    /// The registered scheduled actions by ID, with their types.
    /// </summary>
    private readonly Dictionary<Guid, (ScheduledActionDefinition Definition, Type ActionType)> _actions = [];

    /// <summary>
    /// The IDs of the registered scheduled actions by type.
    /// </summary>
    private readonly Dictionary<Type, Guid> _idsByType = [];

    /// <summary>
    /// The registered scheduled actions, in listing order, rebuilt on each
    /// registration.
    /// </summary>
    private IReadOnlyList<ScheduledActionDefinition> _ordered = [];

    #endregion

    #region Registration

    /// <summary>
    /// Registers the discovered scheduled action types and checks them.
    /// Called from <c>PluginManager.InitPlugins</c> for the core's and the
    /// plugins' alike.
    /// </summary>
    /// <param name="discoveredActions">The types, with the ID of the plugin that owns each.</param>
    /// <exception cref="InvalidOperationException">
    /// A type is also an <see cref="IExecutableAction"/>, or declares an
    /// invalid minimum interval or default trigger, or default triggers closer
    /// together than its minimum.
    /// </exception>
    public void AddParts(IEnumerable<(Guid PluginId, Type ActionType)> discoveredActions)
    {
        foreach (var (pluginId, actionType) in discoveredActions)
        {
            // One definition per piece of work: a type is one or the other.
            if (typeof(IExecutableAction).IsAssignableFrom(actionType))
                throw new InvalidOperationException($"Scheduled action type '{actionType.FullName}' is also an executable action. A type may be only one of the two.");

            // The same derivation as an executable action's ID, so the stored
            // schedules of the actions that became scheduled ones still match.
            var id = UuidUtility.GetV5(actionType.FullName!, pluginId);
            var probe = (IScheduledAction)services.GetRequiredService(actionType);
            var minimumInterval = probe.MinimumInterval ?? ActionTrigger.MinimumInterval;
            if (minimumInterval < ActionTrigger.MinimumInterval || minimumInterval > ActionTrigger.MaximumInterval || minimumInterval.Ticks % TimeSpan.TicksPerMinute is not 0)
            {
                throw new InvalidOperationException(
                    $"Scheduled action type '{actionType.FullName}' declares a minimum interval of {minimumInterval}, but it must be whole minutes from {ActionTrigger.MinimumInterval} to {ActionTrigger.MaximumInterval}."
                );
            }

            var defaultTriggers = (probe.DefaultTriggers ?? []).ToList();
            foreach (var trigger in defaultTriggers)
            {
                if ((trigger is null ? "A trigger is null." : trigger.GetValidationError(minimumInterval)) is { } error)
                    throw new InvalidOperationException($"Scheduled action type '{actionType.FullName}' declares an invalid default trigger: {error}");
            }

            if (ActionTriggerSchedule.GetSpacingError(defaultTriggers, minimumInterval) is { } spacingError)
                throw new InvalidOperationException($"Scheduled action type '{actionType.FullName}' declares default triggers that run it too often: {spacingError}");

            var categoryName = probe.Category is ActionCategory.PluginInferred
                ? pluginManager.GetPluginInfo(pluginId)?.Name ?? actionType.Assembly.GetName().Name!
                : probe.Category.ToString();
            var jobKey = probe is IQueueJobScheduledAction jobAction
                ? jobAction.JobKey
                : JobKeyBuilder<ScheduledActionJob>.Create().UsingJobData(ConfigureJob(id)).Build();
            var definition = new ScheduledActionDefinition(
                id,
                probe.Name,
                probe.Description,
                probe.Category,
                categoryName,
                probe.RequiresConfirmation,
                probe.ConfirmationMessage,
                pluginId,
                defaultTriggers,
                minimumInterval,
                probe.ScheduleCountsManualRuns,
                jobKey
            );
            _actions[id] = (definition, actionType);
            _idsByType[actionType] = id;
        }

        _ordered = _actions.Values
            .Select(entry => entry.Definition)
            .OrderBy(action => action.Category)
            .ThenBy(action => action.CategoryName)
            .ThenBy(action => action.Name)
            .ToList();
    }

    #endregion

    #region Lookups

    /// <inheritdoc/>
    public IReadOnlyList<ScheduledActionDefinition> GetActions()
        => _ordered;

    /// <inheritdoc/>
    public ScheduledActionDefinition? GetAction(Type actionType)
    {
        ArgumentNullException.ThrowIfNull(actionType);
        return _idsByType.TryGetValue(actionType, out var id) ? _actions[id].Definition : null;
    }

    /// <summary>
    /// Gets a registered scheduled action by its ID.
    /// </summary>
    /// <param name="actionId">The scheduled action ID.</param>
    /// <returns>The scheduled action, or <c>null</c> when none has the ID.</returns>
    public ScheduledActionDefinition? GetAction(Guid actionId)
        => _actions.TryGetValue(actionId, out var entry) ? entry.Definition : null;

    /// <summary>
    /// Resolves a fresh instance of a scheduled action from DI.
    /// </summary>
    /// <param name="actionId">The scheduled action ID.</param>
    /// <param name="provider">
    /// The container to resolve it from, such as a running job's own, so its
    /// scoped dependencies are that run's; <c>null</c> for the root one.
    /// </param>
    /// <exception cref="KeyNotFoundException">No scheduled action has the ID.</exception>
    /// <returns>The instance.</returns>
    public IScheduledAction CreateInstance(Guid actionId, IServiceProvider? provider = null)
        => _actions.TryGetValue(actionId, out var entry)
            ? (IScheduledAction)(provider ?? services).GetRequiredService(entry.ActionType)
            : throw new KeyNotFoundException($"No scheduled action has the ID {actionId}.");

    #endregion

    #region Running

    /// <inheritdoc/>
    public async Task<ActionValidationResult?> InvokeAsync(Guid actionId, CancellationToken token)
    {
        var action = CreateInstance(actionId);
        if (await action.Validate(token).ConfigureAwait(false) is { } refusal)
            return refusal;

        // A scheduled action whose work is one queue job queues that job
        // itself, so it keeps the job's own filters, limits and key.
        if (action is IQueueJobScheduledAction jobAction)
        {
            await jobAction.EnqueueJob(token).ConfigureAwait(false);
            return null;
        }

        var priority = QueuePriority.ScheduledFor(typeof(ScheduledActionJob), prioritize: false);
        await scheduler.EnqueueWithPriority(ConfigureJob(actionId), priority, ct: token).ConfigureAwait(false);
        return null;
    }

    /// <summary>
    /// Sets up the job that runs a scheduled action, which its dedup key is
    /// taken from.
    /// </summary>
    /// <param name="actionId">The scheduled action ID.</param>
    /// <returns>The job configurator.</returns>
    private static Action<ScheduledActionJob> ConfigureJob(Guid actionId)
        => job => job.ActionId = actionId;

    #endregion
}
