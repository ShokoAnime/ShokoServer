using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Quartz;
using Shoko.Server.Extensions;
using Shoko.Server.Models;
using Shoko.Server.Providers.AniDB;
using Shoko.Server.Providers.AniDB.Interfaces;
using Shoko.Server.Repositories;
using Shoko.Server.Repositories.Cached;
using Shoko.Server.Scheduling.Acquisition.Attributes;
using Shoko.Server.Scheduling.Attributes;
using Shoko.Server.Scheduling.Concurrency;
using Shoko.Server.Scheduling.Jobs.Actions;
using Shoko.Server.Scheduling.Jobs.AniDB;
using Shoko.Server.Scheduling.Jobs.TMDB;
using Shoko.Server.Services;
using Shoko.Server.Settings;
using Shoko.Server.Utilities;

namespace Shoko.Server.Scheduling.Jobs.Shoko;

[DatabaseRequired]
[AniDBUdpRateLimited]
[DisallowConcurrencyGroup(ConcurrencyGroups.AniDB_UDP)]
[JobKeyGroup(JobKeyGroup.Import)]
public class ProcessFileJob : BaseJob
{
    private readonly ISchedulerFactory _schedulerFactory;
    private readonly JobFactory _jobFactory;
    private readonly IServerSettings _settings;
    private readonly IUDPConnectionHandler _udpConnectionHandler;
    private readonly VideoLocalService _vlService;
    private readonly VideoLocal_PlaceService _vlPlaceService;
    private readonly VideoLocal_UserRepository _vlUsers;

    private SVR_VideoLocal _vlocal;
    private string _fileName;

    public int VideoLocalID { get; set; }

    public bool ForceAniDB { get; set; }

    public bool SkipMyList { get; set; }

    public override string TypeName => "Get Cross-References for File";

    public override string Title => "Getting Cross-References for File";

    public override Dictionary<string, object> Details => new()
    {
        { "File Path", _fileName ?? VideoLocalID.ToString() }
    };

    public override void PostInit()
    {
        _vlocal = RepoFactory.VideoLocal.GetByID(VideoLocalID);
        if (_vlocal == null) throw new JobExecutionException($"VideoLocal not Found: {VideoLocalID}");
        _fileName = Utils.GetDistinctPath(_vlocal?.FirstValidPlace?.FullServerPath);
    }
    public override async Task Process()
    {
        _logger.LogInformation("Processing {Job}: {FileName}", nameof(ProcessFileJob), _fileName ?? VideoLocalID.ToString());

        // Check if the video local (file) is available.
        if (_vlocal == null)
        {
            _vlocal = RepoFactory.VideoLocal.GetByID(VideoLocalID);
            if (_vlocal == null)
                return;
        }

        // Store a hash-set of the old cross-references for comparison later.
        var oldXRefs = _vlocal.EpisodeCrossReferences
            .Select(xref => xref.ToString())
            .Join(',');

        // Process and get the AniDB file entry.
        var aniFile = await ProcessFile_AniDB().ConfigureAwait(false);

        // Check if an AniDB file is now available and if the cross-references changed.
        var newXRefs = _vlocal.EpisodeCrossReferences
            .Select(xref => xref.ToString())
            .Join(',');
        var xRefsMatch = newXRefs == oldXRefs;
        // Fire the file matched event on first import and any later scans where the xrefs changed
        if (aniFile != null && !string.IsNullOrEmpty(newXRefs) && (!_vlocal.DateTimeImported.HasValue || !xRefsMatch))
        {
            // Set/update the import date
            _vlocal.DateTimeImported = DateTime.Now;
            RepoFactory.VideoLocal.Save(_vlocal);

            // Dispatch the on file matched event.
            ShokoEventHandler.Instance.OnFileMatched(_vlocal.FirstValidPlace, _vlocal);
        }
        // Fire the file not matched event if we didn't update the cross-references.
        else
        {
            var autoMatchAttempts = RepoFactory.AniDB_FileUpdate.GetByFileSizeAndHash(_vlocal.FileSize, _vlocal.Hash).Count;
            var hasXRefs = !string.IsNullOrEmpty(newXRefs) && xRefsMatch;
            var isUDPBanned = _udpConnectionHandler.IsBanned;
            ShokoEventHandler.Instance.OnFileNotMatched(_vlocal.FirstValidPlace, _vlocal, autoMatchAttempts, hasXRefs, isUDPBanned);
        }

        // Rename and/or move the physical file(s) if needed.
        if (_settings.Plugins.Renamer.RelocateOnImport)
        {
            var scheduler = await _schedulerFactory.GetScheduler().ConfigureAwait(false);
            await scheduler.StartJob<RenameMoveFileJob>(job => job.VideoLocalID = _vlocal.VideoLocalID).ConfigureAwait(false);
        }
    }

