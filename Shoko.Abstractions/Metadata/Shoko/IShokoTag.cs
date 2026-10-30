using System.Collections.Generic;

namespace Shoko.Abstractions.Metadata.Shoko;

/// <summary>
/// A Shoko tag.
/// </summary>
public interface IShokoTag : ITag
{
    /// <summary>
    ///   The custom tag ID, the same ID <see cref="IMetadata.ID"/> holds as
    ///   text.
    /// </summary>
    int LocalID { get; }

    /// <summary>
    /// All Shoko series the tag is set on.
    /// </summary>
    IReadOnlyList<IShokoSeries> AllShokoSeries { get; }
}
