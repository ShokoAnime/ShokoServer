using System;
using System.Collections.Generic;
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
using Shoko.Abstractions.Extensions;
using Shoko.Abstractions.Metadata.Anidb.Enums;
using Shoko.Abstractions.Metadata.Anidb.Services;
using Shoko.Abstractions.Metadata.Services;
using Shoko.Abstractions.Metadata.Shoko;
using Shoko.Abstractions.Plugin;
using Shoko.Abstractions.User;
using Shoko.Abstractions.Utilities;
using Shoko.Abstractions.Video;
using Shoko.Abstractions.Video.Services;
using Shoko.QueueProcessor.Abstractions;
using Shoko.QueueProcessor.Scheduling;
using Shoko.Server.Databases;
using Shoko.Server.Models.Shoko;
using Shoko.Server.Providers.AniDB;
using Shoko.Server.Providers.AniDB.Interfaces;
using Shoko.Server.Providers.AniDB.UDP.Info;
using Shoko.Server.Providers.TMDB;
using Shoko.Server.Repositories.Cached;
using Shoko.Server.Repositories.Cached.AniDB;
using Shoko.Server.Repositories.Direct;
using Shoko.Server.Scheduling.Jobs.Actions;
using Shoko.Server.Scheduling.Jobs.AniDB;
using Shoko.Server.Scheduling.Jobs.Shoko;
using Shoko.Server.Services.Configuration;
using Shoko.Server.Settings;

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

    private readonly IImageManager _imageManager;

    private readonly TmdbMetadataService _tmdbService;

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
    ///   <see langword="null"/> when the action declares no parameters.
    /// </param>
    private sealed record RegisteredAction(ExecutableActionInfo Info, Type ActionType, JsonSchema? ParameterSchema);

    private readonly VideoLocalRepository _videoLocals;

    private readonly VideoLocal_PlaceRepository _videoLocalPlaces;

    private readonly StoredReleaseInfoRepository _storedReleaseInfos;

    private readonly StoredReleaseInfo_MatchAttemptRepository _storedReleaseInfoMatchAttempts;

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
        IImageManager imageManager,
        TmdbMetadataService tmdbService,
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
        StoredReleaseInfo_MatchAttemptRepository storedReleaseInfoMatchAttempts,
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
        _imageManager = imageManager;
        _videoService = videoService;
        _tmdbService = tmdbService;
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
        _storedReleaseInfoMatchAttempts = storedReleaseInfoMatchAttempts;
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
            var parameters = _actionUiDefinitionBuilder.Build(id, probe.Name, probe.Description, actionType);

            _actions[id] = new RegisteredAction(new ExecutableActionInfo(
                id,
                probe.Name,
                probe.Description,
                probe.Category,
                categoryName,
                scope,
                probe.Permission,
                probe.RequiresConfirmation,
                probe.ConfirmationMessage,
                pluginId,
                parameters?.Definition
            ), actionType, parameters?.Schema);
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
    internal static void PopulateParameters(IExecutableAction action, IReadOnlyDictionary<string, object?>? parameters)
    {
        if (parameters is not { Count: > 0 })
            return;

        JsonConvert.PopulateObject(JsonConvert.SerializeObject(parameters), action, _populateSettings);
    }

    /// <summary>
    ///   The action's own metadata is hidden from population as well as from
    ///   the schema, so a payload naming <c>Name</c> or <c>Permission</c> cannot
    ///   write to the instance even if it somehow reaches here unvalidated.
    /// </summary>
    private static readonly JsonSerializerSettings _populateSettings = new()
    {
        ContractResolver = new ActionMetadataContractResolver(),
    };

    /// <summary>
    ///   Checks an invocation payload against the action's parameter schema.
    /// </summary>
    /// <remarks>
    ///   <para>
    ///     Only the API boundary calls this. An in-process caller passes a typed
    ///     dictionary it built in code rather than a document it parsed, and the
    ///     failure it wants is a compiler error, not a dictionary of paths — so
    ///     <see cref="InvokeAsync(Guid, IReadOnlyDictionary{string, object?}, IUser?, CancellationToken)"/>
    ///     stays free of it.
    ///   </para>
    ///   <para>
    ///     The errors come back keyed by property path, which is the shape the
    ///     configuration endpoints already return for a rejected body.
    ///   </para>
    /// </remarks>
    /// <param name="actionId">The action being invoked.</param>
    /// <param name="parameters">
    ///   The payload, or <see langword="null"/> when the caller sent no body.
    /// </param>
    /// <returns>Errors per property path; empty when the payload is acceptable.</returns>
    public IReadOnlyDictionary<string, IReadOnlyList<string>> ValidateParameters(Guid actionId, JObject? parameters)
    {
        // No body is how every action has always been invoked, and how one that
        // takes no parameters still is. There is nothing to check.
        if (parameters is null)
            return new Dictionary<string, IReadOnlyList<string>>();

        if (!_actions.TryGetValue(actionId, out var registered))
            throw new KeyNotFoundException($"No action registered for {actionId}");

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

    /// <inheritdoc cref="IActionService.InvokeAsync(Guid, IUser?, CancellationToken)"/>
    public Task<ActionValidationResult?> InvokeAsync(Guid actionId, IUser? caller = null, CancellationToken token = default)
        => InvokeCoreAsync(actionId, scopeEntity: null, parameters: null, caller, token);

    /// <inheritdoc cref="IActionService.InvokeAsync(Guid, IReadOnlyDictionary{string, object?}, IUser?, CancellationToken)"/>
    public Task<ActionValidationResult?> InvokeAsync(Guid actionId, IReadOnlyDictionary<string, object?> parameters, IUser? caller = null, CancellationToken token = default)
        => InvokeCoreAsync(actionId, scopeEntity: null, parameters, caller, token);

    /// <inheritdoc cref="IActionService.InvokeAsync(Guid, IShokoGroup, IUser?, CancellationToken)"/>
    public Task<ActionValidationResult?> InvokeAsync(Guid actionId, IShokoGroup group, IUser? caller = null, CancellationToken token = default)
        => InvokeCoreAsync(actionId, group, parameters: null, caller, token);

    /// <inheritdoc cref="IActionService.InvokeAsync(Guid, IShokoGroup, IReadOnlyDictionary{string, object?}, IUser?, CancellationToken)"/>
    public Task<ActionValidationResult?> InvokeAsync(Guid actionId, IShokoGroup group, IReadOnlyDictionary<string, object?> parameters, IUser? caller = null, CancellationToken token = default)
        => InvokeCoreAsync(actionId, group, parameters, caller, token);

    /// <inheritdoc cref="IActionService.InvokeAsync(Guid, IShokoSeries, IUser?, CancellationToken)"/>
    public Task<ActionValidationResult?> InvokeAsync(Guid actionId, IShokoSeries series, IUser? caller = null, CancellationToken token = default)
        => InvokeCoreAsync(actionId, series, parameters: null, caller, token);

    /// <inheritdoc cref="IActionService.InvokeAsync(Guid, IShokoSeries, IReadOnlyDictionary{string, object?}, IUser?, CancellationToken)"/>
    public Task<ActionValidationResult?> InvokeAsync(Guid actionId, IShokoSeries series, IReadOnlyDictionary<string, object?> parameters, IUser? caller = null, CancellationToken token = default)
        => InvokeCoreAsync(actionId, series, parameters, caller, token);

    /// <inheritdoc cref="IActionService.InvokeAsync(Guid, IShokoEpisode, IUser?, CancellationToken)"/>
    public Task<ActionValidationResult?> InvokeAsync(Guid actionId, IShokoEpisode episode, IUser? caller = null, CancellationToken token = default)
        => InvokeCoreAsync(actionId, episode, parameters: null, caller, token);

    /// <inheritdoc cref="IActionService.InvokeAsync(Guid, IShokoEpisode, IReadOnlyDictionary{string, object?}, IUser?, CancellationToken)"/>
    public Task<ActionValidationResult?> InvokeAsync(Guid actionId, IShokoEpisode episode, IReadOnlyDictionary<string, object?> parameters, IUser? caller = null, CancellationToken token = default)
        => InvokeCoreAsync(actionId, episode, parameters, caller, token);

    /// <inheritdoc cref="IActionService.InvokeAsync(Guid, IVideo, IUser?, CancellationToken)"/>
    public Task<ActionValidationResult?> InvokeAsync(Guid actionId, IVideo video, IUser? caller = null, CancellationToken token = default)
        => InvokeCoreAsync(actionId, video, parameters: null, caller, token);

    /// <inheritdoc cref="IActionService.InvokeAsync(Guid, IVideo, IReadOnlyDictionary{string, object?}, IUser?, CancellationToken)"/>
    public Task<ActionValidationResult?> InvokeAsync(Guid actionId, IVideo video, IReadOnlyDictionary<string, object?> parameters, IUser? caller = null, CancellationToken token = default)
        => InvokeCoreAsync(actionId, video, parameters, caller, token);

    /// <summary>
    ///   The invoke entry point. Scope-agnostic on purpose — the caller
    ///   resolves <paramref name="scopeEntity"/> (an <see cref="AnimeSeries"/>,
    ///   <see cref="AnimeGroup"/>, <see cref="AnimeEpisode"/>,
    ///   <see cref="VideoLocal"/>, or <see langword="null"/> for Global) before
    ///   calling this.
    /// </summary>
    /// <returns>
    ///   <see langword="null"/> when the action was accepted and enqueued, or a
    ///   rejection reason — mapped to a 400 by the controller — when the
    ///   invocation was refused without ever touching the queue.
    /// </returns>
    private async Task<ActionValidationResult?> InvokeCoreAsync(Guid actionId, object? scopeEntity, IReadOnlyDictionary<string, object?>? parameters, IUser? caller, CancellationToken token)
    {
        if (!_actions.TryGetValue(actionId, out var registered))
            throw new KeyNotFoundException($"No action registered for {actionId}");

        var info = registered.Info;

        // Reject invocations via the wrong scope (e.g. a series-scoped action invoked
        // with no series, or a global action invoked with one) instead of letting the
        // context cast fail later in the job.
        var expectedScope = scopeEntity switch
        {
            AnimeSeries => ActionScope.Series,
            AnimeGroup => ActionScope.Group,
            AnimeEpisode => ActionScope.Episode,
            VideoLocal => ActionScope.Video,
            _ => ActionScope.Global,
        };
        if (info.Scope != expectedScope)
        {
            return new ActionValidationResult(
                $"The action '{info.Name}' ({info.Id}) is not applicable to the {expectedScope.ToString().ToLowerInvariant()} scope."
            );
        }

        // Trusted programmatic calls (no caller) skip the permission gate; HTTP
        // invocations always pass the authenticated user.
        if (caller is not null && info.Permission is ActionPermission.Admin && !caller.IsAdmin)
            return new ActionValidationResult("Administrator privileges are required for this action.");

        // Validate runs synchronously, before anything touches the queue. It needs a
        // real instance (not just JobDataJson) since Validate isn't queued — resolved,
        // context-populated, and discarded, with the same transient lifetime as execution.
        var probe = (IExecutableAction)_services.GetRequiredService(registered.ActionType);
        if (probe is IScopedAction scoped && scopeEntity is not null)
            scoped.SetContext(scopeEntity);
        if (probe is IActionCaller callerAware)
        {
            if (caller is null)
                return new ActionValidationResult($"The action '{info.Name}' requires a calling user.");

            callerAware.SetCaller(caller);
        }

        // Populate the probe with the caller's parameters too, so Validate
        // observes the same values Execute will, not the compiled-in defaults.
        PopulateParameters(probe, parameters);

        var validation = await probe.Validate(token);
        if (validation is not null)
            return validation;

        // Always queued from here on — there is no direct-execution path. The job
        // re-resolves a fresh transient instance later; the probe instance above is
        // discarded.
        await _scheduler.Enqueue<ActionExecutionJob>(j =>
        {
            j.ActionId = actionId;
            j.ScopeEntityId = scopeEntity switch
            {
                AnimeSeries series => series.AnimeSeriesID,
                AnimeGroup group => group.AnimeGroupID,
                AnimeEpisode episode => episode.AnimeEpisodeID,
                VideoLocal video => video.VideoLocalID,
                _ => null,
            };
            j.Scope = info.Scope;
            j.CallerUserId = caller?.ID ?? 0;
            j.Parameters = parameters?.ToDictionary(pair => pair.Key, pair => pair.Value);
        }, ct: token);

        // Bare ack — no tracking ID.
        return null;
    }

    #endregion

    public async Task RunImport_IntegrityCheck()
    {
        // files which have not been hashed yet
        // or files which do not have a VideoInfo record
        var filesToHash = _videoLocals.GetVideosWithoutHash();
        var dictFilesToHash = new Dictionary<int, VideoLocal>();
        foreach (var vl in filesToHash)
        {
            dictFilesToHash[vl.VideoLocalID] = vl;
            var p = vl.FirstResolvedPlace;
            if (p == null) continue;

            await _scheduler.StartJob<HashFileJob>(c => c.FilePath = p.Path!);
        }

        foreach (var vl in filesToHash)
        {
            // don't use if it is in the previous list
            if (dictFilesToHash.ContainsKey(vl.VideoLocalID)) continue;

            try
            {
                var p = vl.FirstResolvedPlace;
                if (p == null) continue;

                await _scheduler.StartJob<HashFileJob>(c => c.FilePath = p.Path!);
            }
            catch (Exception ex)
            {
                _logger.LogInformation("Error RunImport_IntegrityCheck XREF: {Detailed} - {Ex}", vl.ToStringDetailed(), ex.ToString());
            }
        }

        if (!_videoReleaseService.AutoMatchEnabled)
            return;

        // files which have been hashed, but don't have an associated episode
        var settings = _settingsProvider.GetSettings();
        var filesWithoutEpisode = _videoLocals.GetVideosWithoutEpisode();
        foreach (var vl in filesWithoutEpisode)
        {
            if (settings.Import.MaxAutoScanAttemptsPerFile != 0)
            {
                var matchAttempts = _storedReleaseInfoMatchAttempts.GetByEd2kAndFileSize(vl.Hash, vl.FileSize).Count;
                if (matchAttempts > settings.Import.MaxAutoScanAttemptsPerFile)
                    continue;
            }

            await _videoReleaseService.ScheduleFindReleaseForVideo(vl);
        }
    }

    public Task RunImport_GetImages()
        => _imageManager.ScheduleAllAutoDownloads();

    public Task RunImport_ScanTMDB()
        => _tmdbService.ScanForMatches();

    public Task RunImport_PurgeUnlinkedTmdbPeople()
        => _tmdbService.PurgeUnlinkedPeople();

    public Task RunImport_PurgeUnlinkedTmdbShowNetworks()
        => _tmdbService.PurgeUnlinkedShowNetworks();

    public async Task RunImport_UpdateAllAniDB()
    {
        var refreshMethod = AnidbRefreshMethod.Remote | AnidbRefreshMethod.DeferToRemoteIfUnsuccessful | AnidbRefreshMethod.SkipSupplementaryUpdate;
        foreach (var anime in _anidbAnimes.GetAll())
            await _anidbService.ScheduleRefreshOfAnime(anime, refreshMethod).ConfigureAwait(false);
    }

    public async Task RemoveRecordsWithoutPhysicalFiles(bool removeMyList = true)
    {
        _logger.LogInformation("Remove Missing Files: Start");
        var seriesToUpdate = new HashSet<AnimeSeries>();
        using var session = _databaseFactory.SessionFactory.OpenSession();

        // remove missing files in valid managed folders
        var filesAll = _videoLocalPlaces.GetAll()
            .Where(a => a.ManagedFolder is not null)
            .GroupBy(a => a.ManagedFolder!)
            .ToDictionary(a => a.Key!, a => a.ToList());
        foreach (var vl in filesAll.Keys.SelectMany(a => filesAll[a]))
        {
            if (File.Exists(vl.Path)) continue;

            // delete video local record
            _logger.LogInformation("Removing Missing File: {ID}", vl.VideoID);
            await ((VideoService)_videoService).RemoveRecordWithOpenTransaction(session, vl, seriesToUpdate, removeMyList);
        }

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
        foreach (var v in videoLocalsAll)
        {
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

            if (removeMyList)
                await ((VideoService)_videoService).ScheduleRemovalFromMyList(v);

            {
                using var transaction = session.BeginTransaction();
                _videoLocals.DeleteWithOpenTransaction(session, v);
                transaction.Commit();
            }
        }

        // Clean up failed imports
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

        // update everything we modified
        await Task.WhenAll(seriesToUpdate.Select(a => _scheduler.StartJob<RefreshAnimeStatsJob>(b => b.AnimeID = a.AniDB_ID)));

        _logger.LogInformation("Remove Missing Files: Finished");
    }

    public async Task UpdateAllStats()
    {
        await Task.WhenAll(_animeSeries.GetAll().Select(a => _scheduler.StartJob<RefreshAnimeStatsJob>(b => b.AnimeID = a.AniDB_ID)));
    }

    public async Task<int> UpdateAnidbReleaseInfo(bool countOnly = false)
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
            _logger.LogInformation("Queuing {Count} GetFile commands", missingFiles.Count);
            foreach (var id in missingFiles)
                await _videoReleaseService.ScheduleFindReleaseForVideo(id, force: true);

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
            foreach (var a in incorrectGroups)
                await _scheduler.StartJob<GetAniDBReleaseGroupJob>(c => c.GroupID = a);
        }

        return missingFiles.Count;
    }

    public async Task RefreshAniDBMovedFiles(bool force)
    {
        var settings = _settingsProvider.GetSettings();
        if (force || settings.AniDb.Notification_HandleMovedFiles)
        {
            var messages = _anidbMessages.GetUnhandledFileMoveMessages();
            if (messages.Count > 0)
            {
                foreach (var msg in messages)
                {
                    await _scheduler.StartJob<ProcessFileMovedMessageJob>(c => c.MessageID = msg.MessageID);
                }
            }
        }
    }

    public void CheckForPreviouslyIgnored()
    {
        try
        {
            var filesAll = _videoLocals.GetAll();
            IReadOnlyList<VideoLocal> filesIgnored = _videoLocals.GetIgnoredVideos();

            foreach (var vl in filesAll)
            {
                if (!vl.IsIgnored)
                {
                    // Check if we have this file marked as previously ignored, matches only if it has the same hash
                    var resultVideoLocalsIgnored =
                        filesIgnored.Where(s => s.Hash == vl.Hash).ToList();

                    if (resultVideoLocalsIgnored.Count != 0)
                    {
                        vl.IsIgnored = true;
                        _videoLocals.Save(vl, false);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in CheckForPreviouslyIgnored: {Ex}", ex);
        }
    }

    public async Task DownloadMissingAnidbAnimeXmls()
    {
        // Check existing anime.
        var index = 0;
        var queuedAnimeSet = new HashSet<int>();
        var localAnimeSet = _anidbAnimes.GetAll()
            .Select(a => a.AnimeID)
            .OrderBy(a => a)
            .ToHashSet();
        _logger.LogInformation("Checking {AllAnimeCount} anime for missing XML files…", localAnimeSet.Count);
        foreach (var animeID in localAnimeSet)
        {
            if (++index % 10 == 1 || index == localAnimeSet.Count)
                _logger.LogInformation("Checking {AllAnimeCount} anime for missing XML files — {CurrentCount}/{AllAnimeCount}", localAnimeSet.Count, index + 1, localAnimeSet.Count);

            var rawXml = await _xmlUtils.LoadAnimeHTTPFromFile(animeID);
            if (rawXml != null)
                continue;

            _logger.LogDebug("Found anime {AnimeID} with missing XML", animeID);
            await QueueAniDBRefresh(animeID, true, false, false, SkipSupplementaryUpdate: true);
            queuedAnimeSet.Add(animeID);
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

    public async Task ScheduleMissingAnidbAnimeForFiles()
    {
        // Attempt to fix cross-references with incomplete data.
        var index = 0;
        var videos = _videoLocals.GetVideosWithMissingCrossReferenceData();
        var unknownEpisodeDict = videos
            .SelectMany(file => file.EpisodeCrossReferences)
            .Where(xref => xref.AnimeID is 0)
            .GroupBy(xref => xref.EpisodeID)
            .ToDictionary(groupBy => groupBy.Key, groupBy => groupBy.ToList());
        _logger.LogInformation("Attempting to fix {MissingAnimeCount} cross-references with unknown anime…", unknownEpisodeDict.Count);
        foreach (var (episodeId, xrefs) in unknownEpisodeDict)
        {
            if (++index % 10 == 1)
                _logger.LogInformation("Attempting to fix cross-references with unknown anime — {CurrentCount}/{MissingAnimeCount}", index + 1, unknownEpisodeDict.Count);

            var episode = _anidbEpisodes.GetByEpisodeID(episodeId);
            if (episode is not null)
            {
                foreach (var xref in xrefs)
                    xref.AnimeID = episode.AnimeID;
                _crossRefFileEpisodes.Save(xrefs);
                continue;
            }

            int? epAnimeID = null;
            var epRequest = _requestFactory.Create<RequestGetEpisode>(r => r.EpisodeID = episodeId);
            try
            {
                var epResponse = epRequest.Send();
                epAnimeID = epResponse.Response?.AnimeID;
            }
            catch (Exception e)
            {
                _logger.LogError(e, "Could not get Episode Info for {EpisodeID}", episodeId);
            }

            if (epAnimeID is not null)
            {
                foreach (var xref in xrefs)
                    xref.AnimeID = epAnimeID.Value;
                _crossRefFileEpisodes.Save(xrefs);
            }
        }

        // Queue missing anime needed by existing files.
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
        foreach (var animeID in missingAnimeSet)
        {
            if (++index % 10 == 1 || index == missingAnimeSet.Count)
                _logger.LogInformation("Queueing anime that needs an update — {CurrentCount}/{MissingAnimeCount}", index, missingAnimeSet.Count);

            await _anidbService.ScheduleRefreshOfAnimeByID(animeID, refreshMethod);
        }
    }

    public async Task ScheduleMissingAnidbCreators()
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
        var progressCount = 0;
        foreach (var creatorID in allMissingCreators)
        {
            await _scheduler.StartJob<GetAniDBCreatorJob>(c => c.CreatorID = creatorID).ConfigureAwait(false);

            if (++progressCount % 10 == 0)
                _logger.LogInformation("Scheduling AniDB Creators for a refresh. (Progress={Count}/{Total})", progressCount, allMissingCreators.Count);
        }

        _logger.LogInformation("Scheduled {Count} AniDB Creators in {TimeSpan}", allMissingCreators.Count, DateTime.Now - startedAt);
    }

    public async Task CreateMissingSeries()
    {
        var missingSeries = _videoLocals.GetAll().SelectMany(vid =>
        {
            var xrefs = _crossRefFileEpisodes.GetByEd2k(vid.Hash);
            var aniDBAnime = xrefs.Select(a => _anidbAnimes.GetByAnimeID(a.AnimeID)).WhereNotNull();
            return aniDBAnime.Where(a => _animeSeries.GetByAnimeID(a.AnimeID) == null);
        }).ToList();

        _logger.LogInformation("Creating {Count} Series that are missing.", missingSeries.Count);

        var methods = AnidbRefreshMethod.Cache | AnidbRefreshMethod.DeferToRemoteIfUnsuccessful | AnidbRefreshMethod.CreateShokoSeries;
        foreach (var aniDBAnime in missingSeries)
            await _anidbService.ScheduleRefreshOfAnime(aniDBAnime, methods, prioritize: false);

        _logger.LogInformation("Queued Creation of {Count} Series that were missing.", missingSeries.Count);
    }

    public async Task<int> VerifyAllUnverifiedRelations()
    {
        var unverifiedAnimeIDs = _anidbAnimeRelations.GetAll()
            .Where(r => !r.Verified)
            .Select(r => r.AnimeID)
            .Distinct()
            .ToList();

        _logger.LogInformation("Scheduling verification of relations for {Count} anime with unverified relations", unverifiedAnimeIDs.Count);

        foreach (var animeID in unverifiedAnimeIDs)
            await _scheduler.StartJob<VerifyAniDBRelationsJob>(c => c.AnimeID = animeID);

        return unverifiedAnimeIDs.Count;
    }

}
