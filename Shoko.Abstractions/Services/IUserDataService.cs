using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Shoko.Abstractions.Events;
using Shoko.Abstractions.Metadata.Shoko;
using Shoko.Abstractions.User;
using Shoko.Abstractions.UserData;
using Shoko.Abstractions.UserData.Enums;
using Shoko.Abstractions.Video;

namespace Shoko.Abstractions.Services;

/// <summary>
///   Responsible for everything related to user data across videos, episodes
///   and series.
/// </summary>
public interface IUserDataService
{
    #region Video User Data

    /// <summary>
    ///   Dispatched when video user data is saved.
    /// </summary>
    event EventHandler<VideoUserDataSavedEventArgs>? VideoUserDataSaved;

    /// <summary>
    ///   Gets user video data for the given user and video.
    /// </summary>
    /// <param name="video">
    ///   The video to get user data for.
    /// </param>
    /// <param name="user">
    ///   The user to get user data for.
    /// </param>
    /// <exception cref="ArgumentNullException">
    ///   Thrown when <paramref name="video"/> or <paramref name="user"/> is
    ///   <c>null</c>.
    /// </exception>
    /// <returns>
    ///   The user data for the given video and user, or <c>null</c> if no such
    ///   data currently exists.
    /// </returns>
    IVideoUserData? GetVideoUserData(IVideo video, IUser user);

    /// <summary>
    ///   Gets all user data across all videos for the given user.
    /// </summary>
    /// <param name="user">
    ///   The user to get user data for.
    /// </param>
    /// <exception cref="ArgumentNullException">
    ///   Thrown when <paramref name="user"/> is <c>null</c>.
    /// </exception>
    /// <returns>
    ///   An enumerable sequence containing all user data across all videos for
    ///   the given user.
    /// </returns>
    IEnumerable<IVideoUserData> GetVideoUserDataForUser(IUser user);

    /// <summary>
    ///   Gets all user data for across all users for the given video.
    /// </summary>
    /// <param name="video">
    ///   The video to get user data for.
    /// </param>
    /// <exception cref="ArgumentNullException">
    ///   Thrown when <paramref name="video"/> is <c>null</c>.
    /// </exception>
    /// <returns>
    ///   A read-only list of all user data across all users for the given
    ///   video.
    /// </returns>
    IReadOnlyList<IVideoUserData> GetVideoUserDataForVideo(IVideo video);

    /// <summary>
    ///   Sets the video watch status.
    /// </summary>
    /// <param name="video">
    ///   The video to set the watch status for.
    /// </param>
    /// <param name="user">
    ///   The user to set the watch status for.
    /// </param>
    /// <param name="isWatched">
    ///   Optional. When set to <c>true</c> the video will be marked as watched;
    ///   otherwise, it will be marked as unwatched.
    /// </param>
    /// <param name="lastPlayedAt">
    ///   Optional. When the video was watched.
    /// </param>
    /// <param name="reason">
    ///   Optional. The reason why the video watch status was updated.
    /// </param>
    /// <param name="updateStatsNow">
    ///   Optional. When set to <c>true</c> will update the series stats after
    ///   saving. If doing multiple updates on the same series at once, it is
    ///   recommended to set this to <c>false</c> for all but the last update.
    /// </param>
    /// <exception cref="ArgumentNullException">
    ///   Thrown when <paramref name="video"/> or <paramref name="user"/> is <c>null</c>.
    /// </exception>
    /// <returns>
    ///   The task containing the new or updated user data for the video and user.
    /// </returns>
    Task<IVideoUserData> SetVideoWatchedStatus(IVideo video, IUser user, bool isWatched = true, DateTime? lastPlayedAt = null, VideoUserDataSaveReason reason = VideoUserDataSaveReason.None, bool updateStatsNow = true);