    private async Task<SVR_AniDB_File> ProcessFile_AniDB()
    {
        _logger.LogTrace("Checking for AniDB_File record for: {VidLocalHash} --- {VidLocalFileName}", _vlocal.Hash, _fileName);
        // check if we already have this AniDB_File info in the database

        var animeIDs = new Dictionary<int, bool>();

        var aniFile = GetLocalAniDBFile(_vlocal);
        if (aniFile == null || aniFile.FileSize == 0)
            aniFile ??= await TryGetAniDBFileFromAniDB(animeIDs).ConfigureAwait(false);
        if (aniFile == null) return null;

        await PopulateAnimeForFile(_vlocal, aniFile.EpisodeCrossReferences, animeIDs).ConfigureAwait(false);

        // We do this inside, as the info will not be available as needed otherwise
        var videoLocals =
            aniFile.EpisodeIDs?.SelectMany(a => RepoFactory.VideoLocal.GetByAniDBEpisodeID(a))
                .WhereNotNull()
                .ToList();
        if (videoLocals == null) return null;

        // Get status from existing eps/files if needed
        GetWatchedStateIfNeeded(_vlocal, videoLocals);

        // update stats for groups and series. The series are not saved until here, so it's absolutely necessary!!
        await Task.WhenAll(animeIDs.Keys.Select(a => _jobFactory.CreateJob<RefreshAnimeStatsJob>(b => b.AnimeID = a).Process())).ConfigureAwait(false);

        if (_settings.FileQualityFilterEnabled)
        {
            videoLocals.Sort(FileQualityFilter.CompareTo);
            var keep = videoLocals
                .Take(FileQualityFilter.Settings.MaxNumberOfFilesToKeep)
                .ToList();
            foreach (var vl2 in keep)
            {
                videoLocals.Remove(vl2);
            }

            if (!FileQualityFilter.Settings.AllowDeletionOfImportedFiles &&
                videoLocals.Contains(_vlocal))
            {
                videoLocals.Remove(_vlocal);
            }

            videoLocals = videoLocals.Where(a => !FileQualityFilter.CheckFileKeep(a)).ToList();

            foreach (var place in videoLocals.SelectMany(a => a.Places))
                await _vlPlaceService.RemoveRecordAndDeletePhysicalFile(place).ConfigureAwait(false);
        }

        // we have an AniDB File, so check the release group info
        if (aniFile.GroupID != 0)
        {
            var releaseGroup = RepoFactory.AniDB_ReleaseGroup.GetByGroupID(aniFile.GroupID);
            if (releaseGroup == null)
            {
                // may as well download it immediately. We can change it later if it becomes an issue
                // this will only happen if it's null, and most people grab mostly the same release groups
                var groupCommand = _jobFactory.CreateJob<GetAniDBReleaseGroupJob>(c => c.GroupID = aniFile.GroupID);
                await groupCommand.Process().ConfigureAwait(false);
            }
        }

        // Add this file to the users list
        if (_settings.AniDb.MyList_AddFiles && !SkipMyList && _vlocal.MyListID <= 0)
        {
            var scheduler = await _schedulerFactory.GetScheduler().ConfigureAwait(false);
            await scheduler.StartJob<AddFileToMyListJob>(c =>
            {
                c.Hash = _vlocal.Hash;
                c.ReadStates = true;
            }).ConfigureAwait(false);
        }

        return aniFile;
    }

