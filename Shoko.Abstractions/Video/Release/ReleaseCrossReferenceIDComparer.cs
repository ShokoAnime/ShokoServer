using System.Collections.Generic;
using Shoko.Abstractions.Metadata;

namespace Shoko.Abstractions.Video.Release;

/// <summary>
///   Orders the IDs of a release cross-reference with its main entry first:
///   AniDB, then the other sources in their order; within a source, episodes
///   and movies, then series. Stored releases keep their IDs in this order.
/// </summary>
public sealed class ReleaseCrossReferenceIDComparer : IComparer<MetadataGuid>
{
    /// <summary>
    ///   The comparer.
    /// </summary>
    public static ReleaseCrossReferenceIDComparer Instance { get; } = new();

    private ReleaseCrossReferenceIDComparer() { }

    /// <inheritdoc/>
    public int Compare(MetadataGuid? x, MetadataGuid? y)
    {
        if (ReferenceEquals(x, y))
            return 0;
        if (x is null)
            return -1;
        if (y is null)
            return 1;

        var bySource = x.Source.CompareTo(y.Source);
        if (bySource is not 0)
            return bySource;

        var byRank = Rank(x.EntityType).CompareTo(Rank(y.EntityType));
        return byRank is not 0 ? byRank : x.CompareTo(y);
    }

    private static int Rank(MetadataEntityType type)
    {
        if (type == MetadataEntityType.Episode || type == MetadataEntityType.Movie)
            return 0;
        return type == MetadataEntityType.Series ? 1 : 2;
    }
}
