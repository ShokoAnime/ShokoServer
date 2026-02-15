using System;
using System.Collections.Generic;

namespace Shoko.Abstractions.Filtering;

/// <summary>
///   Filterable user information.
/// </summary>
public interface IFilterableUserInfo
{
    /// <summary>
    /// Probably will be removed in the future. Custom Tags would handle this better
    /// </summary>
    bool IsFavorite { get; }

    /// <summary>
    ///   All the tags the user have assigned to any of the series in the filterable.
    /// </summary>
    IReadOnlySet<string> UserTags { get; }

    /// <summary>
    /// The number of episodes watched
    /// </summary>
    int WatchedEpisodes { get; }

    /// <summary>
    /// The number of episodes that have not been watched
    /// </summary>
    int UnwatchedEpisodes { get; }

    /// <summary>
    /// Has any user votes
    /// </summary>
    bool HasVotes { get; }

    /// <summary>
    /// Has permanent (after finishing) user votes
    /// </summary>
    bool HasPermanentVotes { get; }

    /// <summary>
    /// Has permanent (after finishing) user votes
    /// </summary>
    bool MissingPermanentVotes { get; }

    /// <summary>
    /// The number of series in a group with any vote set, be it temporary or
    /// permanent.
    /// </summary>
    int SeriesVoteCount { get; }

    /// <summary>
    /// The number of series in a group with a temporary vote set.
    /// </summary>
    int SeriesTemporaryVoteCount { get; }

    /// <summary>
    /// The number of series in a group with a permanent vote set.
    /// </summary>
    int SeriesPermanentVoteCount { get; }

    /// <summary>
    /// First Watched Date
    /// </summary>
    DateTime? WatchedDate { get; }

    /// <summary>
    /// Latest Watched Date
    /// </summary>
    DateTime? LastWatchedDate { get; }

    /// <summary>
    /// Lowest User Rating on a scale of 1-10, or 0 if unrated.
    /// </summary>
    double LowestUserRating { get; }

    /// <summary>
    /// Highest User Rating on a scale of 1-10, or 0 if unrated.
    /// </summary>
    double HighestUserRating { get; }
}
