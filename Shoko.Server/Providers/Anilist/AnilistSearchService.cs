using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Shoko.Abstractions.Extensions;
using Shoko.Abstractions.Filtering.Services;
using Shoko.Abstractions.Metadata;
using Shoko.Abstractions.Metadata.Anidb;
using Shoko.Abstractions.Metadata.Anilist;
using Shoko.Abstractions.Metadata.Anilist.Services;
using Shoko.Abstractions.Metadata.Enums;
using Shoko.Server.Models.AniDB;
using Shoko.Server.Repositories.Cached.Anilist;
using Shoko.Server.Settings;
using Shoko.Server.Utilities;

#nullable enable
namespace Shoko.Server.Providers.Anilist;

/// <summary>
/// Service for searching AniList. Mirrors <see cref="TMDB.TmdbSearchService"/>
/// for shows: the same title-variant attempts, the same candidate pool and the
/// same rating priorities, with the difference that AniList search results
/// already carry every title, so no per-candidate fetch is needed.
/// </summary>
public partial class AnilistSearchService : IAnilistSearchService
{
    /// <summary>
    /// This regex might save the day if the local database doesn't contain any prequel metadata, but the title itself contains a suffix that indicates it's a sequel of sorts.
    /// </summary>
    [GeneratedRegex(@"\(\d{4}\)$|\bs(?:eason)? (?:\d+|(?=[MDCLXVI])M*(?:C[MD]|D?C{0,3})(X[CL]|L?X{0,3})(I[XV]|V?I{0,3}))$|\bs\d+$|第(零〇一二三四五六七八九十百千萬億兆京垓點)+季$|\b(?:second|2nd|third|3rd|fourth|4th|fifth|5th|sixth|6th) season$", RegexOptions.IgnoreCase | RegexOptions.Compiled | RegexOptions.Multiline)]
    private partial Regex SequelSuffixRemovalRegex();

    private readonly ILogger<AnilistSearchService> _logger;

    private readonly ISettingsProvider _settingsProvider;

    private readonly IFuzzySearchService _fuzzySearch;

    private readonly AnilistApiClient _apiClient;

    private readonly Anilist_AnimeRepository _anilistAnime;

    private readonly CrossRef_AniDB_Anilist_AnimeRepository _xrefAnidbAnilistAnime;

    /// <summary>
    /// Max days into the future to search for matches against.
    /// </summary>
    private readonly TimeSpan _maxDaysIntoTheFuture = TimeSpan.FromDays(15);

    public AnilistSearchService(
        ILogger<AnilistSearchService> logger,
        ISettingsProvider settingsProvider,
        IFuzzySearchService fuzzySearch,
        AnilistApiClient apiClient,
        Anilist_AnimeRepository anilistAnime,
        CrossRef_AniDB_Anilist_AnimeRepository xrefAnidbAnilistAnime
    )
    {
        _logger = logger;
        _settingsProvider = settingsProvider;
        _fuzzySearch = fuzzySearch;
        _apiClient = apiClient;
        _anilistAnime = anilistAnime;
        _xrefAnidbAnilistAnime = xrefAnidbAnilistAnime;
    }

    #region Search

    /// <inheritdoc/>
    async Task<(IReadOnlyList<IAnilistAnimeSearchResult> Page, int TotalCount)> IAnilistSearchService.SearchAnime(AnilistSearchOptions options)
    {
        var (page, totalCount) = await SearchAnime(options).ConfigureAwait(false);
        return (page, totalCount);
    }

    /// <summary>
    /// Search AniList for anime.
    /// </summary>
    public async Task<(IReadOnlyList<AnilistAnimeSearchResult> Page, int TotalCount)> SearchAnime(AnilistSearchOptions options)
    {
        _logger.LogDebug("Searching AniList for '{Query}' (Year: {Year}, Season: {Season} {SeasonYear}, Types: {Types}, Restricted: {Restricted}, Page: {Page}, Size: {PageSize})",
            options.Query, options.Year, options.Season, options.SeasonYear, options.Types is { Count: > 0 } ? string.Join(",", options.Types) : null, options.IncludeRestricted, options.Page, options.PageSize);

        var result = await _apiClient.SearchAnimeAsync(options).ConfigureAwait(false);
        if (result is null)
            return ([], 0);

        var totalCount = result["pageInfo"]?["total"]?.GetValue<int>() ?? 0;

        // A page size of zero asks for the total alone, the same as the TMDB
        // search. AniList's `perPage` has a floor of 1, so the smallest page is
        // still fetched and then dropped.
        if (options.PageSize <= 0)
            return ([], totalCount);

        var searchResults = result["media"] is JsonArray mediaArray
            ? mediaArray.Where(m => m is not null).Select(m => new AnilistAnimeSearchResult(m!)).ToList()
            : [];
        return (searchResults, totalCount);
    }

