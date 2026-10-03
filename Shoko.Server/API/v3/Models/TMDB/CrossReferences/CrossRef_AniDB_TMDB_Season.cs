using System;
using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.DependencyInjection;
using Shoko.Abstractions.Core.Services;
using Shoko.Abstractions.Metadata;
using Shoko.Abstractions.Metadata.Containers;
using Shoko.Abstractions.Metadata.CrossReferences;
using Shoko.Abstractions.Metadata.Enums;
using Shoko.Abstractions.Metadata.Services;
using Shoko.Abstractions.Metadata.Shoko;
using Shoko.Server.Models.AniDB;
using Shoko.Server.Models.CrossReference.Embedded;
using Shoko.Server.Models.Metadata;
using Shoko.Server.Models.Shoko;
using Shoko.Server.Repositories;

#pragma warning disable CS0618
namespace Shoko.Server.API.v3.Models.TMDB;

/// <summary>
/// Not actually stored in the database, but made from the episode cross-reference.
/// </summary>
public class CrossRef_AniDB_TMDB_Season : IEquatable<CrossRef_AniDB_TMDB_Season>, IMetadataSeasonCrossReference, IWithImages
{
    #region Columns

    public int AnidbAnimeID { get; set; }

    public int TmdbShowID { get; set; }

    private readonly int _tmdbSeasonID;

    private readonly string? _tmdbEpisodeGroupCollectionID;

    private readonly int? _ordering;

    public string TmdbSeasonID => IsAlternateSeason ? _tmdbEpisodeGroupCollectionID : _tmdbSeasonID.ToString();

    public int SeasonNumber { get; set; }

    /// <summary>
    ///   Inherited from the episode cross-reference this season was projected
    ///   from, since the season itself is never matched directly.
    /// </summary>
    public MatchRating MatchRating { get; set; }

    [MemberNotNullWhen(true, nameof(_tmdbEpisodeGroupCollectionID))]
    public bool IsAlternateSeason => _tmdbSeasonID is < 1 && !string.IsNullOrEmpty(_tmdbEpisodeGroupCollectionID);

    #endregion

    #region Constructors

    public CrossRef_AniDB_TMDB_Season(int anidbAnimeId, int tmdbSeasonId, int tmdbShowId, int seasonNumber = 1, MatchRating rating = MatchRating.UserVerified)
    {
        AnidbAnimeID = anidbAnimeId;
        _tmdbSeasonID = tmdbSeasonId;
        TmdbShowID = tmdbShowId;
        SeasonNumber = seasonNumber;
        MatchRating = rating;
    }

    public CrossRef_AniDB_TMDB_Season(int anidbAnimeId, string tmdbEpisodeGroupCollectionId, int tmdbShowId, int seasonNumber = 1, MatchRating rating = MatchRating.UserVerified)
    {
        AnidbAnimeID = anidbAnimeId;
        _tmdbEpisodeGroupCollectionID = tmdbEpisodeGroupCollectionId;
        TmdbShowID = tmdbShowId;
        SeasonNumber = seasonNumber;
        MatchRating = rating;
    }

    /// <summary>
    ///   TMDB's view of a season link worked out from the episode links,
    ///   keeping its place among the anime's season links.
    /// </summary>
    /// <param name="link">The season link.</param>
    internal CrossRef_AniDB_TMDB_Season(MetadataSeasonCrossReference link)
    {
        AnidbAnimeID = link.AnidbAnimeID;
        if (int.TryParse(link.ProviderID.ID, out var seasonID))
            _tmdbSeasonID = seasonID;
        else
            _tmdbEpisodeGroupCollectionID = link.ProviderID.ID;
        TmdbShowID = int.TryParse(link.ProviderParentID.ID, out var showID) ? showID : 0;
        SeasonNumber = link.SeasonNumber;
        MatchRating = link.MatchRating;
        _ordering = link.Ordering;
    }

    #endregion

    #region Methods

    public AniDB_Anime? AnidbAnime =>
        RepoFactory.AniDB_Anime.GetByAnimeID(AnidbAnimeID);

    public AnimeSeries? AnimeSeries =>
        RepoFactory.AnimeSeries.GetByAnimeID(AnidbAnimeID);

    public Metadata_Season? TmdbSeason =>
        IsAlternateSeason || _tmdbSeasonID is < 1 ? null : RepoFactory.Metadata_Season.GetByProviderID(MetadataSource.TMDB, _tmdbSeasonID.ToString());

    public ISeason? TmdbAlternateOrderingSeason =>
        !IsAlternateSeason || string.IsNullOrEmpty(_tmdbEpisodeGroupCollectionID)
            ? null
            : ISystemService.StaticServices.GetRequiredService<IMetadataService>().GetSeason(new(MetadataSource.TMDB, MetadataEntityType.Season, _tmdbEpisodeGroupCollectionID));

    public Metadata_Series? TmdbShow =>
        TmdbShowID == 0 ? null : RepoFactory.Metadata_Series.GetByProviderID(MetadataSource.TMDB, TmdbShowID.ToString());

    public bool Equals(CrossRef_AniDB_TMDB_Season? other)
    {
        if (other is null) return false;
        if (ReferenceEquals(this, other)) return true;
        return AnidbAnimeID == other.AnidbAnimeID
               && TmdbSeasonID == other.TmdbSeasonID
               && TmdbShowID == other.TmdbShowID
               && SeasonNumber == other.SeasonNumber;
    }

    public override bool Equals(object? obj)
        => Equals(obj as CrossRef_AniDB_TMDB_Season);

    public override int GetHashCode()
        => HashCode.Combine(AnidbAnimeID, TmdbSeasonID, TmdbShowID, SeasonNumber);

    #endregion

    #region IMetadata Implementation

    // The TMDB entry the link points at, whose images the link shows.
    MetadataGuid IMetadata.ID => new(MetadataSource.TMDB, MetadataEntityType.Season, TmdbSeasonID);

    #endregion

    #region IMetadataSeasonCrossReference Implementation

    MetadataGuid? IMetadataCrossReference.ProviderID
        => string.IsNullOrEmpty(TmdbSeasonID) ? null : new(MetadataSource.TMDB, MetadataEntityType.Season, TmdbSeasonID);

    MetadataSource IMetadataCrossReference.Source => MetadataSource.TMDB;

    MetadataGuid IMetadataSeasonCrossReference.ProviderParentID => new(MetadataSource.TMDB, MetadataEntityType.Series, TmdbShowID.ToString());

    MetadataEntityType IMetadataCrossReference.EntityType => MetadataEntityType.Season;

    // A season sorts by its number within the show unless it was worked out
    // among the anime's season links, which keeps its place there.
    int IMetadataCrossReference.Ordering => _ordering ?? SeasonNumber;

    IShokoSeries? IMetadataCrossReference.ShokoSeries => AnimeSeries;

    IMetadata? IMetadataCrossReference.Provider => (IMetadata?)TmdbSeason ?? TmdbAlternateOrderingSeason;

    // Worked out from the episode links, so never written by anyone.
    Guid? IMetadataCrossReference.WrittenBy => null;

    #endregion
}
