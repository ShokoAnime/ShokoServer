using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
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
/// Keeps the scheduled actions of the core and the plugins, the classes and
/// those their providers hand out, checks what they declare, and queues their
/// runs.
/// </summary>
/// <param name="scheduler">The queue.</param>
/// <param name="pluginManager">Names the category of a plugin's own group, and the plugin of a provider.</param>
/// <param name="services">Resolves a fresh instance of a scheduled action.</param>
/// <param name="logger">Logs the provided actions refused while the server runs.</param>
public sealed partial class ScheduledActionRegistry(
    IQueueScheduler scheduler,
    IPluginManager pluginManager,
    IServiceProvider services,
    ILogger<ScheduledActionRegistry> logger
) : IScheduledActionSource
{
    #region Fields

    /// <summary>
    /// Guards the registrations.
    /// </summary>
    private readonly Lock _lock = new();

    /// <summary>
    /// The registered scheduled actions by ID.
    /// </summary>
    private readonly Dictionary<Guid, Entry> _actions = [];

    /// <summary>
    /// The IDs of the registered scheduled action classes by type.
    /// </summary>
    private readonly Dictionary<Type, Guid> _idsByType = [];

    /// <summary>
    /// The registered scheduled actions, in listing order, replaced whole on
    /// each change so a reader never sees one half made.
    /// </summary>
    private IReadOnlyList<ScheduledActionDefinition> _ordered = [];

    /// <summary>
    /// The registered providers.
    /// </summary>
    private IReadOnlyList<IScheduledActionProvider> _providers = [];

    #endregion

    #region Properties

    /// <inheritdoc/>
    public IReadOnlyList<IScheduledActionProvider> Providers => _providers;

    #endregion

    #region Events

    /// <summary>
    /// Raised after a provider's actions changed, outside the lock.
    /// </summary>
    public event EventHandler<ScheduledActionSourceChangedEventArgs>? Changed;

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
        lock (_lock)
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
                var label = $"Scheduled action type '{actionType.FullName}'";
                var minimumInterval = CheckDeclarations(label, probe.MinimumInterval, probe.DefaultTriggers, out var defaultTriggers);
                var jobKey = probe is IQueueJobScheduledAction jobAction
                    ? jobAction.JobKey
                    : JobKeyBuilder<ScheduledActionJob>.Create().UsingJobData(ConfigureJob(id)).Build();
                var definition = new ScheduledActionDefinition(
                    id,
                    probe.Name,
                    probe.Description,
                    probe.Category,
                    GetCategoryName(probe.Category, pluginId, actionType),
                    probe.RequiresConfirmation,
                    probe.ConfirmationMessage,
                    pluginId,
                    defaultTriggers,
                    minimumInterval,
                    probe.ScheduleCountsManualRuns,
                    jobKey
                );
                _actions[id] = new(definition, actionType, null, null);
                _idsByType[actionType] = id;
            }

            Reorder();
        }
    }

    /// <summary>
    /// Registers the scheduled action providers and the actions they have at
    /// start-up, and follows their changes from then on. Called from
    /// <c>PluginManager.InitPlugins</c>.
    /// </summary>
    /// <param name="providers">The providers.</param>
    /// <exception cref="InvalidOperationException">
    /// A provider hands out an action with an invalid key, the same key twice,
    /// or invalid declarations, as for a class.
    /// </exception>
    public void AddProviders(IEnumerable<IScheduledActionProvider> providers)
    {
        var list = providers.ToList();
        lock (_lock)
            _providers = [.. _providers, .. list];
        foreach (var provider in list)
        {
            Sync(provider, atStart: true);
            provider.ActionsChanged += OnProviderChanged;
        }
    }

    /// <summary>
    /// Reads a provider's actions again when it says they changed.
    /// </summary>
    /// <param name="sender">The provider.</param>
    /// <param name="eventArgs">Nothing.</param>
    private void OnProviderChanged(object? sender, EventArgs eventArgs)
    {
        if (sender is not IScheduledActionProvider provider)
            return;

        try
        {
            Sync(provider, atStart: false);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Could not read the scheduled actions of {Provider} again", provider.GetType().FullName);
        }
    }

    /// <summary>
    /// Brings a provider's registered actions in line with what it lists now,
    /// and raises <see cref="Changed"/> when anything changed.
    /// </summary>
    /// <param name="provider">The provider.</param>
    /// <param name="atStart">
    /// Whether this is the start-up read, which throws on an invalid action;
    /// later an invalid action is logged and left out, as is a key given twice.
    /// </param>
    /// <exception cref="InvalidOperationException">An action is invalid, at start-up.</exception>
    private void Sync(IScheduledActionProvider provider, bool atStart)
    {
        var providerType = provider.GetType();
        var pluginId = pluginManager.GetPluginInfo(providerType.Assembly)?.ID ?? Guid.Empty;
        var wanted = new Dictionary<Guid, Entry>();
        foreach (var descriptor in provider.GetActions() ?? [])
        {
            try
            {
                var entry = Describe(provider, providerType, pluginId, descriptor);
                if (!wanted.TryAdd(entry.Definition.ID, entry))
                    throw new InvalidOperationException($"Scheduled action provider '{providerType.FullName}' hands out the key '{descriptor.Key}' twice.");
            }
            catch (Exception ex) when (!atStart && ex is InvalidOperationException or ArgumentException)
            {
                logger.LogError(ex, "Left out a scheduled action of {Provider}", providerType.FullName);
            }
        }

        var added = new List<ScheduledActionDefinition>();
        var updated = new List<ScheduledActionDefinition>();
        var removed = new List<ScheduledActionDefinition>();
        lock (_lock)
        {
            foreach (var (id, entry) in _actions.Where(pair => ReferenceEquals(pair.Value.Provider, provider)).ToList())
            {
                if (wanted.ContainsKey(id))
                    continue;

                _actions.Remove(id);
                removed.Add(entry.Definition);
            }

            foreach (var (id, entry) in wanted)
            {
                if (!_actions.TryGetValue(id, out var current))
                    added.Add(entry.Definition);
                else if (!SameDeclarations(current.Definition, entry.Definition))
                    updated.Add(entry.Definition);
                else
                    continue;

                _actions[id] = entry;
            }

            if (added.Count is 0 && updated.Count is 0 && removed.Count is 0)
                return;

            Reorder();
        }

        Changed?.Invoke(this, new(added, updated, removed));
    }

    /// <summary>
    /// Checks one provided action and makes its definition.
    /// </summary>
    /// <param name="provider">Its provider.</param>
    /// <param name="providerType">The provider's type.</param>
    /// <param name="pluginId">The ID of the plugin the provider is in.</param>
    /// <param name="descriptor">The action.</param>
    /// <returns>The entry.</returns>
    /// <exception cref="InvalidOperationException">The action is invalid.</exception>
    private Entry Describe(IScheduledActionProvider provider, Type providerType, Guid pluginId, ScheduledActionDescriptor descriptor)
    {
        if (descriptor is null)
            throw new InvalidOperationException($"Scheduled action provider '{providerType.FullName}' hands out a null action.");
        if (descriptor.Key is not { Length: > 0 and <= 64 } key || !KeyRegex().IsMatch(key))
            throw new InvalidOperationException($"Scheduled action provider '{providerType.FullName}' hands out the key '{descriptor.Key}'; a key is lowercase kebab-case, at most 64 characters.");
        if (string.IsNullOrWhiteSpace(descriptor.Name))
            throw new InvalidOperationException($"Scheduled action provider '{providerType.FullName}' hands out the action '{key}' without a name.");

        var label = $"Scheduled action '{key}' of provider '{providerType.FullName}'";
        var minimumInterval = CheckDeclarations(label, descriptor.MinimumInterval, descriptor.DefaultTriggers, out var defaultTriggers);
        var id = UuidUtility.GetV5($"ScheduledActionProvider={providerType.FullName},Action={key}", pluginId);
        var definition = new ScheduledActionDefinition(
            id,
            descriptor.Name,
            descriptor.Description,
            descriptor.Category,
            GetCategoryName(descriptor.Category, pluginId, providerType),
            descriptor.RequiresConfirmation,
            descriptor.ConfirmationMessage,
            pluginId,
            defaultTriggers,
            minimumInterval,
            descriptor.ScheduleCountsManualRuns,
            JobKeyBuilder<ScheduledActionJob>.Create().UsingJobData(ConfigureJob(id)).Build()
        );
        return new(definition, null, provider, key);
    }

    /// <summary>
    /// Checks a minimum interval and the default triggers against it.
    /// </summary>
    /// <param name="label">Names the action in a message.</param>
    /// <param name="declaredMinimum">The declared minimum interval, or <c>null</c> for the least allowed.</param>
    /// <param name="declaredTriggers">The declared default triggers.</param>
    /// <param name="defaultTriggers">The default triggers, as a list.</param>
    /// <returns>The minimum interval in effect.</returns>
    /// <exception cref="InvalidOperationException">Either is invalid.</exception>
    private static TimeSpan CheckDeclarations(string label, TimeSpan? declaredMinimum, IReadOnlyList<ActionTrigger>? declaredTriggers, out List<ActionTrigger> defaultTriggers)
    {
        var minimumInterval = declaredMinimum ?? ActionTrigger.MinimumInterval;
        if (minimumInterval < ActionTrigger.MinimumInterval || minimumInterval > ActionTrigger.MaximumInterval || minimumInterval.Ticks % TimeSpan.TicksPerMinute is not 0)
            throw new InvalidOperationException($"{label} declares a minimum interval of {minimumInterval}, but it must be whole minutes from {ActionTrigger.MinimumInterval} to {ActionTrigger.MaximumInterval}.");

        defaultTriggers = (declaredTriggers ?? []).ToList();
        foreach (var trigger in defaultTriggers)
        {
            if ((trigger is null ? "A trigger is null." : trigger.GetValidationError(minimumInterval)) is { } error)
                throw new InvalidOperationException($"{label} declares an invalid default trigger: {error}");
        }

        if (ActionTriggerSchedule.GetSpacingError(defaultTriggers, minimumInterval) is { } spacingError)
            throw new InvalidOperationException($"{label} declares default triggers that run it too often: {spacingError}");

        return minimumInterval;
    }

    /// <summary>
    /// Whether two definitions of one action declare the same.
    /// </summary>
    /// <param name="a">One.</param>
    /// <param name="b">The other.</param>
    /// <returns><c>true</c> when nothing a listing shows differs.</returns>
    private static bool SameDeclarations(ScheduledActionDefinition a, ScheduledActionDefinition b)
        => a with { DefaultTriggers = b.DefaultTriggers } == b && a.DefaultTriggers.SequenceEqual(b.DefaultTriggers);

    /// <summary>
    /// The name of the group an action is listed under.
    /// </summary>
    /// <param name="category">Its category.</param>
    /// <param name="pluginId">The ID of its plugin.</param>
    /// <param name="type">Its class or provider's, whose assembly names an unknown plugin.</param>
    /// <returns>The name.</returns>
    private string GetCategoryName(ActionCategory category, Guid pluginId, Type type)
        => category is ActionCategory.PluginInferred
            ? pluginManager.GetPluginInfo(pluginId)?.Name ?? type.Assembly.GetName().Name!
            : category.ToString();

    /// <summary>
    /// Rebuilds the listing order. Called under the lock.
    /// </summary>
    private void Reorder()
        => _ordered = _actions.Values
            .Select(entry => entry.Definition)
            .OrderBy(action => action.Category)
            .ThenBy(action => action.CategoryName)
            .ThenBy(action => action.Name)
            .ToList();

    [GeneratedRegex("^[a-z0-9]+(-[a-z0-9]+)*$")]
    private static partial Regex KeyRegex();

    #endregion

    #region Lookups

    /// <inheritdoc/>
    public IReadOnlyList<ScheduledActionDefinition> GetActions()
        => _ordered;

    /// <inheritdoc/>
    public ScheduledActionDefinition? GetAction(Type actionType)
    {
        ArgumentNullException.ThrowIfNull(actionType);
        lock (_lock)
            return _idsByType.TryGetValue(actionType, out var id) && _actions.TryGetValue(id, out var entry) ? entry.Definition : null;
    }

    /// <summary>
    /// Gets a registered scheduled action by its ID.
    /// </summary>
    /// <param name="actionId">The scheduled action ID.</param>
    /// <returns>The scheduled action, or <c>null</c> when none has the ID.</returns>
    public ScheduledActionDefinition? GetAction(Guid actionId)
    {
        lock (_lock)
            return _actions.TryGetValue(actionId, out var entry) ? entry.Definition : null;
    }

    /// <summary>
    /// Makes a fresh instance of a scheduled action: a class's from DI, or one
    /// running a provided action through its provider.
    /// </summary>
    /// <param name="actionId">The scheduled action ID.</param>
    /// <param name="provider">
    /// The container to resolve a class from, such as a running job's own, so
    /// its scoped dependencies are that run's; <c>null</c> for the root one.
    /// </param>
    /// <exception cref="KeyNotFoundException">No scheduled action has the ID.</exception>
    /// <returns>The instance.</returns>
    public IScheduledAction CreateInstance(Guid actionId, IServiceProvider? provider = null)
    {
        Entry entry;
        lock (_lock)
        {
            if (!_actions.TryGetValue(actionId, out entry!))
                throw new KeyNotFoundException($"No scheduled action has the ID {actionId}.");
        }

        return entry.ActionType is { } actionType
            ? (IScheduledAction)(provider ?? services).GetRequiredService(actionType)
            : new ProvidedScheduledAction(entry.Provider!, entry.Key!, entry.Definition);
    }

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

    #region Types

    /// <summary>
    /// A registered scheduled action: a class, or a provider's action.
    /// </summary>
    /// <param name="Definition">What it declares.</param>
    /// <param name="ActionType">Its class, or <c>null</c> for a provided one.</param>
    /// <param name="Provider">Its provider, or <c>null</c> for a class.</param>
    /// <param name="Key">Its key within the provider, or <c>null</c> for a class.</param>
    private sealed record Entry(ScheduledActionDefinition Definition, Type? ActionType, IScheduledActionProvider? Provider, string? Key);

    /// <summary>
    /// Runs a provided action as a scheduled action instance.
    /// </summary>
    /// <param name="provider">Its provider.</param>
    /// <param name="key">Its key.</param>
    /// <param name="definition">What it declares.</param>
    private sealed class ProvidedScheduledAction(IScheduledActionProvider provider, string key, ScheduledActionDefinition definition) : IScheduledAction
    {
        /// <inheritdoc/>
        public string Name => definition.Name;

        /// <inheritdoc/>
        public string? Description => definition.Description;

        /// <inheritdoc/>
        public ActionCategory Category => definition.Category;

        /// <inheritdoc/>
        public bool RequiresConfirmation => definition.RequiresConfirmation;

        /// <inheritdoc/>
        public string? ConfirmationMessage => definition.ConfirmationMessage;

        /// <inheritdoc/>
        public IReadOnlyList<ActionTrigger> DefaultTriggers => definition.DefaultTriggers;

        /// <inheritdoc/>
        public TimeSpan? MinimumInterval => definition.MinimumInterval;

        /// <inheritdoc/>
        public bool ScheduleCountsManualRuns => definition.ScheduleCountsManualRuns;

        /// <inheritdoc/>
        public Task<ActionValidationResult?> Validate(CancellationToken token)
            => provider.Validate(key, token);

        /// <inheritdoc/>
        public Task Execute(IProgress<decimal> progress, CancellationToken token)
            => provider.Execute(key, progress, token);
    }

    #endregion
}