    #endregion

    #region Auto Search

    /// <inheritdoc/>
    async Task<IReadOnlyList<IAnilistAutoSearchResult>> IAnilistSearchService.SearchForAutoMatch(IAnidbAnime anime)
    {
        if (anime is not AniDB_Anime anidbAnime)
            return [];
        return await SearchForAutoMatch(anidbAnime).ConfigureAwait(false);
    }

    /// <summary>
    /// Performs automatic search for AniList matches for an AniDB anime.
    /// </summary>
    public async Task<IReadOnlyList<AnilistAutoSearchResult>> SearchForAutoMatch(AniDB_Anime anime)
        => anime.AnimeType switch
        {
            // Music videos and unknown types are hard to auto-map.
            AnimeType.MusicVideo or AnimeType.Other or AnimeType.Unknown => [],
            // Every other type is a plain media entry on AniList.
            _ => await AutoSearchForAnime(anime).ConfigureAwait(false),
        };

    private async Task<IReadOnlyList<AnilistAutoSearchResult>> AutoSearchForAnime(AniDB_Anime anime)
    {
        // Get the air date
        var airDate = anime.AirDate?.ToDateTime();
        if (!airDate.HasValue)
        {
            airDate = anime.AniDBEpisodes
                .Where(episode => episode.EpisodeType is EpisodeType.Episode)
                .OrderBy(episode => episode.EpisodeType)
                .ThenBy(episode => episode.EpisodeNumber)
                .Take(2)
                .LastOrDefault()
                ?.GetAirDateAsDate();
        }

        // Abort if the anime has not aired within the _maxDaysIntoTheFuture limit.
        var now = DateTime.Now;
        if (!airDate.HasValue || (airDate.Value > now && airDate.Value - now > _maxDaysIntoTheFuture))
            return [];

        // Find the official title in the origin language
        var allTitles = anime.Titles
            .Cast<ITitle>()
            .Where(title => title.Type is TitleType.Main or TitleType.Official)
            .ToList();
        var mainTitle = allTitles.FirstOrDefault(x => x.Type is TitleType.Main) ?? allTitles.First();
        var language = mainTitle.Language switch
        {
            TitleLanguage.Romaji => TitleLanguage.Japanese,
            TitleLanguage.Pinyin => TitleLanguage.ChineseSimplified,
            TitleLanguage.KoreanTranscription => TitleLanguage.Korean,
            TitleLanguage.ThaiTranscription => TitleLanguage.Thai,
            _ => mainTitle.Language,
        };

        // Find prequel series if available (to get the root series title)
        var series = anime as ISeries;
        var currentDate = airDate.Value;
        IReadOnlyList<IRelatedMetadata<ISeries, ISeries>> currentRelations = anime.RelatedAnime;
        while (currentRelations.Count > 0)
        {
            foreach (var prequelRelation in currentRelations.Where(relation => relation.RelationType == RelationType.Prequel))
            {
                var prequelSeries = prequelRelation.Related;
                if (prequelSeries?.AirDate is not { } prequelDate || prequelDate > currentDate)
                    continue;

                series = prequelSeries;
                currentDate = prequelDate.ToDateTime();
                currentRelations = prequelSeries.RelatedSeries;
                goto continuePrequelWhileLoop;
            }
            break;
            continuePrequelWhileLoop:
            continue;
        }
        var prequelFollowed = series.ID != anime.AnimeID;

        // First attempt the official title in the country of origin
        var originalTitle = language == mainTitle.Language
            ? mainTitle.Value
            : (
                !prequelFollowed
                    ? allTitles.FirstOrDefault(title => title.Type is TitleType.Official && title.Language == language)?.Value
                    : series.Titles.FirstOrDefault(title => title.Type is TitleType.Official && title.Language == language)?.Value
            );
        var match = !string.IsNullOrEmpty(originalTitle)
            ? await AutoSearchForAnimeUsingTitle(anime, originalTitle, airDate.Value, anime.IsRestricted, language == TitleLanguage.Japanese).ConfigureAwait(false)
            : null;

        // And if that failed, then try the official english title.
        if (match is null)
        {
            var englishTitle = !prequelFollowed
                ? allTitles.FirstOrDefault(l => l is { Type: TitleType.Official, Language: TitleLanguage.English })?.Value
                : series.Titles.FirstOrDefault(l => l is { Type: TitleType.Official, Language: TitleLanguage.English })?.Value;
            if (!string.IsNullOrEmpty(englishTitle) && (string.IsNullOrEmpty(originalTitle) || !string.Equals(englishTitle, originalTitle, StringComparison.Ordinal)))
                match = await AutoSearchForAnimeUsingTitle(anime, englishTitle, airDate.Value, anime.IsRestricted, false).ConfigureAwait(false);
        }

        // And the last ditch attempt will be to use the main title.
        match ??= await AutoSearchForAnimeUsingTitle(anime, mainTitle.Value, airDate.Value, anime.IsRestricted, false).ConfigureAwait(false);

        // If we followed a prequel chain, the anime's own titles may still be the better fit; prefer them when they rate at least as well.
        if (prequelFollowed && match is not null)
        {
            var ownTitle = allTitles.FirstOrDefault(title => title.Type is TitleType.Official && title.Language == language)?.Value
                ?? allTitles.FirstOrDefault(l => l is { Type: TitleType.Official, Language: TitleLanguage.English })?.Value
                ?? mainTitle.Value;
            var ownMatch = await AutoSearchForAnimeUsingTitle(anime, ownTitle, airDate.Value, anime.IsRestricted, language == TitleLanguage.Japanese).ConfigureAwait(false);
            if (ownMatch is not null && MatchPriority(ownMatch.MatchRating) <= MatchPriority(match.MatchRating))
                match = ownMatch;
        }

        // Also add all locally known matches
        var existingXrefs = _xrefAnidbAnilistAnime.GetByAnidbAnimeID(anime.AnimeID).ToList();
        if (prequelFollowed && series is AniDB_Anime prequelAnime)
            existingXrefs.AddRange(_xrefAnidbAnilistAnime.GetByAnidbAnimeID(prequelAnime.AnimeID));

        if (existingXrefs.Count > 0)
        {
            var localResults = existingXrefs
                .DistinctBy(x => x.AnilistAnimeID)
                .Select(x => _anilistAnime.GetByAnilistAnimeID(x.AnilistAnimeID))
                .WhereNotNull()
                .Select(x => new AnilistAutoSearchResult(anime, x, MatchRating.FirstAvailable) { IsLocal = true })
                .ToList();

            if (match is not null)
                localResults.Insert(0, match);

            return localResults
                .GroupBy(x => x.AnilistAnime.ID)
                .Select(x => new AnilistAutoSearchResult(x.First()) { IsLocal = x.Any(y => y.IsLocal), IsRemote = x.Any(y => y.IsRemote) })
                .ToList();
        }

        return match is not null ? [match] : [];
    }

