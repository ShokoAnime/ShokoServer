using System;
using System.Threading;
using System.Threading.Tasks;

namespace Shoko.Server.Services;

/// <summary>
///   A provider the core ships that keeps its people and networks in tables
///   of its own rather than the stores, and purges the ones left unused
///   itself when <see cref="MetadataPurgeService"/> asks.
/// </summary>
internal interface ICoreMetadataOrphanPurger
{
    /// <summary>
    ///   Stamps the people and networks nothing uses any more, and removes the
    ///   ones stamped before a cutoff with their links and image links. Rows
    ///   left behind for entries that are gone are removed too.
    /// </summary>
    /// <param name="orphanedBefore">Remove only the people and networks orphaned before this time.</param>
    /// <param name="cancellationToken">Cancels the work.</param>
    /// <returns>How many people, networks and leftover entries were removed.</returns>
    Task<int> PurgeOrphaned(DateTime orphanedBefore, CancellationToken cancellationToken = default);
}
