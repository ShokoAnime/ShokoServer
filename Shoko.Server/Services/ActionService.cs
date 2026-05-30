using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Shoko.Abstractions.Extensions;
using Shoko.Abstractions.Metadata.Anidb.Enums;
using Shoko.Abstractions.Metadata.Anidb.Services;
using Shoko.Abstractions.Metadata.Services;
using Shoko.Abstractions.Plugin;
using Shoko.Abstractions.Video.Services;
using Shoko.QueueProcessor.Abstractions;
using Shoko.QueueProcessor.Scheduling;
using Shoko.Server.Databases;
using Shoko.Server.Models.Shoko;
using Shoko.Server.Providers.AniDB;
using Shoko.Server.Providers.AniDB.Interfaces;
using Shoko.Server.Providers.AniDB.UDP.Info;
using Shoko.Server.Providers.TMDB;
using Shoko.Server.Repositories;
using Shoko.Server.Repositories.Cached;
using Shoko.Server.Repositories.Cached.AniDB;
using Shoko.Server.Repositories.Direct;
using Shoko.Server.Scheduling.Jobs.Actions;
using Shoko.Server.Scheduling.Jobs.AniDB;
using Shoko.Server.Scheduling.Jobs.Shoko;
using Shoko.Server.Server;
using Shoko.Server.Settings;

namespace Shoko.Server.Services;