    /// <summary>
    ///   Saves the user data for the video and user.
    /// </summary>
    /// <param name="user">
    ///   The user to save video user data for.
    /// </param>
    /// <param name="video">
    ///   The video to save video user data for.
    /// </param>
    /// <param name="userDataUpdate">
    ///   The update containing the details to save.
    /// </param>
    /// <param name="reason">
    ///   The reason why the user data was updated.
    /// </param>
    /// <param name="updateStatsNow">
    ///   Optional. When set to <c>true</c> will update the series stats after
    ///   saving. If doing multiple updates on the same series at once, it is
    ///   recommended to set this to <c>false</c> for all but the last update.
    /// </param>
    /// <exception cref="ArgumentNullException">
    ///   Thrown when <paramref name="video"/> or <paramref name="user"/> is <c>null</c>.
    /// </exception>
    /// <returns>
    ///   The task containing the new or updated user data for the video and user.
    /// </returns>
    Task<IVideoUserData> SaveVideoUserData(IVideo video, IUser user, VideoUserDataUpdate userDataUpdate, VideoUserDataSaveReason reason = VideoUserDataSaveReason.None, bool updateStatsNow = true);

    /// <summary>
    ///   Imports some user data for the video and user from the given import
    ///   source.
    /// </summary>
    /// <param name="user">
    ///   The user to save video user data for.
    /// </param>
    /// <param name="video">
    ///   The video to save video user data for.
    /// </param>
    /// <param name="userDataUpdate">
    ///   The update containing the details to save.
    /// </param>
    /// <param name="importSource">
    ///   The import source.
    /// </param>
    /// <param name="updateStatsNow">
    ///   Optional. When set to <c>true</c> will update the series stats after
    ///   saving. If doing multiple updates on the same series at once, it is
    ///   recommended to set this to <c>false</c> for all but the last update.
    /// </param>
    /// <exception cref="ArgumentNullException">
    ///   Thrown when <paramref name="video"/> or <paramref name="user"/> is <c>null</c>.
    /// </exception>
    /// <returns>
    ///   The task containing the new or updated user data for the video and user.
    /// </returns>
    Task<IVideoUserData> ImportVideoUserData(IVideo video, IUser user, VideoUserDataUpdate userDataUpdate, string importSource, bool updateStatsNow = true);

    #endregion

    #region Episode User Data

    /// <summary>
    ///   Dispatched when episode user data is saved.
    /// </summary>
    event EventHandler<EpisodeUserDataSavedEventArgs>? EpisodeUserDataSaved;

    /// <summary>
    ///   Gets user episode data for the given user and episode.
    /// </summary>
    /// <param name="episode">
    ///   The episode.
    /// </param>
    /// <param name="user">
    ///   The user.
    /// </param>
    /// <exception cref="ArgumentNullException">
    ///   Thrown when the <paramref name="episode"/> or <paramref name="user"/>
    ///   is <c>null</c>.
    /// </exception>
    /// <returns>
    ///   The user data for the episode and user.
    /// </returns>
    IEpisodeUserData GetEpisodeUserData(IShokoEpisode episode, IUser user);

    /// <summary>
    ///   Gets all user episode data for the given user.
    /// </summary>
    /// <param name="user">
    ///   The user.
    /// </param>
    /// <exception cref="ArgumentNullException">
    ///   Thrown when the <paramref name="user"/> is <c>null</c>.
    /// </exception>
    /// <returns>
    ///   The user data for the episode and user.
    /// </returns>
    IEnumerable<IEpisodeUserData> GetEpisodeUserDataForUser(IUser user);

    /// <summary>
    /// Gets all user episode data for the given episode.
    /// </summary>
    /// <param name="episode">
    ///   The episode.
    /// </param>
    /// <exception cref="ArgumentNullException">
    ///   Thrown when the <paramref name="episode"/> is <c>null</c>.
    /// </exception>
    /// <returns>
    ///   A list of user data for the episode.
    /// </returns>
    IReadOnlyList<IEpisodeUserData> GetEpisodeUserDataForEpisode(IShokoEpisode episode);

