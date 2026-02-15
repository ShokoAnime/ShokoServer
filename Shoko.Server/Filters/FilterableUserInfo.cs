using System;
using System.Collections.Generic;
using Shoko.Abstractions.Filtering;

namespace Shoko.Server.Filters;

public class FilterableUserInfo : IFilterableUserInfo
{
    private readonly Lazy<bool> _hasVotes;
    private readonly Lazy<bool> _hasPermanentVotes;
    private readonly Lazy<bool> _missingPermanentVotes;
    private readonly Lazy<int> _seriesVoteCount;
    private readonly Lazy<int> _seriesTemporaryVoteCount;
    private readonly Lazy<int> _seriesPermanentVoteCount;
    private readonly Lazy<double> _highestUserRating;
    private readonly Lazy<bool> _isFavorite;
    private readonly Lazy<IReadOnlySet<string>> _userTags;
    private readonly Lazy<DateTime?> _lastWatchedDate;
    private readonly Lazy<double> _lowestUserRating;
    private readonly Lazy<int> _unwatchedEpisodes;
    private readonly Lazy<DateTime?> _watchedDate;
    private readonly Lazy<int> _watchedEpisodes;

    public bool IsFavorite => _isFavorite.Value;

    public Func<bool> IsFavoriteDelegate
    {
        init => _isFavorite = new Lazy<bool>(value);
    }

    public IReadOnlySet<string> UserTags => _userTags.Value;

    public Func<IReadOnlySet<string>> UserTagsDelegate
    {
        init => _userTags = new Lazy<IReadOnlySet<string>>(value);
    }

    public int WatchedEpisodes => _watchedEpisodes.Value;

    public Func<int> WatchedEpisodesDelegate
    {
        init => _watchedEpisodes = new Lazy<int>(value);
    }

    public int UnwatchedEpisodes => _unwatchedEpisodes.Value;

    public Func<int> UnwatchedEpisodesDelegate
    {
        init => _unwatchedEpisodes = new Lazy<int>(value);
    }

    public bool HasVotes => _hasVotes.Value;

    public Func<bool> HasVotesDelegate
    {
        init => _hasVotes = new Lazy<bool>(value);
    }

    public bool HasPermanentVotes => _hasPermanentVotes.Value;

    public Func<bool> HasPermanentVotesDelegate
    {
        init => _hasPermanentVotes = new Lazy<bool>(value);
    }

    public bool MissingPermanentVotes => _missingPermanentVotes.Value;

    public Func<bool> MissingPermanentVotesDelegate
    {
        init => _missingPermanentVotes = new Lazy<bool>(value);
    }

    public int SeriesVoteCount => _seriesVoteCount.Value;

    public required Func<int> SeriesVoteCountDelegate
    {
        init => _seriesVoteCount = new Lazy<int>(value);
    }

    public int SeriesTemporaryVoteCount => _seriesTemporaryVoteCount.Value;

    public required Func<int> SeriesTemporaryVoteCountDelegate
    {
        init => _seriesTemporaryVoteCount = new Lazy<int>(value);
    }

    public int SeriesPermanentVoteCount => _seriesPermanentVoteCount.Value;

    public required Func<int> SeriesPermanentVoteCountDelegate
    {
        init => _seriesPermanentVoteCount = new Lazy<int>(value);
    }

    public DateTime? WatchedDate => _watchedDate.Value;

    public Func<DateTime?> WatchedDateDelegate
    {
        init => _watchedDate = new Lazy<DateTime?>(value);
    }

    public DateTime? LastWatchedDate => _lastWatchedDate.Value;

    public Func<DateTime?> LastWatchedDateDelegate
    {
        init => _lastWatchedDate = new Lazy<DateTime?>(value);
    }

    public double LowestUserRating => _lowestUserRating.Value;

    public Func<double> LowestUserRatingDelegate
    {
        init => _lowestUserRating = new Lazy<double>(value);
    }

    public double HighestUserRating => _highestUserRating.Value;

    public Func<double> HighestUserRatingDelegate
    {
        init => _highestUserRating = new Lazy<double>(value);
    }
}
