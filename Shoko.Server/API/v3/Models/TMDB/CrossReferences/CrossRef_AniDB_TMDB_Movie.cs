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
///   An AniDB anime linked to a TMDB movie, read the way the TMDB models give
///   it. The link lives in <see cref="CrossRef_AniDB_Metadata_Movie"/> with
///   every other source's.
/// </summary>
public class CrossRef_AniDB_TMDB_Movie : IMetadataMovieCrossReference, IWithImages
{
    #region Stored Row

    /// <summary>
    ///   The row behind this link.
    /// </summary>
    internal CrossRef_AniDB_Metadata_Movie Row { get; }

    #endregion

    #region Columns

    public int CrossRef_AniDB_TMDB_MovieID => Row.CrossRef_AniDB_Metadata_MovieID;

    public int AnidbAnimeID => Row.AnidbAnimeID;

    public int AnidbEpisodeID => Row.AnidbEpisodeID;

    public int TmdbMovieID => int.TryParse(Row.ProviderID, out var movieID) ? movieID : 0;

    public MatchRating MatchRating => Row.MatchRating;

    #endregion

    #region Constructors

    internal CrossRef_AniDB_TMDB_Movie(CrossRef_AniDB_Metadata_Movie row)
        => Row = row;

    #endregion

    #region Methods

    public AniDB_Episode? AnidbEpisode => RepoFactory.AniDB_Episode.GetByEpisodeID(AnidbEpisodeID);

    public AniDB_Anime? AnidbAnime =>
        RepoFactory.AniDB_Anime.GetByAnimeID(AnidbAnimeID);

    public AnimeEpisode? AnimeEpisode => RepoFactory.AnimeEpisode.GetByAniDBEpisodeID(AnidbEpisodeID);

    public AnimeSeries? AnimeSeries =>
        RepoFactory.AnimeSeries.GetByAnimeID(AnidbAnimeID);

    public Metadata_Movie? TmdbMovie
        => RepoFactory.Metadata_Movie.GetByProviderID(MetadataSource.TMDB, TmdbMovieID.ToString());

    #endregion

    #region IMetadata Implementation

    // The TMDB entry the link points at, whose images the link shows.
    MetadataGuid IMetadata.ID => new(MetadataSource.TMDB, MetadataEntityType.Movie, TmdbMovieID.ToString());

    #endregion

    #region IMetadataMovieCrossReference Implementation

    MetadataGuid? IMetadataCrossReference.ProviderID => TmdbMovieID is 0 ? null : new(MetadataSource.TMDB, MetadataEntityType.Movie, TmdbMovieID.ToString());

    MetadataEntityType IMetadataCrossReference.EntityType => MetadataEntityType.Movie;

    MetadataSource IMetadataCrossReference.Source => MetadataSource.TMDB;

    int IMetadataCrossReference.Ordering => Row.Ordering;

    Guid? IMetadataCrossReference.WrittenBy => Row.WrittenBy;

    IShokoSeries? IMetadataCrossReference.ShokoSeries => AnimeSeries;

    IShokoEpisode? IMetadataMovieCrossReference.ShokoEpisode => AnimeEpisode;

    IMetadata? IMetadataCrossReference.Provider => TmdbMovie;

    #endregion
}
