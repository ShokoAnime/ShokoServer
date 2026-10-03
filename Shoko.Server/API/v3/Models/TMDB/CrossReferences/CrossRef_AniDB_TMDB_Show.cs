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
///   An AniDB anime linked to a TMDB show, read the way the TMDB models give
///   it. The link lives in <see cref="CrossRef_AniDB_Metadata_Series"/> with
///   every other source's.
/// </summary>
public class CrossRef_AniDB_TMDB_Show : IMetadataSeriesCrossReference, IWithImages
{
    #region Stored Row

    /// <summary>
    ///   The row behind this link.
    /// </summary>
    internal CrossRef_AniDB_Metadata_Series Row { get; }

    #endregion

    #region Columns

    public int CrossRef_AniDB_TMDB_ShowID => Row.CrossRef_AniDB_Metadata_SeriesID;

    public int AnidbAnimeID => Row.AnidbAnimeID;

    public int TmdbShowID => int.TryParse(Row.ProviderID, out var showID) ? showID : 0;

    public MatchRating MatchRating => Row.MatchRating;

    #endregion

    #region Constructors

    internal CrossRef_AniDB_TMDB_Show(CrossRef_AniDB_Metadata_Series row)
        => Row = row;

    #endregion

    #region Methods

    public AniDB_Anime? AnidbAnime =>
        RepoFactory.AniDB_Anime.GetByAnimeID(AnidbAnimeID);

    public AnimeSeries? AnimeSeries =>
        RepoFactory.AnimeSeries.GetByAnimeID(AnidbAnimeID);

    public Metadata_Series? TmdbShow =>
        RepoFactory.Metadata_Series.GetByProviderID(MetadataSource.TMDB, TmdbShowID.ToString());

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

    #endregion
}
