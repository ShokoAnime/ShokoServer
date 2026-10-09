using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NJsonSchema;
using Shoko.Abstractions.Actions;
using Shoko.Abstractions.Actions.Services;
using Shoko.Abstractions.Config.Services;
using Shoko.Abstractions.Exceptions;
using Shoko.Abstractions.Extensions;
using Shoko.Abstractions.Metadata.Anidb.Enums;
using Shoko.Abstractions.Metadata.Anidb.Services;
using Shoko.Abstractions.Metadata.Shoko;
using Shoko.Abstractions.Plugin;
using Shoko.Abstractions.UI;
using Shoko.Abstractions.User;
using Shoko.Abstractions.Utilities;
using Shoko.Abstractions.Video;
using Shoko.Abstractions.Video.Services;
using Shoko.QueueProcessor.Abstractions;
using Shoko.QueueProcessor.Scheduling;
using Shoko.Server.Actions;
using Shoko.Server.Databases;
using Shoko.Server.Models.Shoko;
using Shoko.Server.Models.Shoko.Embedded;
using Shoko.Server.Providers.AniDB;
using Shoko.Server.Providers.AniDB.Interfaces;
using Shoko.Server.Providers.AniDB.UDP.Info;
using Shoko.Server.Repositories.Cached;
using Shoko.Server.Repositories.Cached.AniDB;
using Shoko.Server.Repositories.Direct;
using Shoko.Server.Scheduling.Jobs.Actions;
using Shoko.Server.Scheduling.Jobs.AniDB;
using Shoko.Server.Scheduling.Jobs.Shoko;
using Shoko.Server.Services.Configuration;
using Shoko.Server.Settings;
using Shoko.Server.Utilities;

namespace Shoko.Server.Services;

public class ActionService : IActionService
{
    private readonly ILogger<ActionService> _logger;

    private readonly IQueueScheduler _scheduler;

    private readonly IRequestFactory _requestFactory;

    private readonly ISettingsProvider _settingsProvider;

    private readonly IVideoReleaseService _videoReleaseService;

    private readonly IAnidbService _anidbService;

    private readonly IVideoService _videoService;

    private readonly DatabaseFactory _databaseFactory;

    private readonly HttpXmlUtils _xmlUtils;

    private readonly IPluginPackageManager _pluginPackageManager;

    private readonly IPluginManager _pluginManager;

    private readonly IServiceProvider _services;

    private readonly ActionUiDefinitionBuilder _actionUiDefinitionBuilder;

    private readonly IConfigurationService _configurationService;

    /// <summary>
    ///   Registered action types and their metadata. Populated once during
    ///   <see cref="AddParts"/>. A fresh transient instance is resolved from
    ///   DI for every validation and execution.
    /// </summary>
    private readonly Dictionary<Guid, RegisteredAction> _actions = new();

    /// <summary>
    ///   A registered action type paired with the metadata exposed to plugins.
    ///   The concrete type is deliberately not part of
    ///   <see cref="ExecutableActionInfo"/> so the abstraction surface never
    ///   leaks server internals.
    /// </summary>
    /// <param name="Info">The metadata exposed to plugins.</param>
    /// <param name="ActionType">The concrete action type.</param>
    /// <param name="ParameterSchema">
    ///   The schema an invocation payload is checked against, or
    ///   <c>null</c> when the action declares no parameters.
    /// </param>
    private sealed record RegisteredAction(
        ExecutableActionInfo Info,
        Type ActionType,
        JsonSchema? ParameterSchema
    );

    /// <summary>
    ///   The same registrations keyed by their concrete action type.
    /// </summary>
    private readonly Dictionary<Type, RegisteredAction> _actionsByType = new();

    private readonly VideoLocalRepository _videoLocals;

    private readonly VideoLocal_PlaceRepository _videoLocalPlaces;

    private readonly StoredReleaseInfoRepository _storedReleaseInfos;

    private readonly AniDB_AnimeRepository _anidbAnimes;

    private readonly AniDB_EpisodeRepository _anidbEpisodes;

    private readonly AniDB_CreatorRepository _anidbCreators;

    private readonly AniDB_MessageRepository _anidbMessages;

    private readonly CrossRef_File_EpisodeRepository _crossRefFileEpisodes;

    private readonly AnimeSeriesRepository _animeSeries;

    private readonly AnimeEpisodeRepository _animeEpisodes;

    private readonly ScheduledUpdateRepository _scheduledUpdates;

    private readonly AniDB_Anime_RelationRepository _anidbAnimeRelations;

    public ActionService(
        ILogger<ActionService> logger,
        IQueueScheduler schedulerFactory,
        IRequestFactory requestFactory,
        ISettingsProvider settingsProvider,
        IVideoReleaseService videoReleaseService,
        IAnidbService anidbService,
        IVideoService videoService,
        DatabaseFactory databaseFactory,
        HttpXmlUtils xmlUtils,
        IPluginPackageManager pluginPackageManager,
        IPluginManager pluginManager,
        IServiceProvider services,
        ActionUiDefinitionBuilder actionUiDefinitionBuilder,
        IConfigurationService configurationService,
        VideoLocalRepository videoLocals,
        VideoLocal_PlaceRepository videoLocalPlaces,
        StoredReleaseInfoRepository storedReleaseInfos,
        AniDB_AnimeRepository anidbAnimes,
        AniDB_EpisodeRepository anidbEpisodes,
        AniDB_CreatorRepository anidbCreators,
        AniDB_MessageRepository anidbMessages,
        CrossRef_File_EpisodeRepository crossRefFileEpisodes,
        AnimeSeriesRepository animeSeries,
        AnimeEpisodeRepository animeEpisodes,
        ScheduledUpdateRepository scheduledUpdates,
        AniDB_Anime_RelationRepository anidbAnimeRelations
    )
    {
        _logger = logger;
        _scheduler = schedulerFactory;
        _requestFactory = requestFactory;
        _settingsProvider = settingsProvider;
        _videoReleaseService = videoReleaseService;
        _anidbService = anidbService;
        _videoService = videoService;
        _databaseFactory = databaseFactory;
        _xmlUtils = xmlUtils;
        _pluginPackageManager = pluginPackageManager;
        _pluginManager = pluginManager;
        _services = services;
        _actionUiDefinitionBuilder = actionUiDefinitionBuilder;
        _configurationService = configurationService;
        _videoLocals = videoLocals;
        _videoLocalPlaces = videoLocalPlaces;
        _storedReleaseInfos = storedReleaseInfos;
        _anidbAnimes = anidbAnimes;
        _anidbEpisodes = anidbEpisodes;
        _anidbCreators = anidbCreators;
        _anidbMessages = anidbMessages;
        _crossRefFileEpisodes = crossRefFileEpisodes;
        _animeSeries = animeSeries;
        _animeEpisodes = animeEpisodes;
        _scheduledUpdates = scheduledUpdates;
        _anidbAnimeRelations = anidbAnimeRelations;
    }

    #region Action Registry

