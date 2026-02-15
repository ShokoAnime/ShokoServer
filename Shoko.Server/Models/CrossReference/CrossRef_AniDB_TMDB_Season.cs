using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Shoko.Abstractions.Enums;
using Shoko.Abstractions.Metadata;
using Shoko.Abstractions.Metadata.Containers;
using Shoko.Abstractions.Metadata.Shoko;
using Shoko.Abstractions.Metadata.Tmdb;
using Shoko.Abstractions.Metadata.Tmdb.CrossReferences;
using Shoko.Server.Models.AniDB;
using Shoko.Server.Models.Shoko;
using Shoko.Server.Models.TMDB;
using Shoko.Server.Repositories;

#nullable enable
namespace Shoko.Server.Models.CrossReference;

/// <summary>
/// Not actually stored in the database, but made from the episode cross-reference.
/// </summary>
public class CrossRef_AniDB_TMDB_Season : IEquatable<CrossRef_AniDB_TMDB_Season>, ITmdbSeasonCrossReference
{
    #region Columns

    public int AnidbAnimeID { get; set; }

    public int TmdbShowID { get; set; }

    private readonly int _tmdbSeasonID;

    private readonly string? _tmdbEpisodeGroupCollectionID;

    public string TmdbSeasonID => IsAlternateSeason ? _tmdbEpisodeGroupCollectionID : _tmdbSeasonID.ToString();

    public int SeasonNumber { get; set; }

    [MemberNotNullWhen(true, nameof(_tmdbEpisodeGroupCollectionID))]
    public bool IsAlternateSeason => _tmdbSeasonID is < 1 && !string.IsNullOrEmpty(_tmdbEpisodeGroupCollectionID);

    #endregion

    #region Constructors

    public CrossRef_AniDB_TMDB_Season(int anidbAnimeId, int tmdbSeasonId, int tmdbShowId, int seasonNumber = 1)
    {
        AnidbAnimeID = anidbAnimeId;
        _tmdbSeasonID = tmdbSeasonId;
        TmdbShowID = tmdbShowId;
        SeasonNumber = seasonNumber;
    }

    public CrossRef_AniDB_TMDB_Season(int anidbAnimeId, string tmdbEpisodeGroupCollectionId, int tmdbShowId, int seasonNumber = 1)
    {
        AnidbAnimeID = anidbAnimeId;
        _tmdbEpisodeGroupCollectionID = tmdbEpisodeGroupCollectionId;
        TmdbShowID = tmdbShowId;
        SeasonNumber = seasonNumber;
    }

    #endregion
    #region Methods

    public AniDB_Anime? AnidbAnime =>
        RepoFactory.AniDB_Anime.GetByAnimeID(AnidbAnimeID);

    public AnimeSeries? AnimeSeries =>
        RepoFactory.AnimeSeries.GetByAnimeID(AnidbAnimeID);

    public TMDB_Season? TmdbSeason =>
        IsAlternateSeason || _tmdbSeasonID is < 1 ? null : RepoFactory.TMDB_Season.GetByTmdbSeasonID(_tmdbSeasonID);

    public TMDB_AlternateOrdering_Season? TmdbAlternateOrderingSeason =>
        !IsAlternateSeason || string.IsNullOrEmpty(_tmdbEpisodeGroupCollectionID) ? null : RepoFactory.TMDB_AlternateOrdering_Season.GetByTmdbEpisodeGroupID(_tmdbEpisodeGroupCollectionID);

    public TMDB_Show? TmdbShow =>
        TmdbShowID == 0 ? null : RepoFactory.TMDB_Show.GetByTmdbShowID(TmdbShowID);

    /// <summary>
    /// Get all images for the episode, or all images for the given
    /// <paramref name="entityType"/> provided for the episode.
    /// </summary>
    /// <param name="entityType">If set, will restrict the returned list to only
    /// containing the images of the given entity type.</param>
    /// <returns>A read-only list of images that are linked to the episode.
    /// </returns>
    public IReadOnlyList<TMDB_Image> GetImages(ImageEntityType? entityType = null) => IsAlternateSeason ? [] :
        entityType.HasValue
            ? RepoFactory.TMDB_Image.GetByTmdbSeasonIDAndType(_tmdbSeasonID, entityType.Value)
            : RepoFactory.TMDB_Image.GetByTmdbSeasonID(_tmdbSeasonID);

    /// <summary>
    /// Get all images for the episode, or all images for the given
    /// <paramref name="entityType"/> provided for the episode.
    /// </summary>
    /// <param name="entityType">If set, will restrict the returned list to only
    /// containing the images of the given entity type.</param>
    /// <param name="preferredImages">The preferred images.</param>
    /// <returns>A read-only list of images that are linked to the episode.
    /// </returns>
    public IReadOnlyList<IImage> GetImages(ImageEntityType? entityType, IReadOnlyDictionary<ImageEntityType, IImage> preferredImages) =>
        GetImages(entityType)
            .GroupBy(i => i.ImageType)
            .SelectMany(gB => preferredImages.TryGetValue(gB.Key, out var pI) ? gB.Select(i => i.Equals(pI) ? pI : i) : gB)
            .ToList();

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

    #region IWithImages Implementation

    IImage? IWithImages.GetPreferredImageForType(ImageEntityType entityType)
        => null;

    IReadOnlyList<IImage> IWithImages.GetImages(ImageEntityType? entityType)
        => GetImages(entityType);

    #endregion

    #region ITmdbSeasonCrossReference Implementation

    IShokoSeries? ITmdbSeasonCrossReference.ShokoSeries => AnimeSeries;

    ITmdbShow? ITmdbSeasonCrossReference.TmdbShow => TmdbShow;

    ITmdbSeason? ITmdbSeasonCrossReference.TmdbSeason => TmdbSeason;

    #endregion
}
