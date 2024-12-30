using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Quartz;
using Shoko.Server.Models;
using Shoko.Server.Repositories.Cached;
using Shoko.Server.Scheduling;
using Shoko.Server.Scheduling.Jobs.AniDB;
using Shoko.Server.Scheduling.Jobs.Trakt;
using Shoko.Server.Server;
using Shoko.Server.Settings;

namespace Shoko.Server.Services;

public class WatchedStatusService
{
    private readonly AnimeEpisodeRepository _episodes;
    private readonly AnimeEpisode_UserRepository _epUsers;
    private readonly AnimeGroupService _groupService;
    private readonly AnimeSeriesService _seriesService;
    private readonly CrossRef_File_EpisodeRepository _fileEpisodes;
    private readonly ISchedulerFactory _schedulerFactory;
    private readonly ISettingsProvider _settingsProvider;
    private readonly JMMUserRepository _users;
    private readonly VideoLocalService _vlService;
    private readonly VideoLocal_UserRepository _vlUsers;

    public WatchedStatusService(AnimeEpisodeRepository episodes, AnimeEpisode_UserRepository epUsers, AnimeGroupService groupService,
        AnimeSeriesService seriesService, CrossRef_File_EpisodeRepository fileEpisodes, ISchedulerFactory schedulerFactory, ISettingsProvider settingsProvider,
        JMMUserRepository users, VideoLocalService vlService, VideoLocal_UserRepository vlUsers)
    {
        _episodes = episodes;
        _epUsers = epUsers;
        _groupService = groupService;
        _seriesService = seriesService;
        _fileEpisodes = fileEpisodes;
        _schedulerFactory = schedulerFactory;
        _settingsProvider = settingsProvider;
        _users = users;
        _vlService = vlService;
        _vlUsers = vlUsers;
    }


    public async Task SetWatchedStatus(SVR_AnimeEpisode ep, bool watched, bool updateOnline, DateTime? watchedDate, bool updateStats,
        int userID, bool syncTrakt)
    {
        foreach (var vid in ep.VideoLocals)
        {
            await SetWatchedStatus(vid, watched, updateOnline, watchedDate, updateStats, userID,
                syncTrakt, true);
            SetResumePosition(vid, 0, userID);
        }
    }

    public void SaveWatchedStatus(SVR_AnimeEpisode ep, bool watched, int userID, DateTime? watchedDate, bool updateWatchedDate)
    {

        var epUserRecord = _epUsers.GetByUserIDAndEpisodeID(userID, ep.AnimeEpisodeID);

        if (watched)
        {
            // let's check if an update is actually required
            if (epUserRecord?.WatchedDate != null && watchedDate.HasValue &&
                epUserRecord.WatchedDate.Equals(watchedDate.Value) ||
                (epUserRecord?.WatchedDate == null && !watchedDate.HasValue))
                return;

            epUserRecord ??= new SVR_AnimeEpisode_User(userID, ep.AnimeEpisodeID, ep.AnimeSeriesID);
            epUserRecord.WatchedCount++;

            if (epUserRecord.WatchedDate.HasValue && updateWatchedDate || !epUserRecord.WatchedDate.HasValue)
                epUserRecord.WatchedDate = watchedDate ?? DateTime.Now;

            _epUsers.Save(epUserRecord);
        }
        else if (epUserRecord != null && updateWatchedDate)
        {
            epUserRecord.WatchedDate = null;
            _epUsers.Save(epUserRecord);
        }
    }

    public void SetResumePosition(SVR_VideoLocal vl, long resumeposition, int userID)
    {
        var userRecord = _vlService.GetOrCreateUserRecord(vl, userID);
        userRecord.ResumePosition = resumeposition;
        userRecord.LastUpdated = DateTime.Now;
        _vlUsers.Save(userRecord);
    }

    public async Task SetWatchedStatus(SVR_VideoLocal vl, bool watched, int userID)
    {
        await SetWatchedStatus(vl, watched, true, watched ? DateTime.Now : null, true, userID, true, true);
    }

