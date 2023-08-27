using System;
using System.Collections.Generic;
using Shoko.Server.Models.Filters.Interfaces;

namespace Shoko.Server.Models.Filters;

public class UserDependentFilterable : Filterable, IUserDependentFilterable
{
    public bool IsFavorite { get; init; }
    public int WatchedEpisodes { get; init; }
    public int UnwatchedEpisodes { get; init; }
    public bool HasVotes { get; init; }
    public bool HasPermanentVotes { get; init; }
    public bool MissingPermanentVotes { get; init; }
    public DateTime? WatchedDate { get; init; }
    public DateTime? LastWatchedDate { get; init; }
    public decimal LowestUserRating { get; init; }
    public decimal HighestUserRating { get; init; }
}
