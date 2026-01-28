using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Quartz;
using Shoko.Models.Enums;
using Shoko.Models.Server;
using Shoko.Plugin.Abstractions.DataModels;
using Shoko.Plugin.Abstractions.DataModels.Shoko;
using Shoko.Plugin.Abstractions.Enums;
using Shoko.Plugin.Abstractions.Events;
using Shoko.Plugin.Abstractions.Services;
using Shoko.Server.Extensions;
using Shoko.Server.Models;
using Shoko.Server.Providers.TraktTV;
using Shoko.Server.Repositories;
using Shoko.Server.Repositories.Cached;
using Shoko.Server.Scheduling;
using Shoko.Server.Scheduling.Jobs.AniDB;
using Shoko.Server.Scheduling.Jobs.Trakt;
using Shoko.Server.Server;
using Shoko.Server.Settings;

#nullable enable
namespace Shoko.Server.Services;

public class AbstractUserDataService(
    ILogger<AbstractUserDataService> logger,
    ISettingsProvider settingsProvider,
    ISchedulerFactory schedulerFactory,
    AnimeGroupService groupService,
    AnimeSeriesService seriesService,
    VideoLocalService videoService,
    VideoLocal_UserRepository userDataRepository,
    AnimeEpisode_UserRepository userEpisodeDataRepository,
    JMMUserRepository userRepository
) : IUserDataService
{
    public event EventHandler<VideoUserDataSavedEventArgs>? VideoUserDataSaved;

    public event EventHandler<SeriesVotedEventArgs>? SeriesVoted;

    #region Video User Data

    public IVideoUserData? GetVideoUserData(int userID, int videoID)
        => userDataRepository.GetByUserIDAndVideoLocalID(userID, videoID);

    public IReadOnlyList<IVideoUserData> GetVideoUserDataForUser(int userID)
        => userDataRepository.GetByUserID(userID);

    public IReadOnlyList<IVideoUserData> GetVideoUserDataForVideo(int videoID)
        => userDataRepository.GetByVideoLocalID(videoID);

    public Task SetVideoWatchedStatus(IShokoUser user, IVideo video, bool watched = true, DateTime? watchedAt = null, UserDataSaveReason reason = UserDataSaveReason.None, bool updateStatsNow = true)
        => SaveVideoUserData(user, video, new() { ResumePosition = TimeSpan.Zero, LastPlayedAt = watched ? watchedAt ?? DateTime.Now : null, LastUpdatedAt = DateTime.Now }, reason: reason, updateStatsNow: updateStatsNow);

    public async Task<IVideoUserData> SaveVideoUserData(IShokoUser user, IVideo video, VideoUserDataUpdate userDataUpdate, UserDataSaveReason reason = UserDataSaveReason.None, bool updateStatsNow = true)
    {
        ArgumentNullException.ThrowIfNull(user, nameof(user));
        ArgumentNullException.ThrowIfNull(video, nameof(video));
        ArgumentNullException.ThrowIfNull(userDataUpdate, nameof(userDataUpdate));

        var watchedStatusChanged = false;
        if (GetVideoUserData(user.ID, video.ID) is { } existingUserData)
        {
            if (userDataUpdate.LastPlayedAt.HasValue)
                watchedStatusChanged = !existingUserData.LastPlayedAt.HasValue || !existingUserData.LastPlayedAt.Value.Equals(userDataUpdate.LastPlayedAt.Value);
            else
                watchedStatusChanged = existingUserData.LastPlayedAt.HasValue;
        }
        else
        {
            watchedStatusChanged = userDataUpdate.LastPlayedAt.HasValue;
        }

        var settings = settingsProvider.GetSettings();
        var scheduler = await schedulerFactory.GetScheduler();
        var syncTrakt = ((SVR_JMMUser)user).IsTraktUser == 1
                        && settings.TraktTv.Enabled
                        && !string.IsNullOrEmpty(settings.TraktTv.AuthToken)
                        && reason is UserDataSaveReason.None or UserDataSaveReason.UserInteraction or UserDataSaveReason.PlaybackEnd or UserDataSaveReason.AnidbImport;
        var syncAnidb = reason is not UserDataSaveReason.AnidbImport && ((SVR_JMMUser)user).IsAniDBUser == 1 && ((userDataUpdate.LastPlayedAt.HasValue && settings.AniDb.MyList_SetWatched) || (!userDataUpdate.LastPlayedAt.HasValue && settings.AniDb.MyList_SetUnwatched));
        IReadOnlyList<IShokoUser> users = ((SVR_JMMUser)user).IsAniDBUser == 1 ? userRepository.GetAniDBUsers() : [user];
        var lastUpdatedAt = userDataUpdate.LastUpdatedAt ?? DateTime.Now;

        logger.LogDebug("Got update for {VideoID} with reason {Reason}. (WatchedStatusChanged={WatchedStatusChanged},Users={Users})", video.ID, reason, watchedStatusChanged, users.Select(x => x.ID).ToArray());

        foreach (var u in users)
            SaveWatchedStatus(video, u.ID, userDataUpdate.LastPlayedAt, lastUpdatedAt, userDataUpdate.ResumePosition, userDataUpdate.PlaybackCount);

        // now find all the episode records associated with this video file,
        // but we also need to check if there are any other files attached to this episode with a watched status
        var xrefs = video.CrossReferences;
        var toUpdateSeries = new Dictionary<int, SVR_AnimeSeries>();
        if (userDataUpdate.LastPlayedAt.HasValue)
        {
            foreach (var episodeXref in xrefs)
            {
                // get the episodes for this file, may be more than one (One Piece x Toriko)
                var episode = episodeXref.ShokoEpisode;
                if (episode == null)
                    continue;

                // find the total watched percentage
                // e.g. one file can have a % = 100
                // or if 2 files make up one episode they will each have a % = 50
                var epPercentWatched = 0;
                foreach (var videoXref in episode.CrossReferences)
                {
                    var otherVideo = videoXref.Video;
                    if (otherVideo == null)
                        continue;

                    var videoUser = userDataRepository.GetByUserIDAndVideoLocalID(user.ID, otherVideo.ID);
                    if (videoUser?.WatchedDate != null)
                        epPercentWatched += videoXref.Percentage <= 0 ? 100 : videoXref.Percentage;

                    if (epPercentWatched > 95)
                        break;
                }

                if (epPercentWatched <= 95)
                    continue;

                if (updateStatsNow && episode.Series is SVR_AnimeSeries series)
                    toUpdateSeries.TryAdd(series.AnimeSeriesID, series);

                foreach (var u in users)
                    SaveWatchedStatus(episode, u.ID, true, userDataUpdate.LastPlayedAt);

                if (syncTrakt)
                    await scheduler.StartJob<SendEpisodeWatchStateToTraktJob>(c =>
                    {
                        c.AnimeEpisodeID = episode.ID;
                        c.Action = TraktSyncType.HistoryAdd;
                    });
            }
        }
        else
        {
            // if setting a file to unwatched only set the episode unwatched, if ALL the files are unwatched
            foreach (var episodeXref in xrefs)
            {
                var episode = episodeXref.ShokoEpisode;
                if (episode == null)
                    continue;

                var epPercentWatched = 0;
                foreach (var videoXref in episode.CrossReferences)
                {
                    var otherVideo = videoXref.Video;
                    if (otherVideo == null) continue;
                    var videoUser = userDataRepository.GetByUserIDAndVideoLocalID(user.ID, otherVideo.ID);
                    if (videoUser?.WatchedDate != null)
                        epPercentWatched += videoXref.Percentage <= 0 ? 100 : videoXref.Percentage;

                    if (epPercentWatched > 95) break;
                }

                if (epPercentWatched < 95)
                {
                    foreach (var u in users)
                        SaveWatchedStatus(episode, u.ID, false, null);

                    if (updateStatsNow && episode.Series is SVR_AnimeSeries series)
                        toUpdateSeries.TryAdd(series.AnimeSeriesID, series);

                    if (syncTrakt)
                        await scheduler.StartJob<SendEpisodeWatchStateToTraktJob>(c =>
                        {
                            c.AnimeEpisodeID = episode.ID;
                            c.Action = TraktSyncType.HistoryRemove;
                        });
                }
            }
        }

        if (syncAnidb && watchedStatusChanged)
            await scheduler.StartJob<UpdateMyListFileStatusJob>(c =>
            {
                c.Hash = video.Hashes.ED2K;
                c.Watched = userDataUpdate.LastPlayedAt.HasValue;
                c.UpdateSeriesStats = false;
                c.WatchedDate = userDataUpdate.LastPlayedAt?.ToUniversalTime();
            });

        if (updateStatsNow && toUpdateSeries.Count > 0)
        {
            foreach (var series in toUpdateSeries.Values)
                seriesService.UpdateStats(series, true, true);

            var groups = toUpdateSeries.Values
                .Select(a => a.TopLevelAnimeGroup)
                .WhereNotNull()
                .DistinctBy(a => a.AnimeGroupID);
            foreach (var group in groups)
                groupService.UpdateStatsFromTopLevel(group, true, true);
        }

        // Invoke the event(s). Assume the user data is already created, but throw if it isn't.
        foreach (var u in users)
        {
            var uD = userDataRepository.GetByUserIDAndVideoLocalID(u.ID, video.ID) ??
                throw new InvalidOperationException($"User data is null. (Video={video.ID},User={u.ID})");
            VideoUserDataSaved?.Invoke(this, new(reason, u, video, uD));
        }

        var userData = userDataRepository.GetByUserIDAndVideoLocalID(user.ID, video.ID) ??
            throw new InvalidOperationException($"User data is null. (Video={video.ID},User={user.ID})");
        return userData;
    }

    #endregion

    #region Episode User Data

    public async Task<bool> SetEpisodeWatchedStatus(IShokoUser user, IShokoEpisode episode, bool watched = true, DateTime? watchedAt = null, UserDataSaveReason reason = UserDataSaveReason.None, bool updateStatsNow = true)
    {
        ArgumentNullException.ThrowIfNull(user, nameof(user));
        ArgumentNullException.ThrowIfNull(episode, nameof(episode));
        if (episode.VideoList is not { Count: > 0 } videoList)
            return false;

        var now = DateTime.Now;
        var userDataUpdate = new VideoUserDataUpdate()
        {
            LastPlayedAt = watched ? watchedAt?.ToLocalTime() ?? now : null,
            ResumePosition = TimeSpan.Zero,
            LastUpdatedAt = now,
        };
        foreach (var video in videoList)
            await SaveVideoUserData(user, video, userDataUpdate, reason, false);

        if (updateStatsNow && episode.Series is SVR_AnimeSeries series)
        {
            seriesService.UpdateStats(series, true, true);
            if (series.TopLevelAnimeGroup is { } topLevelGroup)
                groupService.UpdateStatsFromTopLevel(topLevelGroup, true, true);
        }

        return true;
    }

    #endregion

    #region Series User Data

    public async Task VoteOnSeries(IShokoSeries series, decimal voteValue, VoteType voteType, IShokoUser? user = null)
    {
        ArgumentNullException.ThrowIfNull(series);

        if (user == null)
            user = RepoFactory.JMMUser.GetAll().FirstOrDefault(u => u.IsAdmin == 1);

        ArgumentNullException.ThrowIfNull(user);

        if (series is not SVR_AnimeSeries svrSeries)
            throw new ArgumentException("Series must be a SVR_AnimeSeries", nameof(series));

        if (svrSeries.AniDB_Anime is not { } anidbAnime)
            throw new ArgumentException("AniDB anime is not available for the series! Aborting!");

        if (voteValue != -1 && (voteValue < 0 || voteValue > 10))
            throw new ArgumentOutOfRangeException(nameof(voteValue), "Vote value must be between -1 and 10");

        var anidbVoteType = voteType == VoteType.Permanent ? AniDBVoteType.Anime : AniDBVoteType.AnimeTemp;

        // Handle deletion case
        if (voteValue == -1)
        {
            var existingVote = RepoFactory.AniDB_Vote.GetByEntityAndType(svrSeries.AniDB_ID, AniDBVoteType.AnimeTemp) ??
                              RepoFactory.AniDB_Vote.GetByEntityAndType(svrSeries.AniDB_ID, AniDBVoteType.Anime);

            if (existingVote != null)
            {
                // Schedule the delete job
                var deleteScheduler = await schedulerFactory.GetScheduler();
                await deleteScheduler.StartJob<VoteAniDBAnimeJob>(c =>
                {
                    c.AnimeID = svrSeries.AniDB_ID;
                    c.VoteType = (AniDBVoteType)existingVote.VoteType;
                    c.VoteValue = -1;
                });

                // Delete from database
                RepoFactory.AniDB_Vote.Delete(existingVote.AniDB_VoteID);

                // Trigger event with 0 value for deletion
                OnSeriesVoted(series, anidbAnime, 0, voteType, user);
            }

            // If no existing vote, do nothing
            return;
        }

        // Save or update the vote
        var dbVote = (RepoFactory.AniDB_Vote.GetByEntityAndType(svrSeries.AniDB_ID, AniDBVoteType.AnimeTemp) ??
                     RepoFactory.AniDB_Vote.GetByEntityAndType(svrSeries.AniDB_ID, AniDBVoteType.Anime)) ??
                     new AniDB_Vote { EntityID = svrSeries.AniDB_ID };
        dbVote.VoteValue = (int)Math.Floor(voteValue * 100);
        dbVote.VoteType = (int)anidbVoteType;

        RepoFactory.AniDB_Vote.Save(dbVote);

        // Trigger the event
        OnSeriesVoted(series, anidbAnime, voteValue, voteType, user);

        // Schedule the AniDB vote job
        var scheduler = await schedulerFactory.GetScheduler();
        await scheduler.StartJob<VoteAniDBAnimeJob>(c =>
        {
            c.AnimeID = svrSeries.AniDB_ID;
            c.VoteType = anidbVoteType;
            c.VoteValue = Convert.ToDouble(voteValue);
        });
    }

    #endregion

    #region Internals

    private void SaveWatchedStatus(IShokoEpisode ep, int userID, bool watched, DateTime? watchedDate)
    {
        var epUserRecord = userEpisodeDataRepository.GetByUserIDAndEpisodeID(userID, ep.ID);
        if (watched)
        {
            // let's check if an update is actually required
            if ((epUserRecord?.WatchedDate != null && watchedDate.HasValue &&
                epUserRecord.WatchedDate.Equals(watchedDate.Value)) ||
                (epUserRecord?.WatchedDate == null && !watchedDate.HasValue))
                return;

            epUserRecord ??= new(userID, ep.ID, ep.SeriesID);
            epUserRecord.WatchedCount++;
            epUserRecord.WatchedDate = watchedDate ?? epUserRecord.WatchedDate ?? DateTime.Now;

            userEpisodeDataRepository.Save(epUserRecord);
            return;
        }

        if (epUserRecord != null)
        {
            epUserRecord.WatchedDate = null;
            userEpisodeDataRepository.Save(epUserRecord);
        }
    }

    private void SaveWatchedStatus(IVideo video, int userID, DateTime? watchedDate, DateTime lastUpdated, TimeSpan? resumePosition = null, int? watchedCount = null)
    {
        var userData = videoService.GetOrCreateUserRecord((SVR_VideoLocal)video, userID);
        userData.WatchedDate = watchedDate;
        if (watchedCount.HasValue)
        {
            if (watchedCount.Value < 0)
                watchedCount = 0;
            userData.WatchedCount = watchedCount.Value;
        }
        else if (watchedDate.HasValue)
            userData.WatchedCount++;

        if (resumePosition.HasValue)
        {
            if (resumePosition.Value < TimeSpan.Zero)
                resumePosition = TimeSpan.Zero;
            else if (video.MediaInfo is { } mediaInfo && resumePosition.Value > mediaInfo.Duration)
                resumePosition = mediaInfo.Duration;
            userData.ResumePositionTimeSpan = resumePosition.Value;
        }

        userData.LastUpdated = lastUpdated;
        userDataRepository.Save(userData);
    }

    private void OnSeriesVoted(IShokoSeries series, ISeries anime, decimal voteValue, VoteType voteType, IShokoUser user)
    {
        try
        {
            SeriesVoted?.Invoke(this, new(series, anime, voteValue, voteType, user));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error occurred while handling series vote event");
        }
    }

    #endregion
}
