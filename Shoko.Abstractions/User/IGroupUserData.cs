using System;
using System.Collections.Generic;
using Shoko.Abstractions.Metadata.Shoko;

namespace Shoko.Abstractions.User;

/// <summary>
///   Represents user-specific data associated with a Shoko group.
/// </summary>
public interface IGroupUserData : IUserData
{
    /// <summary>
    ///   Gets the ID of the Shoko group.
    /// </summary>
    int GroupID { get; }

    #region Watch Stats

    /// <summary>
    ///   Indicates that the group has been watched to completion at least once
    ///   by the user.
    /// </summary>
    bool IsWatched => UnwatchedEpisodeCount is 0;

    /// <summary>
    ///   The number of episodes that have not been watched to completion.
    /// </summary>
    int UnwatchedEpisodeCount { get; }

    /// <summary>
    ///   The number of episodes that have been watched to completion.
    /// </summary>
    int WatchedEpisodeCount { get; }

    /// <summary>
    ///   The date and time when an episode in the group was last played to
    ///   completion.
    /// </summary>
    DateTime? LastPlayedAt { get; }

    /// <summary>
    ///   The number of times any episodes in the group have been played to
    ///   completion.
    /// </summary>
    int PlaybackCount { get; }

    #endregion

    #region Ordering / Filtering

    /// <summary>
    ///   The latest date and time when user data for an episode linked to the
    ///   group was last updated, regardless of if it was watched to completion.
    ///   Computed at runtime from the series user data.
    /// </summary>
    DateTime? LastEpisodeUpdatedAt { get; }

    /// <summary>
    ///   The latest date and time when user data for a video linked to the
    ///   group was last updated, regardless of if it was watched to completion.
    ///   Computed at runtime from the video user data.
    /// </summary>
    DateTime? LastVideoUpdatedAt { get; }

    #endregion

    #region User Data

    /// <summary>
    ///   The unique tags assigned to the group by the user.
    /// </summary>
    IReadOnlyList<string> UserTags { get; }

    #endregion

    /// <summary>
    ///   Gets the Shoko Group associated with this user data, if available.
    /// </summary>
    IShokoGroup? Group { get; }
}