    private async Task<AnilistAutoSearchResult?> AutoSearchForAnimeUsingTitle(AniDB_Anime anime, string originalTitle, DateTime airDate, bool restricted, bool isJapanese)
    {
        var settings = _settingsProvider.GetSettings();
        var candidateCount = settings.Anilist.AutoSearchCandidateCount;
        var includeRestricted = settings.Anilist.AutoLinkRestricted || restricted;
        var seen = new HashSet<int>();
        var candidates = new List<AnilistAnimeSearchResult>();

        // Attempt #1: full title + year
        CollectCandidates(candidates, await SearchRaw(originalTitle, includeRestricted, airDate.Year).ConfigureAwait(false), seen, candidateCount);

        // Attempt #2: sequel-suffix stripped + year
        var strippedTitle = SequelSuffixRemovalRegex().Match(originalTitle) is { Success: true } regexResult
            ? originalTitle[..^regexResult.Length].TrimEnd() : null;
        if (!string.IsNullOrEmpty(strippedTitle) && candidates.Count < candidateCount)
            CollectCandidates(candidates, await SearchRaw(strippedTitle, includeRestricted, airDate.Year).ConfigureAwait(false), seen, candidateCount);

        // Attempt #3: subtitle stripped + year. Only fuzzy-eligible below, so a parent entry matching
        // via its short title cannot outrank the specific entry that exact-matches the full title.
        var baseForSubtitle = strippedTitle ?? originalTitle;
        var colonIndex = baseForSubtitle.IndexOf(isJapanese ? ' ' : ':');
        var titleWithoutSubTitle = colonIndex > 0 ? baseForSubtitle[..colonIndex] : null;
        if (!string.IsNullOrEmpty(titleWithoutSubTitle) && candidates.Count < candidateCount)
            CollectCandidates(candidates, await SearchRaw(titleWithoutSubTitle, includeRestricted, airDate.Year).ConfigureAwait(false), seen, candidateCount);

        // Attempts #4–6: year-free fallbacks mirroring #1–3. These always run, because a late-December
        // premiere or a wrong AniDB air date would never show up in the year-filtered results.
        var yearFreeCap = candidateCount * 2;
        CollectCandidates(candidates, await SearchRaw(originalTitle, includeRestricted).ConfigureAwait(false), seen, yearFreeCap);
        if (!string.IsNullOrEmpty(strippedTitle) && candidates.Count < yearFreeCap)
            CollectCandidates(candidates, await SearchRaw(strippedTitle, includeRestricted).ConfigureAwait(false), seen, yearFreeCap);
        if (!string.IsNullOrEmpty(titleWithoutSubTitle) && candidates.Count < yearFreeCap)
            CollectCandidates(candidates, await SearchRaw(titleWithoutSubTitle, includeRestricted).ConfigureAwait(false), seen, yearFreeCap);

        if (candidates.Count == 0)
            return null;

        // AniDB episode 1's specific air date, for the episode-level date check below.
        var anidbEp1Date = anime.AniDBEpisodes
            .Where(e => e.EpisodeType is EpisodeType.Episode && e.EpisodeNumber == 1)
            .Select(e => e.GetAirDateAsDate())
            .FirstOrDefault();

        // Score every candidate. AniList search results already carry every title, the start date and
        // the episode count, so no per-candidate fetch is needed.
        var scored = new List<(AnilistAnimeSearchResult raw, MatchRating rating, int episodeDiff)>();
        foreach (var candidate in candidates)
        {
            var titleMatch = ScoreQueryVariants([originalTitle, strippedTitle], candidate.AllTitles);
            if (titleMatch == MatchRating.None && !string.IsNullOrEmpty(titleWithoutSubTitle) &&
                (PrefixMatchesAnyName(titleWithoutSubTitle, candidate.AllTitles) || _fuzzySearch.FuzzyScoreAnyName(titleWithoutSubTitle, candidate.AllTitles) is { isNotExact: true }))
                titleMatch = MatchRating.TitleKindaMatches;
            var exactTitle = titleMatch is MatchRating.TitleMatches;
            var fuzzyTitle = titleMatch is MatchRating.TitleKindaMatches;

            var episodeDiff = candidate.EpisodeCount is { } episodeCount ? Math.Abs(anime.EpisodeCountNormal - episodeCount) : int.MaxValue;
            var dateMatch = candidate.FirstAiredAt?.Year == airDate.Year || candidate.SeasonYear == airDate.Year;

            // Episode-level date check: a start date within ±3 days of AniDB's episode 1 is a strong
            // signal even when the year check failed on a year-boundary premiere.
            if (!dateMatch && anidbEp1Date.HasValue && candidate.FirstAiredAt is { Month: not null, Day: not null } startDate)
            {
                var candidateStart = new DateTime(startDate.Year, startDate.Month.Value, startDate.Day.Value);
                dateMatch = Math.Abs((candidateStart - anidbEp1Date.Value).TotalDays) <= 3;
            }

            var rating = (exactTitle, fuzzyTitle, dateMatch) switch
            {
                (true, _, true) => MatchRating.DateAndTitleMatches,
                (true, _, false) => MatchRating.TitleMatches,
                (_, true, true) => MatchRating.DateAndTitleKindaMatches,
                (_, _, true) => MatchRating.DateMatches,
                (_, true, false) => MatchRating.TitleKindaMatches,
                _ => MatchRating.FirstAvailable,
            };

            scored.Add((candidate, rating, episodeDiff));
            _logger.LogTrace("Candidate anime {AnimeName} ({ID}): rating={Rating}, episodeDiff={EpisodeDiff}", candidate.Title, candidate.ID, rating, episodeDiff);

            // Maximum confidence — nothing can outscore this.
            if (rating is MatchRating.DateAndTitleMatches && episodeDiff == 0)
                break;
        }

        var best = scored
            .OrderBy(x => MatchPriority(x.rating))
            .ThenBy(x => x.episodeDiff)
            .First();

        if (!IsAcceptableAutoMatch(best.rating))
            return null;

        _logger.LogInformation("Best match for \"{Query}\": {AnimeName} ({ID}) rating={Rating}", originalTitle, best.raw.Title, best.raw.ID, best.rating);
        return new(anime, best.raw, best.rating) { IsRemote = true };
    }