    /// <summary>
    ///   Sets the episode watch status.
    /// </summary>
    /// <param name="episode">
    ///   The episode.
    /// </param>
    /// <param name="user">
    ///   The user.
    /// </param>
    /// <param name="isWatched">
    ///   Optional. When set to <c>true</c> the video will be marked as watched;
    ///   otherwise, it will be marked as unwatched.
    /// </param>
    /// <param name="lastPlayedAt">
    ///   Optional. When the video was watched.
    /// </param>
    /// <param name="updateStatsNow">
    ///   Optional. When set to <c>true</c> will update the series stats after
    ///   saving. If doing multiple updates on the same series at once, it is
    ///   recommended to set this to <c>false</c> for all but the last update.
    /// </param>
    /// <exception cref="ArgumentNullException">
    ///   Thrown when the <paramref name="episode"/> or <paramref name="user"/>
    ///   is <c>null</c>.
    /// </exception>
    /// <returns>
    ///   The user data for the episode and user.
    /// </returns>
    Task<IEpisodeUserData> SetEpisodeWatchedStatus(IShokoEpisode episode, IUser user, bool isWatched = true, DateTime? lastPlayedAt = null, bool updateStatsNow = true);

    /// <summary>
    ///   Toggles the favorite status of a episode.
    /// </summary>
    /// <param name="episode">
    ///   The episode.
    /// </param>
    /// <param name="user">
    ///   The user.
    /// </param>
    /// <exception cref="ArgumentNullException">
    ///   Thrown when the <paramref name="episode"/> or <paramref name="user"/>
    ///   is <c>null</c>.
    /// </exception>
    /// <returns>
    ///   The user data for the episode and user.
    /// </returns>
    Task<IEpisodeUserData> ToggleEpisodeAsFavorite(IShokoEpisode episode, IUser user);

    /// <summary>
    ///   Sets the favorite status of a episode.
    /// </summary>
    /// <param name="episode">
    ///   The episode.
    /// </param>
    /// <param name="user">
    ///   The user.
    /// </param>
    /// <param name="value">
    ///   The value to set.
    /// </param>
    /// <exception cref="ArgumentNullException">
    ///   Thrown when the <paramref name="episode"/> or <paramref name="user"/>
    ///   is <c>null</c>.
    /// </exception>
    /// <returns>
    ///   The user data for the episode and user.
    /// </returns>
    Task<IEpisodeUserData> SetEpisodeAsFavorite(IShokoEpisode episode, IUser user, bool value);

    /// <summary>
    ///   Adds any new unique tags to the user's user data for the episode.
    /// </summary>
    /// <param name="episode">
    ///   The episode to add the tags for.
    /// </param>
    /// <param name="user">
    ///   The user to add the tags for.
    /// </param>
    /// <param name="tags">
    ///   The tags to add.
    /// </param>
    /// <exception cref="ArgumentNullException">
    ///   Thrown when the <paramref name="episode"/> or <paramref name="user"/>
    ///   is <c>null</c>.
    /// </exception>
    /// <returns>
    ///   The user data for the episode and user, with any new tags added.
    /// </returns>
    Task<IEpisodeUserData> AddUserTagsForEpisode(IShokoEpisode episode, IUser user, params string[] tags);

    /// <summary>
    ///   Adds any new unique tags to the user's user data for the episode.
    /// </summary>
    /// <param name="episode">
    ///   The episode to add the tags for.
    /// </param>
    /// <param name="user">
    ///   The user to add the tags for.
    /// </param>
    /// <param name="tags">
    ///   The tags to add.
    /// </param>
    /// <exception cref="ArgumentNullException">
    ///   Thrown when the <paramref name="episode"/> or <paramref name="user"/>
    ///   is <c>null</c>.
    /// </exception>
    /// <returns>
    ///   The user data for the episode and user, with any new tags added.
    /// </returns>
    Task<IEpisodeUserData> AddUserTagsForEpisode(IShokoEpisode episode, IUser user, IEnumerable<string>? tags);

    /// <summary>
    ///   Removes the selected tags from the user's user data for the episode.
    /// </summary>
    /// <param name="episode">
    ///   The episode to remove the tags for.
    /// </param>
    /// <param name="user">
    ///   The user to remove the tags for.
    /// </param>
    /// <param name="tags">
    ///   The tags to remove.
    /// </param>
    /// <exception cref="ArgumentNullException">
    ///   Thrown when the <paramref name="episode"/> or <paramref name="user"/>
    ///   is <c>null</c>.
    /// </exception>
    /// <returns>
    ///   The user data for the episode and user, with any removed tags removed.
    /// </returns>
    Task<IEpisodeUserData> RemoveUserTagsForEpisode(IShokoEpisode episode, IUser user, params string[] tags);

