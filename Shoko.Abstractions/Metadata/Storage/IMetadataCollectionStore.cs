using System;
using System.Collections.Generic;

namespace Shoko.Abstractions.Metadata.Storage;

/// <summary>
///   Keeps every plugin source's collections and the works they gather, so a
///   provider does not have to.
/// </summary>
/// <remarks>
///   A stored collection reads back with its titles, overviews and
///   images. Only a plugin's source can be written; the core keeps the
///   collections of <c>shoko</c>, <c>user</c>, <c>generated</c>,
///   <c>anidb</c> and <c>tmdb</c> itself.
/// </remarks>
public interface IMetadataCollectionStore
{
    #region Reading

    /// <summary>
    ///   Looks up a collection.
    /// </summary>
    /// <param name="id">The collection, e.g. <c>anilist://collection/7</c>.</param>
    /// <returns>The collection, or <c>null</c> when it is not stored.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="id"/> is <c>null</c>.</exception>
    ICollection? GetCollection(MetadataGuid id);

    /// <summary>
    ///   Every collection a source has stored.
    /// </summary>
    /// <param name="source">The source.</param>
    /// <returns>The collections, in no particular order.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="source"/> is <c>null</c>.</exception>
    IReadOnlyList<ICollection> GetAllCollections(MetadataSource source);

    /// <summary>
    ///   The series and movies a collection gathers.
    /// </summary>
    /// <param name="collection">The collection.</param>
    /// <returns>The members, in the order they were given.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="collection"/> is <c>null</c>.</exception>
    IReadOnlyList<MetadataGuid> GetMembers(MetadataGuid collection);

    /// <summary>
    ///   The collections a series or movie is in.
    /// </summary>
    /// <param name="member">The series or movie.</param>
    /// <returns>The collections.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="member"/> is <c>null</c>.</exception>
    IReadOnlyList<ICollection> GetCollectionsWith(MetadataGuid member);

    #endregion

    #region Writing

    /// <summary>
    ///   Stores a collection whole, replacing what was stored for it: its
    ///   members, titles and overviews become exactly the ones given.
    /// </summary>
    /// <param name="collection">The collection.</param>
    /// <returns><c>1</c> when the collection was added or changed, else <c>0</c>.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="collection"/> is or holds <c>null</c>.</exception>
    /// <exception cref="ArgumentException">
    ///   The ID does not name a collection or is on a source the core keeps
    ///   itself, a member is not a series or movie on the same source, or a
    ///   code is too long.
    /// </exception>
    int SaveCollection(MetadataCollectionData collection);

    /// <summary>
    ///   Removes a collection with its members, titles, overviews, image
    ///   links, tags, studios, cast, crew, relations and suggestions. The
    ///   series and movies it gathered are left alone.
    /// </summary>
    /// <param name="id">The collection.</param>
    /// <returns><c>1</c> when the collection was removed, else <c>0</c>.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="id"/> is <c>null</c>.</exception>
    /// <exception cref="ArgumentException">
    ///   The ID does not name a collection, or is on a source the core keeps
    ///   itself.
    /// </exception>
    int RemoveCollection(MetadataGuid id);

    #endregion
}
