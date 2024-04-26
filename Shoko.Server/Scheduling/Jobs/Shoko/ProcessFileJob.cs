using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Quartz;
using Shoko.Commons.Extensions;
using Shoko.Models.Server;
using Shoko.Server.Models;
using Shoko.Server.Providers.AniDB;
using Shoko.Server.Providers.AniDB.Interfaces;
using Shoko.Server.Repositories;
using Shoko.Server.Scheduling.Acquisition.Attributes;
using Shoko.Server.Scheduling.Attributes;
using Shoko.Server.Scheduling.Concurrency;
using Shoko.Server.Scheduling.Jobs.AniDB;
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

    private readonly VideoLocal_PlaceService _vlPlaceService;

    private SVR_VideoLocal _vlocal;

    private string _fileName;

    public int VideoLocalID { get; set; }

    public bool ForceAniDB { get; set; }

    public bool SkipMyList { get; set; }

    public override string TypeName => "Process File";

    public override string Title => "Get Cross-References for File";

    public override Dictionary<string, object> Details => new()
    {
        { "File Path", _fileName ?? VideoLocalID.ToString() }
    };

    public override void PostInit()
    {
        _vlocal = RepoFactory.VideoLocal.GetByID(VideoLocalID);
        if (_vlocal == null) throw new JobExecutionException($"VideoLocal not Found: {VideoLocalID}");
        _fileName = Utils.GetDistinctPath(_vlocal?.GetBestVideoLocalPlace()?.FullServerPath);
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
        var oldXRefs = _vlocal.EpisodeCrossRefs
            .Select(xref => xref.EpisodeID)
            .ToHashSet();

        // Process and get the AniDB file entry.
        var aniFile = await ProcessFile_AniDB();

        // Check if an AniDB file is now available and if the cross-references changed.
        var newXRefs = _vlocal.EpisodeCrossRefs
            .Select(xref => xref.EpisodeID)
            .ToHashSet();
        var xRefsMatch = newXRefs.SetEquals(oldXRefs);
        if (aniFile != null && newXRefs.Count > 0 && !xRefsMatch)
        {
            // Set/update the import date
            _vlocal.DateTimeImported = DateTime.Now;
            RepoFactory.VideoLocal.Save(_vlocal);

            // Dispatch the on file matched event.
            ShokoEventHandler.Instance.OnFileMatched(_vlocal.GetBestVideoLocalPlace(), _vlocal);
        }
        // Fire the file not matched event if we didn't update the cross-references.
        else
        {
            var autoMatchAttempts = RepoFactory.AniDB_FileUpdate.GetByFileSizeAndHash(_vlocal.FileSize, _vlocal.Hash).Count;
            var hasXRefs = newXRefs.Count > 0 && xRefsMatch;
            var isUDPBanned = _udpConnectionHandler.IsBanned;
            ShokoEventHandler.Instance.OnFileNotMatched(_vlocal.GetBestVideoLocalPlace(), _vlocal, autoMatchAttempts, hasXRefs, isUDPBanned);
        }

        // Rename and/or move the physical file(s) if needed.
        var scheduler = await _schedulerFactory.GetScheduler();
        await scheduler.StartJob<RenameMoveFileJob>(job => job.VideoLocalID = _vlocal.VideoLocalID);
    }

    private async Task<SVR_AniDB_File> ProcessFile_AniDB()
    {
        _logger.LogTrace("Checking for AniDB_File record for: {VidLocalHash} --- {VidLocalFileName}", _vlocal.Hash, _fileName);
        // check if we already have this AniDB_File info in the database

        var animeIDs = new Dictionary<int, bool>();

        var aniFile = GetLocalAniDBFile(_vlocal);
        if (aniFile == null || aniFile.FileSize == 0)
            aniFile ??= await TryGetAniDBFileFromAniDB(animeIDs);
        if (aniFile == null) return null;

        await PopulateAnimeForFile(_vlocal, aniFile.EpisodeCrossRefs, animeIDs);

        // We do this inside, as the info will not be available as needed otherwise
        var videoLocals =
            aniFile.EpisodeIDs?.SelectMany(a => RepoFactory.VideoLocal.GetByAniDBEpisodeID(a))
                .Where(b => b != null)
                .ToList();
        if (videoLocals == null) return null;

        // Get status from existing eps/files if needed
        GetWatchedStateIfNeeded(_vlocal, videoLocals);

        // update stats for groups and series. The series are not saved until here, so it's absolutely necessary!!
        animeIDs.Keys.ForEach(SVR_AniDB_Anime.UpdateStatsByAnimeID);

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
            {
                await _vlPlaceService.RemoveRecordAndDeletePhysicalFile(place);
            }
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
                await groupCommand.Process();
            }
        }

        // Add this file to the users list
        if (_settings.AniDb.MyList_AddFiles && !SkipMyList && _vlocal.MyListID <= 0)
        {
            await (await _schedulerFactory.GetScheduler()).StartJob<AddFileToMyListJob>(c =>
            {
                c.Hash = _vlocal.ED2KHash;
                c.ReadStates = true;
            });
        }

        return aniFile;
    }

    private void GetWatchedStateIfNeeded(SVR_VideoLocal vidLocal, List<SVR_VideoLocal> videoLocals)
    {
        if (!_settings.Import.UseExistingFileWatchedStatus) return;

        // Copy over watched states
        foreach (var user in RepoFactory.JMMUser.GetAll())
        {
            var watchedVideo = videoLocals.FirstOrDefault(a =>
                a?.GetUserRecord(user.JMMUserID)?.WatchedDate != null);
            // No files that are watched
            if (watchedVideo == null)
            {
                continue;
            }

            var watchedRecord = watchedVideo.GetUserRecord(user.JMMUserID);
            var userRecord = vidLocal.GetOrCreateUserRecord(user.JMMUserID);

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
            aniFile = RepoFactory.AniDB_File.GetByHashAndFileSize(vidLocal.Hash, _vlocal.FileSize);

            if (aniFile == null)
            {
                _logger.LogTrace("AniDB_File record not found");
            }
        }

        // If cross refs were wiped, but the AniDB_File was not, we unfortunately need to requery the info
        var crossRefs = RepoFactory.CrossRef_File_Episode.GetByHash(vidLocal.Hash);
        if (crossRefs == null || crossRefs.Count == 0)
        {
            aniFile = null;
        }

        return aniFile;
    }

    private async Task PopulateAnimeForFile(SVR_VideoLocal vidLocal, List<CrossRef_File_Episode> xrefs, Dictionary<int, bool> animeIDs)
    {
        // check if we have the episode info
        // if we don't, we will need to re-download the anime info (which also has episode info)
        if (xrefs.Count == 0)
        {
            // if we have the AniDB file, but no cross refs it means something has been broken
            _logger.LogDebug("Could not find any cross ref records for: {Ed2KHash}", vidLocal.ED2KHash);
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
            var scheduler = await _schedulerFactory.GetScheduler();
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
                });
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
                });
            }
        }
    }

    private async Task<SVR_AniDB_File> TryGetAniDBFileFromAniDB(Dictionary<int, bool> animeIDs)
    {
        // check if we already have a record
        var aniFile = RepoFactory.AniDB_File.GetByHashAndFileSize(_vlocal.Hash, _vlocal.FileSize);

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
                }).Process();
            }
            catch (AniDBBannedException)
            {
                // We're banned, so queue it for later
                _logger.LogError("We are banned. Re-queuing for later: {FileName}", _fileName);

                await (await _schedulerFactory.GetScheduler()).StartJob<ProcessFileJob>(
                    c =>
                    {
                        c.VideoLocalID = _vlocal.VideoLocalID;
                        c.ForceAniDB = true;
                    });
            }
        }

        if (aniFile == null)
        {
            return null;
        }

        // get Anime IDs from the file for processing, the episodes might not be created yet here
        aniFile.EpisodeCrossRefs.Select(a => a.AnimeID).Distinct().ForEach(animeID =>
        {
            animeIDs[animeID] = false;
        });

        return aniFile;
    }

    public ProcessFileJob(ISettingsProvider settingsProvider, ISchedulerFactory schedulerFactory, VideoLocal_PlaceService vlPlaceService,
        IUDPConnectionHandler udpConnectionHandler, JobFactory jobFactory)
    {
        _schedulerFactory = schedulerFactory;
        _settings = settingsProvider.GetSettings();
        _vlPlaceService = vlPlaceService;
        _udpConnectionHandler = udpConnectionHandler;
        _jobFactory = jobFactory;
    }

    protected ProcessFileJob() { }
}