    /// <summary>
    ///   Removes the selected tags from the user's user data for the episode.
    /// </summary>
    /// <param name="episode">
    ///   The episode to remove the tags for.
    /// </param>
    /// <param name="user">
    ///   The user to remove the tags for.
    /// </param>
    /// <param name="tags">
    ///   The tags to remove.
    /// </param>
    /// <exception cref="ArgumentNullException">
    ///   Thrown when the <paramref name="episode"/> or <paramref name="user"/>
    ///   is <c>null</c>.
    /// </exception>
    /// <returns>
    ///   The user data for the episode and user, with any removed tags removed.
    /// </returns>
    Task<IEpisodeUserData> RemoveUserTagsForEpisode(IShokoEpisode episode, IUser user, IEnumerable<string>? tags);

    /// <summary>
    ///   Sets the tags for the user's user data for the episode.
    /// </summary>
    /// <param name="episode">
    ///   The episode to set the tags for.
    /// </param>
    /// <param name="user">
    ///   The user to set the tags for.
    /// </param>
    /// <param name="tags">
    ///   The tags to set. Duplicates will be ignored. Set to an empty
    ///   collection to remove all user tags.
    /// </param>
    /// <exception cref="ArgumentNullException">
    ///   Thrown when the <paramref name="episode"/> or <paramref name="user"/>
    ///   is <c>null</c>.
    /// </exception>
    /// <returns>
    ///   The user data for the episode and user, with the user tags updated to
    ///   an unique and sorted list of the provided tags.
    /// </returns>
    Task<IEpisodeUserData> SetUserTagsForEpisode(IShokoEpisode episode, IUser user, IEnumerable<string>? tags);

    /// <summary>
    ///   Rate an episode.
    /// </summary>
    /// <remarks>
    ///   If the user is an AniDB user, the rating will also be sent to AniDB.
    /// </remarks>
    /// <param name="episode">
    ///   The episode to rate.
    /// </param>
    /// <param name="user">
    ///   The user rating the episode.
    /// </param>
    /// <param name="userRating">
    ///   The user rating. On a scale of 0 to 10. Set to <c>null</c> or -1 to
    ///   remove the rating. All other values are invalid and will lead to an
    ///   exception being thrown.
    /// </param>
    /// <exception cref="ArgumentException">
    ///   Thrown when the <paramref name="userRating"/> is invalid, the episode
    ///   is not an instance of the internal Shoko episode class, or the AniDB
    ///   episode for the Shoko episode is not available.
    /// </exception>
    /// <exception cref="ArgumentNullException">
    ///   Thrown when the <paramref name="episode"/> is <c>null</c>.
    /// </exception>
    /// <returns>
    ///   A task representing the asynchronous operation.
    /// </returns>
    Task<IEpisodeUserData> RateEpisode(IShokoEpisode episode, IUser user, double? userRating);

    /// <summary>
    ///   Unrate an episode.
    /// </summary>
    /// <remarks>
    ///   If the user is an AniDB user, the rating will also be sent to AniDB.
    /// </remarks>
    /// <param name="episode">
    ///   The episode to unrate.
    /// </param>
    /// <param name="user">
    ///   The user unrating the episode. If <c>null</c>, uses the first available
    ///   administrator.
    /// </param>
    /// <exception cref="ArgumentException">
    ///   Thrown when the episode is not an instance of the internal Shoko
    ///   episode class, or the AniDB anime for the Shoko episode is not
    ///   available.
    /// </exception>
    /// <exception cref="ArgumentNullException">
    ///   Thrown when the <paramref name="episode"/> or <paramref name="user"/>
    ///   is <c>null</c>.
    /// </exception>
    /// <returns>
    ///   A task representing the asynchronous operation.
    /// </returns>
    Task<IEpisodeUserData> UnrateEpisode(IShokoEpisode episode, IUser user);

