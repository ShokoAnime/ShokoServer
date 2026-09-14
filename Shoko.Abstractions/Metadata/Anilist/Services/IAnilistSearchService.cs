using System.Collections.Generic;
using System.Threading.Tasks;
using Shoko.Abstractions.Metadata.Anidb;

namespace Shoko.Abstractions.Metadata.Anilist.Services;

/// <summary>
/// Anilist search service for searching anime.
/// </summary>
public interface IAnilistSearchService
{
    /// <summary>
    /// Searches for anime on Anilist.
    /// </summary>
    /// <param name="options">The search options.</param>
    /// <returns>A tuple containing the page of results and the total count.</returns>
    Task<(IReadOnlyList<IAnilistAnimeSearchResult> Page, int TotalCount)> SearchAnime(AnilistSearchOptions options);

    /// <summary>
    /// Performs an automatic search for Anilist matches for an AniDB anime.
    /// </summary>
    /// <param name="anime">The AniDB anime to search for.</param>
    /// <returns>A list of auto-match results.</returns>
    Task<IReadOnlyList<IAnilistAutoSearchResult>> SearchForAutoMatch(IAnidbAnime anime);
}
