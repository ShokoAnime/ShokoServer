using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using Shoko.Abstractions.Metadata.Shoko;
using Shoko.Abstractions.UserData.Enums;

namespace Shoko.Abstractions.UserData;

/// <summary>
///   Represents user-specific data associated with a Shoko series.
/// </summary>
public interface ISeriesUserData : IUserData
{
    /// <summary>
    ///   Gets the ID of the Shoko series.
    /// </summary>
    int SeriesID { get; }

    #region Series Data

    /// <summary>
    ///   Indicates that the user has marked the series as favorite.
    /// </summary>
    bool IsFavorite { get; }

    /// <summary>
    ///   The unique tags assigned to the series by the user.
    /// </summary>
    IReadOnlyList<string> UserTags { get; }

    /// <summary>
    ///   The user rating, on a scale of 1-10 with a maximum of 1 decimal
    ///   places, or <c>null</c> if unrated.
    /// </summary>
    double? UserRating { get; }

    /// <summary>
    ///   The user rating vote type.
    /// </summary>
    SeriesVoteType? UserRatingVoteType { get; }

    #endregion

    #region Episode Data

    /// <summary>
    ///   Gets the date and time when an episode linked to the series was last
    ///   played to completion.
    /// </summary>
    DateTime? LastEpisodePlayedAt { get; }

    /// <summary>
    ///   The number of normal episodes or specials that have not been watched
    ///   to completion, and are not hidden.
    /// </summary>
    int UnwatchedEpisodeCount { get; }

    /// <summary>
    ///   The number of normal episodes or specials that have not been watched
    ///   to completion, and are hidden.
    /// </summary>
    int HiddenUnwatchedEpisodeCount { get; }

    /// <summary>
    ///   The number of normal episodes or specials that have been watched to
    ///   completion.
    /// </summary>
    int WatchedEpisodeCount { get; }

    #endregion

    #region Video Data

    /// <summary>
    ///   Gets the number of times any videos linked to the series has been
    ///   played to completion.
    /// </summary>
    int VideoPlaybackCount { get; }

    /// <summary>
    ///   Gets the date and time when a video linked to the series was last
    ///   played to completion.
    /// </summary>
    DateTime? LastVideoPlayedAt { get; }

    /// <summary>
    ///   Gets the date and time when the user data for a video linked to the
    ///   series was last updated, regardless of if it was watched to completion
    ///   or not. Can be used to determine continue watching and next-up order
    ///   for the series, etc..
    /// </summary>
    DateTime? LastVideoUpdatedAt { get; }

    #endregion

    /// <summary>
    ///   Indicates that the series has been watched to completion at least
    ///   once by the user, be it locally or otherwise.
    /// </summary>
    bool IsWatched => UnwatchedEpisodeCount is 0;

    /// <summary>
    ///   Indicates that the user has rated the series.
    /// </summary>
    [MemberNotNullWhen(true, nameof(UserRating), nameof(UserRatingVoteType))]
    bool HasUserRating => UserRating.HasValue && UserRatingVoteType.HasValue;

    /// <summary>
    ///   Gets the Shoko Series associated with this user data, if available.
    /// </summary>
    IShokoSeries? Series { get; }
}
