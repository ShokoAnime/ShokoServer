using System.Collections.Generic;
using Shoko.Abstractions.Metadata;
using Shoko.Abstractions.Metadata.Anidb;
using Shoko.Abstractions.Metadata.CrossReferences;
using Shoko.Abstractions.Metadata.Shoko;
using Shoko.Abstractions.Video.Release;

namespace Shoko.Abstractions.Video;

/// <summary>
///   A direct link from a video to an entry of any source, one per
///   cross-reference of the video's stored release, to its main entry (see
///   <see cref="ReleaseCrossReferenceIDComparer"/>). A cross-reference naming
///   an AniDB episode and its anime gives one link, to the episode, and asking
///   for the links of the anime gives that same link.
/// </summary>
/// <remarks>
///   Built from the stored releases, so nothing of its own is stored.
///   <see cref="IVideoCrossReference"/> stays the AniDB-bound view, one per
///   AniDB episode a video is linked to; AniDB entries get one of these too.
/// </remarks>
public interface IMetadataVideoCrossReference : IReleaseVideoCrossReference
{
    #region Identity

    /// <summary>
    ///   The entry the video is linked to: the main entry of
    ///   <see cref="IReleaseVideoCrossReference.IDs"/>, which names the
    ///   cross-reference's other entries too.
    /// </summary>
    MetadataGuid ID { get; }

    /// <summary>
    ///   ED2K hash used for video identification.
    /// </summary>
    string ED2K { get; }

    /// <summary>
    ///   File size used for video identification.
    /// </summary>
    long Size { get; }

    #endregion

    #region Range

    /// <summary>
    ///   How much of the entry the video covers,
    ///   <see cref="IReleaseVideoCrossReference.PercentageEnd"/> less
    ///   <see cref="IReleaseVideoCrossReference.PercentageStart"/>.
    /// </summary>
    int Percentage { get; }

    /// <summary>
    ///   Inferred number of videos sharing the entry, from the
    ///   <see cref="Percentage"/>. E.g. 1 for 100%, 2 for 50%, etc.
    /// </summary>
    int PercentageGroupSize { get; }

    #endregion

    #region Navigation

    /// <summary>
    ///   The local video, if available.
    /// </summary>
    IVideo? Video { get; }

    /// <summary>
    ///   The stored release the link comes from.
    /// </summary>
    IReleaseInfo Release { get; }

    #endregion

    #region Entry

    /// <summary>
    ///   The entry, resolved through the metadata service, if it is known.
    /// </summary>
    IMetadata? Entry { get; }

    /// <summary>
    ///   The entry, if it is an episode.
    /// </summary>
    IEpisode? Episode { get; }

    /// <summary>
    ///   The entry, if it is a series.
    /// </summary>
    ISeries? Series { get; }

    /// <summary>
    ///   The entry, if it is a movie.
    /// </summary>
    IMovie? Movie { get; }

    #endregion

    #region AniDB and Shoko

    /// <summary>
    ///   The AniDB episode, if the entry is one and it is available.
    /// </summary>
    IAnidbEpisode? AnidbEpisode { get; }

    /// <summary>
    ///   The AniDB anime, if the entry is one or an episode of one, and it is
    ///   available.
    /// </summary>
    IAnidbAnime? AnidbAnime { get; }

    /// <summary>
    ///   The Shoko episode of the AniDB episode, if the entry is one.
    /// </summary>
    IShokoEpisode? ShokoEpisode { get; }

    /// <summary>
    ///   The Shoko series of the AniDB anime, if the entry is one or an
    ///   episode of one.
    /// </summary>
    IShokoSeries? ShokoSeries { get; }

    /// <summary>
    ///   The Shoko episodes the entry reaches: for an AniDB episode its own,
    ///   otherwise those of the AniDB episodes the entry is linked to.
    /// </summary>
    IReadOnlyList<IShokoEpisode> LinkedShokoEpisodes { get; }

    /// <summary>
    ///   The Shoko series the entry reaches: for an AniDB entry its own,
    ///   otherwise those of the AniDB anime the entry is linked to.
    /// </summary>
    IReadOnlyList<IShokoSeries> LinkedShokoSeries { get; }

    #endregion

    #region Metadata Cross-References

    /// <summary>
    ///   The series-level cross-references between AniDB and other sources
    ///   for the entry, or for the series of an episode.
    /// </summary>
    IReadOnlyList<IMetadataSeriesCrossReference> MetadataSeriesCrossReferences { get; }

    /// <summary>
    ///   The season-level cross-references between AniDB and other sources
    ///   for the entry, or for the season or series of an episode.
    /// </summary>
    IReadOnlyList<IMetadataSeasonCrossReference> MetadataSeasonCrossReferences { get; }

    /// <summary>
    ///   The episode-level cross-references between AniDB and other sources
    ///   for the entry, or for the episodes of a series or season.
    /// </summary>
    IReadOnlyList<IMetadataEpisodeCrossReference> MetadataEpisodeCrossReferences { get; }

    /// <summary>
    ///   The film cross-references between AniDB and other sources for the
    ///   entry.
    /// </summary>
    IReadOnlyList<IMetadataMovieCrossReference> MetadataMovieCrossReferences { get; }

    #endregion
}
