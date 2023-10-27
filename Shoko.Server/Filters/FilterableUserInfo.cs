using System;
using System.Threading;
using Shoko.Server.Filters.Interfaces;

namespace Shoko.Server.Filters;

public class FilterableUserInfo : IFilterableUserInfo
{
    private readonly Lazy<bool> _hasPermanentVotes;
    private readonly Func<bool> _hasPermanentVotesDelegate;

    private readonly Lazy<bool> _hasVotes;
    private readonly Func<bool> _hasVotesDelegate;

    private readonly Lazy<decimal> _highestUserRating;
    private readonly Func<decimal> _highestUserRatingDelegate;

    private readonly Lazy<bool> _isFavorite;
    private readonly Func<bool> _isFavoriteDelegate;

    private readonly Lazy<DateTime?> _lastWatchedDate;
    private readonly Func<DateTime?> _lastWatchedDateDelegate;

    private readonly Lazy<decimal> _lowestUserRating;
    private readonly Func<decimal> _lowestUserRatingDelegate;

    private readonly Lazy<bool> _missingPermanentVotes;
    private readonly Func<bool> _missingPermanentVotesDelegate;

    private readonly Lazy<int> _unwatchedEpisodes;
    private readonly Func<int> _unwatchedEpisodesDelegate;

    private readonly Lazy<DateTime?> _watchedDate;
    private readonly Func<DateTime?> _watchedDateDelegate;

    private readonly Lazy<int> _watchedEpisodes;
    private readonly Func<int> _watchedEpisodesDelegate;

    public bool IsFavorite => _isFavorite.Value;

    public Func<bool> IsFavoriteDelegate
    {
        init
        {
            _isFavoriteDelegate = value;
            _isFavorite = new Lazy<bool>(_isFavoriteDelegate, LazyThreadSafetyMode.None);
        }
    }

    public int WatchedEpisodes => _watchedEpisodes.Value;

    public Func<int> WatchedEpisodesDelegate
    {
        init
        {
            _watchedEpisodesDelegate = value;
            _watchedEpisodes = new Lazy<int>(_watchedEpisodesDelegate, LazyThreadSafetyMode.None);
        }
    }

    public int UnwatchedEpisodes => _unwatchedEpisodes.Value;

    public Func<int> UnwatchedEpisodesDelegate
    {
        init
        {
            _unwatchedEpisodesDelegate = value;
            _unwatchedEpisodes = new Lazy<int>(_unwatchedEpisodesDelegate, LazyThreadSafetyMode.None);
        }
    }

    public bool HasVotes => _hasVotes.Value;

    public Func<bool> HasVotesDelegate
    {
        init
        {
            _hasVotesDelegate = value;
            _hasVotes = new Lazy<bool>(_hasVotesDelegate, LazyThreadSafetyMode.None);
        }
    }

    public bool HasPermanentVotes => _hasPermanentVotes.Value;

    public Func<bool> HasPermanentVotesDelegate
    {
        init
        {
            _hasPermanentVotesDelegate = value;
            _hasPermanentVotes = new Lazy<bool>(_hasPermanentVotesDelegate, LazyThreadSafetyMode.None);
        }
    }

    public bool MissingPermanentVotes => _missingPermanentVotes.Value;

    public Func<bool> MissingPermanentVotesDelegate
    {
        init
        {
            _missingPermanentVotesDelegate = value;
            _missingPermanentVotes = new Lazy<bool>(_missingPermanentVotesDelegate, LazyThreadSafetyMode.None);
        }
    }

    public DateTime? WatchedDate => _watchedDate.Value;

    public Func<DateTime?> WatchedDateDelegate
    {
        init
        {
            _watchedDateDelegate = value;
            _watchedDate = new Lazy<DateTime?>(_watchedDateDelegate, LazyThreadSafetyMode.None);
        }
    }

    public DateTime? LastWatchedDate => _lastWatchedDate.Value;

    public Func<DateTime?> LastWatchedDateDelegate
    {
        init
        {
            _lastWatchedDateDelegate = value;
            _lastWatchedDate = new Lazy<DateTime?>(_lastWatchedDateDelegate, LazyThreadSafetyMode.None);
        }
    }

    public decimal LowestUserRating => _lowestUserRating.Value;

    public Func<decimal> LowestUserRatingDelegate
    {
        init
        {
            _lowestUserRatingDelegate = value;
            _lowestUserRating = new Lazy<decimal>(_lowestUserRatingDelegate, LazyThreadSafetyMode.None);
        }
    }

    public decimal HighestUserRating => _highestUserRating.Value;

    public Func<decimal> HighestUserRatingDelegate
    {
        init
        {
            _highestUserRatingDelegate = value;
            _highestUserRating = new Lazy<decimal>(_highestUserRatingDelegate, LazyThreadSafetyMode.None);
        }
    }
}
