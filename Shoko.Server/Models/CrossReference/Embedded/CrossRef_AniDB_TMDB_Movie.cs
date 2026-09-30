using System;
using Shoko.Abstractions.Metadata;
using Shoko.Abstractions.Metadata.Containers;
using Shoko.Abstractions.Metadata.CrossReferences;
using Shoko.Abstractions.Metadata.Enums;
using Shoko.Abstractions.Metadata.Shoko;
using Shoko.Abstractions.Metadata.Tmdb;
using Shoko.Server.Models.AniDB;
using Shoko.Server.Models.Shoko;
using Shoko.Server.Models.TMDB;
using Shoko.Server.Repositories;

#pragma warning disable CS0618
namespace Shoko.Server.Models.CrossReference.Embedded;

/// <summary>
/// An AniDB anime linked to a TMDB movie, as TMDB's own code reads it. The link
/// itself lives in <see cref="CrossRef_AniDB_Metadata_Movie"/> with every other
/// source's.
/// </summary>
public class CrossRef_AniDB_TMDB_Movie : IMetadataMovieCrossReference<ITmdbMovie>, IWithImages
{
    #region Stored Row

    /// <summary>
    /// The row behind this link. Replaced when the link is saved, so the view
    /// carries the stored row's ID afterwards.
    /// </summary>
    internal CrossRef_AniDB_Metadata_Movie Row { get; set; }

    #endregion

    #region Database Columns

    public int CrossRef_AniDB_TMDB_MovieID => Row.CrossRef_AniDB_Metadata_MovieID;

    public int AnidbAnimeID
    {
        get => Row.AnidbAnimeID;
        set => Row.AnidbAnimeID = value;
    }

    public int AnidbEpisodeID
    {
        get => Row.AnidbEpisodeID;
        set => Row.AnidbEpisodeID = value;
    }

    public int TmdbMovieID
    {
        get => int.TryParse(Row.ProviderID, out var movieID) ? movieID : 0;
        set => Row.ProviderID = value is 0 ? string.Empty : value.ToString();
    }

    public MatchRating MatchRating
    {
        get => Row.MatchRating;
        set => Row.MatchRating = value;
    }

    #endregion

    #region Constructors

    internal CrossRef_AniDB_TMDB_Movie(CrossRef_AniDB_Metadata_Movie row)
        => Row = row;

    public CrossRef_AniDB_TMDB_Movie()
        : this(new CrossRef_AniDB_Metadata_Movie { Source = MetadataSource.TMDB }) { }

    public CrossRef_AniDB_TMDB_Movie(int anidbEpisodeId, int anidbAnimeId, int tmdbMovieId, MatchRating matchRating = MatchRating.UserVerified)
        : this()
    {
        AnidbEpisodeID = anidbEpisodeId;
        AnidbAnimeID = anidbAnimeId;
        TmdbMovieID = tmdbMovieId;
        MatchRating = matchRating;
    }

    #endregion

    #region Methods

    public AniDB_Episode? AnidbEpisode => RepoFactory.AniDB_Episode.GetByEpisodeID(AnidbEpisodeID);

    public AniDB_Anime? AnidbAnime =>
        RepoFactory.AniDB_Anime.GetByAnimeID(AnidbAnimeID);

    public AnimeEpisode? AnimeEpisode => RepoFactory.AnimeEpisode.GetByAniDBEpisodeID(AnidbEpisodeID);

    public AnimeSeries? AnimeSeries =>
        RepoFactory.AnimeSeries.GetByAnimeID(AnidbAnimeID);

    public TMDB_Movie? TmdbMovie
        => RepoFactory.TMDB_Movie.GetByTmdbMovieID(TmdbMovieID);

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

    ITmdbMovie? IMetadataCrossReference<ITmdbMovie>.Provider => TmdbMovie;

    #endregion
}