    /// <summary>
    ///   Saves the user data for the episode and user.
    /// </summary>
    /// <param name="user">
    ///   The user to save episode user data for.
    /// </param>
    /// <param name="episode">
    ///   The episode to save episode user data for.
    /// </param>
    /// <param name="userDataUpdate">
    ///   The update containing the details to save.
    /// </param>
    /// <param name="updateStatsNow">
    ///   Optional. When set to <c>true</c> will update the series stats after
    ///   saving. If doing multiple updates on the same series at once, it is
    ///   recommended to set this to <c>false</c> for all but the last update.
    /// </param>
    /// <exception cref="ArgumentNullException">
    ///   Thrown when <paramref name="episode"/> or <paramref name="user"/> is <c>null</c>.
    /// </exception>
    /// <returns>
    ///   The task containing the new or updated user data for the episode and user.
    /// </returns>
    Task<IEpisodeUserData> SaveEpisodeUserData(IShokoEpisode episode, IUser user, EpisodeUserDataUpdate userDataUpdate, bool updateStatsNow = true);

    /// <summary>
    ///   Imports some user data for the episode and user from the given import
    ///   source.
    /// </summary>
    /// <param name="user">
    ///   The user to save episode user data for.
    /// </param>
    /// <param name="episode">
    ///   The episode to save episode user data for.
    /// </param>
    /// <param name="userDataUpdate">
    ///   The update containing the details to save.
    /// </param>
    /// <param name="importSource">
    ///   The import source.
    /// </param>
    /// <param name="updateStatsNow">
    ///   Optional. When set to <c>true</c> will update the series stats after
    ///   saving. If doing multiple updates on the same series at once, it is
    ///   recommended to set this to <c>false</c> for all but the last update.
    /// </param>
    /// <exception cref="ArgumentNullException">
    ///   Thrown when <paramref name="episode"/> or <paramref name="user"/> is <c>null</c>.
    /// </exception>
    /// <returns>
    ///   The task containing the new or updated user data for the episode and user.
    /// </returns>
    Task<IEpisodeUserData> ImportEpisodeUserData(IShokoEpisode episode, IUser user, EpisodeUserDataUpdate userDataUpdate, string importSource, bool updateStatsNow = true);

    #endregion

    #region Series User Data

    /// <summary>
    /// Dispatched when series user data is saved.
    /// </summary>
    event EventHandler<SeriesUserDataSavedEventArgs>? SeriesUserDataSaved;

    /// <summary>
    /// Gets user series data for the given user and series.
    /// </summary>
    /// <param name="series">
    ///   The series.
    /// </param>
    /// <param name="user">
    ///   The user.
    /// </param>
    /// <exception cref="ArgumentNullException">
    ///   Thrown when the <paramref name="series"/> or <paramref name="user"/>
    ///   is <c>null</c>.
    /// </exception>
    /// <returns>The user series data.</returns>
    ISeriesUserData GetSeriesUserData(IShokoSeries series, IUser user);

    /// <summary>
    ///   Gets all user series data for the given user.
    /// </summary>
    /// <param name="user">
    ///   The user.
    /// </param>
    /// <exception cref="ArgumentNullException">
    ///   Thrown when the <paramref name="user"/> is <c>null</c>.
    /// </exception>
    /// <returns>
    ///   The user series data.
    /// </returns>
    IEnumerable<ISeriesUserData> GetSeriesUserDataForUser(IUser user);

    /// <summary>
    ///   Gets all user series data for the given series.
    /// </summary>
    /// <param name="series">
    ///   The series.
    /// </param>
    /// <exception cref="ArgumentNullException">
    ///   Thrown when the <paramref name="series"/> is <c>null</c>.
    /// </exception>
    /// <returns>
    ///   A list of user series data.
    /// </returns>
    IReadOnlyList<ISeriesUserData> GetSeriesUserDataForSeries(IShokoSeries series);

    /// <summary>
    ///   Toggles the favorite status of a series.
    /// </summary>
    /// <param name="series">
    ///   The series.
    /// </param>
    /// <param name="user">
    ///   The user.
    /// </param>
    /// <exception cref="ArgumentNullException">
    ///   Thrown when the <paramref name="series"/> or <paramref name="user"/>
    ///   is <c>null</c>.
    /// </exception>
    /// <returns>
    ///   The user data for the series and user.
    /// </returns>
    Task<ISeriesUserData> ToggleSeriesAsFavorite(IShokoSeries series, IUser user);

