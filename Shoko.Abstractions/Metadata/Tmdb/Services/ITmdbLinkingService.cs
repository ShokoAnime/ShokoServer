using System.Collections.Generic;
using System.Threading.Tasks;
using Shoko.Abstractions.Metadata.Enums;
using Shoko.Abstractions.Metadata.Tmdb.CrossReferences;

namespace Shoko.Abstractions.Metadata.Tmdb.Services;

/// <summary>
/// TMDB linking service for managing links between AniDB and TMDB entities.
/// </summary>
public interface ITmdbLinkingService
{
    #region Shared

    /// <summary>
    /// Removes all AniDB to TMDB links.
    /// </summary>
    /// <param name="removeShowLinks">Whether to remove show links.</param>
    /// <param name="removeMovieLinks">Whether to remove movie links.</param>
    void RemoveAllLinks(bool removeShowLinks = true, bool removeMovieLinks = true);

    /// <summary>
    /// Resets the auto-linking state for all series.
    /// </summary>
    /// <param name="disabled">If true, disables auto-linking; if false, enables it.</param>
    void ResetAutoLinkingState(bool disabled = false);

    #endregion

    #region Movie Links

    /// <summary>
    /// Adds a movie link for an AniDB episode.
    /// </summary>
    /// <param name="anidbEpisodeId">The AniDB episode ID.</param>
    /// <param name="tmdbMovieId">The TMDB movie ID.</param>
    /// <param name="additiveLink">If true, adds to existing links; if false, replaces existing links.</param>
    /// <param name="matchRating">The match rating for the link.</param>
    Task AddMovieLinkForEpisode(int anidbEpisodeId, int tmdbMovieId, bool additiveLink = false, MatchRating matchRating = MatchRating.UserVerified);

    /// <summary>
    /// Removes a specific movie link for an AniDB episode.
    /// </summary>
    /// <param name="anidbEpisodeId">The AniDB episode ID.</param>
    /// <param name="tmdbMovieId">The TMDB movie ID.</param>
    /// <param name="purge">If true, also purges the TMDB movie if no longer linked.</param>
    Task RemoveMovieLinkForEpisode(int anidbEpisodeId, int tmdbMovieId, bool purge = false);

    /// <summary>
    /// Removes all movie links for an AniDB anime.
    /// </summary>
    /// <param name="anidbAnimeId">The AniDB anime ID.</param>
    /// <param name="purge">If true, also purges the TMDB movies if no longer linked.</param>
    Task RemoveAllMovieLinksForAnime(int anidbAnimeId, bool purge = false);

    /// <summary>
    /// Removes all movie links for an AniDB episode.
    /// </summary>
    /// <param name="anidbEpisodeId">The AniDB episode ID.</param>
    /// <param name="purge">If true, also purges the TMDB movies if no longer linked.</param>
    Task RemoveAllMovieLinksForEpisode(int anidbEpisodeId, bool purge = false);

    /// <summary>
    /// Removes all links to a TMDB movie.
    /// </summary>
    /// <param name="tmdbMovieId">The TMDB movie ID.</param>
    Task RemoveAllMovieLinksForMovie(int tmdbMovieId);

    #endregion

    #region Show Links

    /// <summary>
    /// Adds a show link for an AniDB anime.
    /// </summary>
    /// <param name="anidbAnimeId">The AniDB anime ID.</param>
    /// <param name="tmdbShowId">The TMDB show ID.</param>
    /// <param name="additiveLink">If true, adds to existing links; if false, replaces existing links.</param>
    /// <param name="matchRating">The match rating for the link.</param>
    Task AddShowLink(int anidbAnimeId, int tmdbShowId, bool additiveLink = true, MatchRating matchRating = MatchRating.UserVerified);

    /// <summary>
    /// Removes a specific show link for an AniDB anime.
    /// </summary>
    /// <param name="anidbAnimeId">The AniDB anime ID.</param>
    /// <param name="tmdbShowId">The TMDB show ID.</param>
    /// <param name="purge">If true, also purges the TMDB show if no longer linked.</param>
    Task RemoveShowLink(int anidbAnimeId, int tmdbShowId, bool purge = false);

    /// <summary>
    /// Removes all show links for an AniDB anime.
    /// </summary>
    /// <param name="animeId">The AniDB anime ID.</param>
    /// <param name="purge">If true, also purges the TMDB shows if no longer linked.</param>
    Task RemoveAllShowLinksForAnime(int animeId, bool purge = false);

    /// <summary>
    /// Removes all links to a TMDB show.
    /// </summary>
    /// <param name="showId">The TMDB show ID.</param>
    Task RemoveAllShowLinksForShow(int showId);

    #endregion

    #region Episode Links

    /// <summary>
    /// Resets all episode links for an AniDB anime.
    /// </summary>
    /// <param name="anidbAnimeId">The AniDB anime ID.</param>
    /// <param name="allowAuto">If true, allows auto-matching to re-link episodes.</param>
    void ResetAllEpisodeLinks(int anidbAnimeId, bool allowAuto);

    /// <summary>
    /// Sets an episode link between an AniDB episode and a TMDB episode.
    /// </summary>
    /// <param name="anidbEpisodeId">The AniDB episode ID.</param>
    /// <param name="tmdbEpisodeId">The TMDB episode ID. Use 0 to set an empty link.</param>
    /// <param name="additiveLink">If true, adds to existing links; if false, replaces existing links.</param>
    /// <param name="index">Optional ordering index for multiple links.</param>
    /// <returns>True if the link was set successfully, false otherwise.</returns>
    bool SetEpisodeLink(int anidbEpisodeId, int tmdbEpisodeId, bool additiveLink = true, int? index = null);

    /// <summary>
    /// Performs auto-matching of AniDB episodes to TMDB episodes.
    /// </summary>
    /// <param name="anidbAnimeId">The AniDB anime ID.</param>
    /// <param name="tmdbShowId">The TMDB show ID.</param>
    /// <param name="tmdbSeasonId">Optional TMDB season ID to restrict matching to.</param>
    /// <param name="useExisting">If true, preserves existing user-verified links.</param>
    /// <param name="saveToDatabase">If true, saves the results to the database.</param>
    /// <param name="useExistingOtherShows">If specified, overrides the setting for considering existing links from other shows.</param>
    /// <returns>A list of episode cross-references.</returns>
    IReadOnlyList<ITmdbEpisodeCrossReference> MatchAnidbToTmdbEpisodes(int anidbAnimeId, int tmdbShowId, int? tmdbSeasonId, bool useExisting = false, bool saveToDatabase = false, bool? useExistingOtherShows = null);

    #endregion
}
