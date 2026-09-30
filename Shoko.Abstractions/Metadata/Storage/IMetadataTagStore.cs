using System;
using System.Collections.Generic;
using Shoko.Abstractions.Metadata.Enums;

namespace Shoko.Abstractions.Metadata.Storage;

/// <summary>
///   Keeps every source's tags and genres, and which entries they apply to,
///   so a provider does not have to.
/// </summary>
/// <remarks>
///   Only a plugin's source can be written; the core keeps the tags of
///   <c>shoko</c>, <c>user</c>, <c>generated</c>, <c>anidb</c> and
///   <c>tmdb</c> itself.
/// </remarks>
public interface IMetadataTagStore
{
    #region Reading

    /// <summary>
    ///   Looks up a tag.
    /// </summary>
    /// <param name="id">The tag, e.g. <c>anilist://tag/Action</c>.</param>
    /// <returns>The tag, or <c>null</c> when it is not stored.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="id"/> is <c>null</c>.</exception>
    ITag? GetTag(MetadataGuid id);

    /// <summary>
    ///   Every tag a source has stored.
    /// </summary>
    /// <param name="source">The source.</param>
    /// <param name="kind">Optional. Only descriptive tags, only genres or only keywords.</param>
    /// <returns>The tags, by name.</returns>
    IReadOnlyList<ITag> GetAllTags(MetadataSource source, TagKind? kind = null);

    /// <summary>
    ///   The tags on an entry, each with its weight and spoiler flag there.
    /// </summary>
    /// <param name="entry">The entry.</param>
    /// <returns>The tags, in the order they were given.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="entry"/> is <c>null</c>.</exception>
    IReadOnlyList<ITag> GetTags(MetadataGuid entry);

    /// <summary>
    ///   The entries a tag is on.
    /// </summary>
    /// <param name="tag">The tag.</param>
    /// <returns>The entries.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="tag"/> is <c>null</c>.</exception>
    IReadOnlyList<MetadataGuid> GetEntriesWithTag(MetadataGuid tag);

    #endregion

    #region Writing

    /// <summary>
    ///   Adds tags, or updates the ones already stored.
    /// </summary>
    /// <param name="tags">The tags.</param>
    /// <exception cref="ArgumentNullException"><paramref name="tags"/> is or holds <c>null</c>.</exception>
    /// <exception cref="ArgumentException">A tag's ID does not name a tag, or is on a source the core keeps itself.</exception>
    void SaveTags(IEnumerable<MetadataTagData> tags);

    /// <summary>
    ///   Makes an entry's tags exactly the ones given, in that order.
    /// </summary>
    /// <param name="entry">The entry.</param>
    /// <param name="tags">The tags as they apply to it. A tag given twice keeps its first place.</param>
    /// <exception cref="ArgumentNullException">
    ///   <paramref name="entry"/> or <paramref name="tags"/> is <c>null</c>.
    /// </exception>
    /// <exception cref="ArgumentException">
    ///   The entry is on a source the core keeps itself, or a tag is not
    ///   stored or is on another source.
    /// </exception>
    void SetTags(MetadataGuid entry, IEnumerable<MetadataEntryTagData> tags);

    /// <summary>
    ///   Removes every tag from an entry.
    /// </summary>
    /// <param name="entry">The entry.</param>
    /// <returns>How many were removed.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="entry"/> is <c>null</c>.</exception>
    /// <exception cref="ArgumentException">The entry is on a source the core keeps itself.</exception>
    int RemoveTags(MetadataGuid entry);

    #endregion
}
