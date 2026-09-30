using System;

namespace Shoko.Abstractions.Metadata.CrossReferences;

/// <summary>
///   The sections of a cross-reference file, one per level of link.
/// </summary>
[Flags]
public enum MetadataCrossReferenceSections
{
    /// <summary>
    ///   No section.
    /// </summary>
    None = 0,

    /// <summary>
    ///   The links between an AniDB episode and a film.
    /// </summary>
    Movie = 1,

    /// <summary>
    ///   The links between an AniDB anime and a series.
    /// </summary>
    Series = 2,

    /// <summary>
    ///   The links between an AniDB episode and an episode.
    /// </summary>
    Episode = 4,

    /// <summary>
    ///   Every section.
    /// </summary>
    All = Movie | Series | Episode,
}
