using Shoko.Abstractions.Metadata.Anidb.Enums;

namespace Shoko.Abstractions.Metadata.Anidb.Models;

/// <summary>
///   Options for a MyList sync, overriding the server settings for the sync
///   run. Null fields fall back to the configured server settings.
/// </summary>
public class MyListSyncOptions
{
    /// <summary>
    ///   Optional. The fetch mode to use for the sync. Null falls back to
    ///   the configured server setting. Set the
    ///   <see cref="MyListFetchMode.IgnoreTimeCheck"/> flag to bypass the
    ///   sync schedule gate and force a refresh even when the cache is
    ///   fresh.
    /// </summary>
    public MyListFetchMode? FetchMode { get; init; }

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
    public MyListWatchedSyncMode? WatchedSyncMode { get; init; }

    /// <summary>
    ///   Optional. When set to <c>true</c>, will update the storage state of
    ///   existing entries to the desired state.
    /// </summary>
    public bool? UpdateStates { get; init; }

    /// <summary>
    ///   Optional. The desired storage state to apply to the entries.
    /// </summary>
    public MyListState? StorageState { get; init; }

    /// <summary>
    ///   Optional. How to remove entries for files no longer in the library.
    /// </summary>
    public MyListDeleteType? DeleteType { get; init; }
}
