using Shoko.Abstractions.Metadata;
using Shoko.Abstractions.Metadata.CrossReferences;
using Shoko.Abstractions.Metadata.Shoko;
using Shoko.Server.Repositories;

namespace Shoko.Server.Models.CrossReference;

/// <summary>
/// One AniDB episode linked to a provider's episode.
/// </summary>
public class CrossRef_AniDB_Metadata_Episode : CrossRef_AniDB_Metadata, IMetadataEpisodeCrossReference
{
    #region Database Columns

    public int CrossRef_AniDB_Metadata_EpisodeID { get; set; }

    /// <summary>
    /// The AniDB episode being linked.
    /// </summary>
    public int AnidbEpisodeID { get; set; }

    /// <summary>
    /// The provider's entry above this one, such as the show the episode
    /// belongs to. Empty when there is none.
    /// </summary>
    public string ProviderParentID { get; set; } = string.Empty;

    /// <summary>
    /// The provider's own ID for the season the episode sits in, or
    /// <c>null</c> when it is not known.
    /// </summary>
    public string? ProviderSeasonID { get; set; }

    /// <summary>
    /// The number of the season the episode sits in, when known.
    /// </summary>
    public int? SeasonNumber { get; set; }

    /// <summary>
    /// The episode's number within its season, when known.
    /// </summary>
    public int? EpisodeNumber { get; set; }

    #endregion

    #region Cross-Reference Implementation

    /// <inheritdoc />
    public override MetadataEntityType EntityType => MetadataEntityType.Episode;

    /// <inheritdoc />
    public IShokoEpisode? ShokoEpisode => RepoFactory.AnimeEpisode.GetByAniDBEpisodeID(AnidbEpisodeID);

    /// <inheritdoc />
    MetadataGuid? IMetadataEpisodeCrossReference.ProviderParentID => ToProviderID(Source, MetadataEntityType.Series, ProviderParentID);

    /// <inheritdoc />
    MetadataGuid? IMetadataEpisodeCrossReference.SeasonID
        => ToProviderID(Source, MetadataEntityType.Season, ProviderSeasonID) ?? TmdbEpisode?.SeasonID;

    /// <inheritdoc />
    int? IMetadataEpisodeCrossReference.SeasonNumber => SeasonNumber ?? TmdbEpisode?.SeasonNumber;

    /// <inheritdoc />
    int? IMetadataEpisodeCrossReference.EpisodeNumber => EpisodeNumber ?? TmdbEpisode?.EpisodeNumber;

    /// <summary>
    /// The TMDB episode a TMDB link names, which fills in what the link does
    /// not record. TMDB keeps its own tables, which stay current where a copy
    /// on the link would not, so its links are written without the numbers.
    /// </summary>
    private IEpisode? TmdbEpisode
        => Source == MetadataSource.TMDB && int.TryParse(ProviderID, out var episodeID) && episodeID > 0
            ? RepoFactory.TMDB_Episode.GetByTmdbEpisodeID(episodeID)
            : null;

    #endregion

    #region Storage

    /// <inheritdoc />
    internal override int RowID => CrossRef_AniDB_Metadata_EpisodeID;

    /// <inheritdoc />
    internal override (MetadataSource Source, int AnidbAnimeID, int AnidbEpisodeID) Slot => (Source, AnidbAnimeID, AnidbEpisodeID);

    #endregion
}
