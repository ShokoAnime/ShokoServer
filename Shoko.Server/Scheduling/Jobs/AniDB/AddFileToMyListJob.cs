using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Quartz;
using Shoko.Server.Models;
using Shoko.Server.Providers.AniDB;
using Shoko.Server.Providers.AniDB.Interfaces;
using Shoko.Server.Providers.AniDB.UDP.Generic;
using Shoko.Server.Providers.AniDB.UDP.User;
using Shoko.Server.Repositories;
using Shoko.Server.Repositories.Cached;
using Shoko.Server.Scheduling.Acquisition.Attributes;
using Shoko.Server.Scheduling.Attributes;
using Shoko.Server.Scheduling.Concurrency;
using Shoko.Server.Scheduling.Jobs.Actions;
using Shoko.Server.Scheduling.Jobs.Trakt;
using Shoko.Server.Server;
using Shoko.Server.Services;
using Shoko.Server.Settings;
using Shoko.Server.Utilities;

namespace Shoko.Server.Scheduling.Jobs.AniDB;

[DatabaseRequired]
[AniDBUdpRateLimited]
[DisallowConcurrencyGroup(ConcurrencyGroups.AniDB_UDP)]
[JobKeyGroup(JobKeyGroup.AniDB)]
public class AddFileToMyListJob : BaseJob
{
    private readonly IRequestFactory _requestFactory;
    private readonly ISchedulerFactory _schedulerFactory;
    private readonly ISettingsProvider _settingsProvider;
    private readonly VideoLocal_UserRepository _vlUsers;
    private readonly WatchedStatusService _watchedService;
    private SVR_VideoLocal _videoLocal;

    public string Hash { get; set; }
    public bool ReadStates { get; set; } = true;

    public override void PostInit()
    {
        _videoLocal = RepoFactory.VideoLocal.GetByEd2k(Hash);
        if (_videoLocal == null) throw new JobExecutionException($"VideoLocal not Found: {Hash}");
    }

    public override string TypeName => "Add File to MyList";

    public override string Title => "Adding File to MyList";
    public override Dictionary<string, object> Details => new()
    {
        {
            "File Path", Utils.GetDistinctPath(_videoLocal?.FirstValidPlace?.FullServerPath) ?? Hash
        }
    };

