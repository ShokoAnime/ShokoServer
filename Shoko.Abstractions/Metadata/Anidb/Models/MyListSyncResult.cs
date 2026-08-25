namespace Shoko.Abstractions.Metadata.Anidb.Models;

/// <summary>
///   What a MyList sync did. The add and remove counts are work the sync
///   queued rather than work it completed; the jobs it enqueued run afterwards
///   on the queue's own schedule.
/// </summary>
public record MyListSyncResult
{
    /// <summary>
    ///   How many MyList entries the sync looked at.
    /// </summary>
    public int TotalEntries { get; init; }

    /// <summary>
    ///   How many of those entries AniDB reports as watched. A census of the
    ///   remote state, not a count of anything the sync changed.
    /// </summary>
    public int WatchedEntries { get; init; }

    /// <summary>
    ///   How many entries the sync reconciled, whether by importing a watched
    ///   state, exporting one, or updating the storage state.
    /// </summary>
    public int ModifiedEntries { get; init; }

    /// <summary>
    ///   How many local files were queued to be added to the MyList because
    ///   AniDB had no entry for them.
    /// </summary>
    public int FilesQueuedForAdd { get; init; }

    /// <summary>
    ///   How many entries were queued for removal because the local library no
    ///   longer has the file.
    /// </summary>
    public int EntriesQueuedForRemoval { get; init; }

    /// <summary>
    ///   How many entries were left alone because the sync could not tell
    ///   whether they were generic. See <c>MyList_UseGenericFileIndex</c>.
    /// </summary>
    public int UnclassifiedEntries { get; init; }

    /// <summary>
    ///   How many episodes had their watched state queued for export because
    ///   the MyList held no generic entry for them. Counts both the generic
    ///   entries created and, under
    ///   <see cref="Enums.MyListWatchedEpisodeMode.AttachToOldest"/>, the
    ///   existing entries updated instead.
    /// </summary>
    public int EpisodesQueuedForAdd { get; init; }

    /// <summary>
    ///   How many generic entries were queued for removal because the episode
    ///   is neither watched locally nor backed by a local file.
    /// </summary>
    public int EpisodesQueuedForRemoval { get; init; }
}