    /// <summary>
    ///   Registers discovered action types and validates them. Called from
    ///   <c>PluginManager.InitPlugins</c> for core and plugin-provided
    ///   actions alike.
    /// </summary>
    /// <remarks>
    ///   Fails fast with a named error when a registered type breaks one of
    ///   the load-time rules, rather than a NRE three weeks in.
    /// </remarks>
    /// <param name="discoveredActions">
    ///   The discovered action types and the ID of the plugin that owns them.
    /// </param>
    public void AddParts(IEnumerable<(Guid PluginId, Type ActionType)> discoveredActions)
    {
        foreach (var (pluginId, actionType) in discoveredActions)
        {
            // Reject any type implementing IScopedAction that isn't one of the four base
            // classes. The guard is redundant — IScopedAction is internal, so a plugin
            // assembly cannot implement it — but it is intentionally kept so misuse fails
            // fast at startup instead of at execution time.
            var baseType = actionType.BaseType;
            if (typeof(IScopedAction).IsAssignableFrom(actionType) &&
                baseType != typeof(SeriesAction) &&
                baseType != typeof(GroupAction) &&
                baseType != typeof(EpisodeAction) &&
                baseType != typeof(VideoAction))
            {
                throw new InvalidOperationException(
                    $"Action type '{actionType.FullName}' implements IScopedAction but does not derive from " +
                    $"{nameof(SeriesAction)}, {nameof(GroupAction)}, {nameof(EpisodeAction)}, or {nameof(VideoAction)}."
                );
            }

            // IDs are UUIDv5, deterministic, namespaced by the owning plugin's ID, and
            // deliberately not stable across class renames or namespace moves.
            var id = UuidUtility.GetV5(actionType.FullName!, pluginId);

            var scope = actionType.IsAssignableTo(typeof(SeriesAction)) ? ActionScope.Series
                : actionType.IsAssignableTo(typeof(GroupAction)) ? ActionScope.Group
                : actionType.IsAssignableTo(typeof(EpisodeAction)) ? ActionScope.Episode
                : actionType.IsAssignableTo(typeof(VideoAction)) ? ActionScope.Video
                : ActionScope.Global;

            var probe = (IExecutableAction)_services.GetRequiredService(actionType);

            // No silent default for Permission — every action must declare it on the type
            // itself, and a getter provided by a base type or an interface default is
            // rejected at load time.
            if (actionType.GetProperty(nameof(IExecutableAction.Permission))?.GetMethod?.DeclaringType != actionType)
            {
                throw new InvalidOperationException(
                    $"Action type '{actionType.FullName}' does not declare its own Permission. " +
                    "Every action must state its permission explicitly."
                );
            }

            // PluginInferred resolves to the owning plugin's own display name — collision-free
            // by construction since plugin names are unique. Anything else falls back to the
            // category's own name.
            var categoryName = probe.Category is ActionCategory.PluginInferred
                ? _pluginManager.GetPluginInfo(pluginId)?.Name ?? actionType.Assembly.GetName().Name!
                : probe.Category.ToString();

            // The action's parameters are its own settable, serialized
            // properties, described the same way a configuration is. Null when
            // the action declares none.
            var parameters = _actionUiDefinitionBuilder.Build(id, probe.Name, probe.Description, actionType, listsOptions: true);

            var info = new ExecutableActionInfo(
                id,
                probe.Name,
                probe.Description,
                probe.Category,
                categoryName,
                probe.IsPrimaryAction,
                scope,
                probe.Permission,
                probe.RequiresConfirmation,
                probe.ConfirmationMessage,
                pluginId,
                parameters?.Definition
            );
            _actions[id] = _actionsByType[actionType] = new RegisteredAction(info, actionType, parameters?.Schema);
        }
    }

    /// <summary>
    ///   Lists registered actions. <paramref name="scope"/> is a filter, not a
    ///   required partition — omitting it lists every action. Non-admin callers
    ///   only see actions they may invoke.
    /// </summary>
    public IReadOnlyList<ExecutableActionInfo> GetActions(ActionScope? scope = null, ActionPermission? callerPermission = null)
        => _actions.Values
            .Select(a => a.Info)
            .Where(a => (scope is null || a.Scope == scope) &&
                        (callerPermission != ActionPermission.User || a.Permission == ActionPermission.User))
            .OrderBy(a => a.Category)
            .ThenBy(a => a.CategoryName)
            .ThenBy(a => a.Name)
            .ToList();

    public ExecutableActionInfo? GetActionInfo(Guid actionId)
        => _actions.TryGetValue(actionId, out var info) ? info.Info : null;

    public ExecutableActionInfo? GetActionInfo<TAction>() where TAction : class, IExecutableAction
        => GetActionInfo(typeof(TAction));

    public ExecutableActionInfo? GetActionInfo(Type actionType)
    {
        ArgumentNullException.ThrowIfNull(actionType);

        return _actionsByType.TryGetValue(actionType, out var info) ? info.Info : null;
    }

    public string GetActionName(Guid actionId)
        => _actions.TryGetValue(actionId, out var info) ? info.Info.Name : actionId.ToString();

    /// <summary>
    ///   The concrete action type for an action ID. Kept internal — plugins
    ///   work with <see cref="ExecutableActionInfo"/> and IDs only, while the
    ///   execution job uses this to resolve a fresh instance from DI.
    /// </summary>
    internal Type GetActionType(Guid actionId)
        => _actions.TryGetValue(actionId, out var info)
            ? info.ActionType
            : throw new KeyNotFoundException($"No action registered for {actionId}");

    /// <summary>
    ///   Populates the action's free-form properties (the open-ended invocation
    ///   parameter case) from a parameter payload. Unknown property names are
    ///   ignored. Used both before <see cref="IExecutableAction.Validate"/>
    ///   runs on the probe instance and before
    ///   <see cref="IExecutableAction.Execute"/> runs inside
    ///   <see cref="ActionExecutionJob"/>, so both observe the same
    ///   caller-supplied values.
    /// </summary>
    /// <param name="action">The action to populate.</param>
    /// <param name="parameters">The parameters, or <c>null</c>.</param>
    /// <exception cref="GenericValidationException">
    ///   A value could not be read into its parameter, keyed by the path of
    ///   that value.
    /// </exception>
    internal static void PopulateParameters(IExecutableAction action, IReadOnlyDictionary<string, object?>? parameters)
    {
        if (parameters is not { Count: > 0 })
            return;

        // A converter's own exception carries no path, so it is taken from the reader.
        using var reader = new JsonTextReader(new StringReader(JsonConvert.SerializeObject(parameters, _populateSettings)));
        try
        {
            JsonSerializer.Create(_populateSettings).Populate(reader, action);
        }
        catch (JsonException ex)
        {
            var path = ex is JsonSerializationException { Path.Length: > 0 } serializationException ? serializationException.Path : reader.Path;
            throw new GenericValidationException(ex.Message, new Dictionary<string, IReadOnlyList<string>> { [path] = [ex.Message] });
        }
    }

    /// <summary>
    ///   The action's own metadata is hidden from population as well as from
    ///   the schema, so a payload naming <c>Name</c> or <c>Permission</c> cannot
    ///   write to the instance even if it somehow reaches here unvalidated. A
    ///   flags enum is read from the list of its members, and a type parsable
    ///   from text from that text.
    /// </summary>
    private static readonly JsonSerializerSettings _populateSettings = new()
    {
        ContractResolver = new ActionMetadataContractResolver(),
        Converters = [FlagEnumNewtonsoftConverter.Instance, ParsableNewtonsoftConverter.Instance],
    };

