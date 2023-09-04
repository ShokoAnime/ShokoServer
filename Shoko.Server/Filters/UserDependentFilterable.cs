using System;

namespace Shoko.Server.Filters;

public class UserDependentFilterable : Filterable
{
    /// <summary>
    ///     Probably will be removed in the future. Custom Tags would handle this better
    /// </summary>
    public bool IsFavorite { get; init; }
    /// <summary>
    ///     The number of episodes watched
    /// </summary>
    public int WatchedEpisodes { get; init; }
    /// <summary>
    ///     The number of episodes that have not been watched
    /// </summary>
    public int UnwatchedEpisodes { get; init; }
    /// <summary>
    ///     Has any user votes
    /// </summary>
    public bool HasVotes { get; init; }
    /// <summary>
    ///     Has permanent (after finishing) user votes
    /// </summary>
    public bool HasPermanentVotes { get; init; }
    /// <summary>
    ///     Has permanent (after finishing) user votes
    /// </summary>
    public bool MissingPermanentVotes { get; init; }
    /// <summary>
    ///     First Watched Date
    /// </summary>
    public DateTime? WatchedDate { get; init; }
    /// <summary>
    ///     Latest Watched Date
    /// </summary>
    public DateTime? LastWatchedDate { get; init; }
    /// <summary>
    ///     Lowest User Rating (0-10)
    /// </summary>
    public decimal LowestUserRating { get; init; }
    /// <summary>
    ///     Highest User Rating (0-10)
    /// </summary>
    public decimal HighestUserRating { get; init; }
}