    private async Task<IReadOnlyList<AnilistAnimeSearchResult>> SearchRaw(string query, bool includeRestricted, int? year = null)
    {
        var (results, _) = await SearchAnime(new()
        {
            Query = query,
            IncludeRestricted = includeRestricted,
            Year = year,
            Page = 1,
            PageSize = AnilistApiClient.MaxPageSize / 2,
        }).ConfigureAwait(false);
        return results;
    }

    private static void CollectCandidates(List<AnilistAnimeSearchResult> candidates, IReadOnlyList<AnilistAnimeSearchResult> results, HashSet<int> seen, int candidateCount)
    {
        foreach (var result in results)
        {
            if (candidates.Count >= candidateCount)
                break;
            if (!seen.Add(result.ID))
                continue;
            // Music videos are not worth auto-linking.
            if (result.Type is AnimeType.MusicVideo)
                continue;
            candidates.Add(result);
        }
    }

    #endregion

    #region Scoring

    internal static int MatchPriority(MatchRating rating) => rating switch
    {
        MatchRating.UserVerified => 0,
        MatchRating.DateAndTitleMatches => 1,
        MatchRating.TitleMatches => 2,
        MatchRating.DateAndTitleKindaMatches => 3,
        MatchRating.DateMatches => 4,
        MatchRating.TitleKindaMatches => 5,
        _ => 6,
    };