    /// <summary>
    ///   Sets the favorite status of a series.
    /// </summary>
    /// <param name="series">
    ///   The series.
    /// </param>
    /// <param name="user">
    ///   The user.
    /// </param>
    /// <param name="value">
    ///   The value to set.
    /// </param>
    /// <exception cref="ArgumentNullException">
    ///   Thrown when the <paramref name="series"/> or <paramref name="user"/>
    ///   is <c>null</c>.
    /// </exception>
    /// <returns>
    ///   The user data for the series and user.
    /// </returns>
    Task<ISeriesUserData> SetSeriesAsFavorite(IShokoSeries series, IUser user, bool value);

    /// <summary>
    ///   Adds any new unique tags to the user's user data for the series.
    /// </summary>
    /// <param name="series">
    ///   The series to add the tags for.
    /// </param>
    /// <param name="user">
    ///   The user to add the tags for.
    /// </param>
    /// <param name="tags">
    ///   The tags to add.
    /// </param>
    /// <exception cref="ArgumentNullException">
    ///   Thrown when the <paramref name="series"/> or <paramref name="user"/>
    ///   is <c>null</c>.
    /// </exception>
    /// <returns>
    ///   The user data for the series and user, with any new tags added.
    /// </returns>
    Task<ISeriesUserData> AddUserTagsForSeries(IShokoSeries series, IUser user, params string[] tags);

    /// <summary>
    ///   Adds any new unique tags to the user's user data for the series.
    /// </summary>
    /// <param name="series">
    ///   The series to add the tags for.
    /// </param>
    /// <param name="user">
    ///   The user to add the tags for.
    /// </param>
    /// <param name="tags">
    ///   The tags to add.
    /// </param>
    /// <exception cref="ArgumentNullException">
    ///   Thrown when the <paramref name="series"/> or <paramref name="user"/>
    ///   is <c>null</c>.
    /// </exception>
    /// <returns>
    ///   The user data for the series and user, with any new tags added.
    /// </returns>
    Task<ISeriesUserData> AddUserTagsForSeries(IShokoSeries series, IUser user, IEnumerable<string>? tags);

    /// <summary>
    ///   Removes the selected tags from the user's user data for the series.
    /// </summary>
    /// <param name="series">
    ///   The series to remove the tags for.
    /// </param>
    /// <param name="user">
    ///   The user to remove the tags for.
    /// </param>
    /// <param name="tags">
    ///   The tags to remove.
    /// </param>
    /// <exception cref="ArgumentNullException">
    ///   Thrown when the <paramref name="series"/> or <paramref name="user"/>
    ///   is <c>null</c>.
    /// </exception>
    /// <returns>
    ///   The user data for the series and user, with any removed tags removed.
    /// </returns>
    Task<ISeriesUserData> RemoveUserTagsForSeries(IShokoSeries series, IUser user, params string[] tags);

    /// <summary>
    ///   Removes the selected tags from the user's user data for the series.
    /// </summary>
    /// <param name="series">
    ///   The series to remove the tags for.
    /// </param>
    /// <param name="user">
    ///   The user to remove the tags for.
    /// </param>
    /// <param name="tags">
    ///   The tags to remove.
    /// </param>
    /// <exception cref="ArgumentNullException">
    ///   Thrown when the <paramref name="series"/> or <paramref name="user"/>
    ///   is <c>null</c>.
    /// </exception>
    /// <returns>
    ///   The user data for the series and user, with any removed tags removed.
    /// </returns>
    Task<ISeriesUserData> RemoveUserTagsForSeries(IShokoSeries series, IUser user, IEnumerable<string>? tags);

    /// <summary>
    ///   Sets the tags for the user's user data for the series.
    /// </summary>
    /// <param name="series">
    ///   The series to set the tags for.
    /// </param>
    /// <param name="user">
    ///   The user to set the tags for.
    /// </param>
    /// <param name="tags">
    ///   The tags to set. Duplicates will be ignored. Set to an empty
    ///   collection to remove all user tags.
    /// </param>
    /// <exception cref="ArgumentNullException">
    ///   Thrown when the <paramref name="series"/> or <paramref name="user"/>
    ///   is <c>null</c>.
    /// </exception>
    /// <returns>
    ///   The user data for the series and user, with the user tags updated to
    ///   an unique and sorted list of the provided tags.
    /// </returns>
    Task<ISeriesUserData> SetUserTagsForSeries(IShokoSeries series, IUser user, IEnumerable<string>? tags);

