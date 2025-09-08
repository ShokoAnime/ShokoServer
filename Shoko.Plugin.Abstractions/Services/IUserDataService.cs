

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Shoko.Plugin.Abstractions.DataModels;
using Shoko.Plugin.Abstractions.DataModels.Shoko;
using Shoko.Plugin.Abstractions.Enums;
using Shoko.Plugin.Abstractions.Events;

namespace Shoko.Plugin.Abstractions.Services;
/// <summary>
/// User data service.
/// </summary>
public interface IUserDataService
{
    #region Video User Data

    /// <summary>
    /// Dispatched when video user data is saved.
    /// </summary>
    event EventHandler<VideoUserDataSavedEventArgs> VideoUserDataSaved;

    /// <summary>
    /// Gets user video data for the given user and video.
    /// </summary>
    /// <param name="userID">The user ID.</param>
    /// <param name="videoID">The video ID.</param>
    /// <returns>The user video data.</returns>
    IVideoUserData? GetVideoUserData(int userID, int videoID);

    /// <summary>
    /// Gets all user video data for the given user.
    /// </summary>
    /// <param name="userID">The user ID.</param>
    /// <returns>The user video data.</returns>
    IReadOnlyList<IVideoUserData> GetVideoUserDataForUser(int userID);

    /// <summary>
    /// Gets all user video data for the given video.
    /// </summary>
    /// <param name="videoID">The video ID.</param>
    /// <returns>A list of user video data.</returns>
    IReadOnlyList<IVideoUserData> GetVideoUserDataForVideo(int videoID);

    /// <summary>
    /// Sets the video watch status.
    /// </summary>
    /// <param name="user">The user.</param>
    /// <param name="video">The video.</param>
    /// <param name="watched">Optional. If set to <c>true</c> the video is watched; otherwise, <c>false</c>.</param>
    /// <param name="watchedAt">Optional. The watched at.</param>
    /// <param name="reason">Optional. The reason why the video watch status was updated.</param>
    /// <param name="updateStatsNow">if set to <c>true</c> will update the series stats immediately after saving.</param>
    /// <exception cref="ArgumentNullException">The <paramref name="user"/> is null.</exception>
    /// <exception cref="ArgumentNullException">The <paramref name="video"/> is null.</exception>
    /// <returns>A task.</returns>
    Task SetVideoWatchedStatus(IShokoUser user, IVideo video, bool watched = true, DateTime? watchedAt = null, UserDataSaveReason reason = UserDataSaveReason.None, bool updateStatsNow = true);

    /// <summary>
    /// Saves the video user data.
    /// </summary>
    /// <param name="user">The user.</param>
    /// <param name="video">The video.</param>
    /// <param name="userDataUpdate">The user data update.</param>
    /// <param name="reason">The reason why the user data was updated.</param>
    /// <param name="updateStatsNow">if set to <c>true</c> will update the series stats immediately after saving.</param>
    /// <exception cref="ArgumentNullException">The <paramref name="user"/> is null.</exception>
    /// <exception cref="ArgumentNullException">The <paramref name="video"/> is null.</exception>
    /// <returns>The task containing the new or updated user video data.</returns>
    Task<IVideoUserData> SaveVideoUserData(IShokoUser user, IVideo video, VideoUserDataUpdate userDataUpdate, UserDataSaveReason reason = UserDataSaveReason.None, bool updateStatsNow = true);

    #endregion

    #region Series User Data

    /// <summary>
    /// Dispatched when a user submits a rating/vote for a series.
    /// </summary>
    event EventHandler<SeriesVotedEventArgs> SeriesVoted;

    /// <summary>
    /// Votes on a series.
    /// </summary>
    /// <param name="series">The series to vote on.</param>
    /// <param name="voteValue">The vote value.</param>
    /// <param name="voteType">The type of vote (Permanent/Temporary).</param>
    /// <param name="user">The user voting.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task VoteOnSeries(IShokoSeries series, decimal voteValue, VoteType voteType, IShokoUser user);

    #endregion

    #region Episode User Data

    /// <summary>
    /// Sets the episode watch status.
    /// </summary>
    /// <remarks>
    /// Attempting to set the episode watch status for an episode without files will result in a no-op.
    /// </remarks>
    /// <param name="user">The user.</param>
    /// <param name="episode">The episode.</param>
    /// <param name="watched">Optional. If set to <c>true</c> the episode is watched; otherwise, <c>false</c>.</param>
    /// <param name="watchedAt">Optional. The watched at.</param>
    /// <param name="reason">Optional. The reason why the episode watch status was updated.</param>
    /// <param name="updateStatsNow">if set to <c>true</c> will update the series stats immediately after saving.</param>
    /// <exception cref="ArgumentNullException">The <paramref name="user"/> is null.</exception>
    /// <exception cref="ArgumentNullException">The <paramref name="episode"/> is null.</exception>
    /// <returns>The task containing the result if the episode watch status was updated.</returns>
    Task<bool> SetEpisodeWatchedStatus(IShokoUser user, IShokoEpisode episode, bool watched = true, DateTime? watchedAt = null, UserDataSaveReason reason = UserDataSaveReason.None, bool updateStatsNow = true);

    #endregion
}
