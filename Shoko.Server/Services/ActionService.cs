using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Shoko.Abstractions.Action;
using Shoko.Abstractions.Action.Enums;
using Shoko.Abstractions.Action.Services;
using Shoko.Abstractions.Extensions;
using Shoko.Abstractions.Metadata.Anidb.Enums;
using Shoko.Abstractions.Metadata.Anidb.Services;
using Shoko.Abstractions.Metadata.Services;
using Shoko.Abstractions.Metadata.Shoko;
using Shoko.Abstractions.Metadata.Tmdb.Services;
using Shoko.Abstractions.Plugin;
using Shoko.Abstractions.User;
using Shoko.Abstractions.Utilities;
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
using Shoko.Server.Scheduling.Jobs.Plex;
using Shoko.Server.Scheduling.Jobs.Shoko;
using Shoko.Server.Settings;

namespace Shoko.Server.Services;

/// <summary>
///   Service for executing Shoko actions (import, metadata refresh, etc.) and
///   for registering, discovering, and executing plugin-provided actions.
///   Implements <see cref="IActionService"/> for the plugin action system.
/// </summary>
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

    private readonly IPluginManager _pluginManager;

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

    private readonly AniDB_Anime_RelationRepository _anidbAnimeRelations;

    private readonly ITmdbLinkingService _tmdbLinkingService;

    private readonly JMMUserRepository _jmmUsers;

    /// <summary>
    ///   Registered plugin action types and their metadata. Populated once
    ///   during <see cref="AddParts"/>. A fresh instance is created from the
    ///   type on each execution via <see cref="IPluginManager.GetExport{T}"/>.
    /// </summary>
    private readonly List<(Type ActionType, ExecutableActionInfo Info)> _registeredActions = [];

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
        IPluginManager pluginManager,
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
        AniDB_Anime_RelationRepository anidbAnimeRelations,
        ITmdbLinkingService tmdbLinkingService,
        JMMUserRepository jmmUsers
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
        _pluginManager = pluginManager;
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
        _anidbAnimeRelations = anidbAnimeRelations;
        _tmdbLinkingService = tmdbLinkingService;
        _jmmUsers = jmmUsers;
    }

    #region IActionService

    /// <inheritdoc />
    public void AddParts(IEnumerable<IExecutableAction> actions)
    {
        if (_registeredActions.Count > 0)
            return;

        foreach (var action in actions)
        {
            var actionType = action.GetType();
            var pluginInfo = _pluginManager.GetPluginInfo(actionType.Assembly);
            if (pluginInfo is null)
                continue;

            var scopes = GetActionScopes(actionType);
            if (scopes.Count == 0)
                continue;

            var categoryName = action.Category switch
            {
                ActionCategory.PluginInferred => pluginInfo.Name,
                _ => action.Category.ToString(),
            };

            var info = new ExecutableActionInfo
            {
                ID = UuidUtility.GetV5(actionType.FullName!, pluginInfo.ID),
                Name = action.Name,
                Description = action.Description ?? string.Empty,
                Category = action.Category,
                CategoryName = categoryName,
                Scopes = scopes,
                RequiresConfirmation = action.RequiresConfirmation,
                PluginInfo = pluginInfo,
            };

            _registeredActions.Add((actionType, info));
        }
    }

    /// <inheritdoc />
    public IReadOnlyList<ExecutableActionInfo> GetActions(IEnumerable<ActionScope>? scopes = null, IEnumerable<ActionCategory>? categories = null, IEnumerable<string>? categoryNames = null)
    {
        var query = _registeredActions.Select(ra => ra.Info).AsEnumerable();

        if (scopes is not null)
        {
            var scopeSet = scopes.ToHashSet();
            query = query.Where(a => scopeSet.Overlaps(a.Scopes));
        }

        if (categories is not null)
        {
            var categorySet = categories.ToHashSet();
            query = query.Where(a => categorySet.Contains(a.Category));
        }

        if (categoryNames is not null)
        {
            var categoryNameSet = categoryNames.ToHashSet(StringComparer.OrdinalIgnoreCase);
            query = query.Where(a => categoryNameSet.Contains(a.CategoryName));
        }

        return query
            .OrderBy(a => a.Category)
            .ThenBy(a => a.CategoryName)
            .ThenBy(a => a.Name)
            .ToList();
    }

    /// <inheritdoc />
    public ExecutableActionInfo? GetActionById(Guid actionId)
        => _registeredActions.FirstOrDefault(ra => ra.Info.ID == actionId).Info;

    /// <inheritdoc />
    public async Task ExecuteGlobalAction(ExecutableActionInfo actionInfo, CancellationToken cancellationToken = default)
    {
        if (!actionInfo.Scopes.Contains(ActionScope.Global))
            throw new InvalidOperationException($"The action '{actionInfo.Name}' ({actionInfo.ID}) does not support global execution.");

        if (ResolveAction(actionInfo) is not IExecutableGlobalAction globalAction)
            throw new InvalidOperationException($"The action '{actionInfo.Name}' ({actionInfo.ID}) does not implement {nameof(IExecutableGlobalAction)}.");

        cancellationToken.ThrowIfCancellationRequested();
        await globalAction.Execute(cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public Task ScheduleExecuteOfGlobalAction(ExecutableActionInfo actionInfo, CancellationToken cancellationToken = default, bool prioritize = false)
    {
        if (!actionInfo.Scopes.Contains(ActionScope.Global))
            throw new InvalidOperationException($"The action '{actionInfo.Name}' ({actionInfo.ID}) does not support global execution.");

        return _scheduler.StartJob<ExecuteActionJob>(j => j.ActionID = actionInfo.ID, prioritize: prioritize);
    }

    /// <inheritdoc />
    public async Task ExecuteGlobalUserAction(ExecutableActionInfo actionInfo, IUser user, CancellationToken cancellationToken = default)
    {
        if (!actionInfo.Scopes.Contains(ActionScope.GlobalUser))
            throw new InvalidOperationException($"The action '{actionInfo.Name}' ({actionInfo.ID}) does not support global user execution.");

        if (ResolveAction(actionInfo) is not IExecutableGlobalUserAction globalUserAction)
            throw new InvalidOperationException($"The action '{actionInfo.Name}' ({actionInfo.ID}) does not implement {nameof(IExecutableGlobalUserAction)}.");

        cancellationToken.ThrowIfCancellationRequested();
        await globalUserAction.Execute(user, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public Task ScheduleExecuteOfGlobalUserAction(ExecutableActionInfo actionInfo, IUser user, CancellationToken cancellationToken = default, bool prioritize = false)
    {
        if (!actionInfo.Scopes.Contains(ActionScope.GlobalUser))
            throw new InvalidOperationException($"The action '{actionInfo.Name}' ({actionInfo.ID}) does not support global user execution.");

        return _scheduler.StartJob<ExecuteActionJob>(j =>
        {
            j.ActionID = actionInfo.ID;
            j.UserID = user.ID;
        }, prioritize: prioritize);
    }

    /// <inheritdoc />
    public async Task ExecuteSeriesAction(ExecutableActionInfo actionInfo, IShokoSeries series, CancellationToken cancellationToken = default)
    {
        if (!actionInfo.Scopes.Contains(ActionScope.Series))
            throw new InvalidOperationException($"The action '{actionInfo.Name}' ({actionInfo.ID}) does not support series execution.");

        if (ResolveAction(actionInfo) is not IExecutableSeriesAction seriesAction)
            throw new InvalidOperationException($"The action '{actionInfo.Name}' ({actionInfo.ID}) does not implement {nameof(IExecutableSeriesAction)}.");

        cancellationToken.ThrowIfCancellationRequested();
        await seriesAction.Execute(series, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public Task ScheduleExecuteOfSeriesAction(ExecutableActionInfo actionInfo, IShokoSeries series, CancellationToken cancellationToken = default, bool prioritize = false)
    {
        if (!actionInfo.Scopes.Contains(ActionScope.Series))
            throw new InvalidOperationException($"The action '{actionInfo.Name}' ({actionInfo.ID}) does not support series execution.");

        return _scheduler.StartJob<ExecuteActionJob>(j =>
        {
            j.ActionID = actionInfo.ID;
            j.AnimeID = series.AnidbAnimeID;
        }, prioritize: prioritize);
    }

    /// <inheritdoc />
    public async Task ExecuteSeriesUserAction(ExecutableActionInfo actionInfo, IShokoSeries series, IUser user, CancellationToken cancellationToken = default)
    {
        if (!actionInfo.Scopes.Contains(ActionScope.SeriesUser))
            throw new InvalidOperationException($"The action '{actionInfo.Name}' ({actionInfo.ID}) does not support series user execution.");

        if (ResolveAction(actionInfo) is not IExecutableSeriesUserAction seriesUserAction)
            throw new InvalidOperationException($"The action '{actionInfo.Name}' ({actionInfo.ID}) does not implement {nameof(IExecutableSeriesUserAction)}.");

        cancellationToken.ThrowIfCancellationRequested();
        await seriesUserAction.Execute(series, user, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public Task ScheduleExecuteOfSeriesUserAction(ExecutableActionInfo actionInfo, IShokoSeries series, IUser user, CancellationToken cancellationToken = default, bool prioritize = false)
    {
        if (!actionInfo.Scopes.Contains(ActionScope.SeriesUser))
            throw new InvalidOperationException($"The action '{actionInfo.Name}' ({actionInfo.ID}) does not support series user execution.");

        return _scheduler.StartJob<ExecuteActionJob>(j =>
        {
            j.ActionID = actionInfo.ID;
            j.AnimeID = series.AnidbAnimeID;
            j.UserID = user.ID;
        }, prioritize: prioritize);
    }

    /// <inheritdoc />
    public async Task ExecuteGroupAction(ExecutableActionInfo actionInfo, IShokoGroup group, CancellationToken cancellationToken = default)
    {
        if (!actionInfo.Scopes.Contains(ActionScope.Group))
            throw new InvalidOperationException($"The action '{actionInfo.Name}' ({actionInfo.ID}) does not support group execution.");

        if (ResolveAction(actionInfo) is not IExecutableGroupAction groupAction)
            throw new InvalidOperationException($"The action '{actionInfo.Name}' ({actionInfo.ID}) does not implement {nameof(IExecutableGroupAction)}.");

        cancellationToken.ThrowIfCancellationRequested();
        await groupAction.Execute(group, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public Task ScheduleExecuteOfGroupAction(ExecutableActionInfo actionInfo, IShokoGroup group, CancellationToken cancellationToken = default, bool prioritize = false)
    {
        if (!actionInfo.Scopes.Contains(ActionScope.Group))
            throw new InvalidOperationException($"The action '{actionInfo.Name}' ({actionInfo.ID}) does not support group execution.");

        return _scheduler.StartJob<ExecuteActionJob>(j =>
        {
            j.ActionID = actionInfo.ID;
            j.GroupID = group.ID;
        }, prioritize: prioritize);
    }

    /// <inheritdoc />
    public async Task ExecuteGroupUserAction(ExecutableActionInfo actionInfo, IShokoGroup group, IUser user, CancellationToken cancellationToken = default)
    {
        if (!actionInfo.Scopes.Contains(ActionScope.GroupUser))
            throw new InvalidOperationException($"The action '{actionInfo.Name}' ({actionInfo.ID}) does not support group user execution.");

        if (ResolveAction(actionInfo) is not IExecutableGroupUserAction groupUserAction)
            throw new InvalidOperationException($"The action '{actionInfo.Name}' ({actionInfo.ID}) does not implement {nameof(IExecutableGroupUserAction)}.");

        cancellationToken.ThrowIfCancellationRequested();
        await groupUserAction.Execute(group, user, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public Task ScheduleExecuteOfGroupUserAction(ExecutableActionInfo actionInfo, IShokoGroup group, IUser user, CancellationToken cancellationToken = default, bool prioritize = false)
    {
        if (!actionInfo.Scopes.Contains(ActionScope.GroupUser))
            throw new InvalidOperationException($"The action '{actionInfo.Name}' ({actionInfo.ID}) does not support group user execution.");

        return _scheduler.StartJob<ExecuteActionJob>(j =>
        {
            j.ActionID = actionInfo.ID;
            j.GroupID = group.ID;
            j.UserID = user.ID;
        }, prioritize: prioritize);
    }

    /// <inheritdoc />
    public async Task ExecuteEpisodeAction(ExecutableActionInfo actionInfo, IShokoEpisode episode, CancellationToken cancellationToken = default)
    {
        if (!actionInfo.Scopes.Contains(ActionScope.Episode))
            throw new InvalidOperationException($"The action '{actionInfo.Name}' ({actionInfo.ID}) does not support episode execution.");

        if (ResolveAction(actionInfo) is not IExecutableEpisodeAction episodeAction)
            throw new InvalidOperationException($"The action '{actionInfo.Name}' ({actionInfo.ID}) does not implement {nameof(IExecutableEpisodeAction)}.");

        cancellationToken.ThrowIfCancellationRequested();
        await episodeAction.Execute(episode, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public Task ScheduleExecuteOfEpisodeAction(ExecutableActionInfo actionInfo, IShokoEpisode episode, CancellationToken cancellationToken = default, bool prioritize = false)
    {
        if (!actionInfo.Scopes.Contains(ActionScope.Episode))
            throw new InvalidOperationException($"The action '{actionInfo.Name}' ({actionInfo.ID}) does not support episode execution.");

        return _scheduler.StartJob<ExecuteActionJob>(j =>
        {
            j.ActionID = actionInfo.ID;
            j.EpisodeID = episode.ID;
        }, prioritize: prioritize);
    }

    /// <inheritdoc />
    public async Task ExecuteEpisodeUserAction(ExecutableActionInfo actionInfo, IShokoEpisode episode, IUser user, CancellationToken cancellationToken = default)
    {
        if (!actionInfo.Scopes.Contains(ActionScope.EpisodeUser))
            throw new InvalidOperationException($"The action '{actionInfo.Name}' ({actionInfo.ID}) does not support episode user execution.");

        if (ResolveAction(actionInfo) is not IExecutableEpisodeUserAction episodeUserAction)
            throw new InvalidOperationException($"The action '{actionInfo.Name}' ({actionInfo.ID}) does not implement {nameof(IExecutableEpisodeUserAction)}.");

        cancellationToken.ThrowIfCancellationRequested();
        await episodeUserAction.Execute(episode, user, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public Task ScheduleExecuteOfEpisodeUserAction(ExecutableActionInfo actionInfo, IShokoEpisode episode, IUser user, CancellationToken cancellationToken = default, bool prioritize = false)
    {
        if (!actionInfo.Scopes.Contains(ActionScope.EpisodeUser))
            throw new InvalidOperationException($"The action '{actionInfo.Name}' ({actionInfo.ID}) does not support episode user execution.");

        return _scheduler.StartJob<ExecuteActionJob>(j =>
        {
            j.ActionID = actionInfo.ID;
            j.EpisodeID = episode.ID;
            j.UserID = user.ID;
        }, prioritize: prioritize);
    }

    /// <summary>
    ///   Collects all <see cref="ActionScope"/> values that an executable
    ///   action supports, based on which sub-interfaces its type implements.
    /// </summary>
    private static IReadOnlySet<ActionScope> GetActionScopes(Type actionType)
    {
        var result = new HashSet<ActionScope>();
        if (typeof(IExecutableGlobalAction).IsAssignableFrom(actionType))
            result.Add(ActionScope.Global);
        if (typeof(IExecutableGlobalUserAction).IsAssignableFrom(actionType))
            result.Add(ActionScope.GlobalUser);

        if (typeof(IExecutableGroupAction).IsAssignableFrom(actionType))
            result.Add(ActionScope.Group);
        if (typeof(IExecutableGroupUserAction).IsAssignableFrom(actionType))
            result.Add(ActionScope.GroupUser);

        if (typeof(IExecutableSeriesAction).IsAssignableFrom(actionType))
            result.Add(ActionScope.Series);
        if (typeof(IExecutableSeriesUserAction).IsAssignableFrom(actionType))
            result.Add(ActionScope.SeriesUser);

        if (typeof(IExecutableEpisodeAction).IsAssignableFrom(actionType))
            result.Add(ActionScope.Episode);
        if (typeof(IExecutableEpisodeUserAction).IsAssignableFrom(actionType))
            result.Add(ActionScope.EpisodeUser);

        return result;
    }

    private IExecutableAction ResolveAction(ExecutableActionInfo actionInfo)
    {
        var (actionType, _) = _registeredActions.FirstOrDefault(ra => ra.Info.ID == actionInfo.ID);
        var instance = (
            actionType is not null
                ? _pluginManager.GetExport<IExecutableAction>(actionType)
                : null
        ) ??
            throw new InvalidOperationException($"The action '{actionInfo.Name}' ({actionInfo.ID}) is not registered.");
        return instance;
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

    public Task RunImport_SyncVotes()
        => _scheduler.StartJob<SyncAniDBVotesJob>(c => (c.UserID, c.Export) = (0, true));

    public Task PurgeAllTmdbLinks()
    {
        _tmdbLinkingService.RemoveAllLinks(true, true);
        _tmdbLinkingService.ResetAutoLinkingState(false);
        return Task.CompletedTask;
    }

    public Task UpdateAniDBCalendar()
        => _scheduler.StartJob<GetAniDBCalendarJob>(c => c.ForceRefresh = true);

    public async Task PlexSyncAll()
    {
        foreach (var user in _jmmUsers.GetAll())
        {
            if (string.IsNullOrEmpty(user.PlexToken))
                continue;
            await _scheduler.StartJob<SyncPlexWatchedStatesJob>(c => c.User = user);
        }
    }

    public async Task AddAllManualLinksToMyList()
    {
        var files = _videoLocals.GetManuallyLinkedVideos();
        foreach (var vl in files)
            await _scheduler.StartJob<AddFileToMyListJob>(c => c.Hash = vl.Hash);
    }

    public Task GetAniDBNotifications()
        => _scheduler.StartJob<CheckAniDBNotificationsJob>(c => c.ForceRefresh = true);

    public Task AVDumpMismatchedFiles()
    {
        var settings = _settingsProvider.GetSettings();
        if (string.IsNullOrWhiteSpace(settings.AniDb.AVDumpKey))
            return Task.CompletedTask;

        var mismatchedFiles = _videoLocals.GetAll()
            .Where(file => !file.IsEmpty() && file.MediaInfo != null)
            .Select(file => (Video: file, AniDB: file.ReleaseInfo))
            .Where(tuple => tuple.AniDB is { ProviderName: "AniDB", IsCorrupted: false } && tuple.Video.MediaInfo?.MenuStreams.Count != 0 != tuple.AniDB.IsChaptered)
            .Select(tuple => (Path: tuple.Video.FirstResolvedPlace?.Path!, tuple.Video))
            .Where(tuple => !string.IsNullOrEmpty(tuple.Path))
            .ToDictionary(tuple => tuple.Video.VideoLocalID, tuple => tuple.Path);

        foreach (var (fileId, filePath) in mismatchedFiles)
            _scheduler.StartJob<AVDumpFilesJob>(a => a.Videos = new() { { fileId, filePath } });

        _logger.LogInformation("Queued {QueuedAnimeCount} files for avdumping", mismatchedFiles.Count);
        return Task.CompletedTask;
    }
}
