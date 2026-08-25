using System;
using Shoko.Abstractions.Metadata.Anidb.Enums;

namespace Shoko.Server.Services.Mylist;

/// <summary>
/// The watched-state reconciliation rules, as a pure function of the two sides
/// and the sync settings. Deliberately free of the entity being reconciled: a
/// file entry and a generic entry follow the same rules and differ only in what
/// the caller does with the answer.
///
/// Both dates must be UTC. AniDB works in UTC while the local database stores
/// local time, so a caller reading a watched date out of the database has to
/// convert it first — comparing the two kinds directly makes every entry look
/// different forever, and re-exports it on every sync.
/// </summary>
public static class MylistSyncDecisions
{
    /// <summary>
    /// Decides which way, if either, to close the difference between the local
    /// watched date and the one AniDB holds.
    /// </summary>
    /// <param name="localWatchedDate">
    ///   When the user watched it locally, or <c>null</c> if they have not.
    ///   Expected at AniDB precision already.
    /// </param>
    /// <param name="remoteViewedAt">
    ///   When AniDB says it was viewed, or <c>null</c> if it says unviewed.
    /// </param>
    /// <param name="remoteUpdatedAt">
    ///   The day AniDB last changed the entry, used to spot a same-day clash.
    /// </param>
    /// <param name="readWatched">
    ///   Whether importing a watch AniDB has and the library does not is allowed.
    /// </param>
    /// <param name="readUnwatched">
    ///   Whether importing an unwatch is allowed.
    /// </param>
    /// <param name="setWatched">
    ///   Whether exporting a watch the library has and AniDB does not is allowed.
    /// </param>
    /// <param name="setUnwatched">
    ///   Whether exporting an unwatch is allowed.
    /// </param>
    /// <param name="watchedSyncMode">
    ///   How to break the tie when both sides changed on the same day.
    /// </param>
    public static MylistWatchedAction DecideWatchedAction(
        DateTime? localWatchedDate,
        DateTime? remoteViewedAt,
        DateOnly remoteUpdatedAt,
        bool readWatched,
        bool readUnwatched,
        bool setWatched,
        bool setUnwatched,
        MylistWatchedSyncMode watchedSyncMode
    )
    {
        // a same-day difference is a genuine clash — both sides moved within the
        // resolution AniDB reports — so the mode breaks the tie instead of the
        // read/set settings, which only govern older differences
        if (localWatchedDate is { } localDate && remoteUpdatedAt == DateOnly.FromDateTime(localDate))
        {
            return watchedSyncMode switch
            {
                MylistWatchedSyncMode.TrustLocal when !localWatchedDate.Equals(remoteViewedAt)
                    => MylistWatchedAction.Export(localDate.ToUniversalTime()),

                // the local side is watched by definition here, so the only way
                // remote can win is by saying it is not
                MylistWatchedSyncMode.TrustRemote when remoteViewedAt is null
                    => MylistWatchedAction.Import(null),

                _ => MylistWatchedAction.None,
            };
        }

        if (readWatched && localWatchedDate is null && remoteViewedAt is not null)
            return MylistWatchedAction.Import(remoteViewedAt);

        // having just imported one direction, do not turn around and undo it
        if (readUnwatched && localWatchedDate is not null && remoteViewedAt is null)
            return MylistWatchedAction.Import(null);

        if (setUnwatched && localWatchedDate is null && remoteViewedAt is not null)
            return MylistWatchedAction.Export(null);

        if (setWatched && localWatchedDate is { } exportDate && !localWatchedDate.Equals(remoteViewedAt))
            return MylistWatchedAction.Export(exportDate.ToUniversalTime());

        return MylistWatchedAction.None;
    }
}
