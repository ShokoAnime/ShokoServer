using System;

namespace Shoko.Server.Filters.Interfaces;

public interface IUserDependentFilterable : IFilterable
{
    /// <summary>
    /// Probably will be removed in the future. Custom Tags would handle this better
    /// </summary>
    bool IsFavorite { get; init; }
    
    /// <summary>
    /// The number of episodes watched
    /// </summary>
    int WatchedEpisodes { get; init; }
    
    /// <summary>
    /// The number of episodes that have not been watched
    /// </summary>
    int UnwatchedEpisodes { get; init; }
    
    /// <summary>
    /// Has any user votes
    /// </summary>
    bool HasVotes { get; init; }
    
    /// <summary>
    /// Has permanent (after finishing) user votes
    /// </summary>
    bool HasPermanentVotes { get; init; }
    
    /// <summary>
    /// Has permanent (after finishing) user votes
    /// </summary>
    bool MissingPermanentVotes { get; init; }
    
    /// <summary>
    /// First Watched Date
    /// </summary>
    DateTime? WatchedDate { get; init; }
    
    /// <summary>
    /// Latest Watched Date
    /// </summary>
    DateTime? LastWatchedDate { get; init; }
    
    /// <summary>
    /// Lowest User Rating (0-10)
    /// </summary>
    decimal LowestUserRating { get; init; }

    /// <summary>
    /// Highest User Rating (0-10)
    /// </summary>
    public decimal HighestUserRating { get; init; }
}