    public async Task SetWatchedStatus(SVR_VideoLocal vl, bool watched, bool updateOnline, DateTime? watchedDate, bool updateStats, int userID,
        bool syncTrakt, bool updateWatchedDate, DateTime? lastUpdated = null)
    {
        var settings = _settingsProvider.GetSettings();
        var scheduler = await _schedulerFactory.GetScheduler();
        var user = _users.GetByID(userID);
        if (user == null) return;

        var aniDBUsers = _users.GetAniDBUsers();

        if (user.IsAniDBUser == 0)
            SaveWatchedStatus(vl, watched, userID, watchedDate, updateWatchedDate, lastUpdated);
        else
            foreach (var juser in aniDBUsers.Where(juser => juser.IsAniDBUser == 1))
                SaveWatchedStatus(vl, watched, juser.JMMUserID, watchedDate, updateWatchedDate, lastUpdated);

        // now lets find all the associated AniDB_File record if there is one
        if (user.IsAniDBUser == 1)
        {
            if (updateOnline)
                if ((watched && settings.AniDb.MyList_SetWatched) ||
                    (!watched && settings.AniDb.MyList_SetUnwatched))
                {
                    await scheduler.StartJob<UpdateMyListFileStatusJob>(
                        c =>
                        {
                            c.Hash = vl.Hash;
                            c.Watched = watched;
                            c.UpdateSeriesStats = false;
                            c.Watched = watched;
                            c.WatchedDate = watchedDate?.ToUniversalTime();
                        }
                    );
                }
        }

        // now find all the episode records associated with this video file,
        // but we also need to check if there are any other files attached to this episode with a watched status

        SVR_AnimeSeries ser;
        // get all files associated with this episode
        var xrefs = vl.EpisodeCrossReferences;
        var toUpdateSeries = new Dictionary<int, SVR_AnimeSeries>();
        if (watched)
        {
            // find the total watched percentage
            // e.g. one file can have a % = 100
            // or if 2 files make up one episode they will each have a % = 50

            foreach (var xref in xrefs)
            {
                // get the episodes for this file, may be more than one (One Piece x Toriko)
                var ep = _episodes.GetByAniDBEpisodeID(xref.EpisodeID);
                // a show we don't have
                if (ep == null) continue;

                // get all the files for this episode
                var epPercentWatched = 0;
                foreach (var filexref in ep.FileCrossReferences)
                {
                    var xrefVideoLocal = filexref.VideoLocal;
                    if (xrefVideoLocal == null) continue;
                    var vidUser = _vlUsers.GetByUserIDAndVideoLocalID(userID, xrefVideoLocal.VideoLocalID);
                    if (vidUser?.WatchedDate != null)
                        epPercentWatched += filexref.Percentage <= 0 ? 100 : filexref.Percentage;

                    if (epPercentWatched > 95) break;
                }

                if (epPercentWatched <= 95) continue;

                ser = ep.AnimeSeries;
                // a problem
                if (ser == null) continue;
                toUpdateSeries.TryAdd(ser.AnimeSeriesID, ser);
                if (user.IsAniDBUser == 0)
                    SaveWatchedStatus(ep, true, userID, watchedDate, updateWatchedDate);
                else
                    foreach (var juser in aniDBUsers.Where(a => a.IsAniDBUser == 1))
                        SaveWatchedStatus(ep, true, juser.JMMUserID, watchedDate, updateWatchedDate);

                if (syncTrakt && settings.TraktTv.Enabled &&
                    !string.IsNullOrEmpty(settings.TraktTv.AuthToken))
                {
                    await scheduler.StartJob<SyncTraktEpisodeHistoryJob>(
                        c =>
                        {
                            c.AnimeEpisodeID = ep.AnimeEpisodeID;
                            c.Action = TraktSyncAction.Add;
                        }
                    );
                }
            }
        }
        else
        {
            // if setting a file to unwatched only set the episode unwatched, if ALL the files are unwatched
            foreach (var xrefEp in xrefs)
            {
                // get the episodes for this file, may be more than one (One Piece x Toriko)
                var ep = _episodes.GetByAniDBEpisodeID(xrefEp.EpisodeID);
                // a show we don't have
                if (ep == null) continue;

                // get all the files for this episode
                var epPercentWatched = 0;
                foreach (var filexref in ep.FileCrossReferences)
                {
                    var xrefVideoLocal = filexref.VideoLocal;
                    if (xrefVideoLocal == null) continue;
                    var vidUser = _vlUsers.GetByUserIDAndVideoLocalID(userID, xrefVideoLocal.VideoLocalID);
                    if (vidUser?.WatchedDate != null)
                        epPercentWatched += filexref.Percentage <= 0 ? 100 : filexref.Percentage;

                    if (epPercentWatched > 95) break;
                }

                if (epPercentWatched < 95)
                {
                    if (user.IsAniDBUser == 0)
                        SaveWatchedStatus(ep, false, userID, watchedDate, true);
                    else
                        foreach (var juser in aniDBUsers.Where(juser => juser.IsAniDBUser == 1))
                            SaveWatchedStatus(ep, false, juser.JMMUserID, watchedDate, true);

                    ser = ep.AnimeSeries;
                    // a problem
                    if (ser == null) continue;
                    toUpdateSeries.TryAdd(ser.AnimeSeriesID, ser);

                    if (syncTrakt && settings.TraktTv.Enabled &&
                        !string.IsNullOrEmpty(settings.TraktTv.AuthToken))
                    {
                        await scheduler.StartJob<SyncTraktEpisodeHistoryJob>(
                            c =>
                            {
                                c.AnimeEpisodeID = ep.AnimeEpisodeID;
                                c.Action = TraktSyncAction.Remove;
                            }
                        );
                    }
                }
            }
        }


        // update stats for groups and series
        if (toUpdateSeries.Count > 0 && updateStats)
        {
            foreach (var s in toUpdateSeries.Values)
            {
                // update all the groups above this series in the hierarchy
                _seriesService.UpdateStats(s, true, true);
            }

            var groups = toUpdateSeries.Values.Select(a => a.AnimeGroup?.TopLevelAnimeGroup).Where(a => a != null)
                .DistinctBy(a => a.AnimeGroupID);

            foreach (var group in groups)
            {
                _groupService.UpdateStatsFromTopLevel(group, true, true);
            }
        }
    }

    private void SaveWatchedStatus(SVR_VideoLocal vl, bool watched, int userID, DateTime? watchedDate, bool updateWatchedDate, DateTime? lastUpdated = null)
    {
        SVR_VideoLocal_User vidUserRecord;
        // mark as watched
        if (watched)
        {
            vidUserRecord = _vlService.GetOrCreateUserRecord(vl, userID);
            vidUserRecord.WatchedDate = DateTime.Now;
            vidUserRecord.WatchedCount++;

            if (watchedDate.HasValue && updateWatchedDate)
                vidUserRecord.WatchedDate = watchedDate.Value;

            vidUserRecord.LastUpdated = lastUpdated ?? DateTime.Now;
            _vlUsers.Save(vidUserRecord);
            return;
        }

        // unmark
        vidUserRecord = _vlUsers.GetByUserIDAndVideoLocalID(userID, vl.VideoLocalID);
        if (vidUserRecord == null) return;

        vidUserRecord.WatchedDate = null;
        _vlUsers.Save(vidUserRecord);
    }
}
