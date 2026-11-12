using System;
using System.Collections.Generic;
using System.Linq;
using Shoko.Abstractions.Extensions;
using Shoko.Abstractions.Metadata;
using Shoko.Abstractions.Metadata.Anidb;
using Shoko.Abstractions.Metadata.CrossReferences;
using Shoko.Abstractions.Metadata.Shoko;
using Shoko.Abstractions.Video;
using Shoko.Abstractions.Video.Release;
using Shoko.Server.Models.CrossReference;
using Shoko.Server.Repositories;

namespace Shoko.Server.Models.Release;

/// <summary>
///   A direct link from a video to the main entry of one cross-reference of
///   its stored release. The entry and what it reaches are read on first use.
/// </summary>
public sealed class MetadataVideoCrossReference : IMetadataVideoCrossReference
{
    #region Fields

    private readonly StoredReleaseInfo _release;

    private readonly IReleaseVideoCrossReference _crossReference;

    private readonly Lazy<IMetadata?> _entry;

    #endregion

    #region Constructors

    /// <summary>
    ///   Links a video to the main entry of a cross-reference of its release.
    /// </summary>
    /// <param name="release">The stored release.</param>
    /// <param name="crossReference">The cross-reference of the release.</param>
    /// <param name="id">Its main entry.</param>
    private MetadataVideoCrossReference(StoredReleaseInfo release, IReleaseVideoCrossReference crossReference, MetadataGuid id)
    {
        _release = release;
        _crossReference = crossReference;
        ID = id;
        _entry = new(() => MetadataEntryResolver.Resolve(id));
    }

    /// <summary>
    ///   The links a release makes: one per cross-reference, to its main entry.
    /// </summary>
    /// <param name="release">The stored release.</param>
    /// <returns>The links.</returns>
    public static IEnumerable<IMetadataVideoCrossReference> For(StoredReleaseInfo release)
        => For(release.CrossReferences.Select(xref => (IReleaseVideoCrossReference)xref), release);

    /// <summary>
    ///   The links of the cross-references of a release naming an entry, its
    ///   main one or not: the same links <see cref="For(StoredReleaseInfo)"/>
    ///   gives for them.
    /// </summary>
    /// <param name="release">The stored release.</param>
    /// <param name="id">The entry.</param>
    /// <returns>The links.</returns>
    public static IEnumerable<IMetadataVideoCrossReference> For(StoredReleaseInfo release, MetadataGuid id)
        => For(release.CrossReferences.Where(xref => xref.IDs.Contains(id)), release);

    private static IEnumerable<IMetadataVideoCrossReference> For(IEnumerable<IReleaseVideoCrossReference> crossReferences, StoredReleaseInfo release)
        => crossReferences
            .Select(xref => xref.MainID is { } main ? new MetadataVideoCrossReference(release, xref, main) : null)
            .WhereNotNull();

    #endregion

    #region Identity

    /// <inheritdoc/>
    public MetadataGuid ID { get; }

    /// <inheritdoc/>
    public string ED2K => _release.ED2K;

    /// <inheritdoc/>
    public long Size => _release.FileSize;

    #endregion

    #region Range

    /// <inheritdoc/>
    public IReadOnlyList<MetadataGuid> IDs => _crossReference.IDs;

    /// <inheritdoc/>
    public int PercentageStart => _crossReference.PercentageStart;

    /// <inheritdoc/>
    public int PercentageEnd => _crossReference.PercentageEnd;

    /// <inheritdoc/>
    public int Percentage => PercentageEnd - PercentageStart;

    /// <inheritdoc/>
    public int PercentageGroupSize => CrossRef_File_Episode.PercentageToFileCount(Percentage);

    #endregion

    #region Navigation

    /// <inheritdoc/>
    public IVideo? Video => RepoFactory.VideoLocal.GetByEd2kAndSize(_release.ED2K, _release.FileSize);

    /// <inheritdoc/>
    public IReleaseInfo Release => _release;

    #endregion

    #region Entry

    /// <inheritdoc/>
    public IMetadata? Entry => _entry.Value;

    /// <inheritdoc/>
    public IEpisode? Episode => Entry as IEpisode;

    /// <inheritdoc/>
    public ISeries? Series => Entry as ISeries;

    /// <inheritdoc/>
    public IMovie? Movie => Entry as IMovie;

    #endregion

    #region AniDB and Shoko

    /// <inheritdoc/>
    public IAnidbEpisode? AnidbEpisode => Entry as IAnidbEpisode;

    /// <inheritdoc/>
    public IAnidbAnime? AnidbAnime => ResolvedAnidbAnimeID is > 0 and var animeID ? RepoFactory.AniDB_Anime.GetByAnimeID(animeID) : null;

    /// <inheritdoc/>
    public IShokoEpisode? ShokoEpisode => this.AnidbEpisodeID is > 0 and var episodeID ? RepoFactory.AnimeEpisode.GetByAniDBEpisodeID(episodeID) : null;

    /// <inheritdoc/>
    public IShokoSeries? ShokoSeries => ResolvedAnidbAnimeID is > 0 and var animeID ? RepoFactory.AnimeSeries.GetByAnimeID(animeID) : null;

    /// <inheritdoc/>
    public IReadOnlyList<IShokoEpisode> LinkedShokoEpisodes
        => this.IsAnidb
            ? ShokoEpisode is { } shokoEpisode ? [shokoEpisode] : []
            : Episode?.ShokoEpisodes ?? Movie?.ShokoEpisodes ?? [];

    /// <inheritdoc/>
    public IReadOnlyList<IShokoSeries> LinkedShokoSeries
        => this.IsAnidb
            ? ShokoSeries is { } shokoSeries ? [shokoSeries] : []
            : Series?.ShokoSeries ?? Movie?.ShokoSeries ?? Episode?.ShokoEpisodes.Select(episode => episode.Series).DistinctBy(series => series.ID).ToList() ?? [];

    /// <summary>
    ///   The AniDB anime the entry is an episode of, as the cross-reference
    ///   names it or else as the episode says, or <c>0</c>.
    /// </summary>
    private int ResolvedAnidbAnimeID
        => this.AnidbEpisodeID is > 0 and var episodeID
            ? this.AnidbAnimeID ?? RepoFactory.AniDB_Episode.GetByEpisodeID(episodeID)?.AnimeID ?? 0
            : 0;

    #endregion

    #region Metadata Cross-References

    /// <inheritdoc/>
    public IReadOnlyList<IMetadataSeriesCrossReference> MetadataSeriesCrossReferences
        => Series?.MetadataSeriesCrossReferences ?? Episode?.MetadataSeriesCrossReferences ?? [];

    /// <inheritdoc/>
    public IReadOnlyList<IMetadataSeasonCrossReference> MetadataSeasonCrossReferences
        => Series?.MetadataSeasonCrossReferences ??
            (AnidbEpisode is { } anidbEpisode ? anidbEpisode.Series.MetadataSeasonCrossReferences : Episode?.Season?.MetadataSeasonCrossReferences) ??
            [];

    /// <inheritdoc/>
    public IReadOnlyList<IMetadataEpisodeCrossReference> MetadataEpisodeCrossReferences
        => Episode?.MetadataEpisodeCrossReferences ?? Series?.MetadataEpisodeCrossReferences ?? [];

    /// <inheritdoc/>
    public IReadOnlyList<IMetadataMovieCrossReference> MetadataMovieCrossReferences
        => Movie?.MetadataMovieCrossReferences ?? Episode?.MetadataMovieCrossReferences ?? Series?.MetadataMovieCrossReferences ?? [];

    #endregion
}
