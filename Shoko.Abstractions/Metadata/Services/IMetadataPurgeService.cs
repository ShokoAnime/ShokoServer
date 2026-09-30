using System;
using System.Threading;
using System.Threading.Tasks;

namespace Shoko.Abstractions.Metadata.Services;

/// <summary>
///   Removes what the core keeps for a source's entries, people, studios and
///   networks once nothing needs it any more.
/// </summary>
/// <remarks>
///   A purge runs in the core's purge job under the entry's lock, and leaves
///   an entry something links to alone unless forced. The core runs
///   <see cref="PurgeUnused"/> and <see cref="PurgeOrphaned"/> daily; a
///   provider never sweeps its own entries. Creators, characters, studios
///   and networks never go with an entry, only through <see cref="PurgeOrphaned"/>.
/// </remarks>
public interface IMetadataPurgeService
{
    #region Entries

    /// <summary>
    ///   Queue a purge of one series, film or collection.
    /// </summary>
    /// <remarks>
    ///   Removes the entry with everything the stores hold for it, its last
    ///   refresh time, and a series' orderings and hidden episode flags, then
    ///   lets every provider claiming the source clean up what it keeps. A
    ///   series or film also queues the purge of each stored collection
    ///   holding it, which goes ahead once none of its members is linked.
    /// </remarks>
    /// <param name="entryID">The series, film or collection.</param>
    /// <param name="force">
    ///   Whether to purge it even though something still links to it,
    ///   removing those links first: the series and film links naming it and
    ///   the episode links pointing into a series. A forced purge of a
    ///   collection goes ahead while its members are linked.
    /// </param>
    /// <param name="cancellationToken">Cancels the work.</param>
    /// <returns>
    ///   <see langword="true"/> once the purge is queued, or
    ///   <see langword="false"/> when nothing purges the entry's source.
    /// </returns>
    /// <exception cref="ArgumentNullException"><paramref name="entryID"/> is <see langword="null"/>.</exception>
    Task<bool> PurgeEntry(MetadataGuid entryID, bool force = false, CancellationToken cancellationToken = default);

    /// <summary>
    ///   Queue a purge of every stored series and film of a source that
    ///   nothing links to, and for a plugin source of every stored collection
    ///   none of whose members anything links to. A TMDB collection goes with
    ///   the last movie it holds instead.
    /// </summary>
    /// <remarks>
    ///   The core runs this daily for every source, taking the entries not
    ///   refreshed within the admin's setting (two weeks unless changed).
    /// </remarks>
    /// <param name="source">The source: TMDB or a plugin source.</param>
    /// <param name="olderThan">
    ///   Purge only the entries last refreshed before this time, or never
    ///   refreshed at all; left out, every unused entry is purged.
    /// </param>
    /// <param name="entityType">
    ///   Purge only series, only films or only collections; left out, all
    ///   three.
    /// </param>
    /// <param name="cancellationToken">Cancels the work.</param>
    /// <returns>How many purges were queued.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="source"/> is <see langword="null"/>.</exception>
    Task<int> PurgeUnused(MetadataSource source, DateTime? olderThan = null, MetadataEntityType? entityType = null, CancellationToken cancellationToken = default);

    /// <summary>
    ///   Queue a purge of every stored collection of a source, whether its
    ///   members are linked or not.
    /// </summary>
    /// <remarks>
    ///   Nothing links a collection, so this loses no link: a collection is
    ///   stored again when the provider refreshes a series or film it holds.
    /// </remarks>
    /// <param name="source">The source: TMDB or a plugin source.</param>
    /// <param name="cancellationToken">Cancels the work.</param>
    /// <returns>How many purges were queued.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="source"/> is <see langword="null"/>.</exception>
    Task<int> PurgeCollections(MetadataSource source, CancellationToken cancellationToken = default);

    #endregion

    #region Orphans

    /// <summary>
    ///   Remove the creators, characters, studios and networks nothing has
    ///   used for longer than a while, with their alternative names and image
    ///   links.
    /// </summary>
    /// <remarks>
    ///   Runs at once rather than in a job. An unused one never stamped is
    ///   stamped now and removed by a later purge. A refresh may save a person
    ///   or studio just before naming it, so keep the cutoff a day or more back.
    /// </remarks>
    /// <param name="source">
    ///   One plugin source or TMDB, or all of them when left out. TMDB purges
    ///   its people and networks from its own tables by the same cutoff; its
    ///   companies go with the last entry naming them.
    /// </param>
    /// <param name="orphanedBefore">
    ///   Remove only the ones orphaned before this time, which should be a day
    ///   or more ago; left out, the ones orphaned for longer than the admin's
    ///   setting, a week unless changed.
    /// </param>
    /// <param name="cancellationToken">Cancels the work.</param>
    /// <returns>How many creators, characters, studios and networks were removed.</returns>
    Task<int> PurgeOrphaned(MetadataSource? source = null, DateTime? orphanedBefore = null, CancellationToken cancellationToken = default);

    #endregion
}
