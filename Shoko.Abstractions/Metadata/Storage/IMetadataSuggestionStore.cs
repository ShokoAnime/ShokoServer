using System;
using System.Collections.Generic;

namespace Shoko.Abstractions.Metadata.Storage;

/// <summary>
///   Keeps every source's suggestions between its entries, so a provider does
///   not have to.
/// </summary>
/// <remarks>
///   Every source but the core's own can be written; the core keeps the
///   suggestions of <c>shoko</c>, <c>user</c>, <c>generated</c> and
///   <c>anidb</c> itself.
/// </remarks>
public interface IMetadataSuggestionStore
{
    /// <summary>
    ///   What an entry suggests, among entries of one kind.
    /// </summary>
    /// <typeparam name="TBase">The entry's own kind.</typeparam>
    /// <typeparam name="TSuggested">The kind of entry suggested.</typeparam>
    /// <param name="entry">The entry.</param>
    /// <returns>The suggestions, by order where the source ranks them.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="entry"/> is <c>null</c>.</exception>
    IReadOnlyList<ISuggestedMetadata<TBase, TSuggested>> GetSuggestions<TBase, TSuggested>(MetadataGuid entry)
        where TBase : IMetadata
        where TSuggested : IMetadata;

    /// <summary>
    ///   The suggestions that point at an entry, read from their own end.
    /// </summary>
    /// <typeparam name="TBase">The kind of entry suggesting.</typeparam>
    /// <typeparam name="TSuggested">The entry's own kind.</typeparam>
    /// <param name="entry">The entry suggested.</param>
    /// <returns>The suggestions.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="entry"/> is <c>null</c>.</exception>
    IReadOnlyList<ISuggestedMetadata<TBase, TSuggested>> GetSuggestedBy<TBase, TSuggested>(MetadataGuid entry)
        where TBase : IMetadata
        where TSuggested : IMetadata;

    /// <summary>
    ///   Makes an entry's suggestions exactly the ones given.
    /// </summary>
    /// <param name="entry">The entry.</param>
    /// <param name="suggestions">
    ///   What it suggests. A suggestion given twice, of the same kind, keeps
    ///   its first place.
    /// </param>
    /// <exception cref="ArgumentNullException">
    ///   <paramref name="entry"/> or <paramref name="suggestions"/> is <c>null</c>.
    /// </exception>
    /// <exception cref="ArgumentException">
    ///   The entry is on a source the core keeps itself, or a suggested entry
    ///   is on another source.
    /// </exception>
    void SetSuggestions(MetadataGuid entry, IEnumerable<MetadataSuggestionData> suggestions);

    /// <summary>
    ///   Removes every suggestion an entry makes.
    /// </summary>
    /// <param name="entry">The entry.</param>
    /// <returns>How many were removed.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="entry"/> is <c>null</c>.</exception>
    /// <exception cref="ArgumentException">The entry is on a source the core keeps itself.</exception>
    int RemoveSuggestions(MetadataGuid entry);
}
