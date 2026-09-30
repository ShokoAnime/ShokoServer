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
/// An AniDB episode linked to a TMDB episode, as TMDB's own code reads it. The
/// link itself lives in <see cref="CrossRef_AniDB_Metadata_Episode"/> with
/// every other source's.
/// </summary>
public class CrossRef_AniDB_TMDB_Episode : IMetadataEpisodeCrossReference<ITmdbEpisode>, IWithImages
{
    #region Stored Row

    /// <summary>
    /// The row behind this link. Replaced when the link is saved, so the view
    /// carries the stored row's ID afterwards.
    /// </summary>
    internal CrossRef_AniDB_Metadata_Episode Row { get; set; }

    #endregion

    #region Database Columns

    public int CrossRef_AniDB_TMDB_EpisodeID => Row.CrossRef_AniDB_Metadata_EpisodeID;

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

    public int TmdbShowID
    {
        get => int.TryParse(Row.ProviderParentID, out var showID) ? showID : 0;
        set => Row.ProviderParentID = value is 0 ? string.Empty : value.ToString();
    }

    public int TmdbEpisodeID
    {
        get => int.TryParse(Row.ProviderID, out var episodeID) ? episodeID : 0;
        set => Row.ProviderID = value is 0 ? string.Empty : value.ToString();
    }

    public int Ordering
    {
        get => Row.Ordering;
        set => Row.Ordering = value;
    }

    public MatchRating MatchRating
    {
        get => Row.MatchRating;
        set => Row.MatchRating = value;
    }

    #endregion

    #region Constructors

    internal CrossRef_AniDB_TMDB_Episode(CrossRef_AniDB_Metadata_Episode row)
        => Row = row;

    public CrossRef_AniDB_TMDB_Episode()
        : this(new CrossRef_AniDB_Metadata_Episode { Source = MetadataSource.TMDB }) { }

    public CrossRef_AniDB_TMDB_Episode(int anidbEpisodeId, int anidbAnimeId, int tmdbEpisodeId, int tmdbShowId, MatchRating rating = MatchRating.UserVerified, int ordering = 0)
        : this()
    {
        AnidbEpisodeID = anidbEpisodeId;
        AnidbAnimeID = anidbAnimeId;
        TmdbEpisodeID = tmdbEpisodeId;
        TmdbShowID = tmdbShowId;
        Ordering = ordering;
        MatchRating = rating;
    }

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

    public TMDB_Episode? TmdbEpisode =>
        TmdbEpisodeID == 0 ? null : RepoFactory.TMDB_Episode.GetByTmdbEpisodeID(TmdbEpisodeID);

    public CrossRef_AniDB_TMDB_Season? TmdbSeasonCrossReference =>
        TmdbEpisode is { } tmdbEpisode
            ? new(AnidbAnimeID, tmdbEpisode.TmdbSeasonID, TmdbShowID, tmdbEpisode.SeasonNumber, MatchRating)
            : null;

    public TMDB_Season? TmdbSeason =>
        TmdbEpisode?.TmdbSeason;

    public TMDB_Show? TmdbShow =>
        TmdbShowID == 0 ? null : RepoFactory.TMDB_Show.GetByTmdbShowID(TmdbShowID);

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

    ITmdbEpisode? IMetadataCrossReference<ITmdbEpisode>.Provider => TmdbEpisode;

    #endregion
}
