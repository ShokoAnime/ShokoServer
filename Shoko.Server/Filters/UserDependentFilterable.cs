using System;
using System.Threading;
using Shoko.Server.Filters.Interfaces;

namespace Shoko.Server.Filters;

public class UserDependentFilterable : Filterable, IUserDependentFilterable
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

    public bool IsFavorite
    {
        get => _isFavorite.Value;
        init => throw new NotSupportedException();
    }

    public Func<bool> IsFavoriteDelegate
    {
        get => _isFavoriteDelegate;
        init
        {
            _isFavoriteDelegate = value;
            _isFavorite = new Lazy<bool>(_isFavoriteDelegate, LazyThreadSafetyMode.ExecutionAndPublication);
        }
    }

    public int WatchedEpisodes
    {
        get => _watchedEpisodes.Value;
        init => throw new NotSupportedException();
    }

    public Func<int> WatchedEpisodesDelegate
    {
        get => _watchedEpisodesDelegate;
        init
        {
            _watchedEpisodesDelegate = value;
            _watchedEpisodes = new Lazy<int>(_watchedEpisodesDelegate, LazyThreadSafetyMode.ExecutionAndPublication);
        }
    }

    public int UnwatchedEpisodes
    {
        get => _unwatchedEpisodes.Value;
        init => throw new NotSupportedException();
    }

    public Func<int> UnwatchedEpisodesDelegate
    {
        get => _unwatchedEpisodesDelegate;
        init
        {
            _unwatchedEpisodesDelegate = value;
            _unwatchedEpisodes = new Lazy<int>(_unwatchedEpisodesDelegate, LazyThreadSafetyMode.ExecutionAndPublication);
        }
    }

    public bool HasVotes
    {
        get => _hasVotes.Value;
        init => throw new NotSupportedException();
    }

    public Func<bool> HasVotesDelegate
    {
        get => _hasVotesDelegate;
        init
        {
            _hasVotesDelegate = value;
            _hasVotes = new Lazy<bool>(_hasVotesDelegate, LazyThreadSafetyMode.ExecutionAndPublication);
        }
    }

    public bool HasPermanentVotes
    {
        get => _hasPermanentVotes.Value;
        init => throw new NotSupportedException();
    }

    public Func<bool> HasPermanentVotesDelegate
    {
        get => _hasPermanentVotesDelegate;
        init
        {
            _hasPermanentVotesDelegate = value;
            _hasPermanentVotes = new Lazy<bool>(_hasPermanentVotesDelegate, LazyThreadSafetyMode.ExecutionAndPublication);
        }
    }

    public bool MissingPermanentVotes
    {
        get => _missingPermanentVotes.Value;
        init => throw new NotSupportedException();
    }

    public Func<bool> MissingPermanentVotesDelegate
    {
        get => _missingPermanentVotesDelegate;
        init
        {
            _missingPermanentVotesDelegate = value;
            _missingPermanentVotes = new Lazy<bool>(_missingPermanentVotesDelegate, LazyThreadSafetyMode.ExecutionAndPublication);
        }
    }

    public DateTime? WatchedDate
    {
        get => _watchedDate.Value;
        init => throw new NotSupportedException();
    }

    public Func<DateTime?> WatchedDateDelegate
    {
        get => _watchedDateDelegate;
        init
        {
            _watchedDateDelegate = value;
            _watchedDate = new Lazy<DateTime?>(_watchedDateDelegate, LazyThreadSafetyMode.ExecutionAndPublication);
        }
    }

    public DateTime? LastWatchedDate
    {
        get => _lastWatchedDate.Value;
        init => throw new NotSupportedException();
    }

    public Func<DateTime?> LastWatchedDateDelegate
    {
        get => _lastWatchedDateDelegate;
        init
        {
            _lastWatchedDateDelegate = value;
            _lastWatchedDate = new Lazy<DateTime?>(_lastWatchedDateDelegate, LazyThreadSafetyMode.ExecutionAndPublication);
        }
    }

    public decimal LowestUserRating
    {
        get => _lowestUserRating.Value;
        init => throw new NotSupportedException();
    }

    public Func<decimal> LowestUserRatingDelegate
    {
        get => _lowestUserRatingDelegate;
        init
        {
            _lowestUserRatingDelegate = value;
            _lowestUserRating = new Lazy<decimal>(_lowestUserRatingDelegate, LazyThreadSafetyMode.ExecutionAndPublication);
        }
    }

    public decimal HighestUserRating
    {
        get => _highestUserRating.Value;
        init => throw new NotSupportedException();
    }

    public Func<decimal> HighestUserRatingDelegate
    {
        get => _highestUserRatingDelegate;
        init
        {
            _highestUserRatingDelegate = value;
            _highestUserRating = new Lazy<decimal>(_highestUserRatingDelegate, LazyThreadSafetyMode.ExecutionAndPublication);
        }
    }
}
