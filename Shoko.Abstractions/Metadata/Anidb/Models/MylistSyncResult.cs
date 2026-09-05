namespace Shoko.Abstractions.Metadata.Anidb.Models;

/// <summary>
///   What a MyList sync did. The add and remove counts are work the sync
///   queued rather than work it completed; the jobs it enqueued run afterwards
///   on the queue's own schedule.
/// </summary>
public record MylistSyncResult
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
    ///   How many entries the local library no longer has the file for were
    ///   left alone because the delete type marks entries with a state they
    ///   already hold. A marked entry stays in the MyList and so keeps reading
    ///   as missing, with nothing left to write to it, which is why these are
    ///   reported rather than counted towards
    ///   <see cref="EntriesQueuedForRemoval"/>.
    /// </summary>
    public int EntriesAlreadyDisposed { get; init; }

    /// <summary>
    ///   How many generic entries that record nothing were left alone because
    ///   the delete type marks entries with a state they already hold. A marked
    ///   entry stays in the MyList and so keeps reading as vestigial, with
    ///   nothing left to write to it, which is why these are reported rather
    ///   than counted towards <see cref="EpisodesQueuedForRemoval"/>.
    /// </summary>
    public int EpisodesAlreadyDisposed { get; init; }

    /// <summary>
    ///   How many steps the sync had nothing to do for, across everything it
    ///   looked at: entries already in sync, files already in the MyList,
    ///   episodes a file entry already covers, and so on. The other counts here
    ///   for things left alone are each a part of this one.
    ///
    ///   Counted whether or not
    ///   <see cref="MylistSyncOptions.IncludeNoOperations"/> kept them, so it
    ///   also says how much longer the plan would be with that turned on.
    /// </summary>
    public int NoOperations { get; init; }

    /// <summary>
    ///   How many entries were left alone because the sync could not tell
    ///   whether they were generic. See <c>MyList_UseGenericFileIndex</c>.
    /// </summary>
    public int UnclassifiedEntries { get; init; }

    /// <summary>
    ///   How many episodes had their watched state queued for export because
    ///   the MyList held no generic entry for them. Counts both the generic
    ///   entries created and, under
    ///   <see cref="Enums.MylistWatchedEpisodeMode.AttachToOldest"/>, the
    ///   existing entries updated instead.
    /// </summary>
    public int EpisodesQueuedForAdd { get; init; }

    /// <summary>
    ///   How many generic entries were queued for removal because the episode
    ///   is neither watched locally nor backed by a local file.
    /// </summary>
    public int EpisodesQueuedForRemoval { get; init; }

    /// <summary>
    ///   What the sync did, step by step. On a plan-only run this is what it
    ///   would have done, having done none of it, and the same plan can be
    ///   handed back to apply it.
    /// </summary>
    public required MylistSyncPlan Plan { get; init; }

    /// <summary>
    ///   Whether the plan was carried out. <c>false</c> on a plan-only run,
    ///   where nothing was changed either locally or on AniDB.
    /// </summary>
    public bool IsApplied { get; init; }
}
