using System.Collections.Generic;
using System.Linq;
using Shoko.Models.Enums;
using Shoko.Plugin.Abstractions.DataModels;
using Shoko.Plugin.Abstractions.Enums;
using Shoko.Server.Models.TMDB;
using Shoko.Server.Repositories;

#nullable enable
namespace Shoko.Server.Models.CrossReference;

public class CrossRef_AniDB_TMDB_Episode
{
    #region Database Columns

    public int CrossRef_AniDB_TMDB_EpisodeID { get; set; }

    public int AnidbAnimeID { get; set; }

    public int AnidbEpisodeID { get; set; }

    public int TmdbShowID { get; set; }

    public int TmdbEpisodeID { get; set; }

    public int Ordering { get; set; }

    public MatchRating MatchRating { get; set; }

    #endregion
    #region Constructors

    public CrossRef_AniDB_TMDB_Episode() { }

    public CrossRef_AniDB_TMDB_Episode(int anidbEpisodeId, int anidbAnimeId, int tmdbEpisodeId, int tmdbShowId, MatchRating rating = MatchRating.UserVerified, int ordering = 0)
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

    public SVR_AniDB_Episode? AnidbEpisode =>
        RepoFactory.AniDB_Episode.GetByEpisodeID(AnidbEpisodeID);

    public SVR_AniDB_Anime? AnidbAnime =>
        RepoFactory.AniDB_Anime.GetByAnimeID(AnidbAnimeID);

    public SVR_AnimeEpisode? AnimeEpisode =>
        RepoFactory.AnimeEpisode.GetByAniDBEpisodeID(AnidbEpisodeID);

    public SVR_AnimeSeries? AnimeSeries =>
        RepoFactory.AnimeSeries.GetByAnimeID(AnidbAnimeID);

    public TMDB_Episode? TmdbEpisode =>
        TmdbEpisodeID == 0 ? null : RepoFactory.TMDB_Episode.GetByTmdbEpisodeID(TmdbEpisodeID);

    public CrossRef_AniDB_TMDB_Season? TmdbSeasonCrossReference =>
        TmdbEpisode is { } tmdbEpisode
            ? new(AnidbAnimeID, tmdbEpisode.TmdbSeasonID, TmdbShowID, tmdbEpisode.SeasonNumber)
            : null;

    public TMDB_Season? TmdbSeason =>
        TmdbEpisode?.Season;

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
        ? RepoFactory.TMDB_Image.GetByTmdbEpisodeIDAndType(TmdbEpisodeID, entityType.Value)
        : RepoFactory.TMDB_Image.GetByTmdbEpisodeID(TmdbEpisodeID);

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

    #endregion
}
