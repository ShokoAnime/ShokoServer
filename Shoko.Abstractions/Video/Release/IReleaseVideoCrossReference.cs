
using System.Collections.Generic;
using Shoko.Abstractions.Metadata;

namespace Shoko.Abstractions.Video.Release;

/// <summary>
/// Video cross-reference included in an <see cref="IReleaseInfo"/>.
/// </summary>
public interface IReleaseVideoCrossReference
{
    /// <summary>
    /// The entries this part of the video is linked to, from any source, such
    /// as an AniDB episode (<c>anidb://episode/1</c>) and its anime
    /// (<c>anidb://series/1</c>). Every source and kind needs a release
    /// cross-reference validator before a release carrying it can be saved.
    /// Only series, episodes and movies may be named; a release naming any
    /// other kind, a season or a collection among them, is refused.
    /// A stored release keeps them with the main entry, the one a video is
    /// linked to, first (<see cref="ReleaseCrossReferenceIDComparer"/>).
    /// </summary>
    IReadOnlyList<MetadataGuid> IDs { get; }

    /// <summary>
    /// Where in the mapped content the video starts covering in
    /// the range [0, 99], but must be less than <see cref="PercentageEnd"/>.
    /// </summary>
    int PercentageStart { get; }

    /// <summary>
    /// Where in the mapped content the video stops covering in
    /// the range [1, 100], but must be greater than <see cref="PercentageStart"/>.
    /// </summary>
    int PercentageEnd { get; }
}
