using System;
using System.Collections.Generic;
using System.Linq;
using Shoko.Abstractions.Metadata.Containers;

namespace Shoko.Abstractions.Metadata;

/// <summary>
///   Reads one source's IDs out of an entry's cross-source IDs.
/// </summary>
public static class CrossSourceIDExtensions
{
    /// <summary>
    ///   The IDs another source gave the entry, in the order the entry lists
    ///   them.
    /// </summary>
    /// <param name="entry">The series, movie or episode.</param>
    /// <param name="source">The source whose IDs to keep.</param>
    /// <returns>The IDs of that source, or an empty list.</returns>
    /// <exception cref="ArgumentNullException">
    ///   <paramref name="entry"/> or <paramref name="source"/> is
    ///   <see langword="null"/>.
    /// </exception>
    public static IReadOnlyList<MetadataGuid> GetCrossSourceIDs(this IWithCrossSources entry, MetadataSource source)
    {
        ArgumentNullException.ThrowIfNull(entry);
        return FromSource(entry.CrossSourceIDs, source);
    }

    /// <summary>
    ///   Keeps the IDs of one source.
    /// </summary>
    /// <param name="ids">The IDs.</param>
    /// <param name="source">The source whose IDs to keep.</param>
    /// <returns>The IDs of that source, in order.</returns>
    /// <exception cref="ArgumentNullException">
    ///   <paramref name="source"/> is <see langword="null"/>.
    /// </exception>
    private static List<MetadataGuid> FromSource(IReadOnlyList<MetadataGuid> ids, MetadataSource source)
    {
        ArgumentNullException.ThrowIfNull(source);
        return [.. ids.Where(id => id.Source == source)];
    }
}