    public override async Task Process()
    {
        _logger.LogInformation("Processing {Job}: {FileName} - {Hash} - {ReadStates}",
            nameof(AddFileToMyListJob), _videoLocal?.FirstValidPlace?.FileName, Hash, ReadStates);

        if (_videoLocal == null) return;

        var settings = _settingsProvider.GetSettings();

        // when adding a file via the API, newWatchedStatus will return with current watched status on AniDB
        // if the file is already on the user's list

        var isManualLink = _videoLocal.AniDBFile == null;

        // mark the video file as watched
        var aniDBUsers = RepoFactory.JMMUser.GetAniDBUsers();
        var juser = aniDBUsers.FirstOrDefault();
        DateTime? originalWatchedDate = null;
        if (juser != null)
        {
            originalWatchedDate = _vlUsers.GetByUserIDAndVideoLocalID(juser.JMMUserID, _videoLocal.VideoLocalID)?.WatchedDate?.ToUniversalTime();
        }

        UDPResponse<ResponseMyListFile> response = null;
        // this only gets overwritten if the response is File Already in MyList
        var state = settings.AniDb.MyList_StorageState;

        if (isManualLink)
        {
            var episodes = _videoLocal.AnimeEpisodes.Select(a => a.AniDB_Episode).ToArray();
            foreach (var episode in episodes)
            {
                var request = _requestFactory.Create<RequestAddEpisode>(
                    r =>
                    {
                        r.State = state.GetMyList_State();
                        r.IsWatched = originalWatchedDate.HasValue;
                        r.WatchedDate = originalWatchedDate;
                        r.AnimeID = episode.AnimeID;
                        r.EpisodeNumber = episode.EpisodeNumber;
                        r.EpisodeType = (EpisodeType)episode.EpisodeType;
                    }
                );
                response = request.Send();

                if (response.Code != UDPReturnCode.FILE_ALREADY_IN_MYLIST)
                {
                    continue;
                }

                var updateRequest = _requestFactory.Create<RequestUpdateEpisode>(
                    r =>
                    {
                        r.State = state.GetMyList_State();
                        r.IsWatched = originalWatchedDate.HasValue;
                        r.WatchedDate = originalWatchedDate;
                        r.AnimeID = episode.AnimeID;
                        r.EpisodeNumber = episode.EpisodeNumber;
                        r.EpisodeType = (EpisodeType)episode.EpisodeType;
                    }
                );
                updateRequest.Send();
            }
        }
        else
        {
            var request = _requestFactory.Create<RequestAddFile>(
                r =>
                {
                    r.State = state.GetMyList_State();
                    r.IsWatched = originalWatchedDate.HasValue;
                    r.WatchedDate = originalWatchedDate;
                    r.Hash = _videoLocal.Hash;
                    r.Size = _videoLocal.FileSize;
                }
            );
            response = request.Send();

            if (response.Code == UDPReturnCode.FILE_ALREADY_IN_MYLIST)
            {
                var updateRequest = _requestFactory.Create<RequestUpdateFile>(
                    r =>
                    {
                        r.State = state.GetMyList_State();
                        r.Hash = _videoLocal.Hash;
                        r.Size = _videoLocal.FileSize;
                        if (!originalWatchedDate.HasValue) return;
                        r.IsWatched = originalWatchedDate.HasValue;
                        r.WatchedDate = originalWatchedDate;
                    }
                );
                updateRequest.Send();
            }
        }

        // never true for Manual Links, so no worries about the loop overwriting it
        if ((response?.Response?.MyListID ?? 0) != 0)
        {
            _videoLocal.MyListID = response.Response.MyListID;
            RepoFactory.VideoLocal.Save(_videoLocal);
        }

        var newWatchedDate = response?.Response?.WatchedDate;
        _logger.LogInformation(
            "Added File to MyList. File: {FileName}  Manual Link: {IsManualLink}  Watched Locally: {Unknown}  Watched AniDB: {ResponseIsWatched}  Local State: {AniDbMyListStorageState}  AniDB State: {State}  ReadStates: {ReadStates}  ReadWatched Setting: {AniDbMyListReadWatched}  ReadUnwatched Setting: {AniDbMyListReadUnwatched}",
            _videoLocal.FirstValidPlace?.FileName, isManualLink, originalWatchedDate != null,
            response?.Response?.IsWatched, settings.AniDb.MyList_StorageState, state, ReadStates,
            settings.AniDb.MyList_ReadWatched, settings.AniDb.MyList_ReadUnwatched
        );
        if (juser != null)
        {
            var watched = newWatchedDate != null && !DateTime.UnixEpoch.Equals(newWatchedDate);
            var watchedLocally = originalWatchedDate != null;

            if (ReadStates)
            {
                // handle import watched settings. Don't update AniDB in either case, we'll do that with the storage state
                if (settings.AniDb.MyList_ReadWatched && watched && !watchedLocally)
                {
                    await _watchedService.SetWatchedStatus(_videoLocal, true, false, newWatchedDate?.ToLocalTime(), false, juser.JMMUserID,
                        false, false);
                }
                else if (settings.AniDb.MyList_ReadUnwatched && !watched && watchedLocally)
                {
                    await _watchedService.SetWatchedStatus(_videoLocal, false, false, null, false, juser.JMMUserID,
                        false, false);
                }
            }
        }

        // if we don't have xrefs, then no series or eps.
        var series = _videoLocal.EpisodeCrossReferences.Select(a => a.AnimeID).Distinct().Except([0]).ToArray();
        if (series.Length <= 0)
        {
            return;
        }

        var scheduler = await _schedulerFactory.GetScheduler();
        await Task.WhenAll(series.Select(id => scheduler.StartJob<RefreshAnimeStatsJob>(a => a.AnimeID = id)));

        // let's also try adding to the users trakt collection
        if (settings.TraktTv.Enabled && !string.IsNullOrEmpty(settings.TraktTv.AuthToken))
        {
            foreach (var aep in _videoLocal.AnimeEpisodes)
            {
                await scheduler.StartJob<SyncTraktCollectionEpisodeJob>(
                    c =>
                    {
                        c.AnimeEpisodeID = aep.AnimeEpisodeID;
                        c.Action = TraktSyncAction.Add;
                    }
                );
            }
        }
    }
    
    public AddFileToMyListJob(IRequestFactory requestFactory, ISettingsProvider settingsProvider, ISchedulerFactory schedulerFactory, VideoLocal_UserRepository vlUsers, WatchedStatusService watchedService)
    {
        _requestFactory = requestFactory;
        _settingsProvider = settingsProvider;
        _schedulerFactory = schedulerFactory;
        _vlUsers = vlUsers;
        _watchedService = watchedService;
    }

    protected AddFileToMyListJob() { }
}
