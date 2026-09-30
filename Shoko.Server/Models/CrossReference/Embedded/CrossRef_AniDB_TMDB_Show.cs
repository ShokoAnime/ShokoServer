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
/// An AniDB anime linked to a TMDB show, as TMDB's own code reads it. The link
/// itself lives in <see cref="CrossRef_AniDB_Metadata"/> with every other
/// source's.
/// </summary>
public class CrossRef_AniDB_TMDB_Show : IMetadataSeriesCrossReference<ITmdbShow>, IWithImages
{
    #region Stored Row

    /// <summary>
    /// The row behind this link. Replaced when the link is saved, so the view
    /// carries the stored row's ID afterwards.
    /// </summary>
    internal CrossRef_AniDB_Metadata_Series Row { get; set; }

    #endregion

    #region Database Columns

    public int CrossRef_AniDB_TMDB_ShowID => Row.CrossRef_AniDB_Metadata_SeriesID;

    public int AnidbAnimeID
    {
        get => Row.AnidbAnimeID;
        set => Row.AnidbAnimeID = value;
    }

    public int TmdbShowID
    {
        get => int.TryParse(Row.ProviderID, out var showID) ? showID : 0;
        set => Row.ProviderID = value is 0 ? string.Empty : value.ToString();
    }

    public MatchRating MatchRating
    {
        get => Row.MatchRating;
        set => Row.MatchRating = value;
    }

    #endregion

    #region Constructors

    internal CrossRef_AniDB_TMDB_Show(CrossRef_AniDB_Metadata_Series row)
        => Row = row;

    public CrossRef_AniDB_TMDB_Show()
        : this(new CrossRef_AniDB_Metadata_Series { Source = MetadataSource.TMDB }) { }

    public CrossRef_AniDB_TMDB_Show(int anidbAnimeId, int tmdbShowId, MatchRating matchRating = MatchRating.UserVerified)
        : this()
    {
        AnidbAnimeID = anidbAnimeId;
        TmdbShowID = tmdbShowId;
        MatchRating = matchRating;
    }

    #endregion

    #region Methods

    public AniDB_Anime? AnidbAnime =>
        RepoFactory.AniDB_Anime.GetByAnimeID(AnidbAnimeID);

    public AnimeSeries? AnimeSeries =>
        RepoFactory.AnimeSeries.GetByAnimeID(AnidbAnimeID);

    public TMDB_Show? TmdbShow =>
        RepoFactory.TMDB_Show.GetByTmdbShowID(TmdbShowID);

    #endregion

    #region IMetadata Implementation

    // The TMDB entry the link points at, whose images the link shows.
    MetadataGuid IMetadata.ID => new(MetadataSource.TMDB, MetadataEntityType.Series, TmdbShowID.ToString());

    #endregion

    #region IMetadataSeriesCrossReference Implementation

    MetadataGuid? IMetadataCrossReference.ProviderID => TmdbShowID is 0 ? null : new(MetadataSource.TMDB, MetadataEntityType.Series, TmdbShowID.ToString());

    MetadataEntityType IMetadataCrossReference.EntityType => MetadataEntityType.Series;

    MetadataSource IMetadataCrossReference.Source => MetadataSource.TMDB;

    int IMetadataCrossReference.Ordering => Row.Ordering;

    Guid? IMetadataCrossReference.WrittenBy => Row.WrittenBy;

    IShokoSeries? IMetadataCrossReference.ShokoSeries => AnimeSeries;

    IMetadata? IMetadataCrossReference.Provider => TmdbShow;

    ITmdbShow? IMetadataCrossReference<ITmdbShow>.Provider => TmdbShow;

    #endregion
}
