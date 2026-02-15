using System;
using System.Collections.Generic;
using Shoko.Abstractions.Filtering;

namespace Shoko.Tests;

public class TestFilterableUserInfo : IFilterableUserInfo
{
    public bool IsFavorite { get; init; }
    public int WatchedEpisodes { get; init; }
    public int UnwatchedEpisodes { get; init; }
    public bool HasVotes { get; init; }
    public bool HasPermanentVotes { get; init; }
    public bool MissingPermanentVotes { get; init; }
    public DateTime? WatchedDate { get; init; }
    public DateTime? LastWatchedDate { get; init; }
    public double LowestUserRating { get; init; }
    public double HighestUserRating { get; init; }
    public IReadOnlySet<string> UserTags { get; init; }
    public int SeriesVoteCount { get; init; }
    public int SeriesTemporaryVoteCount { get; init; }
    public int SeriesPermanentVoteCount { get; init; }
}
