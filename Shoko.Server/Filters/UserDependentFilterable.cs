using System;
using System.Threading;

namespace Shoko.Server.Filters;

public class UserDependentFilterable : Filterable
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

    /// <summary>
    ///     Probably will be removed in the future. Custom Tags would handle this better
    /// </summary>
    public bool IsFavorite => _isFavorite.Value;

    public Func<bool> IsFavoriteDelegate
    {
        get => _isFavoriteDelegate;
        init
        {
            _isFavoriteDelegate = value;
            _isFavorite = new Lazy<bool>(_isFavoriteDelegate, LazyThreadSafetyMode.ExecutionAndPublication);
        }
    }

    /// <summary>
    ///     The number of episodes watched
    /// </summary>
    public int WatchedEpisodes => _watchedEpisodes.Value;

    public Func<int> WatchedEpisodesDelegate
    {
        get => _watchedEpisodesDelegate;
        init
        {
            _watchedEpisodesDelegate = value;
            _watchedEpisodes = new Lazy<int>(_watchedEpisodesDelegate, LazyThreadSafetyMode.ExecutionAndPublication);
        }
    }

    /// <summary>
    ///     The number of episodes that have not been watched
    /// </summary>
    public int UnwatchedEpisodes => _unwatchedEpisodes.Value;

    public Func<int> UnwatchedEpisodesDelegate
    {
        get => _unwatchedEpisodesDelegate;
        init
        {
            _unwatchedEpisodesDelegate = value;
            _unwatchedEpisodes = new Lazy<int>(_unwatchedEpisodesDelegate, LazyThreadSafetyMode.ExecutionAndPublication);
        }
    }

    /// <summary>
    ///     Has any user votes
    /// </summary>
    public bool HasVotes => _hasVotes.Value;

    public Func<bool> HasVotesDelegate
    {
        get => _hasVotesDelegate;
        init
        {
            _hasVotesDelegate = value;
            _hasVotes = new Lazy<bool>(_hasVotesDelegate, LazyThreadSafetyMode.ExecutionAndPublication);
        }
    }

    /// <summary>
    ///     Has permanent (after finishing) user votes
    /// </summary>
    public bool HasPermanentVotes => _hasPermanentVotes.Value;

    public Func<bool> HasPermanentVotesDelegate
    {
        get => _hasPermanentVotesDelegate;
        init
        {
            _hasPermanentVotesDelegate = value;
            _hasPermanentVotes = new Lazy<bool>(_hasPermanentVotesDelegate, LazyThreadSafetyMode.ExecutionAndPublication);
        }
    }

    /// <summary>
    ///     Has permanent (after finishing) user votes
    /// </summary>
    public bool MissingPermanentVotes => _missingPermanentVotes.Value;

    public Func<bool> MissingPermanentVotesDelegate
    {
        get => _missingPermanentVotesDelegate;
        init
        {
            _missingPermanentVotesDelegate = value;
            _missingPermanentVotes = new Lazy<bool>(_missingPermanentVotesDelegate, LazyThreadSafetyMode.ExecutionAndPublication);
        }
    }

    /// <summary>
    ///     First Watched Date
    /// </summary>
    public DateTime? WatchedDate => _watchedDate.Value;

    public Func<DateTime?> WatchedDateDelegate
    {
        get => _watchedDateDelegate;
        init
        {
            _watchedDateDelegate = value;
            _watchedDate = new Lazy<DateTime?>(_watchedDateDelegate, LazyThreadSafetyMode.ExecutionAndPublication);
        }
    }

    /// <summary>
    ///     Latest Watched Date
    /// </summary>
    public DateTime? LastWatchedDate => _lastWatchedDate.Value;

    public Func<DateTime?> LastWatchedDateDelegate
    {
        get => _lastWatchedDateDelegate;
        init
        {
            _lastWatchedDateDelegate = value;
            _lastWatchedDate = new Lazy<DateTime?>(_lastWatchedDateDelegate, LazyThreadSafetyMode.ExecutionAndPublication);
        }
    }

    /// <summary>
    ///     Lowest User Rating (0-10)
    /// </summary>
    public decimal LowestUserRating => _lowestUserRating.Value;

    public Func<decimal> LowestUserRatingDelegate
    {
        get => _lowestUserRatingDelegate;
        init
        {
            _lowestUserRatingDelegate = value;
            _lowestUserRating = new Lazy<decimal>(_lowestUserRatingDelegate, LazyThreadSafetyMode.ExecutionAndPublication);
        }
    }

    /// <summary>
    ///     Highest User Rating (0-10)
    /// </summary>
    public decimal HighestUserRating => _highestUserRating.Value;

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
