using System;
using Shoko.Abstractions.Metadata.Anidb.Enums;

namespace Shoko.Server.Services;

/// <summary>
/// What reconciling one MyList entry against the local watched state calls for.
/// </summary>
public enum MyListWatchedActionKind
{
    /// <summary>
    /// The two sides already agree, or the settings do not allow closing the
    /// difference in either direction.
    /// </summary>
    None = 0,

    /// <summary>
    /// Write AniDB's watched state onto the local record.
    /// </summary>
    Import = 1,

    /// <summary>
    /// Send the local watched state to AniDB.
    /// </summary>
    Export = 2,
}

/// <summary>
/// The action to take for one entry, and the date it carries. For an import
/// that is the date to record locally, and for an export the date to send;
/// <c>null</c> in either direction means "not watched".
/// </summary>
public readonly record struct MyListWatchedAction(MyListWatchedActionKind Kind, DateTime? Date)
{
    public static readonly MyListWatchedAction None = new(MyListWatchedActionKind.None, null);

    public static MyListWatchedAction Import(DateTime? date) => new(MyListWatchedActionKind.Import, date);

    public static MyListWatchedAction Export(DateTime? date) => new(MyListWatchedActionKind.Export, date);
}

/// <summary>
/// The watched-state reconciliation rules, as a pure function of the two sides
/// and the sync settings. Deliberately free of the entity being reconciled: a
/// file entry and a generic entry follow the same rules and differ only in what
/// the caller does with the answer.
/// </summary>
public static class MyListSyncDecisions
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
    public static MyListWatchedAction DecideWatchedAction(
        DateTime? localWatchedDate,
        DateTime? remoteViewedAt,
        DateOnly remoteUpdatedAt,
        bool readWatched,
        bool readUnwatched,
        bool setWatched,
        bool setUnwatched,
        MyListWatchedSyncMode watchedSyncMode
    )
    {
        // a same-day difference is a genuine clash — both sides moved within the
        // resolution AniDB reports — so the mode breaks the tie instead of the
        // read/set settings, which only govern older differences
        if (localWatchedDate is { } localDate && remoteUpdatedAt == DateOnly.FromDateTime(localDate))
        {
            return watchedSyncMode switch
            {
                MyListWatchedSyncMode.TrustLocal when !localWatchedDate.Equals(remoteViewedAt)
                    => MyListWatchedAction.Export(localDate.ToUniversalTime()),

                // the local side is watched by definition here, so the only way
                // remote can win is by saying it is not
                MyListWatchedSyncMode.TrustRemote when remoteViewedAt is null
                    => MyListWatchedAction.Import(null),

                _ => MyListWatchedAction.None,
            };
        }

        if (readWatched && localWatchedDate is null && remoteViewedAt is not null)
            return MyListWatchedAction.Import(remoteViewedAt);

        // having just imported one direction, do not turn around and undo it
        if (readUnwatched && localWatchedDate is not null && remoteViewedAt is null)
            return MyListWatchedAction.Import(null);

        if (setUnwatched && localWatchedDate is null && remoteViewedAt is not null)
            return MyListWatchedAction.Export(null);

        if (setWatched && localWatchedDate is { } exportDate && !localWatchedDate.Equals(remoteViewedAt))
            return MyListWatchedAction.Export(exportDate.ToUniversalTime());

        return MyListWatchedAction.None;
    }
}
