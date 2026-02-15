using System.Collections.Generic;
using Shoko.Abstractions.Metadata.Containers;

namespace Shoko.Abstractions.Metadata.Shoko;

/// <summary>
/// A Shoko tag.
/// </summary>
public interface IShokoTag : ITag
{
    /// <summary>
    /// All Shoko series the tag is set on.
    /// </summary>
    IReadOnlyList<IShokoSeries> AllShokoSeries { get; }
}

/// <summary>
/// A Shoko tag with additional information for a single Shoko series.
/// </summary>
public interface IShokoTagForSeries : IShokoTag
{
    /// <summary>
    /// The ID of the Shoko series the tag is set on.
    /// </summary>
    int ShokoSeriesID { get; }

    /// <summary>
    /// A direct link to the Shoko Seres metadata.
    /// </summary>
    IShokoSeries ShokoSeries { get; }
}
