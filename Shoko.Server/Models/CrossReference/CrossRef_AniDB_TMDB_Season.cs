using System;
using System.Collections.Generic;
using System.Linq;
using Shoko.Plugin.Abstractions.DataModels;
using Shoko.Plugin.Abstractions.Enums;
using Shoko.Server.Models.TMDB;
using Shoko.Server.Repositories;

#nullable enable
namespace Shoko.Server.Models.CrossReference;

/// <summary>
/// Not actually stored in the database, but made from the episode cross-reference.
/// </summary>
public class CrossRef_AniDB_TMDB_Season : IEquatable<CrossRef_AniDB_TMDB_Season>
{
    #region Columns

    public int AnidbAnimeID { get; set; }

    public int TmdbShowID { get; set; }

    public int TmdbSeasonID { get; set; }

    public int SeasonNumber { get; set; }

    #endregion
    #region Constructors

    public CrossRef_AniDB_TMDB_Season(int anidbAnimeId, int tmdbSeasonId, int tmdbShowId, int seasonNumber = 1)
    {
        AnidbAnimeID = anidbAnimeId;
        TmdbSeasonID = tmdbSeasonId;
        TmdbShowID = tmdbShowId;
        SeasonNumber = seasonNumber;
    }

    #endregion
    #region Methods

    public SVR_AniDB_Anime? AnidbAnime =>
        RepoFactory.AniDB_Anime.GetByAnimeID(AnidbAnimeID);

    public SVR_AnimeSeries? AnimeSeries =>
        RepoFactory.AnimeSeries.GetByAnimeID(AnidbAnimeID);

    public TMDB_Season? TmdbSeason =>
        TmdbSeasonID == 0 ? null : RepoFactory.TMDB_Season.GetByTmdbSeasonID(TmdbSeasonID);

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
    public IReadOnlyList<TMDB_Image> GetImages(ImageEntityType? entityType = null) => entityType.HasValue
        ? RepoFactory.TMDB_Image.GetByTmdbSeasonIDAndType(TmdbSeasonID, entityType.Value)
        : RepoFactory.TMDB_Image.GetByTmdbSeasonID(TmdbSeasonID);

    /// <summary>
    /// Get all images for the episode, or all images for the given
    /// <paramref name="entityType"/> provided for the episode.
    /// </summary>
    /// <param name="entityType">If set, will restrict the returned list to only
    /// containing the images of the given entity type.</param>
    /// <param name="preferredImages">The preferred images.</param>
    /// <returns>A read-only list of images that are linked to the episode.
    /// </returns>
    public IReadOnlyList<IImageMetadata> GetImages(ImageEntityType? entityType, IReadOnlyDictionary<ImageEntityType, IImageMetadata> preferredImages) =>
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
}
