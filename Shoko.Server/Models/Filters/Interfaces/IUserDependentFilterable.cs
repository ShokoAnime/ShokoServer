using System;
using System.Collections.Generic;

namespace Shoko.Server.Models.Filters.Interfaces;

public interface IUserDependentFilterable : IFilterable
{
    /// <summary>
    /// Probably will be removed in the future. Custom Tags would handle this better
    /// </summary>
    bool IsFavorite { get; }

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
    /// First Watched Date
    /// </summary>
    DateTime? WatchedDate { get; }

    /// <summary>
    /// Latest Watched Date
    /// </summary>
    DateTime? LastWatchedDate { get; }

    /// <summary>
    /// Lowest User Rating (0-10)
    /// </summary>
    decimal LowestUserRating { get; }

    /// <summary>
    /// Highest User Rating (0-10)
    /// </summary>
    decimal HighestUserRating { get; }
}
