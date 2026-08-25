using System;

namespace Shoko.Abstractions.Metadata.Anidb.Enums;

/// <summary>
///   Which tiers of the MyList a sync reconciles. The two are independent: a
///   file entry covers one release, while a generic entry covers an episode
///   the user has no file for.
/// </summary>
[Flags]
public enum MylistSyncTargets : byte
{
    /// <summary>
    ///   Reconcile nothing. Only useful for turning a sync into a no-op.
    /// </summary>
    None = 0,

    /// <summary>
    ///   Reconcile file entries against local files.
    /// </summary>
    Videos = 1,

    /// <summary>
    ///   Reconcile generic entries against episodes, including creating and
    ///   removing them for episodes with no local file.
    /// </summary>
    Episodes = 2,

    /// <summary>
    ///   Reconcile both tiers; the default.
    /// </summary>
    All = Videos | Episodes,
}