    /// <inheritdoc />
    public IReadOnlyDictionary<string, IReadOnlyList<string>> ValidateParameters(Guid actionId, JObject? parameters)
    {
        if (!_actions.TryGetValue(actionId, out var registered))
            throw new KeyNotFoundException($"No action registered for {actionId}");

        // No body is how every action has always been invoked, and how one that
        // takes no parameters still is. There is nothing to check.
        if (parameters is null)
            return new Dictionary<string, IReadOnlyList<string>>();

        if (registered.ParameterSchema is not { } schema)
        {
            return parameters.Count is 0
                ? new Dictionary<string, IReadOnlyList<string>>()
                : new Dictionary<string, IReadOnlyList<string>>
                {
                    [string.Empty] = [$"The action '{registered.Info.Name}' does not take any parameters."],
                };
        }

        return _configurationService.Validate(parameters.ToString(Formatting.None), schema);
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<UiOption>> GetParameterOptionsAsync(
        Guid actionId,
        string path,
        IReadOnlyDictionary<string, object?>? parameters = null,
        IUser? caller = null,
        CancellationToken token = default
    ) => GetParameterOptionsCoreAsync(actionId, null, path, parameters, caller, token);

    /// <inheritdoc />
    public Task<IReadOnlyList<UiOption>> GetParameterOptionsAsync(
        Guid actionId,
        IShokoGroup group,
        string path,
        IReadOnlyDictionary<string, object?>? parameters = null,
        IUser? caller = null,
        CancellationToken token = default
    ) => GetParameterOptionsCoreAsync(actionId, group, path, parameters, caller, token);

    /// <inheritdoc />
    public Task<IReadOnlyList<UiOption>> GetParameterOptionsAsync(
        Guid actionId,
        IShokoSeries series,
        string path,
        IReadOnlyDictionary<string, object?>? parameters = null,
        IUser? caller = null,
        CancellationToken token = default
    ) => GetParameterOptionsCoreAsync(actionId, series, path, parameters, caller, token);

    /// <inheritdoc />
    public Task<IReadOnlyList<UiOption>> GetParameterOptionsAsync(
        Guid actionId,
        IShokoEpisode episode,
        string path,
        IReadOnlyDictionary<string, object?>? parameters = null,
        IUser? caller = null,
        CancellationToken token = default
    ) => GetParameterOptionsCoreAsync(actionId, episode, path, parameters, caller, token);

    /// <inheritdoc />
    public Task<IReadOnlyList<UiOption>> GetParameterOptionsAsync(
        Guid actionId,
        IVideo video,
        string path,
        IReadOnlyDictionary<string, object?>? parameters = null,
        IUser? caller = null,
        CancellationToken token = default
    ) => GetParameterOptionsCoreAsync(actionId, video, path, parameters, caller, token);

    /// <summary>
    ///   The options entry point, scope-agnostic in the same way as
    ///   <see cref="InvokeCoreAsync"/>.
    /// </summary>
    /// <exception cref="GenericValidationException">
    ///   The action may not be invoked here or by this caller, the path does
    ///   not lead to a parameter that takes options, or a parameter value
    ///   cannot be read.
    /// </exception>
    private Task<IReadOnlyList<UiOption>> GetParameterOptionsCoreAsync(
        Guid actionId,
        object? scopeEntity,
        string path,
        IReadOnlyDictionary<string, object?>? parameters,
        IUser? caller,
        CancellationToken token
    )
    {
        var registered = ResolveAction(actionId);
        var rejection = CheckApplicable(registered, ScopeOf(scopeEntity), caller);
        var (probe, hidden) = rejection is null ? PrepareProbe(registered, scopeEntity, parameters, caller) : (null, rejection);
        if (hidden is not null)
            throw new GenericValidationException(hidden.Reason, new Dictionary<string, IReadOnlyList<string>> { [string.Empty] = [hidden.Reason] });

        OptionsRequest request;
        try
        {
            request = UiOptionsProvider.Resolve(probe!, path, isNewtonsoftJson: true);
        }
        catch (ArgumentException ex)
        {
            throw new GenericValidationException(ex.Message, new Dictionary<string, IReadOnlyList<string>> { [nameof(path)] = [ex.Message] });
        }

        // Handed what execution has: the prepared instance, its entity and its
        // caller, with services for anything else.
        return UiOptionsProvider.InvokeAsync(
            request.Method,
            _pluginManager,
            request.Owner,
            [probe, scopeEntity, caller, token],
            ConvertParameterValue,
            request.Key
        );
    }

    /// <summary>
    ///   Serialises a parameter value the way the action's own parameter
    ///   schema was generated.
    /// </summary>
    private static JToken? ConvertParameterValue(object? value)
        => value is null ? null : JToken.FromObject(value, JsonSerializer.Create(ShokoJsonSerializers.CreateNewtonsoftSettings()));

    /// <inheritdoc cref="IActionService.InvokeAsync(Guid, IReadOnlyDictionary{string, object?}, IUser?, CancellationToken)"/>
    public Task<ActionValidationResult?> InvokeAsync(Guid actionId, IReadOnlyDictionary<string, object?>? parameters = null, IUser? caller = null, CancellationToken token = default)
        => InvokeCoreAsync(actionId, scopeEntity: null, parameters, caller, token);

    /// <inheritdoc cref="IActionService.InvokeAsync(Guid, IShokoGroup, IReadOnlyDictionary{string, object?}, IUser?, CancellationToken)"/>
    public Task<ActionValidationResult?> InvokeAsync(Guid actionId, IShokoGroup group, IReadOnlyDictionary<string, object?>? parameters = null, IUser? caller = null, CancellationToken token = default)
        => InvokeCoreAsync(actionId, group, parameters, caller, token);

    /// <inheritdoc cref="IActionService.InvokeAsync(Guid, IShokoSeries, IReadOnlyDictionary{string, object?}, IUser?, CancellationToken)"/>
    public Task<ActionValidationResult?> InvokeAsync(Guid actionId, IShokoSeries series, IReadOnlyDictionary<string, object?>? parameters = null, IUser? caller = null, CancellationToken token = default)
        => InvokeCoreAsync(actionId, series, parameters, caller, token);

    /// <inheritdoc cref="IActionService.InvokeAsync(Guid, IShokoEpisode, IReadOnlyDictionary{string, object?}, IUser?, CancellationToken)"/>
    public Task<ActionValidationResult?> InvokeAsync(Guid actionId, IShokoEpisode episode, IReadOnlyDictionary<string, object?>? parameters = null, IUser? caller = null, CancellationToken token = default)
        => InvokeCoreAsync(actionId, episode, parameters, caller, token);

    /// <inheritdoc cref="IActionService.InvokeAsync(Guid, IVideo, IReadOnlyDictionary{string, object?}, IUser?, CancellationToken)"/>
    public Task<ActionValidationResult?> InvokeAsync(Guid actionId, IVideo video, IReadOnlyDictionary<string, object?>? parameters = null, IUser? caller = null, CancellationToken token = default)
        => InvokeCoreAsync(actionId, video, parameters, caller, token);

    /// <inheritdoc cref="IActionService.ValidateAsync(Guid, IReadOnlyDictionary{string, object?}, IUser?, CancellationToken)"/>
    public Task<ActionValidationResult?> ValidateAsync(Guid actionId, IReadOnlyDictionary<string, object?>? parameters = null, IUser? caller = null, CancellationToken token = default)
        => ValidateCoreAsync(actionId, scopeEntity: null, parameters, caller, token);

    /// <inheritdoc cref="IActionService.ValidateAsync(Guid, IShokoGroup, IReadOnlyDictionary{string, object?}, IUser?, CancellationToken)"/>
    public Task<ActionValidationResult?> ValidateAsync(Guid actionId, IShokoGroup group, IReadOnlyDictionary<string, object?>? parameters = null, IUser? caller = null, CancellationToken token = default)
        => ValidateCoreAsync(actionId, group, parameters, caller, token);

    /// <inheritdoc cref="IActionService.ValidateAsync(Guid, IShokoSeries, IReadOnlyDictionary{string, object?}, IUser?, CancellationToken)"/>
    public Task<ActionValidationResult?> ValidateAsync(Guid actionId, IShokoSeries series, IReadOnlyDictionary<string, object?>? parameters = null, IUser? caller = null, CancellationToken token = default)
        => ValidateCoreAsync(actionId, series, parameters, caller, token);

    /// <inheritdoc cref="IActionService.ValidateAsync(Guid, IShokoEpisode, IReadOnlyDictionary{string, object?}, IUser?, CancellationToken)"/>
    public Task<ActionValidationResult?> ValidateAsync(Guid actionId, IShokoEpisode episode, IReadOnlyDictionary<string, object?>? parameters = null, IUser? caller = null, CancellationToken token = default)
        => ValidateCoreAsync(actionId, episode, parameters, caller, token);

    /// <inheritdoc cref="IActionService.ValidateAsync(Guid, IVideo, IReadOnlyDictionary{string, object?}, IUser?, CancellationToken)"/>
    public Task<ActionValidationResult?> ValidateAsync(Guid actionId, IVideo video, IReadOnlyDictionary<string, object?>? parameters = null, IUser? caller = null, CancellationToken token = default)
        => ValidateCoreAsync(actionId, video, parameters, caller, token);

    /// <inheritdoc cref="IActionService.InvokeBulkAsync(Guid, IReadOnlyList{IShokoGroup}, IReadOnlyDictionary{string, object?}, IUser?, CancellationToken)"/>
    public Task InvokeBulkAsync(Guid actionId, IReadOnlyList<IShokoGroup> groups, IReadOnlyDictionary<string, object?>? parameters = null, IUser? caller = null, CancellationToken token = default)
        => InvokeBulkCoreAsync(actionId, groups, ActionScope.Group, parameters, caller, token);

    /// <inheritdoc cref="IActionService.InvokeBulkAsync(Guid, IReadOnlyList{IShokoSeries}, IReadOnlyDictionary{string, object?}, IUser?, CancellationToken)"/>
    public Task InvokeBulkAsync(Guid actionId, IReadOnlyList<IShokoSeries> series, IReadOnlyDictionary<string, object?>? parameters = null, IUser? caller = null, CancellationToken token = default)
        => InvokeBulkCoreAsync(actionId, series, ActionScope.Series, parameters, caller, token);

    /// <inheritdoc cref="IActionService.InvokeBulkAsync(Guid, IReadOnlyList{IShokoEpisode}, IReadOnlyDictionary{string, object?}, IUser?, CancellationToken)"/>
    public Task InvokeBulkAsync(Guid actionId, IReadOnlyList<IShokoEpisode> episodes, IReadOnlyDictionary<string, object?>? parameters = null, IUser? caller = null, CancellationToken token = default)
        => InvokeBulkCoreAsync(actionId, episodes, ActionScope.Episode, parameters, caller, token);

    /// <inheritdoc cref="IActionService.InvokeBulkAsync(Guid, IReadOnlyList{IVideo}, IReadOnlyDictionary{string, object?}, IUser?, CancellationToken)"/>
    public Task InvokeBulkAsync(Guid actionId, IReadOnlyList<IVideo> videos, IReadOnlyDictionary<string, object?>? parameters = null, IUser? caller = null, CancellationToken token = default)
        => InvokeBulkCoreAsync(actionId, videos, ActionScope.Video, parameters, caller, token);

    /// <summary>
    ///   The invoke entry point. Scope-agnostic on purpose — the caller
    ///   resolves <paramref name="scopeEntity"/> (an <see cref="AnimeSeries"/>,
    ///   <see cref="AnimeGroup"/>, <see cref="AnimeEpisode"/>,
    ///   <see cref="VideoLocal"/>, or <c>null</c> for Global) before
    ///   calling this.
    /// </summary>
    /// <returns>
    ///   <c>null</c> when the action was accepted and enqueued, or a
    ///   rejection reason — mapped to a 400 by the controller — when the
    ///   invocation was refused without ever touching the queue.
    /// </returns>
    private async Task<ActionValidationResult?> InvokeCoreAsync(Guid actionId, object? scopeEntity, IReadOnlyDictionary<string, object?>? parameters, IUser? caller, CancellationToken token)
    {
        var registered = ResolveAction(actionId);
        if (CheckApplicable(registered, ScopeOf(scopeEntity), caller) is { } rejection)
            return rejection;

        if (await ValidateEntryAsync(registered, scopeEntity, parameters, caller, token) is { } validation)
            return validation;

        await EnqueueCoreAsync(registered, scopeEntity, parameters, caller, token);

        // Bare ack — no tracking ID.
        return null;
    }

    /// <summary>
    ///   The bulk entry point, scope-agnostic in the same way as
    ///   <see cref="InvokeCoreAsync"/>. Every entry is validated before any of
    ///   them is queued.
    /// </summary>
    /// <remarks>
    ///   Whether the action exists, applies to the scope and may be invoked by
    ///   this caller are properties of the action, so they are checked once and
    ///   fail the whole call rather than producing one identical complaint per
    ///   entry.
    /// </remarks>
    /// <exception cref="GenericValidationException">
    ///   The action, or at least one entry, was rejected. Nothing was queued.
    /// </exception>
    private async Task InvokeBulkCoreAsync(Guid actionId, IReadOnlyList<object> scopeEntities, ActionScope scope, IReadOnlyDictionary<string, object?>? parameters, IUser? caller, CancellationToken token)
    {
        var errors = await ValidateBulkCoreAsync(actionId, scopeEntities, scope, parameters, caller, token);
        if (errors.Count > 0)
        {
            throw new GenericValidationException(
                errors.ContainsKey(string.Empty)
                    ? "The action cannot be invoked."
                    : $"{errors.Count} of {scopeEntities.Count} entries were rejected.",
                errors
            );
        }

        var registered = ResolveAction(actionId);
        foreach (var scopeEntity in scopeEntities)
            await EnqueueCoreAsync(registered, scopeEntity, parameters, caller, token);
    }

    /// <summary>
    ///   Everything <see cref="InvokeCoreAsync"/> does before it queues, and
    ///   nothing after. Scope-agnostic in the same way.
    /// </summary>
    /// <returns>
    ///   <c>null</c> when the action would be accepted, or the
    ///   reason it would be refused.
    /// </returns>
    private async Task<ActionValidationResult?> ValidateCoreAsync(Guid actionId, object? scopeEntity, IReadOnlyDictionary<string, object?>? parameters, IUser? caller, CancellationToken token)
    {
        var registered = ResolveAction(actionId);
        return CheckApplicable(registered, ScopeOf(scopeEntity), caller)
            ?? await ValidateEntryAsync(registered, scopeEntity, parameters, caller, token);
    }

    /// <summary>
    ///   Validates every entry and reports the refusals rather than throwing,
    ///   so <see cref="InvokeBulkCoreAsync"/> can decide what to do with them.
    /// </summary>
    private async Task<IReadOnlyDictionary<string, IReadOnlyList<string>>> ValidateBulkCoreAsync(Guid actionId, IReadOnlyList<object> scopeEntities, ActionScope scope, IReadOnlyDictionary<string, object?>? parameters, IUser? caller, CancellationToken token)
    {
        var registered = ResolveAction(actionId);
        if (CheckApplicable(registered, scope, caller) is { } rejection)
            return new Dictionary<string, IReadOnlyList<string>> { [string.Empty] = [rejection.Reason] };

        var errors = new Dictionary<string, IReadOnlyList<string>>();
        for (var index = 0; index < scopeEntities.Count; index++)
            if (await ValidateEntryAsync(registered, scopeEntities[index], parameters, caller, token) is { } validation)
                errors[string.Create(CultureInfo.InvariantCulture, $"IDs[{index}]")] = [validation.Reason];

        return errors;
    }

    private RegisteredAction ResolveAction(Guid actionId)
        => _actions.TryGetValue(actionId, out var registered)
            ? registered
            : throw new KeyNotFoundException($"No action registered for {actionId}");

    /// <summary>
    ///   The scope an entity implies. A <c>null</c> entity is the
    ///   global scope rather than an absent one.
    /// </summary>
    private static ActionScope ScopeOf(object? scopeEntity)
        => scopeEntity switch
        {
            AnimeSeries => ActionScope.Series,
            AnimeGroup => ActionScope.Group,
            AnimeEpisode => ActionScope.Episode,
            VideoLocal => ActionScope.Video,
            _ => ActionScope.Global,
        };

    /// <summary>
    ///   Whether the action can be invoked at all, independently of which
    ///   entity it would be applied to.
    /// </summary>
    /// <returns>
    ///   A rejection, or <c>null</c> when the action is applicable.
    /// </returns>
    private static ActionValidationResult? CheckApplicable(RegisteredAction registered, ActionScope scope, IUser? caller)
    {
        var info = registered.Info;

        // Reject invocations via the wrong scope (e.g. a series-scoped action invoked
        // with no series, or a global action invoked with one) instead of letting the
        // context cast fail later in the job.
        if (info.Scope != scope)
        {
            return new ActionValidationResult(
                $"The action '{info.Name}' ({info.ID}) is not applicable to the {scope.ToString().ToLowerInvariant()} scope."
            );
        }

        // Trusted programmatic calls (no caller) skip the permission gate; HTTP
        // invocations always pass the authenticated user.
        if (caller is not null && info.Permission is ActionPermission.Admin && !caller.IsAdmin)
            return new ActionValidationResult("Administrator privileges are required for this action.");

        return null;
    }

    /// <summary>
    ///   Whether the caller may act on the entity: a series or episode whose
    ///   series the caller may see, a file with no series or one the caller may
    ///   see, or a group visible in whole unless the action only touches the
    ///   series the caller may see.
    /// </summary>
    /// <param name="action">The action.</param>
    /// <param name="scopeEntity">The entity the action is scoped to, or <c>null</c>.</param>
    /// <param name="caller">The invoking user, or <c>null</c> for a trusted call.</param>
    /// <returns>A rejection, or <c>null</c> when the caller may act on it.</returns>
    internal static ActionValidationResult? CheckVisible(IExecutableAction action, object? scopeEntity, IUser? caller)
    {
        if (AnimeGroupView.IsUnrestricted(caller))
            return null;

        var visible = scopeEntity switch
        {
            AnimeGroup group when action is IVisibleSeriesGroupAction => caller!.IsAllowedToSee(group),
            AnimeGroup group => AnimeGroupView.For(group, caller) is { IsVisible: true, IsComplete: true },
            AnimeSeries series => caller!.IsAllowedToSee(series),
            AnimeEpisode episode => episode.AnimeSeries is not { } series || caller!.IsAllowedToSee(series),
            VideoLocal video => JMMUser.IsVideoVisible(video, caller!.IsAllowedToSee),
            _ => true,
        };
        return visible ? null : new ActionValidationResult($"The {ScopeOf(scopeEntity).ToString().ToLowerInvariant()} is not visible to the calling user.");
    }

    /// <summary>
    ///   Runs the action's own validation against one entity, on a throwaway
    ///   instance.
    /// </summary>
    /// <remarks>
    ///   Validate runs synchronously, before anything touches the queue. It needs a
    ///   real instance (not just JobDataJson) since Validate isn't queued — resolved,
    ///   context-populated, and discarded, with the same transient lifetime as execution.
    /// </remarks>
    /// <returns>
    ///   A rejection, or <c>null</c> when the entity passed.
    /// </returns>
    private async Task<ActionValidationResult?> ValidateEntryAsync(RegisteredAction registered, object? scopeEntity, IReadOnlyDictionary<string, object?>? parameters, IUser? caller, CancellationToken token)
    {
        var (probe, rejection) = PrepareProbe(registered, scopeEntity, parameters, caller);
        return rejection ?? await probe!.Validate(token);
    }

    /// <summary>
    ///   Resolves a throwaway instance and prepares it the way execution will
    ///   see it: scoped, given its caller and populated with the parameters.
    /// </summary>
    /// <returns>
    ///   The instance, or the reason the caller may not act on the entity.
    /// </returns>
    private (IExecutableAction? Probe, ActionValidationResult? Rejection) PrepareProbe(
        RegisteredAction registered,
        object? scopeEntity,
        IReadOnlyDictionary<string, object?>? parameters,
        IUser? caller
    )
    {
        var probe = (IExecutableAction)_services.GetRequiredService(registered.ActionType);
        if (CheckVisible(probe, scopeEntity, caller) is { } hidden)
            return (null, hidden);

        if (probe is IScopedAction scoped && scopeEntity is not null)
            scoped.SetContext(scopeEntity);
        if (probe is IActionCaller callerAware)
        {
            if (caller is null)
                return (null, new ActionValidationResult($"The action '{registered.Info.Name}' requires a calling user."));

            callerAware.SetCaller(caller);
        }

        // Populate the probe with the caller's parameters too, so Validate
        // observes the same values Execute will, not the compiled-in defaults.
        PopulateParameters(probe, parameters);
        return (probe, null);
    }

    /// <summary>
    ///   Queues the action for one entity. Always queued — there is no
    ///   direct-execution path. The job re-resolves a fresh transient instance
    ///   later; the probe instance validation used is discarded.
    /// </summary>
    private Task EnqueueCoreAsync(RegisteredAction registered, object? scopeEntity, IReadOnlyDictionary<string, object?>? parameters, IUser? caller, CancellationToken token)
        => _scheduler.Enqueue(ConfigureJob(registered.Info, scopeEntity, parameters, caller), ct: token);

    /// <summary>
    ///   Sets up the job running an action, which its dedup key is taken from.
    /// </summary>
    /// <param name="info">The action.</param>
    /// <param name="scopeEntity">The entity the action is scoped to, or <c>null</c>.</param>
    /// <param name="parameters">The invocation parameters, or <c>null</c>.</param>
    /// <param name="caller">The invoking user, or <c>null</c>.</param>
    /// <returns>The job configurator.</returns>
    private static Action<ActionExecutionJob> ConfigureJob(ExecutableActionInfo info, object? scopeEntity, IReadOnlyDictionary<string, object?>? parameters, IUser? caller)
        => j =>
        {
            j.ActionId = info.ID;
            j.ScopeEntityId = scopeEntity switch
            {
                AnimeSeries series => series.AnimeSeriesID,
                AnimeGroup group => group.AnimeGroupID,
                AnimeEpisode episode => episode.AnimeEpisodeID,
                VideoLocal video => video.VideoLocalID,
                _ => null,
            };
            j.Scope = info.Scope;
            j.CallerUserId = caller?.LocalID ?? 0;
            j.Parameters = parameters?.ToDictionary(pair => pair.Key, pair => pair.Value);
        };

    #endregion

    #region Maintenance

    /// <summary>
    ///   Removes the records of files that are gone from disk, merges
    ///   duplicate files and drops orphaned rows, then queues a stats refresh
    ///   of every series touched.
    /// </summary>
    /// <remarks>
    ///   Each file is removed in a transaction of its own, so a cancelled run
    ///   leaves the rest for the next one. The series touched so far get
    ///   their stats refresh queued however the run ends.
    /// </remarks>
    /// <param name="removeMylist">Whether to remove the files from the AniDB MyList too.</param>
    /// <param name="progress">Told how far the run is, from 0 to 100.</param>
    /// <param name="token">Stops the run between two files.</param>
    /// <returns>A task that completes once the records are removed.</returns>
    /// <exception cref="OperationCanceledException"><paramref name="token"/> was cancelled.</exception>
    public async Task RemoveRecordsWithoutPhysicalFiles(bool removeMylist = true, IProgress<decimal>? progress = null, CancellationToken token = default)
    {
        _logger.LogInformation("Remove Missing Files: Start");
        var stages = new StagedProgress(progress, 4, 1, 4, 1);
        stages.Report(0);
        var seriesToUpdate = new HashSet<AnimeSeries>();
        try
        {
            using var session = _databaseFactory.SessionFactory.OpenSession();

            // remove missing files in valid managed folders
            var filesAll = _videoLocalPlaces.GetAll()
                .Where(a => a.ManagedFolder is not null)
                .GroupBy(a => a.ManagedFolder!)
                .SelectMany(a => a)
                .ToList();
            var placeItems = new ItemProgress(stages, filesAll.Count);
            foreach (var vl in filesAll)
            {
                token.ThrowIfCancellationRequested();
                placeItems.Increment();
                if (File.Exists(vl.Path)) continue;

                // delete video local record
                _logger.LogInformation("Removing Missing File: {ID}", vl.VideoID);
                // skipEvents covers the secondary events — outward side effects, the
                // MyList removal among them — rather than first-party ones like the
                // file-deleted event, which fire either way. So it inverts this flag
                await ((VideoService)_videoService).RemoveRecordWithOpenTransaction(session, vl, seriesToUpdate, skipEvents: !removeMylist);
            }

            stages.NextStage();
            token.ThrowIfCancellationRequested();
            var videoLocalsAll = _videoLocals.GetAll().ToList();
            // remove empty video locals
            {
                using var transaction = session.BeginTransaction();
                _videoLocals.DeleteWithOpenTransaction(session, videoLocalsAll.Where(a => a.IsEmpty()).ToList());
                transaction.Commit();
            }

            // Remove duplicate video locals
            var locals = videoLocalsAll
                .Where(a => !string.IsNullOrWhiteSpace(a.Hash))
                .GroupBy(a => a.Hash)
                .ToDictionary(g => g.Key, g => g.ToList());
            var toRemove = new List<VideoLocal>();
            var comparer = new VideoLocalComparer();

            foreach (var hash in locals.Keys)
            {
                var values = locals[hash].ToList();
                values.Sort(comparer);
                var to = values.First();
                values.Remove(to);
                foreach (var places in values.Select(from => from.Places).Where(places => places != null && places.Count != 0))
                {
                    using var transaction = session.BeginTransaction();
                    foreach (var place in places)
                    {
                        place.VideoID = to.VideoLocalID;
                        _videoLocalPlaces.SaveWithOpenTransaction(session, place);
                    }

                    transaction.Commit();
                }

                toRemove.AddRange(values);
            }

            {
                using var transaction = session.BeginTransaction();
                foreach (var remove in toRemove)
                {
                    _videoLocals.DeleteWithOpenTransaction(session, remove);
                }

                transaction.Commit();
            }

            // Remove files in invalid managed folders
            stages.NextStage();
            var videoItems = new ItemProgress(stages, videoLocalsAll.Count);
            foreach (var v in videoLocalsAll)
            {
                token.ThrowIfCancellationRequested();
                videoItems.Increment();
                var places = v.Places;
                if (places.Count > 0)
                {
                    using var transaction = session.BeginTransaction();
                    foreach (var place in places.Where(place => string.IsNullOrWhiteSpace(place?.Path)))
                    {
#pragma warning disable CS0618
                        _logger.LogInformation("Remove Records With Orphaned Managed Folder: {Filename}", v.FileName);
#pragma warning restore CS0618
                        seriesToUpdate.UnionWith(v.AnimeEpisodes.Select(a => a.AnimeSeries).WhereNotNull().DistinctBy(a => a.AnimeSeriesID));
                        _videoLocalPlaces.DeleteWithOpenTransaction(session, place);
                    }

                    transaction.Commit();
                }

                // Remove duplicate places
                places = v.Places;
                if (places.Count == 1) continue;

                if (places.Count > 0)
                {
                    places = places.DistinctBy(a => a.Path).ToList();
                    places = v.Places.Except(places).ToList() ?? [];
                    foreach (var place in places)
                    {
                        using var transaction = session.BeginTransaction();
                        _videoLocalPlaces.DeleteWithOpenTransaction(session, place);
                        transaction.Commit();
                    }
                }

                if (v.Places.Count > 0) continue;

                // delete video local record
#pragma warning disable CS0618
                _logger.LogInformation("RemoveOrphanedVideoLocal : {Filename}", v.FileName);
#pragma warning restore CS0618
                seriesToUpdate.UnionWith(v.AnimeEpisodes.Select(a => a.AnimeSeries).WhereNotNull().DistinctBy(a => a.AnimeSeriesID));

                if (removeMylist)
                    await ((VideoService)_videoService).ScheduleRemovalFromMylist(v);

                {
                    using var transaction = session.BeginTransaction();
                    _videoLocals.DeleteWithOpenTransaction(session, v);
                    transaction.Commit();
                }
            }

            // Clean up failed imports
            stages.NextStage();
            token.ThrowIfCancellationRequested();
            var list = _videoLocals.GetAll()
                .SelectMany(a => a.EpisodeCrossReferences)
                .Where(a => a.AniDBAnime == null || a.AniDBEpisode == null)
                .ToArray();
            {
                using var transaction = session.BeginTransaction();
                foreach (var xref in list)
                {
                    // We don't need to update anything since they don't exist
                    _crossRefFileEpisodes.DeleteWithOpenTransaction(session, xref);
                }

                transaction.Commit();
            }

            // clean up orphaned video local places
            var placesToRemove = _videoLocalPlaces.GetAll().Where(a => a.VideoLocal == null).ToList();
            {
                using var transaction = session.BeginTransaction();
                foreach (var place in placesToRemove)
                {
                    // We don't need to update anything since they don't exist
                    _videoLocalPlaces.DeleteWithOpenTransaction(session, place);
                }

                transaction.Commit();
            }

            // NOTE: use 'purge unused releases' if you want to remove the cross-references too.
        }
        finally
        {
            // update everything we modified
            await Task.WhenAll(seriesToUpdate.Select(a => _scheduler.StartJob<RefreshAnimeStatsJob>(b => b.AnimeID = a.AniDB_ID)));
        }

        stages.Complete();
        _logger.LogInformation("Remove Missing Files: Finished");
    }

    /// <summary>
    ///   Queues a stats refresh of every series.
    /// </summary>
    /// <param name="progress">Told how far the queuing is, from 0 to 100.</param>
    /// <param name="token">Stops the queuing between two series.</param>
    /// <returns>A task that completes once the refreshes are queued.</returns>
    /// <exception cref="OperationCanceledException"><paramref name="token"/> was cancelled.</exception>
    public async Task UpdateAllStats(IProgress<decimal>? progress = null, CancellationToken token = default)
    {
        var series = _animeSeries.GetAll();
        var items = new ItemProgress(progress, series.Count);
        items.Report(0);
        foreach (var each in series)
        {
            token.ThrowIfCancellationRequested();
            await _scheduler.StartJob<RefreshAnimeStatsJob>(b => b.AnimeID = each.AniDB_ID);
            items.Increment();
        }
    }

    /// <summary>
    ///   Queues a new release search of the AniDB files missing their release
    ///   group, and a fetch of the release groups missing their names.
    /// </summary>
    /// <param name="countOnly">Whether to only count the files, without queuing anything.</param>
    /// <param name="progress">Told how far the queuing is, from 0 to 100.</param>
    /// <param name="token">Stops the queuing between two files or groups.</param>
    /// <returns>How many files miss their release group.</returns>
    /// <exception cref="OperationCanceledException"><paramref name="token"/> was cancelled.</exception>
    public async Task<int> UpdateAnidbReleaseInfo(bool countOnly = false, IProgress<decimal>? progress = null, CancellationToken token = default)
    {
        _logger.LogInformation("Updating Missing AniDB_File Info");
        var missingFiles = !_videoReleaseService.AutoMatchEnabled ? [] : _storedReleaseInfos.GetAll()
            .Where(r => r.ProviderName is "AniDB" && (string.IsNullOrEmpty(r.GroupID) || r.GroupSource is not "AniDB"))
            .Select(a => _videoLocals.GetByEd2kAndSize(a.ED2K, a.FileSize))
            .WhereNotNull()
            .Select(a => a)
            .ToList();
        if (!countOnly)
        {
            var stages = new StagedProgress(progress, 2);
            stages.Report(0);
            _logger.LogInformation("Queuing {Count} GetFile commands", missingFiles.Count);
            var fileItems = new ItemProgress(stages, missingFiles.Count);
            foreach (var id in missingFiles)
            {
                token.ThrowIfCancellationRequested();
                await _videoReleaseService.ScheduleFindReleaseForVideo(id, force: true);
                fileItems.Increment();
            }

            stages.NextStage();
            var incorrectGroups = _storedReleaseInfos.GetAll()
                .Where(r =>
                    !string.IsNullOrEmpty(r.GroupID) &&
                    r.GroupSource is "AniDB" &&
                    int.TryParse(r.GroupID, out var groupID) && (
                        string.IsNullOrEmpty(r.GroupName) ||
                        string.IsNullOrEmpty(r.GroupShortName)
                    )
                )
                .DistinctBy(a => a.GroupID)
                .Select(a => int.Parse(a.GroupID!))
                .ToHashSet();
            _logger.LogInformation("Queuing {Count} GetReleaseGroup commands", incorrectGroups.Count);
            var groupItems = new ItemProgress(stages, incorrectGroups.Count);
            foreach (var a in incorrectGroups)
            {
                token.ThrowIfCancellationRequested();
                await _scheduler.StartJob<GetAniDBReleaseGroupJob>(c => c.GroupID = a);
                groupItems.Increment();
            }

            stages.Complete();
        }

        return missingFiles.Count;
    }

    /// <summary>
    ///   Queues the handling of every AniDB file-moved message not handled
    ///   yet.
    /// </summary>
    /// <param name="force">Whether to queue them even when the setting to handle moved files is off.</param>
    /// <param name="progress">Told how far the queuing is, from 0 to 100.</param>
    /// <param name="token">Stops the queuing between two messages.</param>
    /// <returns>A task that completes once the messages are queued.</returns>
    /// <exception cref="OperationCanceledException"><paramref name="token"/> was cancelled.</exception>
    public async Task RefreshAniDBMovedFiles(bool force, IProgress<decimal>? progress = null, CancellationToken token = default)
    {
        var settings = _settingsProvider.GetSettings();
        if (!force && !settings.AniDb.Notification_HandleMovedFiles)
            return;

        var messages = _anidbMessages.GetUnhandledFileMoveMessages();
        var items = new ItemProgress(progress, messages.Count);
        items.Report(0);
        foreach (var msg in messages)
        {
            token.ThrowIfCancellationRequested();
            await _scheduler.StartJob<ProcessFileMovedMessageJob>(c => c.MessageID = msg.MessageID);
            items.Increment();
        }
    }

    /// <summary>
    ///   Queues a remote refresh of every AniDB anime missing its cached XML
    ///   file.
    /// </summary>
    /// <param name="progress">Told how far the check is, from 0 to 100.</param>
    /// <param name="token">Stops the check between two anime.</param>
    /// <returns>A task that completes once the refreshes are queued.</returns>
    /// <exception cref="OperationCanceledException"><paramref name="token"/> was cancelled.</exception>
    public async Task DownloadMissingAnidbAnimeXmls(IProgress<decimal>? progress = null, CancellationToken token = default)
    {
        // Check existing anime.
        var index = 0;
        var localAnimeSet = _anidbAnimes.GetAll()
            .Select(a => a.AnimeID)
            .OrderBy(a => a)
            .ToHashSet();
        var items = new ItemProgress(progress, localAnimeSet.Count);
        items.Report(0);
        _logger.LogInformation("Checking {AllAnimeCount} anime for missing XML files…", localAnimeSet.Count);
        foreach (var animeID in localAnimeSet)
        {
            token.ThrowIfCancellationRequested();
            if (++index % 10 == 1 || index == localAnimeSet.Count)
                _logger.LogInformation("Checking {AllAnimeCount} anime for missing XML files — {CurrentCount}/{AllAnimeCount}", localAnimeSet.Count, index + 1, localAnimeSet.Count);

            var rawXml = await _xmlUtils.LoadAnimeHTTPFromFile(animeID);
            if (rawXml is null)
            {
                _logger.LogDebug("Found anime {AnimeID} with missing XML", animeID);
                await QueueAniDBRefresh(animeID, true, false, false, SkipSupplementaryUpdate: true);
            }

            items.Increment();
        }
    }

    public async Task<bool> QueueAniDBRefresh(int animeID, bool force, bool downloadRelations, bool createSeriesEntry, bool immediate = false,
        bool cacheOnly = false, bool SkipSupplementaryUpdate = false)
    {
        if (animeID == 0)
            return false;

        var refreshMethod = AnidbRefreshMethod.None;
        if (!cacheOnly)
            refreshMethod |= AnidbRefreshMethod.Remote;
        if (!force)
            refreshMethod |= AnidbRefreshMethod.Cache;
        if (downloadRelations)
            refreshMethod |= AnidbRefreshMethod.DownloadRelations;
        if (createSeriesEntry)
            refreshMethod |= AnidbRefreshMethod.CreateShokoSeries;
        if (force || !cacheOnly)
            refreshMethod |= AnidbRefreshMethod.DeferToRemoteIfUnsuccessful;
        if (SkipSupplementaryUpdate)
            refreshMethod |= AnidbRefreshMethod.SkipSupplementaryUpdate;
        if (immediate)
        {
            try
            {
                return await _anidbService.RefreshAnimeByID(animeID, refreshMethod).ConfigureAwait(false) is not null;
            }
            catch
            {
                return false;
            }
        }

        await _anidbService.ScheduleRefreshOfAnimeByID(animeID, refreshMethod).ConfigureAwait(false);
        return false;
    }

    /// <summary>
    ///   Fills in the anime of file cross-references that lack it, asking
    ///   AniDB for an episode not stored, then queues a refresh of every
    ///   anime the files need that is missing its series or episodes.
    /// </summary>
    /// <param name="progress">Told how far the run is, from 0 to 100.</param>
    /// <param name="token">Stops the run between two episodes or anime.</param>
    /// <returns>A task that completes once the refreshes are queued.</returns>
    /// <exception cref="OperationCanceledException"><paramref name="token"/> was cancelled.</exception>
    public async Task ScheduleMissingAnidbAnimeForFiles(IProgress<decimal>? progress = null, CancellationToken token = default)
    {
        // Attempt to fix cross-references with incomplete data.
        var stages = new StagedProgress(progress, 2);
        stages.Report(0);
        var index = 0;
        var videos = _videoLocals.GetVideosWithMissingCrossReferenceData();
        var unknownEpisodeDict = videos
            .SelectMany(file => file.EpisodeCrossReferences)
            .Where(xref => xref.AnimeID is 0)
            .GroupBy(xref => xref.EpisodeID)
            .ToDictionary(groupBy => groupBy.Key, groupBy => groupBy.ToList());
        _logger.LogInformation("Attempting to fix {MissingAnimeCount} cross-references with unknown anime…", unknownEpisodeDict.Count);
        var episodeItems = new ItemProgress(stages, unknownEpisodeDict.Count);
        foreach (var (episodeId, xrefs) in unknownEpisodeDict)
        {
            token.ThrowIfCancellationRequested();
            if (++index % 10 == 1)
                _logger.LogInformation("Attempting to fix cross-references with unknown anime — {CurrentCount}/{MissingAnimeCount}", index + 1, unknownEpisodeDict.Count);

            var episode = _anidbEpisodes.GetByEpisodeID(episodeId);
            if (episode is not null)
            {
                foreach (var xref in xrefs)
                    xref.AnimeID = episode.AnimeID;
                _crossRefFileEpisodes.Save(xrefs);
                episodeItems.Increment();
                continue;
            }

            int? epAnimeID = null;
            var epRequest = _requestFactory.Create<RequestGetEpisode>(r => r.EpisodeID = episodeId);
            try
            {
                var epResponse = await epRequest.SendAsync(token);
                epAnimeID = epResponse.Response?.AnimeID;
            }
            catch (Exception e) when (e is not OperationCanceledException)
            {
                _logger.LogError(e, "Could not get Episode Info for {EpisodeID}", episodeId);
            }

            if (epAnimeID is not null)
            {
                foreach (var xref in xrefs)
                    xref.AnimeID = epAnimeID.Value;
                _crossRefFileEpisodes.Save(xrefs);
            }

            episodeItems.Increment();
        }

        // Queue missing anime needed by existing files.
        stages.NextStage();
        index = 0;
        var localAnimeSet = _animeSeries.GetAll()
            .Select(a => a.AniDB_ID)
            .ToHashSet();
        var localEpisodeSet = _animeEpisodes.GetAll()
            .Select(episode => episode.AniDB_EpisodeID)
            .ToHashSet();
        var missingAnimeSet = videos
            .SelectMany(file => file.EpisodeCrossReferences)
            .Where(xref => xref.AnimeID > 0 && (!localAnimeSet.Contains(xref.AnimeID) || !localEpisodeSet.Contains(xref.EpisodeID)))
            .Select(xref => xref.AnimeID)
            .ToHashSet();
        var settings = _settingsProvider.GetSettings();
        _logger.LogInformation("Queueing {MissingAnimeCount} anime that needs an update…", missingAnimeSet.Count);
        var refreshMethod = AnidbRefreshMethod.Remote | AnidbRefreshMethod.DeferToRemoteIfUnsuccessful | AnidbRefreshMethod.SkipSupplementaryUpdate | AnidbRefreshMethod.CreateShokoSeries;
        if (settings.AutoGroupSeries || settings.AniDb.DownloadRelatedAnime)
            refreshMethod |= AnidbRefreshMethod.DownloadRelations;
        var animeItems = new ItemProgress(stages, missingAnimeSet.Count);
        foreach (var animeID in missingAnimeSet)
        {
            token.ThrowIfCancellationRequested();
            if (++index % 10 == 1 || index == missingAnimeSet.Count)
                _logger.LogInformation("Queueing anime that needs an update — {CurrentCount}/{MissingAnimeCount}", index, missingAnimeSet.Count);

            await _anidbService.ScheduleRefreshOfAnimeByID(animeID, refreshMethod);
            animeItems.Increment();
        }

        stages.Complete();
    }

    /// <summary>
    ///   Queues a fetch of every AniDB creator whose type is unknown, when
    ///   creators are downloaded at all.
    /// </summary>
    /// <param name="progress">Told how far the queuing is, from 0 to 100.</param>
    /// <param name="token">Stops the queuing between two creators.</param>
    /// <returns>A task that completes once the fetches are queued.</returns>
    /// <exception cref="OperationCanceledException"><paramref name="token"/> was cancelled.</exception>
    public async Task ScheduleMissingAnidbCreators(IProgress<decimal>? progress = null, CancellationToken token = default)
    {
        if (!_settingsProvider.GetSettings().AniDb.DownloadCreators) return;

        var allCreators = _anidbCreators.GetAll();
        var allMissingCreators = allCreators
                .Where(creator => creator.Type is CreatorType.Unknown)
                .Select(creator => creator.CreatorID)
                .Distinct()
                .ToList();

        var startedAt = DateTime.Now;
        _logger.LogInformation("Scheduling {Count} AniDB Creators for a refresh.", allMissingCreators.Count);
        var items = new ItemProgress(progress, allMissingCreators.Count);
        items.Report(0);
        foreach (var creatorID in allMissingCreators)
        {
            token.ThrowIfCancellationRequested();
            await _scheduler.StartJob<GetAniDBCreatorJob>(c => c.CreatorID = creatorID).ConfigureAwait(false);
            items.Increment();

            if (items.Done % 10 == 0)
                _logger.LogInformation("Scheduling AniDB Creators for a refresh. (Progress={Count}/{Total})", items.Done, allMissingCreators.Count);
        }

        _logger.LogInformation("Scheduled {Count} AniDB Creators in {TimeSpan}", allMissingCreators.Count, DateTime.Now - startedAt);
    }

    /// <summary>
    ///   Queues the creation of a series for every stored AniDB anime a file
    ///   links to that has none.
    /// </summary>
    /// <param name="progress">Told how far the queuing is, from 0 to 100.</param>
    /// <param name="token">Stops the queuing between two anime.</param>
    /// <returns>A task that completes once the creations are queued.</returns>
    /// <exception cref="OperationCanceledException"><paramref name="token"/> was cancelled.</exception>
    public async Task CreateMissingSeries(IProgress<decimal>? progress = null, CancellationToken token = default)
    {
        var missingSeries = _videoLocals.GetAll().SelectMany(vid =>
        {
            var xrefs = _crossRefFileEpisodes.GetByEd2k(vid.Hash);
            var aniDBAnime = xrefs.Select(a => _anidbAnimes.GetByAnimeID(a.AnimeID)).WhereNotNull();
            return aniDBAnime.Where(a => _animeSeries.GetByAnimeID(a.AnimeID) == null);
        }).ToList();

        _logger.LogInformation("Creating {Count} Series that are missing.", missingSeries.Count);

        var methods = AnidbRefreshMethod.Cache | AnidbRefreshMethod.DeferToRemoteIfUnsuccessful | AnidbRefreshMethod.CreateShokoSeries;
        var items = new ItemProgress(progress, missingSeries.Count);
        items.Report(0);
        foreach (var aniDBAnime in missingSeries)
        {
            token.ThrowIfCancellationRequested();
            await _anidbService.ScheduleRefreshOfAnime(aniDBAnime, methods, prioritize: false);
            items.Increment();
        }

        _logger.LogInformation("Queued Creation of {Count} Series that were missing.", missingSeries.Count);
    }

    /// <summary>
    ///   Queues a verification of the relations of every anime with an
    ///   unverified one.
    /// </summary>
    /// <param name="progress">Told how far the queuing is, from 0 to 100.</param>
    /// <param name="token">Stops the queuing between two anime.</param>
    /// <returns>How many anime have unverified relations.</returns>
    /// <exception cref="OperationCanceledException"><paramref name="token"/> was cancelled.</exception>
    public async Task<int> VerifyAllUnverifiedRelations(IProgress<decimal>? progress = null, CancellationToken token = default)
    {
        var unverifiedAnimeIDs = _anidbAnimeRelations.GetAll()
            .Where(r => !r.Verified)
            .Select(r => r.AnimeID)
            .Distinct()
            .ToList();

        _logger.LogInformation("Scheduling verification of relations for {Count} anime with unverified relations", unverifiedAnimeIDs.Count);

        var items = new ItemProgress(progress, unverifiedAnimeIDs.Count);
        items.Report(0);
        foreach (var animeID in unverifiedAnimeIDs)
        {
            token.ThrowIfCancellationRequested();
            await _scheduler.StartJob<VerifyAniDBRelationsJob>(c => c.AnimeID = animeID);
            items.Increment();
        }

        return unverifiedAnimeIDs.Count;
    }

    #endregion
}