    private void GetWatchedStateIfNeeded(SVR_VideoLocal vidLocal, List<SVR_VideoLocal> videoLocals)
    {
        if (!_settings.Import.UseExistingFileWatchedStatus) return;

        // Copy over watched states
        foreach (var user in RepoFactory.JMMUser.GetAll())
        {
            var watchedVideo = videoLocals.WhereNotNull()
                .FirstOrDefault(a => _vlUsers.GetByUserIDAndVideoLocalID(user.JMMUserID, a.VideoLocalID)?.WatchedDate != null);
            // No files that are watched
            if (watchedVideo == null)
            {
                continue;
            }

            var watchedRecord = _vlUsers.GetByUserIDAndVideoLocalID(user.JMMUserID, watchedVideo.VideoLocalID);
            var userRecord = _vlService.GetOrCreateUserRecord(vidLocal, user.JMMUserID);

            userRecord.WatchedDate = watchedRecord.WatchedDate;
            userRecord.WatchedCount = watchedRecord.WatchedCount;
            userRecord.ResumePosition = watchedRecord.ResumePosition;
            userRecord.LastUpdated = watchedRecord.LastUpdated;
            RepoFactory.VideoLocalUser.Save(userRecord);
        }
    }

    private SVR_AniDB_File GetLocalAniDBFile(SVR_VideoLocal vidLocal)
    {
        SVR_AniDB_File aniFile = null;
        if (!ForceAniDB)
        {
            aniFile = RepoFactory.AniDB_File.GetByEd2kAndFileSize(vidLocal.Hash, _vlocal.FileSize);

            if (aniFile == null)
            {
                _logger.LogTrace("AniDB_File record not found");
            }
        }

        // If cross refs were wiped, but the AniDB_File was not, we unfortunately need to re-query the info
        var crossRefs = RepoFactory.CrossRef_File_Episode.GetByEd2k(vidLocal.Hash);
        if (crossRefs == null || crossRefs.Count == 0)
        {
            aniFile = null;
        }

        return aniFile;
    }

    private async Task PopulateAnimeForFile(SVR_VideoLocal vidLocal, IReadOnlyList<SVR_CrossRef_File_Episode> xrefs, Dictionary<int, bool> animeIDs)
    {
        // check if we have the episode info
        // if we don't, we will need to re-download the anime info (which also has episode info)
        if (xrefs.Count == 0)
        {
            // if we have the AniDB file, but no cross refs it means something has been broken
            _logger.LogDebug("Could not find any cross ref records for: {Ed2KHash}", vidLocal.Hash);
        }
        else
        {
            foreach (var xref in xrefs)
            {
                var ep = RepoFactory.AniDB_Episode.GetByEpisodeID(xref.EpisodeID);

                if (animeIDs.ContainsKey(xref.AnimeID))
                {
                    animeIDs[xref.AnimeID] = animeIDs[xref.AnimeID] || ep == null;
                }
                else
                {
                    animeIDs.Add(xref.AnimeID, ep == null);
                }
            }
        }

        foreach (var kV in animeIDs)
        {
            var animeID = kV.Key;
            if (animeID == 0) continue;
            // get from DB
            var anime = RepoFactory.AniDB_Anime.GetByAnimeID(animeID);
            var update = RepoFactory.AniDB_AnimeUpdate.GetByAnimeID(animeID);
            var series = RepoFactory.AnimeSeries.GetByAnimeID(animeID);
            var animeRecentlyUpdated = false;
            var missingEpisodes = kV.Value;

            if (anime == null || update == null || series == null)
            {
                missingEpisodes = true;
            }
            else
            {
                var ts = DateTime.Now - update.UpdatedAt;
                if (ts.TotalHours < _settings.AniDb.MinimumHoursToRedownloadAnimeInfo)
                {
                    animeRecentlyUpdated = true;
                }
            }

            // even if we are missing episode info, don't get data  more than once every `x` hours
            // this is to prevent banning
            var scheduler = await _schedulerFactory.GetScheduler().ConfigureAwait(false);
            if (missingEpisodes)
            {
                _logger.LogInformation("Queuing immediate GET for AniDB_Anime: {AnimeID}", animeID);
                // this should detect and handle a ban, which will leave Result null, and defer
                await scheduler.StartJobNow<GetAniDBAnimeJob>(c =>
                {
                    c.AnimeID = animeID;
                    c.ForceRefresh = true;
                    c.DownloadRelations = _settings.AutoGroupSeries || _settings.AniDb.DownloadRelatedAnime;
                    c.CreateSeriesEntry = true;
                }).ConfigureAwait(false);
            }
            else if (!animeRecentlyUpdated)
            {
                _logger.LogInformation("Queuing GET for AniDB_Anime: {AnimeID}", animeID);
                // this should detect and handle a ban, which will leave Result null, and defer
                await scheduler.StartJob<GetAniDBAnimeJob>(c =>
                {
                    c.AnimeID = animeID;
                    c.ForceRefresh = true;
                    c.DownloadRelations = _settings.AutoGroupSeries || _settings.AniDb.DownloadRelatedAnime;
                }).ConfigureAwait(false);
            }

            var tmdbShowXrefs = RepoFactory.CrossRef_AniDB_TMDB_Show.GetByAnidbAnimeID(animeID);
            foreach (var xref in tmdbShowXrefs)
                await scheduler.StartJob<UpdateTmdbShowJob>(job =>
                {
                    job.TmdbShowID = xref.TmdbShowID;
                    job.DownloadImages = true;
                }).ConfigureAwait(false);
        }
    }