    /// <summary>
    ///   Rate a series.
    /// </summary>
    /// <remarks>
    ///   If the user is an AniDB user, the rating will also be sent to AniDB.
    /// </remarks>
    /// <param name="series">
    ///   The series to rate.
    /// </param>
    /// <param name="user">
    ///   The user rating the series.
    /// </param>
    /// <param name="userRating">
    ///   The user rating. On a scale of 0 to 10. Set to <c>null</c> or -1 to
    ///   remove the rating. All other values are invalid and will lead to an
    ///   exception being thrown.
    /// </param>
    /// <param name="voteType">
    ///   The vote type. Applies to AniDB upstream, but we're supporting
    ///   it for consistency. If not set, will be inferred based on if the
    ///   series has ended already.
    /// </param>
    /// <exception cref="ArgumentException">
    ///   Thrown when the <paramref name="userRating"/> is invalid, the series
    ///   is not an instance of the internal Shoko series class, or the AniDB
    ///   anime for the Shoko series is not available.
    /// </exception>
    /// <exception cref="ArgumentNullException">
    ///   Thrown when the <paramref name="series"/> or <paramref name="user"/>
    ///   is <c>null</c>.
    /// </exception>
    /// <returns>
    ///   A task representing the asynchronous operation.
    /// </returns>
    Task<ISeriesUserData> RateSeries(IShokoSeries series, IUser user, double? userRating, SeriesVoteType? voteType = null);

    /// <summary>
    ///   Unrate a series.
    /// </summary>
    /// <remarks>
    ///   If the user is an AniDB user, the rating will also be sent to AniDB.
    /// </remarks>
    /// <param name="series">
    ///   The series to unrate.
    /// </param>
    /// <param name="user">
    ///   The user unrating the series.
    /// </param>
    /// <exception cref="ArgumentException">
    ///   Thrown when the series is not an instance of the internal Shoko series
    ///   class, or the AniDB anime for the Shoko series is not available.
    /// </exception>
    /// <exception cref="ArgumentNullException">
    ///   Thrown when the <paramref name="series"/> or <paramref name="user"/>
    ///   is <c>null</c>.
    /// </exception>
    /// <returns>
    ///   A task representing the asynchronous operation.
    /// </returns>
    Task<ISeriesUserData> UnrateSeries(IShokoSeries series, IUser user);

    /// <summary>
    ///   Saves the user data for the series and user.
    /// </summary>
    /// <param name="user">
    ///   The user to save series user data for.
    /// </param>
    /// <param name="series">
    ///   The series to save series user data for.
    /// </param>
    /// <param name="userDataUpdate">
    ///   The update containing the details to save.
    /// </param>
    /// <exception cref="ArgumentNullException">
    ///   Thrown when <paramref name="series"/> or <paramref name="user"/> is <c>null</c>.
    /// </exception>
    /// <returns>
    ///   The task containing the new or updated user data for the series and user.
    /// </returns>
    Task<ISeriesUserData> SaveSeriesUserData(IShokoSeries series, IUser user, SeriesUserDataUpdate userDataUpdate);

    /// <summary>
    ///   Imports some user data for the series and user from the given import
    ///   source.
    /// </summary>
    /// <param name="user">
    ///   The user to save series user data for.
    /// </param>
    /// <param name="series">
    ///   The series to save series user data for.
    /// </param>
    /// <param name="userDataUpdate">
    ///   The update containing the details to save.
    /// </param>
    /// <param name="importSource">
    ///   The import source.
    /// </param>
    /// <exception cref="ArgumentNullException">
    ///   Thrown when <paramref name="series"/> or <paramref name="user"/> is <c>null</c>.
    /// </exception>
    /// <returns>
    ///   The task containing the new or updated user data for the series and user.
    /// </returns>
    Task<ISeriesUserData> ImportSeriesUserData(IShokoSeries series, IUser user, SeriesUserDataUpdate userDataUpdate, string importSource);

    #endregion
}