public class ActionService
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
        ScheduledUpdateRepository scheduledUpdates
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
    }

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

            await _scheduler.StartJob<HashFileJob>(c => c.FilePath = p.Path);
        }

        foreach (var vl in filesToHash)
        {
            // don't use if it is in the previous list
            if (dictFilesToHash.ContainsKey(vl.VideoLocalID)) continue;

            try
            {
                var p = vl.FirstResolvedPlace;
                if (p == null) continue;

                await _scheduler.StartJob<HashFileJob>(c => c.FilePath = p.Path);
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
        var refreshMethod = AnidbRefreshMethod.Remote | AnidbRefreshMethod.DeferToRemoteIfUnsuccessful | AnidbRefreshMethod.SkipTmdbUpdate;
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
            .Where(a => a.ManagedFolder != null)
            .GroupBy(a => a.ManagedFolder)
            .ToDictionary(a => a.Key, a => a.ToList());
        foreach (var vl in filesAll.Keys.SelectMany(a => filesAll[a]))
        {
            if (File.Exists(vl.Path)) continue;

            // delete video local record
            _logger.LogInformation("Removing Missing File: {ID}", vl.VideoID);
            await ((VideoService)_videoService).RemoveRecordWithOpenTransaction(session, vl, seriesToUpdate, removeMyList);
        }

        var videoLocalsAll = _videoLocals.GetAll().ToList();
        // remove empty video locals
        BaseRepository.Lock(session, videoLocalsAll, (s, vls) =>
        {
            using var transaction = s.BeginTransaction();
            _videoLocals.DeleteWithOpenTransaction(s, vls.Where(a => a.IsEmpty()).ToList());
            transaction.Commit();
        });

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
                BaseRepository.Lock(session, places, (s, ps) =>
                {
                    using var transaction = s.BeginTransaction();
                    foreach (var place in ps)
                    {
                        place.VideoID = to.VideoLocalID;
                        _videoLocalPlaces.SaveWithOpenTransaction(s, place);
                    }

                    transaction.Commit();
                });
            }

            toRemove.AddRange(values);
        }

        BaseRepository.Lock(session, toRemove, (s, ps) =>
        {
            using var transaction = s.BeginTransaction();
            foreach (var remove in ps)
            {
                _videoLocals.DeleteWithOpenTransaction(s, remove);
            }

            transaction.Commit();
        });

        // Remove files in invalid managed folders
        foreach (var v in videoLocalsAll)
        {
            var places = v.Places;
            if (v.Places?.Count > 0)
            {
                BaseRepository.Lock(session, places, (s, ps) =>
                {
                    using var transaction = s.BeginTransaction();
                    foreach (var place in ps.Where(place => string.IsNullOrWhiteSpace(place?.Path)))
                    {
#pragma warning disable CS0618
                        _logger.LogInformation("Remove Records With Orphaned Managed Folder: {Filename}", v.FileName);
#pragma warning restore CS0618
                        seriesToUpdate.UnionWith(v.AnimeEpisodes.Select(a => a.AnimeSeries)
                            .DistinctBy(a => a.AnimeSeriesID));
                        _videoLocalPlaces.DeleteWithOpenTransaction(s, place);
                    }

                    transaction.Commit();
                });
            }

            // Remove duplicate places
            places = v.Places;
            if (places?.Count == 1) continue;

            if (places?.Count > 0)
            {
                places = places.DistinctBy(a => a.Path).ToList();
                places = v.Places?.Except(places).ToList() ?? [];
                foreach (var place in places)
                {
                    BaseRepository.Lock(session, place, (s, p) =>
                    {
                        using var transaction = s.BeginTransaction();
                        _videoLocalPlaces.DeleteWithOpenTransaction(s, p);
                        transaction.Commit();
                    });
                }
            }

            if (v.Places?.Count > 0) continue;

            // delete video local record
#pragma warning disable CS0618
            _logger.LogInformation("RemoveOrphanedVideoLocal : {Filename}", v.FileName);
#pragma warning restore CS0618
            seriesToUpdate.UnionWith(v.AnimeEpisodes.Select(a => a.AnimeSeries)
                .DistinctBy(a => a.AnimeSeriesID));

            if (removeMyList)
                await ((VideoService)_videoService).ScheduleRemovalFromMyList(v);

            BaseRepository.Lock(session, v, (s, vl) =>
            {
                using var transaction = s.BeginTransaction();
                _videoLocals.DeleteWithOpenTransaction(s, vl);
                transaction.Commit();
            });
        }

        // Clean up failed imports
        var list = _videoLocals.GetAll()
            .SelectMany(a => a.EpisodeCrossReferences)
            .Where(a => a.AniDBAnime == null || a.AniDBEpisode == null)
            .ToArray();
        BaseRepository.Lock(session, s =>
        {
            using var transaction = s.BeginTransaction();
            foreach (var xref in list)
            {
                // We don't need to update anything since they don't exist
                _crossRefFileEpisodes.DeleteWithOpenTransaction(s, xref);
            }

            transaction.Commit();
        });

        // clean up orphaned video local places
        var placesToRemove = _videoLocalPlaces.GetAll().Where(a => a.VideoLocal == null).ToList();
        BaseRepository.Lock(session, s =>
        {
            using var transaction = s.BeginTransaction();
            foreach (var place in placesToRemove)
            {
                // We don't need to update anything since they don't exist
                _videoLocalPlaces.DeleteWithOpenTransaction(s, place);
            }

            transaction.Commit();
        });

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
                .Select(a => int.Parse(a.GroupID))
                .ToHashSet();
            _logger.LogInformation("Queuing {Count} GetReleaseGroup commands", incorrectGroups.Count);
            foreach (var a in incorrectGroups)
                await _scheduler.StartJob<GetAniDBReleaseGroupJob>(c => c.GroupID = a);
        }

        return missingFiles.Count;
    }

    public async Task CheckForUnreadNotifications(bool ignoreSchedule)
    {
        var settings = _settingsProvider.GetSettings();
        if (!ignoreSchedule && settings.AniDb.Notification_UpdateFrequency == ScheduledUpdateFrequency.Never) return;

        var schedule = _scheduledUpdates.GetByUpdateType((int)ScheduledUpdateType.AniDBNotify);
        if (schedule == null)
        {
            schedule = new()
            {
                UpdateType = (int)ScheduledUpdateType.AniDBNotify,
                UpdateDetails = string.Empty
            };
        }
        else
        {
            var freqHours = settings.AniDb.Notification_UpdateFrequency.Hours;
            var tsLastRun = DateTime.Now - schedule.LastUpdate;

            // The NOTIFY command must not be issued more than once every 20 minutes according to the AniDB UDP API documentation:
            // https://wiki.anidb.net/UDP_API_Definition#NOTIFY:_Notifications
            // We will use 30 minutes as a safe interval.
            if (tsLastRun.TotalMinutes < 30) return;

            // if we have run this in the last freqHours and are not forcing it, then exit
            if (!ignoreSchedule && tsLastRun.TotalHours < freqHours) return;
        }

        schedule.LastUpdate = DateTime.Now;
        _scheduledUpdates.Save(schedule);
        await _scheduler.StartJob<GetAniDBNotifyJob>();

        // process any unhandled moved file messages
        await RefreshAniDBMovedFiles(false);
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

    public async Task CheckForCalendarUpdate(bool forceRefresh)
    {
        var settings = _settingsProvider.GetSettings();
        if (settings.AniDb.Calendar_UpdateFrequency == ScheduledUpdateFrequency.Never && !forceRefresh) return;

        var freqHours = settings.AniDb.Calendar_UpdateFrequency.Hours;

        // update the calendar every 12 hours
        // we will always assume that an anime was downloaded via http first

        var schedule = _scheduledUpdates.GetByUpdateType((int)ScheduledUpdateType.AniDBCalendar);
        if (schedule != null)
        {
            // if we have run this in the last 12 hours and are not forcing it, then exit
            var tsLastRun = DateTime.Now - schedule.LastUpdate;
            if (tsLastRun.TotalHours < freqHours && !forceRefresh) return;
        }

        await _scheduler.StartJob<GetAniDBCalendarJob>(c => c.ForceRefresh = forceRefresh);
    }

    public async Task CheckForAnimeUpdate()
    {
        var settings = _settingsProvider.GetSettings();
        if (settings.AniDb.Anime_UpdateFrequency == ScheduledUpdateFrequency.Never) return;

        var freqHours = settings.AniDb.Anime_UpdateFrequency.Hours;

        // check for any updated anime info every 12 hours

        var schedule = _scheduledUpdates.GetByUpdateType((int)ScheduledUpdateType.AniDBUpdates);
        if (schedule != null)
        {
            // if we have run this in the last 12 hours and are not forcing it, then exit
            var tsLastRun = DateTime.Now - schedule.LastUpdate;
            if (tsLastRun.TotalHours < freqHours) return;
        }

        await _scheduler.StartJob<GetUpdatedAniDBAnimeJob>(c => c.ForceRefresh = true);
    }

    public async Task CheckForMyListSyncUpdate(bool forceRefresh)
    {
        var settings = _settingsProvider.GetSettings();
        if (settings.AniDb.MyList_UpdateFrequency == ScheduledUpdateFrequency.Never && !forceRefresh) return;
        var freqHours = settings.AniDb.MyList_UpdateFrequency.Hours;

        // update the calendar every 24 hours

        var schedule = _scheduledUpdates.GetByUpdateType((int)ScheduledUpdateType.AniDBMyListSync);
        if (schedule != null)
        {
            // if we have run this in the last 24 hours and are not forcing it, then exit
            var tsLastRun = DateTime.Now - schedule.LastUpdate;
            _logger.LogTrace("Last AniDB MyList Sync: {Time} minutes ago", tsLastRun.TotalMinutes);
            if (tsLastRun.TotalHours < freqHours && !forceRefresh) return;
        }

        await _scheduler.StartJob<SyncAniDBMyListJob>(c => c.ForceRefresh = forceRefresh);
    }

    public async Task CheckForAniDBFileUpdate(bool forceRefresh)
    {
        var settings = _settingsProvider.GetSettings();
        if (settings.AniDb.File_UpdateFrequency == ScheduledUpdateFrequency.Never && !forceRefresh)
            return;

        // check for any updated anime info every 12 hours
        var freqHours = settings.AniDb.File_UpdateFrequency.Hours;
        var schedule = _scheduledUpdates.GetByUpdateType((int)ScheduledUpdateType.AniDBFileUpdates);
        if (schedule != null)
        {
            // if we have run this in the last 12 hours and are not forcing it, then exit
            var tsLastRun = DateTime.Now - schedule.LastUpdate;
            if (tsLastRun.TotalHours < freqHours && !forceRefresh) return;
        }

        // files which have been hashed, but don't have an associated episode
        if (_videoReleaseService.AutoMatchEnabled)
        {
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
        await ScheduleMissingAnidbAnimeForFiles();

        schedule ??= new()
        {
            UpdateType = (int)ScheduledUpdateType.AniDBFileUpdates,
            UpdateDetails = string.Empty
        };

        schedule.LastUpdate = DateTime.Now;
        _scheduledUpdates.Save(schedule);
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
            await QueueAniDBRefresh(animeID, true, false, false, skipTmdbUpdate: true);
            queuedAnimeSet.Add(animeID);
        }
    }

    public async Task<bool> QueueAniDBRefresh(int animeID, bool force, bool downloadRelations, bool createSeriesEntry, bool immediate = false,
        bool cacheOnly = false, bool skipTmdbUpdate = false)
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
        if (skipTmdbUpdate)
            refreshMethod |= AnidbRefreshMethod.SkipTmdbUpdate;
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
                _logger.LogInformation("Attempting to fix {MissingAnimeCount} cross-references with unknown anime — {CurrentCount}/{MissingAnimeCount}", unknownEpisodeDict.Count, index + 1, unknownEpisodeDict.Count);

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
                _logger.LogError(e, "Could not get Episode Info for {EpisodeID}", episode.EpisodeID);
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
        var refreshMethod = AnidbRefreshMethod.Remote | AnidbRefreshMethod.DeferToRemoteIfUnsuccessful | AnidbRefreshMethod.SkipTmdbUpdate | AnidbRefreshMethod.CreateShokoSeries;
        if (settings.AutoGroupSeries || settings.AniDb.DownloadRelatedAnime)
            refreshMethod |= AnidbRefreshMethod.DownloadRelations;
        foreach (var animeID in missingAnimeSet)
        {
            if (++index % 10 == 1 || index == missingAnimeSet.Count)
                _logger.LogInformation("Queueing {MissingAnimeCount} anime that needs an update — {CurrentCount}/{MissingAnimeCount}", missingAnimeSet.Count, index + 1, missingAnimeSet.Count);

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
                _logger.LogInformation("Scheduling {Count} AniDB Creators for a refresh. (Progress={Count}/{Total})", allMissingCreators.Count, progressCount, allMissingCreators.Count);
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

    /// <summary>
    ///   Schedules a plugin update check job.
    /// </summary>
    /// <param name="forceRefresh">
    ///   Force sync even if not stale.
    /// </param>
    public async Task CheckForPluginUpdates(bool forceRefresh)
    {
        var settings = _settingsProvider.GetSettings();

        // Check if auto-sync is enabled (must be enabled unless forcing)
        if (!settings.Plugins.Updates.IsAutoSyncEnabled && !forceRefresh)
            return;

        // Check frequency setting (skip schedule check if forcing)
        if (!forceRefresh)
        {
            if (settings.Plugins.Updates.AutoUpdateFrequency is ScheduledUpdateFrequency.Never)
                return;

            var schedule = _scheduledUpdates.GetByUpdateType((int)ScheduledUpdateType.PluginUpdates);
            if (schedule != null)
            {
                var freqHours = settings.Plugins.Updates.AutoUpdateFrequency.Hours;
                var tsLastRun = DateTime.Now - schedule.LastUpdate;
                if (tsLastRun.TotalHours < freqHours)
                    return;
            }
        }

        await _pluginPackageManager.ScheduleCheckForUpdates(forceSync: forceRefresh ? true : null).ConfigureAwait(false);
    }
}
