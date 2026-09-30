using System;
using System.Collections.Generic;

namespace Shoko.Abstractions.Metadata.Storage;

/// <summary>
///   Keeps every source's studios and networks, and which entries they
///   worked on or aired, so a provider does not have to.
/// </summary>
/// <remarks>
///   Only a plugin's source can be written; the core keeps the studios and
///   networks of <c>shoko</c>, <c>user</c>, <c>generated</c>, <c>anidb</c>
///   and <c>tmdb</c> itself. No write removes a studio or network: one no
///   entry names any more is purged by <see cref="RemoveOrphaned"/> once it
///   has been orphaned long enough.
/// </remarks>
public interface IMetadataStudioStore
{
    #region Reading

    /// <summary>
    ///   Looks up a studio.
    /// </summary>
    /// <param name="id">The studio, e.g. <c>anilist://studio/7</c>.</param>
    /// <returns>The studio, or <c>null</c> when it is not stored.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="id"/> is <c>null</c>.</exception>
    IStudio? GetStudio(MetadataGuid id);

    /// <summary>
    ///   The studios that worked on an entry, each with its part in it.
    /// </summary>
    /// <param name="entry">The entry.</param>
    /// <returns>The studios, in the order they were given.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="entry"/> is <c>null</c>.</exception>
    IReadOnlyList<IStudio> GetStudios(MetadataGuid entry);

    /// <summary>
    ///   The entries a studio worked on.
    /// </summary>
    /// <param name="studio">The studio.</param>
    /// <returns>The entries.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="studio"/> is <c>null</c>.</exception>
    IReadOnlyList<MetadataGuid> GetEntriesForStudio(MetadataGuid studio);

    /// <summary>
    ///   Looks up a network.
    /// </summary>
    /// <param name="id">The network, e.g. <c>anilist://network/3</c>.</param>
    /// <returns>The network, or <c>null</c> when it is not stored.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="id"/> is <c>null</c>.</exception>
    INetwork? GetNetwork(MetadataGuid id);

    /// <summary>
    ///   The networks an entry aired or streamed on.
    /// </summary>
    /// <param name="entry">The entry.</param>
    /// <returns>The networks, in the order they were given.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="entry"/> is <c>null</c>.</exception>
    IReadOnlyList<INetwork> GetNetworks(MetadataGuid entry);

    /// <summary>
    ///   The entries a network aired or streamed.
    /// </summary>
    /// <param name="network">The network.</param>
    /// <returns>The entries.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="network"/> is <c>null</c>.</exception>
    IReadOnlyList<MetadataGuid> GetEntriesForNetwork(MetadataGuid network);

    #endregion

    #region Writing

    /// <summary>
    ///   Adds studios, or updates the ones already stored.
    /// </summary>
    /// <param name="studios">The studios.</param>
    /// <exception cref="ArgumentNullException"><paramref name="studios"/> is or holds <c>null</c>.</exception>
    /// <exception cref="ArgumentException">A studio's ID does not name a studio, or is on a source the core keeps itself.</exception>
    void SaveStudios(IEnumerable<MetadataStudioData> studios);

    /// <summary>
    ///   Makes an entry's studios exactly the ones given, in that order.
    /// </summary>
    /// <param name="entry">The entry.</param>
    /// <param name="studios">
    ///   The studios and their parts in it. A studio given twice in the same
    ///   part keeps its first place.
    /// </param>
    /// <exception cref="ArgumentNullException">
    ///   <paramref name="entry"/> or <paramref name="studios"/> is <c>null</c>.
    /// </exception>
    /// <exception cref="ArgumentException">
    ///   The entry is on a source the core keeps itself, or a studio is not
    ///   stored or is on another source.
    /// </exception>
    void SetStudios(MetadataGuid entry, IEnumerable<MetadataEntryStudioData> studios);

    /// <summary>
    ///   Removes every studio from an entry.
    /// </summary>
    /// <param name="entry">The entry.</param>
    /// <returns>How many were removed.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="entry"/> is <c>null</c>.</exception>
    /// <exception cref="ArgumentException">The entry is on a source the core keeps itself.</exception>
    int RemoveStudios(MetadataGuid entry);

    /// <summary>
    ///   Adds networks, or updates the ones already stored.
    /// </summary>
    /// <param name="networks">The networks.</param>
    /// <exception cref="ArgumentNullException"><paramref name="networks"/> is or holds <c>null</c>.</exception>
    /// <exception cref="ArgumentException">A network's ID does not name a network, or is on a source the core keeps itself.</exception>
    void SaveNetworks(IEnumerable<MetadataNetworkData> networks);

    /// <summary>
    ///   Makes an entry's networks exactly the ones given, in that order.
    /// </summary>
    /// <param name="entry">The entry.</param>
    /// <param name="networks">The networks. A network given twice keeps its first place.</param>
    /// <exception cref="ArgumentNullException">
    ///   <paramref name="entry"/> or <paramref name="networks"/> is or holds
    ///   <c>null</c>.
    /// </exception>
    /// <exception cref="ArgumentException">
    ///   The entry is on a source the core keeps itself, or a network is not
    ///   stored or is on another source.
    /// </exception>
    void SetNetworks(MetadataGuid entry, IEnumerable<MetadataGuid> networks);

    /// <summary>
    ///   Removes every network from an entry.
    /// </summary>
    /// <param name="entry">The entry.</param>
    /// <returns>How many were removed.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="entry"/> is <c>null</c>.</exception>
    /// <exception cref="ArgumentException">The entry is on a source the core keeps itself.</exception>
    int RemoveNetworks(MetadataGuid entry);

    /// <summary>
    ///   Removes a source's studios and networks that no entry has named since
    ///   before a cutoff, but not their image links.
    /// </summary>
    /// <remarks>
    ///   One is stamped when the last entry naming it lets go and unstamped
    ///   when named again; one found unused without a stamp is stamped now
    ///   rather than removed. The core runs this daily and also unlinks the
    ///   images, so you rarely need it.
    /// </remarks>
    /// <param name="source">The source.</param>
    /// <param name="orphanedBefore">
    ///   Remove only the studios and networks orphaned before this time.
    ///   Saving an unnamed one stamps it anew, so keep this a day or more back.
    /// </param>
    /// <returns>The studios and networks removed.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="source"/> is <c>null</c>.</exception>
    /// <exception cref="ArgumentException">The source is one the core keeps itself.</exception>
    IReadOnlyList<MetadataGuid> RemoveOrphaned(MetadataSource source, DateTime orphanedBefore);

    #endregion
}
