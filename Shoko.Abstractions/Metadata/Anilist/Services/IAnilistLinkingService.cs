using System.Collections.Generic;
using System.Threading.Tasks;
using Shoko.Abstractions.Metadata.Anilist.CrossReferences;
using Shoko.Abstractions.Metadata.Enums;

namespace Shoko.Abstractions.Metadata.Anilist.Services;

/// <summary>
/// Anilist linking service for managing links between AniDB and Anilist entities.
/// </summary>
public interface IAnilistLinkingService
{
    #region Shared

    /// <summary>
    /// Removes all AniDB to Anilist links.
    /// </summary>
    void RemoveAllLinks();

    /// <summary>
    /// Resets the auto-linking state for all series.
    /// </summary>
    /// <param name="disabled">If true, disables auto-linking; if false, enables it.</param>
    void ResetAutoLinkingState(bool disabled = false);

    #endregion

    #region Anime Links

    /// <summary>
    /// Adds an anime link between an AniDB anime and an Anilist anime.
    /// </summary>
    /// <param name="anidbAnimeId">The AniDB anime ID.</param>
    /// <param name="anilistAnimeId">The Anilist anime ID.</param>
    /// <param name="additiveLink">If true, adds to existing links; if false, replaces existing links.</param>
    /// <param name="matchRating">The match rating for the link.</param>
    Task AddAnimeLink(int anidbAnimeId, int anilistAnimeId, bool additiveLink = false, MatchRating matchRating = MatchRating.UserVerified);

    /// <summary>
    /// Removes a specific anime link between an AniDB anime and an Anilist anime.
    /// </summary>
    /// <param name="anidbAnimeId">The AniDB anime ID.</param>
    /// <param name="anilistAnimeId">The Anilist anime ID.</param>
    /// <param name="purge">If true, also purges the Anilist anime if no longer linked.</param>
    Task RemoveAnimeLink(int anidbAnimeId, int anilistAnimeId, bool purge = false);

    /// <summary>
    /// Removes all Anilist links for an AniDB anime.
    /// </summary>
    /// <param name="anidbAnimeId">The AniDB anime ID.</param>
    /// <param name="purge">If true, also purges the Anilist anime entries if no longer linked.</param>
    Task RemoveAllAnimeLinksForAnidbAnime(int anidbAnimeId, bool purge = false);

    /// <summary>
    /// Removes all AniDB links to an Anilist anime.
    /// </summary>
    /// <param name="anilistAnimeId">The Anilist anime ID.</param>
    Task RemoveAllAnimeLinksForAnilistAnime(int anilistAnimeId);

    #endregion

    #region Episode Links

    /// <summary>
    /// Resets all episode links for an AniDB anime.
    /// </summary>
    /// <param name="anidbAnimeId">The AniDB anime ID.</param>
    /// <param name="allowAuto">If true, allows auto-matching to re-link episodes.</param>
    void ResetAllEpisodeLinks(int anidbAnimeId, bool allowAuto);

    /// <summary>
    /// Sets an episode link between an AniDB episode and an Anilist episode.
    /// </summary>
    /// <param name="anidbEpisodeId">The AniDB episode ID.</param>
    /// <param name="anilistEpisodeId">The Anilist episode ID. Use 0 to set an empty link.</param>
    /// <param name="additiveLink">If true, adds to existing links; if false, replaces existing links.</param>
    /// <param name="index">Optional ordering index for multiple links.</param>
    /// <returns>True if the link was set successfully, false otherwise.</returns>
    bool SetEpisodeLink(int anidbEpisodeId, int anilistEpisodeId, bool additiveLink = true, int? index = null);

    /// <summary>
    /// Performs auto-matching of AniDB episodes to Anilist episodes.
    /// </summary>
    /// <param name="anidbAnimeId">The AniDB anime ID.</param>
    /// <param name="anilistAnimeId">The Anilist anime ID.</param>
    /// <param name="useExisting">If true, preserves existing user-verified links.</param>
    /// <param name="saveToDatabase">If true, saves the results to the database.</param>
    /// <param name="useExistingOtherAnime">If set, overrides the setting for whether to consider existing links for other AniDB anime when picking episodes.</param>
    /// <returns>A list of episode cross-references.</returns>
    IReadOnlyList<IAnilistEpisodeCrossReference> MatchAnidbToAnilistEpisodes(int anidbAnimeId, int anilistAnimeId, bool useExisting = false, bool saveToDatabase = false, bool? useExistingOtherAnime = null);

    #endregion
}
