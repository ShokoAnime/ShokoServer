using Shoko.Abstractions.Metadata.Anidb.Enums;

namespace Shoko.Abstractions.Metadata.Anidb.Models;

/// <summary>
///   Options for a MyList sync, overriding the server settings for the sync
///   run. Null fields fall back to the configured server settings.
/// </summary>
public class MylistSyncOptions
{
    /// <summary>
    ///   Optional. The fetch mode to use for the sync. Null falls back to
    ///   the configured server setting. Set the
    ///   <see cref="MylistFetchMode.IgnoreTimeCheck"/> flag to bypass the
    ///   sync schedule gate and force a refresh even when the cache is
    ///   fresh.
    /// </summary>
    public MylistFetchMode? FetchMode { get; init; }

    /// <summary>
    ///   Optional. When set to <c>true</c>, will import watched states from
    ///   AniDB for older differences.
    /// </summary>
    public bool? ReadWatched { get; init; }

    /// <summary>
    ///   Optional. When set to <c>true</c>, will import unwatched states
    ///   from AniDB for older differences.
    /// </summary>
    public bool? ReadUnwatched { get; init; }

    /// <summary>
    ///   Optional. When set to <c>true</c>, will export watched states to
    ///   AniDB for older differences.
    /// </summary>
    public bool? SetWatched { get; init; }

    /// <summary>
    ///   Optional. When set to <c>true</c>, will export unwatched states to
    ///   AniDB for older differences.
    /// </summary>
    public bool? SetUnwatched { get; init; }

    /// <summary>
    ///   Optional. How to resolve watched-state conflicts on same-day
    ///   updates.
    /// </summary>
    public MylistWatchedSyncMode? WatchedSyncMode { get; init; }

    /// <summary>
    ///   Optional. When set to <c>true</c>, will update the storage state of
    ///   existing entries to the desired state.
    /// </summary>
    public bool? UpdateStates { get; init; }

    /// <summary>
    ///   Optional. The desired storage state to apply to the entries.
    /// </summary>
    public MylistState? StorageState { get; init; }

    /// <summary>
    ///   Optional. How to remove entries for files no longer in the library.
    /// </summary>
    public MylistDeleteType? DeleteType { get; init; }

    /// <summary>
    ///   Optional. Which tiers of the MyList to reconcile. Null falls back to
    ///   the configured server setting.
    /// </summary>
    public MylistSyncTargets? Targets { get; init; }

    /// <summary>
    ///   Optional. How to record a locally watched episode that the MyList
    ///   covers only by file entries. Null falls back to the configured server
    ///   setting.
    /// </summary>
    public MylistWatchedEpisodeMode? WatchedEpisodeMode { get; init; }

    /// <summary>
    ///   Optional. When set to <c>true</c>, work out what the sync would do and
    ///   return it on <see cref="MylistSyncResult.Actions"/> without doing any
    ///   of it — nothing local is written and nothing is sent to AniDB. The
    ///   MyList itself is still fetched, since the plan is derived from it.
    ///
    ///   Only meaningful when calling the sync directly. Scheduling a sync with
    ///   this set is refused, because a queued job has nowhere to return a plan.
    /// </summary>
    public bool? Preview { get; init; }
}
