using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Quartz;
using Shoko.Abstractions.Enums;
using Shoko.Abstractions.Events;
using Shoko.Abstractions.Extensions;
using Shoko.Abstractions.Metadata.Shoko;
using Shoko.Abstractions.Services;
using Shoko.Abstractions.User;
using Shoko.Abstractions.UserData;
using Shoko.Abstractions.UserData.Enums;
using Shoko.Abstractions.Video;
using Shoko.Server.Models.Shoko;
using Shoko.Server.Providers.TraktTV;
using Shoko.Server.Repositories.Cached;
using Shoko.Server.Scheduling;
using Shoko.Server.Scheduling.Jobs.AniDB;
using Shoko.Server.Scheduling.Jobs.Trakt;
using Shoko.Server.Settings;

#nullable enable
namespace Shoko.Server.Services;

public class UserDataService(
    ILogger<UserDataService> logger,
    ISettingsProvider settingsProvider,
    ISchedulerFactory schedulerFactory,
    VideoLocal_UserRepository videoUserDataRepository,
    AnimeEpisode_UserRepository episodeUserDataRepository,
    AnimeSeries_UserRepository seriesUserDataRepository,
    AnimeGroup_UserRepository groupUserDataRepository,
    JMMUserRepository userRepository
) : IUserDataService
{
    #region Video User Data

    public event EventHandler<VideoUserDataSavedEventArgs>? VideoUserDataSaved;

    public IVideoUserData? GetVideoUserData(IVideo video, IUser user)
    {
        ArgumentNullException.ThrowIfNull(video);
        if (video.ID is <= 0)
            throw new ArgumentException("video.ID must be greater than 0.", nameof(video));
        ArgumentNullException.ThrowIfNull(user);
        if (user.ID is <= 0)
            throw new ArgumentException("user.ID must be greater than 0.", nameof(user));
        return videoUserDataRepository.GetByUserAndVideoLocalID(user.ID, video.ID);
    }

    public IEnumerable<IVideoUserData> GetVideoUserDataForUser(IUser user)
    {
        ArgumentNullException.ThrowIfNull(user);
        if (user.ID is <= 0)
            throw new ArgumentException("user.ID must be greater than 0.", nameof(user));
        return videoUserDataRepository.GetByUserID(user.ID);
    }

    public IReadOnlyList<IVideoUserData> GetVideoUserDataForVideo(IVideo video)
    {
        ArgumentNullException.ThrowIfNull(video);
        if (video.ID is <= 0)
            throw new ArgumentException("video.ID must be greater than 0.", nameof(video));
        return videoUserDataRepository.GetByVideoLocalID(video.ID);
    }

    public Task<IVideoUserData> SetVideoWatchedStatus(IVideo video, IUser user, bool watched = true, DateTime? watchedAt = null, VideoUserDataSaveReason reason = VideoUserDataSaveReason.None, bool updateStatsNow = true)
        => SaveVideoUserData(video, user, new() { ProgressPosition = TimeSpan.Zero, LastPlayedAt = watched ? watchedAt ?? DateTime.Now : null, LastUpdatedAt = DateTime.Now }, reason: reason, updateStatsNow: updateStatsNow);

    public Task<IVideoUserData> SaveVideoUserData(IVideo video, IUser user, VideoUserDataUpdate userDataUpdate, VideoUserDataSaveReason reason = VideoUserDataSaveReason.None, bool updateStatsNow = true)
        => SaveVideoUserDataInternal(video, user, userDataUpdate, reason, null, updateStatsNow);

    public Task<IVideoUserData> ImportVideoUserData(IVideo video, IUser user, VideoUserDataUpdate userDataUpdate, string importSource, bool updateStatsNow = true)
        => SaveVideoUserDataInternal(video, user, userDataUpdate, VideoUserDataSaveReason.Import, importSource, updateStatsNow);

    private async Task<IVideoUserData> SaveVideoUserDataInternal(IVideo video, IUser user, VideoUserDataUpdate userDataUpdate, VideoUserDataSaveReason reason = VideoUserDataSaveReason.None, string? importSource = null, bool updateStatsNow = true)
    {
        ArgumentNullException.ThrowIfNull(user, nameof(user));
        ArgumentNullException.ThrowIfNull(video, nameof(video));
        ArgumentNullException.ThrowIfNull(userDataUpdate, nameof(userDataUpdate));
        if (reason is VideoUserDataSaveReason.Import && string.IsNullOrWhiteSpace(importSource))
            reason = VideoUserDataSaveReason.None;
        else if (!string.IsNullOrWhiteSpace(importSource))
            importSource = null;
        if (reason is not (
            VideoUserDataSaveReason.None or
            VideoUserDataSaveReason.UserInteraction or
            VideoUserDataSaveReason.PlaybackStart or
            VideoUserDataSaveReason.PlaybackEnd or
            VideoUserDataSaveReason.PlaybackPause or
            VideoUserDataSaveReason.PlaybackResume or
            VideoUserDataSaveReason.PlaybackProgress or
            VideoUserDataSaveReason.Import
        ))
            reason = VideoUserDataSaveReason.None;

        // Set the video as watched and reset the progress if the progress is over 97.5%
        var duration = video.MediaInfo is { } mediaInfo
            ? mediaInfo.Duration
            : (TimeSpan?)null;
        var percentage = userDataUpdate.ProgressPosition.HasValue && duration.HasValue
            ? userDataUpdate.ProgressPosition.Value.TotalMilliseconds / duration.Value.TotalMilliseconds
            : 0d;
        if (percentage > 0.975d)
        {
            userDataUpdate.LastPlayedAt ??= DateTime.Now;
            userDataUpdate.ProgressPosition = null;
        }

        var userData = videoUserDataRepository.GetByUserAndVideoLocalID(user.ID, video.ID)
            ?? new() { JMMUserID = user.ID, VideoLocalID = video.ID };
        var watchedStatusChanged = userDataUpdate.HasLastPlayedAt && (
            userDataUpdate.LastPlayedAt.HasValue
                ? !userData.WatchedDate.HasValue || !userData.WatchedDate.Value.Equals(userDataUpdate.LastPlayedAt.Value)
                : userData.WatchedDate.HasValue
        );
        var shouldSave = userData.VideoLocal_UserID is 0 ||
            watchedStatusChanged ||
            (userDataUpdate.LastUpdatedAt.HasValue && userDataUpdate.LastUpdatedAt.Value != userData.LastUpdated);
        if (watchedStatusChanged)
            userData.WatchedDate = userDataUpdate.LastPlayedAt;

        // Set the playback count if the update has it.
        if (userDataUpdate.PlaybackCount.HasValue)
        {
            // If set to a negative number, infer count from the last played date.
            if (userDataUpdate.PlaybackCount.Value < 0)
                userDataUpdate.PlaybackCount = userData.WatchedDate.HasValue ? 1 : 0;
            if (userDataUpdate.PlaybackCount.Value != userData.WatchedCount)
            {
                userData.WatchedCount = userDataUpdate.PlaybackCount.Value;
                shouldSave = true;
            }
        }
        // Increment the playback count if the video was watched
        else if (watchedStatusChanged && userDataUpdate.LastPlayedAt.HasValue)
        {
            userData.WatchedCount++;
        }

        // Set the progress if the update has it.
        if (userDataUpdate.HasProgressPosition)
        {
            if (userDataUpdate.ProgressPosition.HasValue)
            {
                // Ensure the progress is within a valid range for the video.
                if (userDataUpdate.ProgressPosition.Value < TimeSpan.Zero)
                    userDataUpdate.ProgressPosition = TimeSpan.Zero;
                else if (duration.HasValue && userDataUpdate.ProgressPosition.Value > duration.Value)
                    userDataUpdate.ProgressPosition = duration.Value;
            }

            if (userDataUpdate.ProgressPosition != userData.ProgressPosition)
            {
                userData.ProgressPosition = userDataUpdate.ProgressPosition;
                shouldSave = true;
            }
        }
        // Reset the progress if the video was watched
        else if (watchedStatusChanged && userDataUpdate.LastPlayedAt.HasValue)
        {
            userData.ProgressPosition = null;
        }

        if (shouldSave)
        {
            userData.LastUpdated = userDataUpdate.LastUpdatedAt ?? DateTime.Now;
            videoUserDataRepository.Save(userData);

            logger.LogDebug("Got update for {VideoID} with reason {Reason}. (WatchedStatusChanged={WatchedStatusChanged},User={User})", video.ID, reason, watchedStatusChanged, user.ID);

            try
            {
                VideoUserDataSaved?.Invoke(this, new(reason, user, video, userData, importSource));
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "An error occurred while trying to send the VideoUserDataSaved event; {Message}", ex.Message);
            }
        }

        var toUpdateSeries = new Dictionary<int, AnimeSeries>();
        if (watchedStatusChanged)
        {
            // now find all the episode records associated with this video file,
            // but we also need to check if there are any other files attached to this episode with a watched status
            var xrefs = video.CrossReferences;
            var episodeLastUpdated = DateTime.Now;
            if (userDataUpdate.LastPlayedAt.HasValue)
            {
                foreach (var episodeXref in xrefs)
                {
                    // get the episodes for this file, may be more than one (One Piece x Toriko)
                    if (episodeXref.ShokoEpisode is not { } episode)
                        continue;

                    // find the total watched percentage
                    // e.g. one file can have a % = 100
                    // or if 2 files make up one episode they will each have a % = 50
                    var epPercentWatched = 0;
                    foreach (var videoXref in episode.CrossReferences)
                    {
                        if (videoXref.Video is not { } otherVideo)
                            continue;

                        var videoUser = videoUserDataRepository.GetByUserAndVideoLocalID(user.ID, otherVideo.ID);
                        if (videoUser?.WatchedDate != null)
                            epPercentWatched += videoXref.Percentage <= 0 ? 100 : videoXref.Percentage;

                        if (epPercentWatched > 95)
                            break;
                    }

                    if (epPercentWatched <= 95)
                        continue;

                    if (updateStatsNow && episode.Series is AnimeSeries series)
                        toUpdateSeries.TryAdd(series.AnimeSeriesID, series);

                    await SaveEpisodeUserData(episode, user, new() { LastPlayedAt = userDataUpdate.LastPlayedAt }, updateStatsNow: false);
                }
            }
            else
            {
                // if setting a file to unwatched only set the episode unwatched, if ALL the files are unwatched
                foreach (var episodeXref in xrefs)
                {
                    if (episodeXref.ShokoEpisode is not { } episode)
                        continue;

                    var epPercentWatched = 0;
                    foreach (var videoXref in episode.CrossReferences)
                    {
                        if (videoXref.Video is not { } otherVideo)
                            continue;

                        var videoUser = videoUserDataRepository.GetByUserAndVideoLocalID(user.ID, otherVideo.ID);
                        if (videoUser?.WatchedDate != null)
                            epPercentWatched += videoXref.Percentage <= 0 ? 100 : videoXref.Percentage;

                        if (epPercentWatched > 95)
                            break;
                    }

                    if (epPercentWatched > 95)
                        continue;

                    if (updateStatsNow && episode.Series is AnimeSeries series)
                        toUpdateSeries.TryAdd(series.AnimeSeriesID, series);

                    await SaveEpisodeUserData(episode, user, new() { LastPlayedAt = null }, updateStatsNow: false);
                }
            }

            var settings = settingsProvider.GetSettings();
            var syncAnidb = user.IsAnidbUser &&
                !(reason is VideoUserDataSaveReason.Import && importSource is "AniDB") &&
                ((userDataUpdate.LastPlayedAt.HasValue && settings.AniDb.MyList_SetWatched) || (!userDataUpdate.LastPlayedAt.HasValue && settings.AniDb.MyList_SetUnwatched));
            if (syncAnidb)
            {
                var scheduler = await schedulerFactory.GetScheduler();
                await scheduler.StartJob<UpdateMyListFileStatusJob>(c =>
                {
                    c.Hash = video.ED2K;
                    c.Watched = userDataUpdate.LastPlayedAt.HasValue;
                    c.UpdateSeriesStats = false;
                    c.WatchedDate = userDataUpdate.LastPlayedAt?.ToUniversalTime();
                });
            }
        }

        // We run these events _after_ invoking the above event(s) so the series event(s) are fired after the episode and video event(s).
        if (updateStatsNow && toUpdateSeries.Count > 0)
        {
            foreach (var series in toUpdateSeries.Values)
                UpdateWatchedStats(series, user);
            var groups = toUpdateSeries.Values
                .SelectMany(a => a.AllGroupsAbove)
                .WhereNotNull()
                .DistinctBy(a => a.AnimeGroupID);
            foreach (var group in groups)
                UpdateWatchedStats(group, user);
        }

        return userData;
    }

    #endregion

    #region Episode User Data

    public event EventHandler<EpisodeUserDataSavedEventArgs>? EpisodeUserDataSaved;

    public IEpisodeUserData GetEpisodeUserData(IShokoEpisode episode, IUser user)
    {
        ArgumentNullException.ThrowIfNull(episode);
        if (episode.ID is <= 0)
            throw new ArgumentException("episode.ID must be greater than 0.", nameof(episode));
        ArgumentNullException.ThrowIfNull(user);
        if (user.ID is <= 0)
            throw new ArgumentException("user.ID must be greater than 0.", nameof(user));

        var userData = episodeUserDataRepository.GetByUserAndEpisodeID(user.ID, episode.ID)
            ?? new() { JMMUserID = user.ID, AnimeEpisodeID = episode.ID };
        if (userData.AnimeEpisode_UserID is 0)
            episodeUserDataRepository.Save(userData);
        return userData;
    }

    public IEnumerable<IEpisodeUserData> GetEpisodeUserDataForUser(IUser user)
    {
        ArgumentNullException.ThrowIfNull(user);
        if (user.ID is <= 0)
            throw new ArgumentException("user.ID must be greater than 0.", nameof(user));
        return episodeUserDataRepository.GetByUserID(user.ID);
    }

    public IReadOnlyList<IEpisodeUserData> GetEpisodeUserDataForEpisode(IShokoEpisode episode)
    {
        ArgumentNullException.ThrowIfNull(episode);
        if (episode.ID is <= 0)
            throw new ArgumentException("episode.ID must be greater than 0.", nameof(episode));
        return episodeUserDataRepository.GetByEpisodeID(episode.ID);
    }

    public Task<IEpisodeUserData> RateEpisode(IShokoEpisode episode, IUser user, double? userRating)
    {
        ArgumentNullException.ThrowIfNull(episode);
        if (episode.ID is <= 0)
            throw new ArgumentException("episode.ID must be greater than 0.", nameof(episode));
        ArgumentNullException.ThrowIfNull(user);
        if (user.ID is <= 0)
            throw new ArgumentException("user.ID must be greater than 0.", nameof(user));
        if (userRating is -1)
            userRating = null;
        if (userRating.HasValue)
        {
            userRating = Math.Round(userRating.Value, 1, MidpointRounding.AwayFromZero);
            if (userRating < 1 || userRating > 10)
                throw new ArgumentOutOfRangeException(nameof(userRating), "User rating must be between 1 and 10, or -1 or null for no rating.");
        }
        return SaveEpisodeUserDataInternal(episode, user, new EpisodeUserDataUpdate { UserRating = userRating });
    }

    public Task<IEpisodeUserData> UnrateEpisode(IShokoEpisode episode, IUser user)
        => SaveEpisodeUserDataInternal(episode, user, new EpisodeUserDataUpdate { UserRating = null });

    public Task<IEpisodeUserData> ToggleEpisodeAsFavorite(IShokoEpisode episode, IUser user)
        => SaveEpisodeUserDataInternal(episode, user, new EpisodeUserDataUpdate { IsFavorite = !(episodeUserDataRepository.GetByUserAndEpisodeID(user.ID, episode.ID)?.IsFavorite ?? false) });

    public Task<IEpisodeUserData> SetEpisodeAsFavorite(IShokoEpisode episode, IUser user, bool value)
        => SaveEpisodeUserDataInternal(episode, user, new EpisodeUserDataUpdate { IsFavorite = value });

    public Task<IEpisodeUserData> AddUserTagsForEpisode(IShokoEpisode episode, IUser user, params string[] tags)
        => SaveEpisodeUserDataInternal(episode, user, new EpisodeUserDataUpdate { UserTags = (episodeUserDataRepository.GetByUserAndEpisodeID(user.ID, episode.ID)?.UserTags ?? []).Union(tags) });

    public Task<IEpisodeUserData> AddUserTagsForEpisode(IShokoEpisode episode, IUser user, IEnumerable<string>? tags)
        => SaveEpisodeUserDataInternal(episode, user, new EpisodeUserDataUpdate { UserTags = (episodeUserDataRepository.GetByUserAndEpisodeID(user.ID, episode.ID)?.UserTags ?? []).Union(tags ?? []) });

    public Task<IEpisodeUserData> RemoveUserTagsForEpisode(IShokoEpisode episode, IUser user, params string[] tags)
        => SaveEpisodeUserDataInternal(episode, user, new EpisodeUserDataUpdate { UserTags = (episodeUserDataRepository.GetByUserAndEpisodeID(user.ID, episode.ID)?.UserTags ?? []).Except(tags) });

    public Task<IEpisodeUserData> RemoveUserTagsForEpisode(IShokoEpisode episode, IUser user, IEnumerable<string>? tags)
        => SaveEpisodeUserDataInternal(episode, user, new EpisodeUserDataUpdate { UserTags = (episodeUserDataRepository.GetByUserAndEpisodeID(user.ID, episode.ID)?.UserTags ?? []).Except(tags ?? []) });

    public Task<IEpisodeUserData> SetUserTagsForEpisode(IShokoEpisode episode, IUser user, IEnumerable<string>? tags)
        => SaveEpisodeUserDataInternal(episode, user, new EpisodeUserDataUpdate { UserTags = tags });

    public Task<IEpisodeUserData> SetEpisodeWatchedStatus(IShokoEpisode episode, IUser user, bool watched = true, DateTime? watchedAt = null, bool updateStatsNow = true)
        => SaveEpisodeUserDataInternal(episode, user, new() { LastPlayedAt = watched ? watchedAt ?? DateTime.Now : null }, updateStatsNow: updateStatsNow);

    public Task<IEpisodeUserData> SaveEpisodeUserData(IShokoEpisode episode, IUser user, EpisodeUserDataUpdate userDataUpdate, bool updateStatsNow = true)
        => SaveEpisodeUserDataInternal(episode, user, userDataUpdate, null, updateStatsNow);

    public Task<IEpisodeUserData> ImportEpisodeUserData(IShokoEpisode episode, IUser user, EpisodeUserDataUpdate userDataUpdate, string importSource, bool updateStatsNow = true)
        => SaveEpisodeUserDataInternal(episode, user, userDataUpdate, importSource, updateStatsNow);

    private async Task<IEpisodeUserData> SaveEpisodeUserDataInternal(IShokoEpisode episode, IUser user, EpisodeUserDataUpdate userDataUpdate, string? importSource = null, bool updateStatsNow = true)
    {
        ArgumentNullException.ThrowIfNull(episode);
        ArgumentNullException.ThrowIfNull(user);

        var reason = string.IsNullOrEmpty(importSource)
            ? EpisodeUserDataSaveReason.None
            : EpisodeUserDataSaveReason.Import;
        var userData = episodeUserDataRepository.GetByUserAndEpisodeID(user.ID, episode.ID)
            ?? new() { JMMUserID = user.ID, AnimeEpisodeID = episode.ID, AnimeSeriesID = episode.SeriesID };
        var watchedStatusChanged = userDataUpdate.HasLastPlayedAt && (
            userDataUpdate.LastPlayedAt.HasValue
                ? !userData.WatchedDate.HasValue || !userData.WatchedDate.Value.Equals(userDataUpdate.LastPlayedAt.Value)
                : userData.WatchedDate.HasValue
        );
        var shouldSave = userData.AnimeEpisode_UserID is 0 ||
            watchedStatusChanged ||
            (userDataUpdate.LastUpdatedAt.HasValue && !userData.LastUpdated.Equals(userDataUpdate.LastUpdatedAt.Value));
        if (watchedStatusChanged)
        {
            reason |= EpisodeUserDataSaveReason.LastPlayedAt;
            userData.WatchedDate = userDataUpdate.LastPlayedAt;
        }

        // Set the playback count if the update has it.
        if (userDataUpdate.PlaybackCount.HasValue)
        {
            // If set to a negative number, infer count from the last played date.
            if (userDataUpdate.PlaybackCount.Value < 0)
                userDataUpdate.PlaybackCount = userData.WatchedDate.HasValue ? 1 : 0;
            if (userDataUpdate.PlaybackCount.Value != userData.WatchedCount)
            {
                reason |= EpisodeUserDataSaveReason.PlaybackCount;
                userData.WatchedCount = userDataUpdate.PlaybackCount.Value;
                shouldSave = true;
            }
        }
        // Increment the playback count if the video was watched
        else if (watchedStatusChanged && userDataUpdate.LastPlayedAt.HasValue)
        {
            reason |= EpisodeUserDataSaveReason.PlaybackCount;
            userData.WatchedCount++;
        }

        if (userDataUpdate.IsFavorite.HasValue && userData.IsFavorite != userDataUpdate.IsFavorite)
        {
            userData.IsFavorite = userDataUpdate.IsFavorite.Value;
            reason |= EpisodeUserDataSaveReason.IsFavorite;
            shouldSave = true;
        }

        if (userDataUpdate.UserTags is not null)
        {
            var list = userDataUpdate.UserTags.Distinct().Order().ToList();
            if (!list.SequenceEqual(userData.UserTags))
            {
                userData.UserTags = list;
                reason |= EpisodeUserDataSaveReason.UserTags;
            }
        }

        if (userDataUpdate.HasSetUserRating)
        {
            if (!userDataUpdate.HasUserRating)
            {
                if (userData.HasUserRating)
                {
                    userData.UserRating = null;
                    shouldSave = true;
                    reason |= EpisodeUserDataSaveReason.UserRating;
                }
            }
            else if (userData.UserRating != userDataUpdate.UserRating)
            {
                userData.UserRating = userDataUpdate.UserRating;
                shouldSave = true;
                reason |= EpisodeUserDataSaveReason.UserRating;
            }
        }

        if (shouldSave)
        {
            userData.LastUpdated = userDataUpdate.LastUpdatedAt ?? DateTime.Now;
            episodeUserDataRepository.Save(userData);

            if (user.IsAnidbUser && reason.HasFlag(EpisodeUserDataSaveReason.UserRating))
            {
                // Schedule the AniDB vote job
                var scheduler = await schedulerFactory.GetScheduler();
                await scheduler.StartJob<VoteAniDBEpisodeJob>(c =>
                {
                    c.EpisodeID = episode.AnidbEpisodeID;
                    c.VoteValue = userData.UserRating ?? -1;
                });
            }

            SendEvent(episode, user, userData, reason, importSource);
        }

        if (watchedStatusChanged)
        {
            var settings = settingsProvider.GetSettings();
            var syncTrakt =
                !(reason.HasFlag(EpisodeUserDataSaveReason.Import) && importSource is "Trakt") &&
                user.IsAnidbUser &&
                settings.TraktTv.Enabled && !string.IsNullOrEmpty(settings.TraktTv.AuthToken);
            if (syncTrakt && watchedStatusChanged)
            {
                var scheduler = await schedulerFactory.GetScheduler();
                await scheduler.StartJob<SendEpisodeWatchStateToTraktJob>(c =>
                {
                    c.AnimeEpisodeID = episode.ID;
                    c.Action = userDataUpdate.LastPlayedAt.HasValue ? TraktSyncType.HistoryAdd : TraktSyncType.HistoryRemove;
                });
            }
        }

        if (updateStatsNow && shouldSave)
        {
            var series = (AnimeSeries)episode.Series!;
            UpdateWatchedStats(series, user);
            var groups = series.AllGroupsAbove
                .WhereNotNull()
                .DistinctBy(a => a.AnimeGroupID);
            foreach (var group in groups)
                UpdateWatchedStats(group, user);
        }

        return userData;
    }

    private void SendEvent(IShokoEpisode episode, IUser user, IEpisodeUserData userData, EpisodeUserDataSaveReason reason, string? importSource = null)
    {
        Task.Run(() =>
        {
            var eventArgs = new EpisodeUserDataSavedEventArgs()
            {
                Reason = reason,
                Episode = episode,
                User = user,
                UserData = userData,
                ImportSource = importSource,
            };
            try
            {
                EpisodeUserDataSaved?.Invoke(this, eventArgs);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "An error occurred while trying to send the EpisodeUserDataSaved event; {Message}", ex.Message);
            }
        });
    }

    internal async Task IncrementEpisodeStats(IShokoEpisode episode, IUser user, int statCountType)
    {
        if (episode is null || episode.Series is not { } series)
            return;

        var episodeUserData = episodeUserDataRepository.GetByUserAndEpisodeID(user.ID, episode.ID)
            ?? new() { AnimeEpisodeID = episode.ID, AnimeSeriesID = episode.SeriesID, JMMUserID = user.ID };
        var shouldSave = false;
        switch (statCountType)
        {
            case 2 /* playback started */:
                episodeUserData.PlayedCount++;
                shouldSave = true;
                break;
            case 3 /* playback stopped */:
                episodeUserData.StoppedCount++;
                shouldSave = true;
                break;
            // Forward to save method if it's a stat we care about outside compatibility reasons.
            case 1 /* Completed */:
                await SaveEpisodeUserDataInternal(episode, user, new() { PlaybackCount = episodeUserData.WatchedCount + 1 });
                return;
        }

        var seriesUserData = seriesUserDataRepository.GetByUserAndSeriesID(user.ID, series.ID)
            ?? new() { AnimeSeriesID = series.ID, JMMUserID = user.ID };
        switch (statCountType)
        {
            case 2 /* playback started */:
                seriesUserData.PlayedCount++;
                break;
            case 3 /* playback stopped */:
                seriesUserData.StoppedCount++;
                break;
        }

        if (shouldSave)
        {
            episodeUserDataRepository.Save(episodeUserData);
            seriesUserDataRepository.Save(seriesUserData);
        }
    }

    internal void CreateUserRecordsForNewEpisode(IShokoEpisode episode)
    {
        var videoUserDataList = episode
            .VideoList
            .SelectMany(a => videoUserDataRepository.GetByVideoLocalID(a.ID))
            .ToList();
        if (videoUserDataList.Count <= 0)
            return;
        foreach (var groupBy in videoUserDataList.GroupBy(a => a.JMMUserID))
        {
            var episodeUserData = episodeUserDataRepository.GetByUserAndEpisodeID(groupBy.Key, episode.ID);
            if (episodeUserData is not null)
                continue;

            // get the last watched file
            var userDataList = groupBy.ToList();
            var watchedAt = userDataList
                .Select(userData => userData.WatchedDate)
                .Where(a => a is not null)
                .OrderDescending()
                .FirstOrDefault();
            var watchedCount = userDataList.Count(userData => userData.WatchedCount > 0);
            // if we will create an empty record, don't
            if (watchedAt is null && watchedCount is 0)
                continue;

            episodeUserData = new()
            {
                JMMUserID = groupBy.Key,
                AnimeEpisodeID = episode.ID,
                AnimeSeriesID = episode.SeriesID,
                WatchedDate = watchedAt,
                PlayedCount = watchedCount,
                WatchedCount = watchedCount,
                StoppedCount = watchedCount,
                LastUpdated = DateTime.Now,
            };
            episodeUserDataRepository.Save(episodeUserData);
        }
    }

    #endregion

    #region Series User Data

    public event EventHandler<SeriesUserDataSavedEventArgs>? SeriesUserDataSaved;

    public ISeriesUserData GetSeriesUserData(IShokoSeries series, IUser user)
    {
        ArgumentNullException.ThrowIfNull(series);
        if (series.ID is <= 0)
            throw new ArgumentException("series.ID must be greater than 0.", nameof(series));
        ArgumentNullException.ThrowIfNull(user);
        if (user.ID is <= 0)
            throw new ArgumentException("user.ID must be greater than 0.", nameof(user));

        var userData = seriesUserDataRepository.GetByUserAndSeriesID(user.ID, series.ID)
            ?? new() { JMMUserID = user.ID, AnimeSeriesID = series.ID };
        if (userData.AnimeSeries_UserID is 0)
            seriesUserDataRepository.Save(userData);
        return userData;
    }

    public IEnumerable<ISeriesUserData> GetSeriesUserDataForUser(IUser user)
    {
        ArgumentNullException.ThrowIfNull(user);
        if (user.ID is <= 0)
            throw new ArgumentException("user.ID must be greater than 0.", nameof(user));
        return seriesUserDataRepository.GetByUserID(user.ID);
    }

    public IReadOnlyList<ISeriesUserData> GetSeriesUserDataForSeries(IShokoSeries series)
    {
        ArgumentNullException.ThrowIfNull(series);
        if (series.ID is <= 0)
            throw new ArgumentException("series.ID must be greater than 0.", nameof(series));
        return seriesUserDataRepository.GetBySeriesID(series.ID);
    }

    public Task<ISeriesUserData> RateSeries(IShokoSeries series, IUser user, double? userRating, SeriesVoteType? voteType = null)
    {
        ArgumentNullException.ThrowIfNull(series);
        if (series.ID is <= 0)
            throw new ArgumentException("series.ID must be greater than 0.", nameof(series));
        ArgumentNullException.ThrowIfNull(user);
        if (user.ID is <= 0)
            throw new ArgumentException("user.ID must be greater than 0.", nameof(user));
        if (userRating is -1)
            userRating = null;
        if (userRating.HasValue)
        {
            userRating = Math.Round(userRating.Value, 1, MidpointRounding.AwayFromZero);
            if (userRating < 1 || userRating > 10)
                throw new ArgumentOutOfRangeException(nameof(userRating), "User rating must be between 1 and 10, or -1 or null for no rating.");
            voteType ??= series.AnidbAnime.EndDate is { } endDate && endDate < DateTime.Now
                ? SeriesVoteType.Permanent
                : SeriesVoteType.Temporary;
        }
        else if (voteType.HasValue)
        {
            voteType = null;
        }
        return SaveSeriesUserDataInternal(series, user, new SeriesUserDataUpdate { UserRating = userRating, UserRatingVoteType = voteType });
    }

    public Task<ISeriesUserData> UnrateSeries(IShokoSeries series, IUser user)
        => SaveSeriesUserDataInternal(series, user, new SeriesUserDataUpdate { UserRating = null });

    public Task<ISeriesUserData> ToggleSeriesAsFavorite(IShokoSeries series, IUser user)
        => SaveSeriesUserDataInternal(series, user, new SeriesUserDataUpdate { IsFavorite = !(seriesUserDataRepository.GetByUserAndSeriesID(user.ID, series.ID)?.IsFavorite ?? false) });

    public Task<ISeriesUserData> SetSeriesAsFavorite(IShokoSeries series, IUser user, bool value)
        => SaveSeriesUserDataInternal(series, user, new SeriesUserDataUpdate { IsFavorite = value });

    public Task<ISeriesUserData> AddUserTagsForSeries(IShokoSeries series, IUser user, params string[] tags)
        => SaveSeriesUserDataInternal(series, user, new SeriesUserDataUpdate { UserTags = (seriesUserDataRepository.GetByUserAndSeriesID(user.ID, series.ID)?.UserTags ?? []).Union(tags) });

    public Task<ISeriesUserData> AddUserTagsForSeries(IShokoSeries series, IUser user, IEnumerable<string>? tags)
        => SaveSeriesUserDataInternal(series, user, new SeriesUserDataUpdate { UserTags = (seriesUserDataRepository.GetByUserAndSeriesID(user.ID, series.ID)?.UserTags ?? []).Union(tags ?? []) });

    public Task<ISeriesUserData> RemoveUserTagsForSeries(IShokoSeries series, IUser user, params string[] tags)
        => SaveSeriesUserDataInternal(series, user, new SeriesUserDataUpdate { UserTags = (seriesUserDataRepository.GetByUserAndSeriesID(user.ID, series.ID)?.UserTags ?? []).Except(tags) });

    public Task<ISeriesUserData> RemoveUserTagsForSeries(IShokoSeries series, IUser user, IEnumerable<string>? tags)
        => SaveSeriesUserDataInternal(series, user, new SeriesUserDataUpdate { UserTags = (seriesUserDataRepository.GetByUserAndSeriesID(user.ID, series.ID)?.UserTags ?? []).Except(tags ?? []) });

    public Task<ISeriesUserData> SetUserTagsForSeries(IShokoSeries series, IUser user, IEnumerable<string>? tags)
        => SaveSeriesUserDataInternal(series, user, new SeriesUserDataUpdate { UserTags = tags });

    public Task<ISeriesUserData> SaveSeriesUserData(IShokoSeries series, IUser user, SeriesUserDataUpdate userDataUpdate)
        => SaveSeriesUserDataInternal(series, user, userDataUpdate);

    public Task<ISeriesUserData> ImportSeriesUserData(IShokoSeries series, IUser user, SeriesUserDataUpdate userDataUpdate, string importSource)
        => SaveSeriesUserDataInternal(series, user, userDataUpdate, importSource);

    private async Task<ISeriesUserData> SaveSeriesUserDataInternal(IShokoSeries series, IUser user, SeriesUserDataUpdate userDataUpdate, string? importSource = null)
    {
        var reason = string.IsNullOrEmpty(importSource)
            ? SeriesUserDataSaveReason.None
            : SeriesUserDataSaveReason.Import;
        ArgumentNullException.ThrowIfNull(series);
        if (series.ID is <= 0)
            throw new ArgumentException("series.ID must be greater than 0.", nameof(series));
        ArgumentNullException.ThrowIfNull(user);
        if (user.ID is <= 0)
            throw new ArgumentException("user.ID must be greater than 0.", nameof(user));

        var userData = seriesUserDataRepository.GetByUserAndSeriesID(user.ID, series.ID)
            ?? new() { AnimeSeriesID = series.ID, JMMUserID = user.ID };
        var shouldSave = userData.AnimeSeries_UserID is 0;

        if (userDataUpdate.IsFavorite.HasValue && userData.IsFavorite != userDataUpdate.IsFavorite)
        {
            userData.IsFavorite = userDataUpdate.IsFavorite.Value;
            reason |= SeriesUserDataSaveReason.IsFavorite;
            shouldSave = true;
        }

        if (userDataUpdate.UserTags is not null)
        {
            var list = userDataUpdate.UserTags.Distinct().Order().ToList();
            if (!list.SequenceEqual(userData.UserTags))
            {
                userData.UserTags = list;
                reason |= SeriesUserDataSaveReason.UserTags;
            }
        }

        var providerVoteType = Providers.AniDB.VoteType.AnimeTemporary;
        if (userDataUpdate.HasSetUserRating)
        {
            if (!userDataUpdate.HasUserRating)
            {
                if (userData.HasUserRating)
                {
                    providerVoteType = userData.UserRatingVoteType is SeriesVoteType.Permanent
                        ? Providers.AniDB.VoteType.AnimePermanent
                        : Providers.AniDB.VoteType.AnimeTemporary;
                    userData.UserRating = null;
                    userData.UserRatingVoteType = null;
                    shouldSave = true;
                    reason |= SeriesUserDataSaveReason.UserRating;
                }
            }
            else
            {
                providerVoteType = userDataUpdate.UserRatingVoteType is SeriesVoteType.Permanent
                    ? Providers.AniDB.VoteType.AnimePermanent
                    : Providers.AniDB.VoteType.AnimeTemporary;
                if (userData.UserRating != userDataUpdate.UserRating || userData.UserRatingVoteType != userDataUpdate.UserRatingVoteType)
                {
                    userData.UserRating = userDataUpdate.UserRating;
                    userData.UserRatingVoteType = userDataUpdate.UserRatingVoteType;
                    shouldSave = true;
                    reason |= SeriesUserDataSaveReason.UserRating;
                }
            }
        }

        if (shouldSave)
        {
            userData.LastUpdated = DateTime.Now;
            seriesUserDataRepository.Save(userData);

            if (user.IsAnidbUser && reason.HasFlag(SeriesUserDataSaveReason.UserRating))
            {
                // Schedule the AniDB vote job
                var scheduler = await schedulerFactory.GetScheduler();
                await scheduler.StartJob<VoteAniDBAnimeJob>(c =>
                {
                    c.AnimeID = series.AnidbAnimeID;
                    c.VoteType = providerVoteType;
                    c.VoteValue = userData.UserRating ?? -1;
                });
            }

            SendEvent(series, user, userData, reason, importSource);
        }

        return userData;
    }

    internal void UpdateWatchedStats(IShokoSeries series, IReadOnlyList<IShokoEpisode> episodes)
    {
        var videoLookup = series.CrossReferences
            .Where(a => !string.IsNullOrEmpty(a?.ED2K)).Select(xref =>
                (xref.AnidbEpisodeID, VideoLocal: xref.Video!))
            .Where(a => a.VideoLocal is not null)
            .ToLookup(a => a.AnidbEpisodeID, b => b.VideoLocal);
        var videoUserDataLookup = videoLookup
            .SelectMany(xref => xref.SelectMany(a => videoUserDataRepository.GetByVideoLocalID(a.ID)).Select(a => (EpisodeID: xref.Key, VideoLocalUser: a)) ?? [])
            .Where(a => a.VideoLocalUser is not null)
            .ToLookup(a => (a.EpisodeID, UserID: a.VideoLocalUser.JMMUserID), b => b.VideoLocalUser);
        var episodeUserDataLookup = episodes.SelectMany(
                ep =>
                {
                    var users = episodeUserDataRepository.GetByEpisodeID(ep.ID);
                    return users.Select(a => (EpisodeID: ep.AnidbEpisodeID, AnimeEpisode_User: a));
                }
            )
            .Where(a => a.AnimeEpisode_User is not null)
            .ToLookup(a => (a.EpisodeID, UserID: a.AnimeEpisode_User.JMMUserID), b => b.AnimeEpisode_User);
        var lockObj = new object();
        foreach (var user in userRepository.GetAll())
            UpdateWatchedStatsInternal(series, episodes, user, videoLookup, videoUserDataLookup, episodeUserDataLookup, lockObj);
    }

    private void UpdateWatchedStats(IShokoSeries series, IUser user)
    {
        var episodes = series.Episodes;
        var videoLookup = series.CrossReferences
            .Where(a => !string.IsNullOrEmpty(a?.ED2K)).Select(xref =>
                (xref.AnidbEpisodeID, VideoLocal: xref.Video!))
            .Where(a => a.VideoLocal is not null)
            .ToLookup(a => a.AnidbEpisodeID, b => b.VideoLocal);
        var videoUserDataLookup = videoLookup
            .SelectMany(xref => xref.SelectMany(a => videoUserDataRepository.GetByVideoLocalID(a.ID)).Select(a => (EpisodeID: xref.Key, VideoLocalUser: a)) ?? [])
            .Where(a => a.VideoLocalUser is not null)
            .ToLookup(a => (a.EpisodeID, UserID: a.VideoLocalUser.JMMUserID), b => b.VideoLocalUser);
        var episodeUserDataLookup = episodes.SelectMany(
                ep =>
                {
                    var users = episodeUserDataRepository.GetByEpisodeID(ep.ID);
                    return users.Select(a => (EpisodeID: ep.AnidbEpisodeID, AnimeEpisode_User: a));
                }
            )
            .Where(a => a.AnimeEpisode_User is not null)
            .ToLookup(a => (a.EpisodeID, UserID: a.AnimeEpisode_User.JMMUserID), b => b.AnimeEpisode_User);
        var lockObj = new object();
        UpdateWatchedStatsInternal(series, episodes, user, videoLookup, videoUserDataLookup, episodeUserDataLookup, lockObj);
    }

    private void UpdateWatchedStatsInternal(
        IShokoSeries series,
        IReadOnlyList<IShokoEpisode> episodes,
        IUser user,
        ILookup<int, IVideo> videoLookup,
        ILookup<(int EpisodeID, int UserID), VideoLocal_User> videoUserDataLookup,
        ILookup<(int EpisodeID, int UserID), AnimeEpisode_User> episodeUserDataLookup,
        object lockObj
    )
    {
        var unwatchedCount = 0;
        var hiddenUnwatchedCount = 0;
        var watchedCount = 0;
        var watchedEpisodeCount = 0;
        DateTime? lastVideoUpdate = null;
        DateTime? lastEpisodeUpdate = null;
        DateTime? watchedDate = null;
        var userData = seriesUserDataRepository.GetByUserAndSeriesID(user.ID, series.ID)
            ?? new() { JMMUserID = user.ID, AnimeSeriesID = series.ID, LastUpdated = DateTime.Now };
        Parallel.ForEach(episodes, new() { MaxDegreeOfParallelism = 4 }, ep =>
            {
                if (ep.Type is not EpisodeType.Episode or EpisodeType.Special)
                    return;
                VideoLocal_User? videoUserData = null;
                DateTime? videoUpdated = null;
                if (videoLookup.Contains(ep.AnidbEpisodeID) && videoUserDataLookup.Contains((ep.AnidbEpisodeID, user.ID)))
                {
                    videoUserData = videoUserDataLookup[(ep.AnidbEpisodeID, user.ID)]
                        .OrderByDescending(a => a.WatchedDate.HasValue)
                        .ThenByDescending(a => a.LastUpdated)
                        .FirstOrDefault();
                    videoUpdated = videoUserDataLookup[(ep.AnidbEpisodeID, user.ID)]
                        .OrderByDescending(a => a.LastUpdated)
                        .FirstOrDefault()?.LastUpdated;
                }
                var episodeUserData = episodeUserDataLookup.Contains((ep.AnidbEpisodeID, user.ID))
                    ? episodeUserDataLookup[(ep.AnidbEpisodeID, user.ID)].First()
                    : null;
                lock (lockObj)
                {
                    if (videoUpdated.HasValue && (lastVideoUpdate is null || videoUpdated.Value > lastVideoUpdate.Value))
                        lastVideoUpdate = videoUpdated;
                    if (episodeUserData is not null && (!lastEpisodeUpdate.HasValue || episodeUserData.LastUpdated > lastEpisodeUpdate))
                        lastEpisodeUpdate = episodeUserData.LastUpdated;
                    if (videoUserData?.WatchedDate is null && episodeUserData?.WatchedDate is null)
                    {
                        if (ep.IsHidden)
                            Interlocked.Increment(ref hiddenUnwatchedCount);
                        else
                            Interlocked.Increment(ref unwatchedCount);
                        return;
                    }
                    if (videoUserData is not null && (watchedDate is null || (videoUserData.WatchedDate is not null && videoUserData.WatchedDate.Value > watchedDate.Value)))
                        watchedDate = videoUserData.WatchedDate;
                    if (episodeUserData is not null && (watchedDate is null || (episodeUserData.WatchedDate is not null && episodeUserData.WatchedDate.Value > watchedDate.Value)))
                        watchedDate = episodeUserData.WatchedDate;
                }

                Interlocked.Increment(ref watchedEpisodeCount);
                Interlocked.Add(ref watchedCount, episodeUserData?.WatchedCount ?? videoUserData?.WatchedCount ?? 0);
            });

        if (
            userData.AnimeSeries_UserID is 0 ||
            userData.UnwatchedEpisodeCount != unwatchedCount ||
            userData.HiddenUnwatchedEpisodeCount != hiddenUnwatchedCount ||
            userData.WatchedEpisodeCount != watchedEpisodeCount ||
            userData.WatchedCount != watchedCount ||
            userData.WatchedDate != watchedDate ||
            userData.LastEpisodeUpdate != lastEpisodeUpdate ||
            userData.LastVideoUpdate != lastVideoUpdate
        )
        {
            userData.UnwatchedEpisodeCount = unwatchedCount;
            userData.HiddenUnwatchedEpisodeCount = hiddenUnwatchedCount;
            userData.WatchedEpisodeCount = watchedEpisodeCount;
            userData.WatchedCount = watchedCount;
            userData.WatchedDate = watchedDate;
            userData.LastEpisodeUpdate = lastEpisodeUpdate;
            userData.LastVideoUpdate = lastVideoUpdate;
            seriesUserDataRepository.Save(userData);

            SendEvent(series, user, userData, SeriesUserDataSaveReason.SeriesStats);
        }
    }

    private void SendEvent(IShokoSeries series, IUser user, ISeriesUserData userData, SeriesUserDataSaveReason reason, string? importSource = null)
    {
        Task.Run(() =>
        {
            var eventArgs = new SeriesUserDataSavedEventArgs()
            {
                Reason = reason,
                Series = series,
                User = user,
                UserData = userData,
                ImportSource = importSource,
            };
            try
            {
                SeriesUserDataSaved?.Invoke(this, eventArgs);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "An error occurred while trying to send the SeriesUserDataSaved event; {Message}", ex.Message);
            }
        });
    }

    #endregion

    #region Group User Data

    internal void UpdateWatchedStats(
        IShokoGroup group,
        IUser user,
        IReadOnlyList<IShokoSeries>? allSeries = null,
        Action<AnimeGroup_User, bool, bool>? newAnimeGroupUsers = null
    )
    {
        var userData = groupUserDataRepository.GetByUserAndGroupID(user.ID, group.ID)
            ?? new() { JMMUserID = user.ID, AnimeGroupID = group.ID };
        var isNew = userData.AnimeGroup_UserID is 0;

        // Reset stats
        var watchedCount = 0;
        var unwatchedEpisodeCount = 0;
        var playedCount = 0;
        var stoppedCount = 0;
        var watchedEpisodeCount = 0;
        var watchedDate = (DateTime?)null;

        foreach (var serUserRecord in (allSeries ?? group.AllSeries).Select(ser => seriesUserDataRepository.GetByUserAndSeriesID(user.ID, ser.ID)).WhereNotNull())
        {
            watchedCount += serUserRecord.WatchedCount;
            unwatchedEpisodeCount += serUserRecord.UnwatchedEpisodeCount;
            playedCount += serUserRecord.PlayedCount;
            stoppedCount += serUserRecord.StoppedCount;
            watchedEpisodeCount += serUserRecord.WatchedEpisodeCount;
            if (serUserRecord.WatchedDate != null
                && (watchedDate is null || serUserRecord.WatchedDate > watchedDate))
            {
                watchedDate = serUserRecord.WatchedDate;
            }
        }

        var isUpdated = (
            isNew ||
            userData.WatchedCount != watchedCount ||
            userData.UnwatchedEpisodeCount != unwatchedEpisodeCount ||
            userData.PlayedCount != playedCount ||
            userData.StoppedCount != stoppedCount ||
            userData.WatchedEpisodeCount != watchedEpisodeCount ||
            userData.WatchedDate != watchedDate
        );

        if (newAnimeGroupUsers is null)
        {
            if (isUpdated)
                groupUserDataRepository.Save(userData);
        }
        else
        {
            newAnimeGroupUsers(userData, isNew, isUpdated);
        }
    }

    #endregion
}
