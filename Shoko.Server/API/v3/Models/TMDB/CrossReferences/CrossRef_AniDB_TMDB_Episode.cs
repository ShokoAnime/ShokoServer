using System;
using Shoko.Abstractions.Metadata;
using Shoko.Abstractions.Metadata.Containers;
using Shoko.Abstractions.Metadata.CrossReferences;
using Shoko.Abstractions.Metadata.Enums;
using Shoko.Abstractions.Metadata.Shoko;
using Shoko.Server.Models.AniDB;
using Shoko.Server.Models.CrossReference;
using Shoko.Server.Models.Metadata;
using Shoko.Server.Models.Shoko;
using Shoko.Server.Repositories;

namespace Shoko.Server.API.v3.Models.TMDB;

/// <summary>
///   An AniDB episode linked to a TMDB episode, read the way the TMDB models
///   give it. The link lives in <see cref="CrossRef_AniDB_Metadata_Episode"/>
///   with every other source's.
/// </summary>
public class CrossRef_AniDB_TMDB_Episode : IMetadataEpisodeCrossReference, IWithImages
{
    #region Stored Row

    /// <summary>
    ///   The row behind this link.
    /// </summary>
    internal CrossRef_AniDB_Metadata_Episode Row { get; }

    #endregion

    #region Columns

    public int CrossRef_AniDB_TMDB_EpisodeID => Row.CrossRef_AniDB_Metadata_EpisodeID;

    public int AnidbAnimeID => Row.AnidbAnimeID;

    public int AnidbEpisodeID => Row.AnidbEpisodeID;

    public int TmdbShowID => int.TryParse(Row.ProviderParentID, out var showID) ? showID : 0;

    public int TmdbEpisodeID => int.TryParse(Row.ProviderID, out var episodeID) ? episodeID : 0;

    public int Ordering => Row.Ordering;

    public MatchRating MatchRating => Row.MatchRating;

    #endregion

    #region Constructors

    internal CrossRef_AniDB_TMDB_Episode(CrossRef_AniDB_Metadata_Episode row)
        => Row = row;

    #endregion

    #region Methods

    public AniDB_Episode? AnidbEpisode =>
        RepoFactory.AniDB_Episode.GetByEpisodeID(AnidbEpisodeID);

    public AniDB_Anime? AnidbAnime =>
        RepoFactory.AniDB_Anime.GetByAnimeID(AnidbAnimeID);

    public AnimeEpisode? AnimeEpisode =>
        RepoFactory.AnimeEpisode.GetByAniDBEpisodeID(AnidbEpisodeID);

    public AnimeSeries? AnimeSeries =>
        RepoFactory.AnimeSeries.GetByAnimeID(AnidbAnimeID);

    public Metadata_Episode? TmdbEpisode =>
        TmdbEpisodeID == 0 ? null : RepoFactory.Metadata_Episode.GetByProviderID(MetadataSource.TMDB, TmdbEpisodeID.ToString());

    public CrossRef_AniDB_TMDB_Season? TmdbSeasonCrossReference =>
        TmdbEpisode is { SeasonID: { } seasonID } tmdbEpisode && int.TryParse(seasonID, out var tmdbSeasonID)
            ? new(AnidbAnimeID, tmdbSeasonID, TmdbShowID, tmdbEpisode.SeasonNumber ?? 0, MatchRating)
            : null;

    public Metadata_Season? TmdbSeason =>
        TmdbEpisode is { SeasonID: { } seasonID } ? RepoFactory.Metadata_Season.GetByProviderID(MetadataSource.TMDB, seasonID) : null;

    public Metadata_Series? TmdbShow =>
        TmdbShowID == 0 ? null : RepoFactory.Metadata_Series.GetByProviderID(MetadataSource.TMDB, TmdbShowID.ToString());

    #endregion

    #region IMetadata Implementation

    // The TMDB entry the link points at, whose images the link shows.
    MetadataGuid IMetadata.ID => new(MetadataSource.TMDB, MetadataEntityType.Episode, TmdbEpisodeID.ToString());

    #endregion

    #region IMetadataEpisodeCrossReference Implementation

    MetadataGuid? IMetadataEpisodeCrossReference.ProviderParentID
        => TmdbShowID is 0 ? null : new(MetadataSource.TMDB, MetadataEntityType.Series, TmdbShowID.ToString());

    MetadataGuid? IMetadataEpisodeCrossReference.SeasonID => ((IMetadataEpisodeCrossReference)Row).SeasonID;

    int? IMetadataEpisodeCrossReference.SeasonNumber => ((IMetadataEpisodeCrossReference)Row).SeasonNumber;

    int? IMetadataEpisodeCrossReference.EpisodeNumber => ((IMetadataEpisodeCrossReference)Row).EpisodeNumber;

    // Zero is how this source records an episode deliberately linked to
    // nothing, which the contract spells as no entry.
    MetadataGuid? IMetadataCrossReference.ProviderID
        => TmdbEpisodeID is 0 ? null : new(MetadataSource.TMDB, MetadataEntityType.Episode, TmdbEpisodeID.ToString());

    MetadataEntityType IMetadataCrossReference.EntityType => MetadataEntityType.Episode;

    MetadataSource IMetadataCrossReference.Source => MetadataSource.TMDB;

    int IMetadataCrossReference.Ordering => Ordering;

    Guid? IMetadataCrossReference.WrittenBy => Row.WrittenBy;

    IShokoSeries? IMetadataCrossReference.ShokoSeries => AnimeSeries;

    IShokoEpisode? IMetadataEpisodeCrossReference.ShokoEpisode => AnimeEpisode;

    IMetadata? IMetadataCrossReference.Provider => TmdbEpisode;

    #endregion
}