    private async Task<SVR_AniDB_File> TryGetAniDBFileFromAniDB(Dictionary<int, bool> animeIDs)
    {
        // check if we already have a record
        var aniFile = RepoFactory.AniDB_File.GetByEd2kAndFileSize(_vlocal.Hash, _vlocal.FileSize);

        if (aniFile == null || aniFile.FileSize != _vlocal.FileSize)
        {
            ForceAniDB = true;
        }

        if (ForceAniDB)
        {
            // get info from AniDB
            _logger.LogDebug("Getting AniDB_File record from AniDB....");
            try
            {
                aniFile = await _jobFactory.CreateJob<GetAniDBFileJob>(c =>
                {
                    c.VideoLocalID = _vlocal.VideoLocalID;
                    c.ForceAniDB = true;
                }).Process().ConfigureAwait(false);
            }
            catch (AniDBBannedException)
            {
                // We're banned, so queue it for later
                _logger.LogError("We are banned. Re-queuing for later: {FileName}", _fileName);

                var scheduler = await _schedulerFactory.GetScheduler().ConfigureAwait(false);
                await scheduler.StartJob<ProcessFileJob>(
                    c =>
                    {
                        c.VideoLocalID = _vlocal.VideoLocalID;
                        c.ForceAniDB = true;
                    }).ConfigureAwait(false);
            }
        }

        if (aniFile == null)
        {
            return null;
        }

        // get Anime IDs from the file for processing, the episodes might not be created yet here
        aniFile.EpisodeCrossReferences.Select(a => a.AnimeID).Distinct().ForEach(animeID =>
        {
            animeIDs[animeID] = false;
        });

        return aniFile;
    }

    public ProcessFileJob(ISettingsProvider settingsProvider, ISchedulerFactory schedulerFactory, VideoLocal_PlaceService vlPlaceService,
        IUDPConnectionHandler udpConnectionHandler, JobFactory jobFactory, VideoLocal_UserRepository vlUsers, VideoLocalService vlService)
    {
        _schedulerFactory = schedulerFactory;
        _settings = settingsProvider.GetSettings();
        _vlPlaceService = vlPlaceService;
        _udpConnectionHandler = udpConnectionHandler;
        _jobFactory = jobFactory;
        _vlUsers = vlUsers;
        _vlService = vlService;
    }

    protected ProcessFileJob() { }
}