    // FirstAvailable means no title or date correlation at all. Auto-linking those produces false
    // matches, so the same floor as TMDB applies.
    internal static bool IsAcceptableAutoMatch(MatchRating rating) => rating is not MatchRating.FirstAvailable;

    private static bool ExactMatchesAnyName(string query, IReadOnlySet<string> names)
    {
        var normalizedQuery = SeriesSearch.NormalizeForIndex(query);
        if (string.IsNullOrWhiteSpace(normalizedQuery))
            return false;
        return names
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Any(name => string.Equals(normalizedQuery, SeriesSearch.NormalizeForIndex(name), StringComparison.OrdinalIgnoreCase));
    }

    private static bool PrefixMatchesAnyName(string query, IReadOnlySet<string> names)
    {
        var normalizedQuery = SeriesSearch.NormalizeForIndex(query);
        if (string.IsNullOrWhiteSpace(normalizedQuery))
            return false;
        // Whole-word boundary: query must be followed by a space or be the entire name.
        return names.Where(name => !string.IsNullOrWhiteSpace(name))
            .Select(SeriesSearch.NormalizeForIndex)
            .Any(normalizedName =>
                normalizedName.StartsWith(normalizedQuery, StringComparison.OrdinalIgnoreCase) &&
                (normalizedName.Length == normalizedQuery.Length || normalizedName[normalizedQuery.Length] == ' '));
    }

    private MatchRating ScoreQueryVariants(IReadOnlyList<string?> queryVariants, IReadOnlySet<string> names)
    {
        var result = MatchRating.None;
        foreach (var variant in queryVariants)
        {
            if (string.IsNullOrEmpty(variant))
                continue;
            if (ExactMatchesAnyName(variant, names))
                return MatchRating.TitleMatches;
            if (result is MatchRating.None &&
                (_fuzzySearch.FuzzyScoreAnyName(variant, names) is { isNotExact: true } || PrefixMatchesAnyName(variant, names)))
                result = MatchRating.TitleKindaMatches;
        }
        return result;
    }

    #endregion
}
