using System;
using System.Collections.Generic;

namespace Shoko.Abstractions.Metadata.Storage;

/// <summary>
///   Keeps every source's authored relations between its entries, so a
///   provider does not have to.
/// </summary>
/// <remarks>
///   Only a plugin's source can be written; the core keeps the relations of
///   <c>shoko</c>, <c>user</c>, <c>generated</c>, <c>anidb</c> and
///   <c>tmdb</c> itself.
/// </remarks>
public interface IMetadataRelationStore
{
    /// <summary>
    ///   The relations from an entry to entries of one kind.
    /// </summary>
    /// <remarks>
    ///   A relation the source only states from the other end is read back
    ///   reversed, so the graph can be walked from either side.
    /// </remarks>
    /// <typeparam name="TBase">The entry's own kind.</typeparam>
    /// <typeparam name="TRelated">The kind of entry related to.</typeparam>
    /// <param name="entry">The entry.</param>
    /// <returns>The relations.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="entry"/> is <c>null</c>.</exception>
    IReadOnlyList<IRelatedMetadata<TBase, TRelated>> GetRelations<TBase, TRelated>(MetadataGuid entry)
        where TBase : IMetadata
        where TRelated : IMetadata;

    /// <summary>
    ///   Makes an entry's relations exactly the ones given.
    /// </summary>
    /// <param name="entry">The entry.</param>
    /// <param name="relations">The relations from it. A relation given twice keeps its first place.</param>
    /// <exception cref="ArgumentNullException">
    ///   <paramref name="entry"/> or <paramref name="relations"/> is <c>null</c>.
    /// </exception>
    /// <exception cref="ArgumentException">
    ///   The entry is on a source the core keeps itself, or a related entry
    ///   is on another source.
    /// </exception>
    void SetRelations(MetadataGuid entry, IEnumerable<MetadataRelationData> relations);

    /// <summary>
    ///   Removes every relation from an entry.
    /// </summary>
    /// <param name="entry">The entry.</param>
    /// <returns>How many were removed.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="entry"/> is <c>null</c>.</exception>
    /// <exception cref="ArgumentException">The entry is on a source the core keeps itself.</exception>
    int RemoveRelations(MetadataGuid entry);
}
