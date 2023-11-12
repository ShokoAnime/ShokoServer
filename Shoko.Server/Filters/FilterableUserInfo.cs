using System;
using System.Threading;
using Shoko.Server.Filters.Interfaces;

namespace Shoko.Server.Filters;

public class FilterableUserInfo : IFilterableUserInfo
{
    private readonly Lazy<bool> _hasPermanentVotes;
    private readonly Lazy<bool> _hasVotes;
    private readonly Lazy<decimal> _highestUserRating;
    private readonly Lazy<bool> _isFavorite;
    private readonly Lazy<DateTime?> _lastWatchedDate;
    private readonly Lazy<decimal> _lowestUserRating;
    private readonly Lazy<bool> _missingPermanentVotes;
    private readonly Lazy<int> _unwatchedEpisodes;
    private readonly Lazy<DateTime?> _watchedDate;
    private readonly Lazy<int> _watchedEpisodes;

    public bool IsFavorite => _isFavorite.Value;

    public Func<bool> IsFavoriteDelegate
    {
        init => _isFavorite = new Lazy<bool>(value);
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

    public decimal LowestUserRating => _lowestUserRating.Value;

    public Func<decimal> LowestUserRatingDelegate
    {
        init => _lowestUserRating = new Lazy<decimal>(value);
    }

    public decimal HighestUserRating => _highestUserRating.Value;

    public Func<decimal> HighestUserRatingDelegate
    {
        init => _highestUserRating = new Lazy<decimal>(value);
    }
}
