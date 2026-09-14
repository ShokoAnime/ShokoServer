using System.Text.Json.Nodes;
using Shoko.Abstractions.Metadata.Anidb;
using Shoko.Abstractions.Metadata.Anilist;
using Shoko.Abstractions.Metadata.Enums;
using Shoko.Server.Models.AniDB;

#nullable enable
namespace Shoko.Server.Providers.Anilist;

/// <summary>
/// Auto-magic AniDB to Anilist match result implementation.
/// </summary>
public class AnilistAutoSearchResult : IAnilistAutoSearchResult
{
    /// <inheritdoc/>
    public bool IsLocal { get; set; }

    /// <inheritdoc/>
    public bool IsRemote { get; set; }

    /// <inheritdoc/>
    public MatchRating MatchRating { get; set; }

    /// <inheritdoc/>
    IAnidbAnime IAnilistAutoSearchResult.AnidbAnime => AnidbAnime;

    /// <inheritdoc/>
    IAnilistAnimeSearchResult IAnilistAutoSearchResult.AnilistAnime => AnilistAnime;

    /// <summary>
    /// The AniDB anime associated with the search result.
    /// </summary>
    public AniDB_Anime AnidbAnime { get; init; }

    /// <summary>
    /// The Anilist anime search result.
    /// </summary>
    public AnilistAnimeSearchResult AnilistAnime { get; init; }

    /// <summary>
    /// Creates a new auto search result from a remote search.
    /// </summary>
    /// <param name="anime">The AniDB anime.</param>
    /// <param name="media">The Anilist media JSON node.</param>
    /// <param name="matchRating">The match rating.</param>
    public AnilistAutoSearchResult(AniDB_Anime anime, JsonNode media, MatchRating matchRating = MatchRating.FirstAvailable)
    {
        AnidbAnime = anime;
        AnilistAnime = new AnilistAnimeSearchResult(media);
        MatchRating = matchRating;
        IsRemote = true;
    }

    /// <summary>
    /// Creates a new auto search result from a local database match.
    /// </summary>
    /// <param name="anime">The AniDB anime.</param>
    /// <param name="anilistAnime">The local Anilist anime model.</param>
    /// <param name="matchRating">The match rating.</param>
    public AnilistAutoSearchResult(AniDB_Anime anime, Models.Anilist.Anilist_Anime anilistAnime, MatchRating matchRating = MatchRating.FirstAvailable)
    {
        AnidbAnime = anime;
        AnilistAnime = new AnilistAnimeSearchResult(anilistAnime);
        MatchRating = matchRating;
        IsLocal = true;
    }

    /// <summary>
    /// Creates a new auto search result from an existing search result.
    /// </summary>
    /// <param name="anime">The AniDB anime.</param>
    /// <param name="searchResult">The existing search result.</param>
    /// <param name="matchRating">The match rating.</param>
    public AnilistAutoSearchResult(AniDB_Anime anime, AnilistAnimeSearchResult searchResult, MatchRating matchRating = MatchRating.FirstAvailable)
    {
        AnidbAnime = anime;
        AnilistAnime = searchResult;
        MatchRating = matchRating;
    }

    /// <summary>
    /// Copy constructor.
    /// </summary>
    /// <param name="result">The result to copy.</param>
    public AnilistAutoSearchResult(AnilistAutoSearchResult result)
    {
        AnidbAnime = result.AnidbAnime;
        AnilistAnime = result.AnilistAnime;
        MatchRating = result.MatchRating;
        IsLocal = result.IsLocal;
        IsRemote = result.IsRemote;
    }
}
