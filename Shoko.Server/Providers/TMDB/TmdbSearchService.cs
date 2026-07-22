using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Shoko.Abstractions.Extensions;
using Shoko.Abstractions.Filtering.Services;
using Shoko.Abstractions.Metadata;
using Shoko.Abstractions.Metadata.Anidb;
using Shoko.Abstractions.Metadata.Enums;
using Shoko.Abstractions.Metadata.Tmdb;
using Shoko.Abstractions.Metadata.Tmdb.Services;
using Shoko.Server.Models.AniDB;
using Shoko.Server.Settings;
using Shoko.Server.Utilities;
using TMDbLib.Objects.General;
using TMDbLib.Objects.Search;
using TMDbLib.Objects.TvShows;

namespace Shoko.Server.Providers.TMDB;

public partial class TmdbSearchService : ITmdbSearchService
{
    private const string AnimationGenre = "animation";

    /// <summary>
    /// This regex might save the day if the local database doesn't contain any prequel metadata, but the title itself contains a suffix that indicates it's a sequel of sorts.
    /// </summary>
    [GeneratedRegex(@"\(\d{4}\)$|\bs(?:eason)? (?:\d+|(?=[MDCLXVI])M*(?:C[MD]|D?C{0,3})(X[CL]|L?X{0,3})(I[XV]|V?I{0,3}))$|\bs\d+$|第(零〇一二三四五六七八九十百千萬億兆京垓點)+[季期]$|\b(?:second|2nd|third|3rd|fourth|4th|fifth|5th|sixth|6th) season$", RegexOptions.IgnoreCase | RegexOptions.Compiled | RegexOptions.Multiline)]
    private partial Regex SequelSuffixRemovalRegex();

    private readonly ILogger<TmdbSearchService> _logger;

    private readonly TmdbMetadataService _tmdbService;

    private readonly IFuzzySearchService _fuzzySearch;

    private readonly ISettingsProvider _settingsProvider;

    /// <summary>
    /// Max days into the future to search for matches against.
    /// </summary>
    private readonly TimeSpan _maxDaysIntoTheFuture = TimeSpan.FromDays(15);

    public TmdbSearchService(ILogger<TmdbSearchService> logger, TmdbMetadataService tmdbService, IFuzzySearchService fuzzySearch, ISettingsProvider settingsProvider)
    {
        _logger = logger;
        _tmdbService = tmdbService;
        _fuzzySearch = fuzzySearch;
        _settingsProvider = settingsProvider;
    }

    async Task<IReadOnlyList<ITmdbAutoSearchResult>> ITmdbSearchService.SearchForAutoMatch(IAnidbAnime anime)
    {
        if (anime is not AniDB_Anime anidbAnime)
            return [];
        return await SearchForAutoMatch(anidbAnime).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<TmdbAutoSearchResult>> SearchForAutoMatch(AniDB_Anime anime)
        => anime.AnimeType switch
        {
            // Music videos are not allowed on TMDB, and the other and unknown types are hard to auto-map, so just don't.
            AnimeType.MusicVideo or AnimeType.Other or AnimeType.Unknown => [],
            AnimeType.Movie => await AutoSearchForMovies(anime).ConfigureAwait(false),
            // OVA/Web entries with ≤4 main episodes may be standalone movies on TMDB even though AniDB models them as a series.
            // Try movie search first; fall back to show search if nothing matches.
            AnimeType.OVA or AnimeType.Web when IsShortFormAnime(anime) => await AutoSearchForMoviesWithShowFallback(anime).ConfigureAwait(false),
            _ => await AutoSearchForShow(anime).ConfigureAwait(false)
        };

    internal static bool IsShortFormAnime(AniDB_Anime anime)
        => IsShortFormByEpisodeCount(anime.AniDBEpisodes.Count(e => e.EpisodeType is EpisodeType.Episode));

    internal static bool IsShortFormByEpisodeCount(int mainEpisodeCount) => mainEpisodeCount <= 4;

    private async Task<IReadOnlyList<TmdbAutoSearchResult>> AutoSearchForMoviesWithShowFallback(AniDB_Anime anime)
    {
        var movieResults = await AutoSearchForMovies(anime).ConfigureAwait(false);
        if (movieResults.Count > 0)
            return movieResults;
        return await AutoSearchForShow(anime).ConfigureAwait(false);
    }

    #region Movie

    public async Task<(IReadOnlyList<ITmdbMovieSearchResult> Page, int TotalCount)> SearchMovies(string query, bool includeRestricted = false, int year = 0, int page = 1, int pageSize = 6)
    {
        var (results, total) = await SearchMoviesRaw(query, includeRestricted, year, page, pageSize).ConfigureAwait(false);
        return (results.Select(m => new TmdbMovieSearchResult(m)).ToList<ITmdbMovieSearchResult>(), total);
    }

    internal async Task<(List<SearchMovie> Page, int TotalCount)> SearchMoviesRaw(string query, bool includeRestricted = false, int year = 0, int page = 1, int pageSize = 6)
    {
        var results = new List<SearchMovie>();
        var firstPage = await _tmdbService.UseClient(c => c.SearchMovieAsync(query, 1, includeRestricted, year), $"Searching{(includeRestricted ? " all" : string.Empty)} movies for \"{query}\"{(year > 0 ? $" at year {year}" : string.Empty)}").ConfigureAwait(false) ??
            throw new HttpRequestException(HttpRequestError.ConnectionError, "Failed to get search results");
        var total = firstPage.TotalResults;
        if (total == 0)
            return (results, total);

        var lastPage = firstPage.TotalPages;
        var actualPageSize = firstPage.Results!.Count;
        var startIndex = (page - 1) * pageSize;
        var startPage = (int)Math.Floor((decimal)startIndex / actualPageSize) + 1;
        var endIndex = Math.Min(startIndex + pageSize, total);
        var endPage = total == endIndex ? lastPage : Math.Min((int)Math.Floor((decimal)endIndex / actualPageSize) + (endIndex % actualPageSize > 0 ? 1 : 0), lastPage);
        for (var i = startPage; i <= endPage; i++)
        {
            var actualPage = await _tmdbService.UseClient(c => c.SearchMovieAsync(query, i, includeRestricted, year), $"Searching{(includeRestricted ? " all" : string.Empty)} movies for \"{query}\"{(year > 0 ? $" at year {year}" : string.Empty)}").ConfigureAwait(false) ??
                throw new HttpRequestException(HttpRequestError.ConnectionError, "Failed to get search results");
            results.AddRange(actualPage.Results!);
        }

        var skipCount = startIndex - (startPage - 1) * actualPageSize;
        // Stable sort so genuine anime bubbles above unrelated live-action/adult results that only
        // matched on title text — TMDB's own relevance ranking doesn't account for genre at all.
        // Nothing is filtered out; ties keep TMDB's original relative order.
        var pagedResults = results.Skip(skipCount).Take(pageSize)
            .OrderByDescending(m => m.GetGenres().Contains(AnimationGenre, StringComparer.OrdinalIgnoreCase))
            .ToList();

        _logger.LogTrace(
            "Got {Count} movies from {Results} total movies at {IndexRange} across {PageRange}.",
            pagedResults.Count,
            total,
            startIndex == endIndex ? $"index {startIndex}" : $"indexes {startIndex}-{endIndex}",
            startPage == endPage ? $"{startPage} actual page" : $"{startPage}-{endPage} actual pages"
        );

        return (pagedResults, total);
    }

    private async Task<IReadOnlyList<TmdbAutoSearchResult>> AutoSearchForMovies(AniDB_Anime anime)
    {
        // Find the official title in the origin language, to compare it against
        // the original language stored in the offline tmdb search dump.
        var list = new List<TmdbAutoSearchResult>();
        var allTitles = anime.Titles
            .Where(title => title.TitleType is TitleType.Main or TitleType.Official);
        var mainTitle = allTitles.FirstOrDefault(x => x.TitleType is TitleType.Main) ?? allTitles.First();
        var language = mainTitle.Language switch
        {
            TitleLanguage.Romaji => TitleLanguage.Japanese,
            TitleLanguage.Pinyin => TitleLanguage.ChineseSimplified,
            TitleLanguage.KoreanTranscription => TitleLanguage.Korean,
            TitleLanguage.ThaiTranscription => TitleLanguage.Thai,
            _ => mainTitle.Language,
        };
        var title = mainTitle.Title;
        var officialTitle = language == mainTitle.Language ? mainTitle.Title :
            allTitles.FirstOrDefault(title => title.Language == language)?.Title;
        var englishTitle = allTitles.FirstOrDefault(title => title.Language == TitleLanguage.English)?.Title;

        // Try to establish a link for every movie (episode) in the movie
        // collection (anime).
        var episodes = anime.AniDBEpisodes
            .Where(episode => episode.EpisodeType is EpisodeType.Episode or EpisodeType.Special or EpisodeType.Other)
            .OrderBy(episode => episode.EpisodeType)
            .ThenBy(episode => episode.EpisodeNumber)
            .ToList();

        // We only have one movie in the movie collection, so don't search for
        // a sub-title.
        var now = DateTime.Now;
        if (episodes.Count is 1)
        {
            // Abort if the movie have not aired within the _maxDaysIntoTheFuture limit.
            var airDate = anime.AirDate?.ToDateTime() ?? episodes[0].GetAirDateAsDate() ?? null;
            if (!airDate.HasValue || (airDate.Value > now && airDate.Value - now > _maxDaysIntoTheFuture))
                return [];
            await AutoSearchForMovie(list, anime, episodes[0], officialTitle, englishTitle, title, airDate.Value.Year, anime.IsRestricted).ConfigureAwait(false);
            return list;
        }

        // Find the sub title for each movie in the movie collection, then
        // search for a movie matching the combined title.
        foreach (var episode in episodes)
        {
            var allEpisodeTitles = episode.GetTitles();
            var isCompleteMovie = allEpisodeTitles.Any(title => title.Title.Contains("Complete Movie", StringComparison.InvariantCultureIgnoreCase));
            if (isCompleteMovie)
            {
                var airDateForAnime = anime.AirDate?.ToDateTime() ?? episodes[0].GetAirDateAsDate() ?? null;
                if (!airDateForAnime.HasValue || (airDateForAnime.Value > now && airDateForAnime.Value - now > _maxDaysIntoTheFuture))
                    continue;
                await AutoSearchForMovie(list, anime, episode, officialTitle, englishTitle, title, airDateForAnime.Value.Year, anime.IsRestricted).ConfigureAwait(false);
                continue;
            }

            var airDateForEpisode = episode.GetAirDateAsDate() ?? anime.AirDate?.ToDateTime() ?? null;
            if (!airDateForEpisode.HasValue || (airDateForEpisode.Value > now && airDateForEpisode.Value - now > _maxDaysIntoTheFuture))
                continue;

            var officialSubTitle = allEpisodeTitles.FirstOrDefault(title => title.Language == language)?.Title ??
                allEpisodeTitles.FirstOrDefault(title => title.Language == mainTitle.Language)?.Title;
            var englishSubTitle = allEpisodeTitles.FirstOrDefault(title => title.Language == TitleLanguage.English)?.Title;
            var isGenericTitle = string.Equals(englishSubTitle, $"Movie {episode.EpisodeNumber}", StringComparison.InvariantCultureIgnoreCase);
            var officialFullTitle = !string.IsNullOrEmpty(officialSubTitle)
                ? isGenericTitle ? $"{officialTitle} {episode.EpisodeNumber}" : $"{officialTitle} {officialSubTitle}" : null;
            var englishFullTitle = !string.IsNullOrEmpty(englishSubTitle)
                ? isGenericTitle ? $"{englishTitle} {episode.EpisodeNumber}" : $"{englishTitle} {englishSubTitle}" : null;
            var mainFullTitle = !string.IsNullOrEmpty(englishSubTitle)
                ? isGenericTitle ? $"{title} {episode.EpisodeNumber}" : $"{title} {englishSubTitle}" : null;

            // ~~Stolen~~ _Borrowed_ from the Shokofin code-base since we don't want to try linking extras to movies.
            if (episode.EpisodeType is EpisodeType.Special or EpisodeType.Other && !string.IsNullOrEmpty(englishSubTitle))
            {
                // Interviews
                if (englishSubTitle.Contains("interview", StringComparison.InvariantCultureIgnoreCase))
                    continue;

                // Cinema/theatrical intro/outro
                if (
                    (
                        (englishSubTitle.StartsWith("cinema ", StringComparison.InvariantCultureIgnoreCase) || englishSubTitle.StartsWith("theatrical ", StringComparison.InvariantCultureIgnoreCase)) &&
                        (englishSubTitle.Contains("intro", StringComparison.InvariantCultureIgnoreCase) || englishSubTitle.Contains("outro", StringComparison.InvariantCultureIgnoreCase))
                    ) ||
                    englishSubTitle.Contains("manners movie", StringComparison.InvariantCultureIgnoreCase)
                )
                    continue;

                // Behind the Scenes
                if (englishSubTitle.Contains("behind the scenes", StringComparison.InvariantCultureIgnoreCase) ||
                    englishSubTitle.Contains("making of", StringComparison.InvariantCultureIgnoreCase) ||
                    englishSubTitle.Contains("music in", StringComparison.InvariantCultureIgnoreCase) ||
                    englishSubTitle.Contains("advance screening", StringComparison.InvariantCultureIgnoreCase) ||
                    englishSubTitle.Contains("premiere", StringComparison.InvariantCultureIgnoreCase))
                    continue;
            }

            await AutoSearchForMovie(list, anime, episode, officialFullTitle, englishFullTitle, mainFullTitle, airDateForEpisode.Value.Year, anime.IsRestricted).ConfigureAwait(false);
        }

        return list;
    }

    private async Task<bool> AutoSearchForMovie(List<TmdbAutoSearchResult> list, AniDB_Anime anime, AniDB_Episode episode, string? officialTitle, string? englishTitle, string? mainTitle, int year, bool isRestricted)
    {
        TmdbAutoSearchResult? result = null;
        if (!string.IsNullOrEmpty(officialTitle))
            result = await AutoSearchMovieUsingTitle(anime, episode, officialTitle, includeRestricted: isRestricted, year: year).ConfigureAwait(false);

        if (result is null && !string.IsNullOrEmpty(englishTitle))
            result = await AutoSearchMovieUsingTitle(anime, episode, englishTitle, includeRestricted: isRestricted, year: year).ConfigureAwait(false);

        if (result is null && !string.IsNullOrEmpty(mainTitle))
            result = await AutoSearchMovieUsingTitle(anime, episode, mainTitle, includeRestricted: isRestricted, year: year).ConfigureAwait(false);

        if (result is not null)
            list.Add(result);
        return result is not null;
    }

    private async Task<TmdbAutoSearchResult?> AutoSearchMovieUsingTitle(AniDB_Anime anime, AniDB_Episode episode, string query, bool includeRestricted = false, int year = 0)
    {
        var candidateCount = _settingsProvider.GetSettings().TMDB.AutoSearchMovieCandidateCount;
        var seen = new HashSet<int>();
        var candidates = new List<SearchMovie>();

        List<SearchMovie> results;

        // Attempt #1: full title + year
        (results, _) = await SearchMoviesRaw(query, includeRestricted: includeRestricted, year: year).ConfigureAwait(false);
        CollectMovieCandidates(candidates, results, seen, candidateCount, includeRestricted);

        // Attempt #2: sequel-suffix stripped + year
        var strippedTitle = SequelSuffixRemovalRegex().Match(query) is { Success: true } regexResult
            ? query[..^regexResult.Length].TrimEnd() : null;
        if (!string.IsNullOrEmpty(strippedTitle) && candidates.Count < candidateCount)
        {
            (results, _) = await SearchMoviesRaw(strippedTitle, includeRestricted: includeRestricted, year: year).ConfigureAwait(false);
            CollectMovieCandidates(candidates, results, seen, candidateCount, includeRestricted);
        }

        // Attempts #3–4: year-free fallbacks mirroring #1–2.
        // Always run regardless of year-filtered candidate count — TMDB's year filter uses
        // release_date.year, so a movie whose AniDB air date differs from TMDB release year
        // will be missed entirely by year-filtered searches.
        var yearFreeCap = candidateCount * 2;

        (results, _) = await SearchMoviesRaw(query, includeRestricted: includeRestricted).ConfigureAwait(false);
        CollectMovieCandidates(candidates, results, seen, yearFreeCap, includeRestricted);

        if (!string.IsNullOrEmpty(strippedTitle) && candidates.Count < yearFreeCap)
        {
            (results, _) = await SearchMoviesRaw(strippedTitle, includeRestricted: includeRestricted).ConfigureAwait(false);
            CollectMovieCandidates(candidates, results, seen, yearFreeCap, includeRestricted);
        }

        if (candidates.Count == 0)
            return null;

        var scored = new List<(SearchMovie raw, MatchRating rating)>();
        foreach (var candidate in candidates)
        {
            var full = await _tmdbService.UseClient(
                c => c.GetMovieAsync(candidate.Id, "en-US", null, TMDbLib.Objects.Movies.MovieMethods.Translations | TMDbLib.Objects.Movies.MovieMethods.ReleaseDates),
                $"Fetch candidate movie {candidate.Id} \"{candidate.OriginalTitle}\" for auto-match scoring"
            ).ConfigureAwait(false);
            if (full is null) continue;

            var allTitles = new HashSet<string>(
                new[] { full.Title, full.OriginalTitle }
                    .Concat(full.Translations?.Translations?.Select(t => t.Data?.Name).WhereNotNull() ?? [])
                    .WhereNotNull()
                    .Where(s => !string.IsNullOrWhiteSpace(s))
            );

            var titleMatch = ScoreQueryVariants([query, strippedTitle], allTitles);
            var exactTitle = titleMatch is MatchRating.TitleMatches;
            var fuzzyTitle = titleMatch is MatchRating.TitleKindaMatches;
            var dateMatch = MovieMatchesYear(full, year);

            var rating = (exactTitle, fuzzyTitle, dateMatch) switch
            {
                (true, _, true)  => MatchRating.DateAndTitleMatches,
                (true, _, false) => MatchRating.TitleMatches,
                (_, true, true)  => MatchRating.DateAndTitleKindaMatches,
                (_, _, true)     => MatchRating.DateMatches,
                (_, true, false) => MatchRating.TitleKindaMatches,
                _                => MatchRating.FirstAvailable,
            };

            scored.Add((candidate, rating));
            _logger.LogTrace(
                "Candidate movie {MovieName} ({ID}): rating={Rating}, releaseYear={ReleaseYear}",
                full.Title, full.Id, rating, full.ReleaseDate?.Year
            );

            // Maximum confidence — no other candidate can outscore this, so skip remaining fetches.
            if (rating is MatchRating.DateAndTitleMatches)
                break;
        }

        // If all GetMovieAsync calls returned null (transient outage), there's no title/date data to
        // score against, so we can't tell this candidate apart from an unrelated one — don't guess.
        if (scored.Count == 0)
            return null;

        var best = scored
            .OrderBy(x => ShowMatchPriority(x.rating))
            .First();

        if (!IsAcceptableAutoMatch(best.rating))
            return null;

        _logger.LogInformation(
            "Best match for \"{Query}\": {MovieName} ({ID}) rating={Rating}",
            query, best.raw.OriginalTitle, best.raw.Id, best.rating
        );
        return new(anime, episode, best.raw, best.rating) { IsRemote = true };
    }

    #endregion

    #region Show

    public async Task<(IReadOnlyList<ITmdbShowSearchResult> Page, int TotalCount)> SearchShows(string query, bool includeRestricted = false, int year = 0, int page = 1, int pageSize = 6)
    {
        var (results, total) = await SearchShowsRaw(query, includeRestricted, year, page, pageSize).ConfigureAwait(false);
        return (results.Select(s => new TmdbShowSearchResult(s)).ToList<ITmdbShowSearchResult>(), total);
    }

    internal async Task<(List<SearchTv> Page, int TotalCount)> SearchShowsRaw(string query, bool includeRestricted = false, int year = 0, int page = 1, int pageSize = 6)
    {
        var results = new List<SearchTv>();
        var firstPage = await _tmdbService.UseClient(c => c.SearchTvShowAsync(query, 1, includeRestricted, year), $"Searching{(includeRestricted ? " all" : "")} shows for \"{query}\"{(year > 0 ? $" at year {year}" : "")}").ConfigureAwait(false) ??
            throw new HttpRequestException(HttpRequestError.ConnectionError, "Failed to get search results");
        var total = firstPage.TotalResults;
        if (total == 0)
            return (results, total);

        var lastPage = firstPage.TotalPages;
        var actualPageSize = firstPage.Results!.Count;
        var startIndex = (page - 1) * pageSize;
        var startPage = (int)Math.Floor((decimal)startIndex / actualPageSize) + 1;
        var endIndex = Math.Min(startIndex + pageSize, total);
        var endPage = total == endIndex ? lastPage : Math.Min((int)Math.Floor((decimal)endIndex / actualPageSize) + (endIndex % actualPageSize > 0 ? 1 : 0), lastPage);
        for (var i = startPage; i <= endPage; i++)
        {
            var actualPage = await _tmdbService.UseClient(c => c.SearchTvShowAsync(query, i, includeRestricted, year), $"Searching{(includeRestricted ? " all" : "")} shows for \"{query}\"{(year > 0 ? $" at year {year}" : "")}").ConfigureAwait(false) ??
                throw new HttpRequestException(HttpRequestError.ConnectionError, "Failed to get search results");

            results.AddRange(actualPage.Results!);
        }

        var skipCount = startIndex - (startPage - 1) * actualPageSize;
        // Stable sort so genuine anime bubbles above unrelated live-action/adult results that only
        // matched on title text — TMDB's own relevance ranking doesn't account for genre at all.
        // Nothing is filtered out; ties keep TMDB's original relative order.
        var pagedResults = results.Skip(skipCount).Take(pageSize)
            .OrderByDescending(s => s.GetGenres().Contains(AnimationGenre, StringComparer.OrdinalIgnoreCase))
            .ToList();

        _logger.LogTrace(
            "Got {Count} shows from {Results} total shows at {IndexRange} across {PageRange}.",
            pagedResults.Count,
            total,
            startIndex == endIndex ? $"index {startIndex}" : $"indexes {startIndex}-{endIndex}",
            startPage == endPage ? $"{startPage} actual page" : $"{startPage}-{endPage} actual pages"
        );

        return (pagedResults, total);
    }

    private async Task<IReadOnlyList<TmdbAutoSearchResult>> AutoSearchForShow(AniDB_Anime anime)
    {
        // TODO: Improve this logic to take tmdb seasons into account, and maybe also take better anidb series relations into account in cases where the tmdb show name and anidb series name are too different.

        // Get the first or second episode to get the aired date if the anime is missing a date.
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

        // Abort if the show have not aired within the _maxDaysIntoTheFuture limit.
        var now = DateTime.Now;
        if (!airDate.HasValue || (airDate.Value > now && airDate.Value - now > _maxDaysIntoTheFuture))
            return [];

        // Find the official title in the origin language, to compare it against
        // the original language stored in the offline tmdb search dump.
        var allTitles = anime.Titles
            .Cast<ITitle>()
            .Where(title => title.Type is TitleType.Main or TitleType.Official);
        var mainTitle = allTitles.FirstOrDefault(x => x.Type is TitleType.Main) ?? allTitles.First();
        var language = mainTitle.Language switch
        {
            TitleLanguage.Romaji => TitleLanguage.Japanese,
            TitleLanguage.Pinyin => TitleLanguage.ChineseSimplified,
            TitleLanguage.KoreanTranscription => TitleLanguage.Korean,
            TitleLanguage.ThaiTranscription => TitleLanguage.Thai,
            _ => mainTitle.Language,
        };

        var series = anime as ISeries;
        var adjustedMainTitle = mainTitle.Value;
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

        // First attempt the official title in the country of origin.
        var originalTitle = language == mainTitle.Language
            ? mainTitle.Value
            : (
                series.ID == anime.AnimeID
                    ? allTitles.FirstOrDefault(title => title.Type is TitleType.Official && title.Language == language)?.Value
                    : series.Titles.FirstOrDefault(title => title.Type is TitleType.Official && title.Language == language)?.Value
            );
        var match = !string.IsNullOrEmpty(originalTitle)
            ? await AutoSearchForShowUsingTitle(anime, originalTitle, airDate.Value, series.Restricted, language == TitleLanguage.Japanese)
            : null;

        // And if that failed, then try the official english title.
        if (match is null)
        {
            var englishTitle = series.ID == anime.AnimeID
                ? allTitles.FirstOrDefault(l => l is { Type: TitleType.Official, Language: TitleLanguage.English })?.Value
                : series.Titles.FirstOrDefault(l => l is { Type: TitleType.Official, Language: TitleLanguage.English })?.Value;
            if (!string.IsNullOrEmpty(englishTitle) && (string.IsNullOrEmpty(originalTitle) || !string.Equals(englishTitle, originalTitle, StringComparison.Ordinal)))
                match = await AutoSearchForShowUsingTitle(anime, englishTitle, airDate.Value, series.Restricted, false);
        }

        // And the last ditch attempt will be to use the main title. We won't try other languages.
        match ??= await AutoSearchForShowUsingTitle(anime, mainTitle.Value, airDate.Value, series.Restricted, false);

        // When the prequel chain was followed, the searches above used the ROOT series title.
        // If the current anime is a separate TMDB entry (e.g. "Fairy Tail: 100 Years Quest"
        // is its own TMDB show, not a season of base Fairy Tail), the root-title search will
        // return the wrong show. Try the current anime's own main title and prefer the result
        // if it scores better. When the own title wins, treat it as if no prequel was followed
        // so the root series' stored xrefs are not added as additional matches below.
        var prequelFollowed = series.ID != anime.AnimeID;
        if (prequelFollowed)
        {
            TmdbAutoSearchResult? ownTitleMatch = null;

            // Try the current anime's own original title first, before falling back to the main title.
            var ownOriginalTitle = language == mainTitle.Language
                ? mainTitle.Value
                : allTitles.FirstOrDefault(t => t.Type is TitleType.Official && t.Language == language)?.Value;
            if (!string.IsNullOrEmpty(ownOriginalTitle) && !string.Equals(ownOriginalTitle, originalTitle, StringComparison.Ordinal))
                ownTitleMatch = await AutoSearchForShowUsingTitle(anime, ownOriginalTitle, airDate.Value, anime.IsRestricted, language == TitleLanguage.Japanese).ConfigureAwait(false);

            if (ownTitleMatch is null && !string.Equals(mainTitle.Value, originalTitle, StringComparison.Ordinal) && !string.Equals(mainTitle.Value, ownOriginalTitle, StringComparison.Ordinal))
                ownTitleMatch = await AutoSearchForShowUsingTitle(anime, mainTitle.Value, airDate.Value, anime.IsRestricted, false).ConfigureAwait(false);

            if (ownTitleMatch is not null && (match is null || ShowMatchPriority(ownTitleMatch.MatchRating) < ShowMatchPriority(match.MatchRating)))
            {
                match = ownTitleMatch;
                prequelFollowed = false;
            }
        }

        // Also add all locally known matches for the current anime and first prequel anime if available.
        var existingXrefs = anime.TmdbShowCrossReferences.ToList();
        if (prequelFollowed && series is AniDB_Anime secondAnime && secondAnime.TmdbShowCrossReferences is { Count: > 0 } seriesXrefs)
            existingXrefs.AddRange(seriesXrefs);
        if (existingXrefs is { Count: > 0 })
        {
            var remoteSeries = existingXrefs
                .DistinctBy(x => x.TmdbShowID)
                .Select(x => (xref: x, show: x.TmdbShow))
                .Where(pair => pair.show is not null)
                .Select(pair => new TmdbAutoSearchResult(
                    anime,
                    new()
                    {
                        Id = pair.show!.Id,
                        OriginalName = pair.show.OriginalTitle,
                        Name = pair.show.EnglishTitle,
                        FirstAirDate = pair.show.FirstAiredAt?.ToDateTime(),
                        BackdropPath = pair.show.BackdropPath,
                        GenreIds = [],
                        MediaType = MediaType.Tv,
                        OriginalLanguage = pair.show.OriginalLanguageCode,
                        OriginCountry = pair.show.TmdbCompanies.Select(c => c.CountryOfOrigin).Distinct().ToList(),
                        Overview = pair.show.EnglishOverview,
                        PosterPath = pair.show.PosterPath,
                        Popularity = pair.show.UserRating,
                        VoteAverage = pair.show.UserRating,
                        VoteCount = pair.show.UserVotes,
                    },
                    pair.xref.MatchRating
                )
                {
                    IsLocal = true,
                })
                .ToList();
            if (match is not null)
                remoteSeries.Insert(0, match);

            return remoteSeries
                .GroupBy(x => (x.IsMovie, x.IsMovie ? x.TmdbMovie!.ID : x.TmdbShow!.ID))
                .Select(x => new TmdbAutoSearchResult(x.First()) { IsLocal = x.Any(y => y.IsLocal), IsRemote = x.Any(y => y.IsRemote) })
                .ToList();
        }

        return match is not null ? [match] : [];
    }

    private async Task<TmdbAutoSearchResult?> AutoSearchForShowUsingTitle(AniDB_Anime anime, string originalTitle, DateTime airDate, bool restricted, bool isJapanese)
    {
        var candidateCount = _settingsProvider.GetSettings().TMDB.AutoSearchShowCandidateCount;
        var seen = new HashSet<int>();
        var candidates = new List<SearchTv>();

        List<SearchTv> results;

        // Attempt #1: full title + year
        (results, _) = await SearchShowsRaw(originalTitle, includeRestricted: restricted, year: airDate.Year).ConfigureAwait(false);
        CollectCandidates(candidates, results, seen, candidateCount, restricted);

        // Attempt #2: sequel-suffix stripped + year
        var strippedTitle = SequelSuffixRemovalRegex().Match(originalTitle) is { Success: true } regexResult
            ? originalTitle[..^regexResult.Length].TrimEnd() : null;
        if (!string.IsNullOrEmpty(strippedTitle) && candidates.Count < candidateCount)
        {
            (results, _) = await SearchShowsRaw(strippedTitle, includeRestricted: restricted, year: airDate.Year).ConfigureAwait(false);
            CollectCandidates(candidates, results, seen, candidateCount, restricted);
        }

        // Attempt #3: subtitle stripped + year.
        // Compute the subtitle-stripped form once; it is also used by the scoring loop below.
        // It is only fuzzy-eligible (prefix/edit-distance), not exact-eligible — a parent show
        // matching via its short title (e.g. "Fairy Tail" from "Fairy Tail: 100 Years Quest")
        // must not score as high as the specific entry that exact-matches the full title.
        var baseForSubtitle = strippedTitle ?? originalTitle;
        var colonIndex = baseForSubtitle.IndexOf(isJapanese ? ' ' : ':');
        var titleWithoutSubTitle = colonIndex > 0 ? baseForSubtitle[..colonIndex] : null;
        if (!string.IsNullOrEmpty(titleWithoutSubTitle) && candidates.Count < candidateCount)
        {
            (results, _) = await SearchShowsRaw(titleWithoutSubTitle, includeRestricted: restricted, year: airDate.Year).ConfigureAwait(false);
            CollectCandidates(candidates, results, seen, candidateCount, restricted);
        }

        // Attempts #4–6: year-free fallbacks mirroring #1–3.
        // These always run regardless of how many year-filtered candidates were collected, because
        // a multi-season show's first_air_date predates the current season year and will never
        // appear in year-filtered results (e.g. TenSura S2 2021 vs. main show premiered 2018).
        // The doubled cap (×2) lets year-free results fill more of the pool — root shows tend to
        // rank lower in unfiltered searches so a slightly larger window reduces missed matches.
        var yearFreeCap = candidateCount * 2;

        (results, _) = await SearchShowsRaw(originalTitle, includeRestricted: restricted).ConfigureAwait(false);
        CollectCandidates(candidates, results, seen, yearFreeCap, restricted);

        if (!string.IsNullOrEmpty(strippedTitle) && candidates.Count < yearFreeCap)
        {
            (results, _) = await SearchShowsRaw(strippedTitle, includeRestricted: restricted).ConfigureAwait(false);
            CollectCandidates(candidates, results, seen, yearFreeCap, restricted);
        }

        if (!string.IsNullOrEmpty(titleWithoutSubTitle) && candidates.Count < yearFreeCap)
        {
            (results, _) = await SearchShowsRaw(titleWithoutSubTitle, includeRestricted: restricted).ConfigureAwait(false);
            CollectCandidates(candidates, results, seen, yearFreeCap, restricted);
        }

        if (candidates.Count == 0)
            return null;

        // AniDB episode 1's specific air date, used only for the episode-level date check below.
        // Nullable — when absent, the episode-level check is skipped entirely rather than falling
        // back to anime.AirDate, which is a series-level date and not the right point of truth
        // for an episode-to-episode comparison.
        var anidbEp1Date = anime.AniDBEpisodes
            .Where(e => e.EpisodeType is EpisodeType.Episode && e.EpisodeNumber == 1)
            .Select(e => e.GetAirDateAsDate())
            .FirstOrDefault();

        // Fetch each candidate in full to get translated titles and per-season data, then score.
        // show.Seasons is included in the base GetTvShowAsync response (no extra per-season fetches needed).
        var scored = new List<(SearchTv raw, MatchRating rating, int bestSeasonEpisodeDiff)>();
        foreach (var candidate in candidates)
        {
            var full = await _tmdbService.UseClient(
                c => c.GetTvShowAsync(candidate.Id, TvShowMethods.Translations),
                $"Fetch candidate show {candidate.Id} \"{candidate.OriginalName}\" for auto-match scoring"
            ).ConfigureAwait(false);
            if (full is null) continue;

            var allTitles = new HashSet<string>(
                new[] { full.Name, full.OriginalName }
                    .Concat(full.Translations?.Translations?.Select(t => t.Data?.Name).WhereNotNull() ?? [])
                    .WhereNotNull()
                    .Where(s => !string.IsNullOrWhiteSpace(s))
            );

            // ScoreQueryVariants is exact-eligible; the subtitle-stripped fallback below is fuzzy-only
            // so a parent title (e.g. "Fairy Tail") cannot outrank the specific entry that exact-matches the full title.
            var titleMatch = ScoreQueryVariants([originalTitle, strippedTitle], allTitles);
            if (titleMatch == MatchRating.None && !string.IsNullOrEmpty(titleWithoutSubTitle) &&
                (PrefixMatchesAnyName(titleWithoutSubTitle, allTitles) ||
                 _fuzzySearch.FuzzyScoreAnyName(titleWithoutSubTitle, allTitles) is { isNotExact: true }))
                titleMatch = MatchRating.TitleKindaMatches;
            var exactTitle = titleMatch is MatchRating.TitleMatches;
            var fuzzyTitle = titleMatch is MatchRating.TitleKindaMatches;

            // Match against individual seasons rather than the show's total episode count.
            // A season whose air year and episode count align with the AniDB anime is strong evidence
            // we have the right show — e.g. AoT S3P2 (10 eps, 2019) matches TMDB season 3 of Attack on
            // Titan, not the show total (~75 eps).
            var nonSpecialSeasons = full.Seasons?.Where(s => s.SeasonNumber > 0).ToList() ?? [];
            var bestSeason = nonSpecialSeasons
                .OrderBy(s => Math.Abs(anime.EpisodeCountNormal - s.EpisodeCount))
                .ThenBy(s => s.AirDate?.Year == airDate.Year ? 0 : 1)
                .FirstOrDefault();
            var bestSeasonEpisodeDiff = bestSeason is not null
                ? Math.Abs(anime.EpisodeCountNormal - bestSeason.EpisodeCount)
                : int.MaxValue;
            var seasonYearMatch = bestSeason?.AirDate?.Year == airDate.Year;

            // Date match: prefer season-level year alignment, fall back to show first-air year.
            var dateMatch = seasonYearMatch || full.FirstAirDate?.Year == airDate.Year;

            // Episode-level date check: if year-level matching failed and AniDB episode 1 has a
            // known air date, compare TMDB season episode 1's air date against it. A close match
            // (±3 days) is a strong signal that this is the correct season — catches cases where
            // the season year check fails (split-cour, year-boundary premieres) but actual dates align.
            if (!dateMatch && bestSeason is not null && anidbEp1Date.HasValue)
            {
                var tmdbSeason = await _tmdbService.UseClient(
                    c => c.GetTvSeasonAsync(candidate.Id, bestSeason.SeasonNumber),
                    $"Fetch season {bestSeason.SeasonNumber} of show {candidate.Id} \"{candidate.OriginalName}\" for episode-date check"
                ).ConfigureAwait(false);
                var tmdbEp1Date = tmdbSeason?.Episodes?
                    .OrderBy(e => e.EpisodeNumber)
                    .FirstOrDefault()?.AirDate;
                if (tmdbEp1Date.HasValue)
                {
                    dateMatch = Math.Abs((tmdbEp1Date.Value - anidbEp1Date.Value).TotalDays) <= 3;
                    _logger.LogTrace(
                        "Episode-date check for show {ShowName} ({ID}) S{Season}E1: tmdb={TmdbDate}, anidb={AnidbDate}, match={Match}",
                        full.Name, full.Id, bestSeason.SeasonNumber, tmdbEp1Date.Value.ToString("yyyy-MM-dd"), anidbEp1Date.Value.ToString("yyyy-MM-dd"), dateMatch
                    );
                }
            }

            var rating = (exactTitle, fuzzyTitle, dateMatch) switch
            {
                (true, _, true)  => MatchRating.DateAndTitleMatches,
                (true, _, false) => MatchRating.TitleMatches,
                (_, true, true)  => MatchRating.DateAndTitleKindaMatches,
                (_, _, true)     => MatchRating.DateMatches,
                (_, true, false) => MatchRating.TitleKindaMatches,
                _                => MatchRating.FirstAvailable,
            };

            scored.Add((candidate, rating, bestSeasonEpisodeDiff));
            _logger.LogTrace(
                "Candidate show {ShowName} ({ID}): rating={Rating}, bestSeasonEpisodeDiff={EpisodeDiff}, seasonYearMatch={SeasonYearMatch}",
                full.Name, full.Id, rating, bestSeasonEpisodeDiff, seasonYearMatch
            );

            // Maximum confidence — no other candidate can outscore this, so skip remaining fetches.
            if (rating is MatchRating.DateAndTitleMatches)
                break;
        }

        // If all GetTvShowAsync calls returned null (transient outage), there's no title/date data to
        // score against, so we can't tell this candidate apart from an unrelated one — don't guess.
        if (scored.Count == 0)
            return null;

        // Kinda values (TitleKindaMatches=7, DateAndTitleKindaMatches=8) are numerically above
        // FirstAvailable=5, so sort by an ordinal priority mapping instead of the raw enum value.
        var best = scored
            .OrderBy(x => ShowMatchPriority(x.rating))
            .ThenBy(x => x.bestSeasonEpisodeDiff)
            .First();

        if (!IsAcceptableAutoMatch(best.rating))
            return null;

        _logger.LogInformation(
            "Best match for \"{Query}\": {ShowName} ({ID}) rating={Rating}",
            originalTitle, best.raw.OriginalName, best.raw.Id, best.rating
        );
        return new(anime, best.raw, best.rating) { IsRemote = true };
    }

    #endregion

    #region Helpers

    private static bool MovieMatchesYear(TMDbLib.Objects.Movies.Movie movie, int year) =>
        year > 0 && (movie.ReleaseDate?.Year == year || (movie.ReleaseDates?.Results?.Any(r => r.ReleaseDates?.Any(rd => rd.ReleaseDate.Year == year) ?? false) ?? false));

    private static int ShowMatchPriority(MatchRating r) => r switch
    {
        MatchRating.UserVerified             => 0,
        MatchRating.DateAndTitleMatches      => 1,
        MatchRating.TitleMatches             => 2,
        MatchRating.DateAndTitleKindaMatches => 3,
        MatchRating.DateMatches              => 4,
        MatchRating.TitleKindaMatches        => 5,
        _                                    => 6,
    };

    // FirstAvailable means no title or date correlation at all, only a shared genre tag. Auto-linking
    // those produced false matches, such as unrelated adult content genre-tagged as animation.
    // TmdbLinkingService's episode-level matching already treats FirstAvailable as no match, so this
    // applies the same floor for show and movie auto-matching.
    internal static bool IsAcceptableAutoMatch(MatchRating rating) => rating is not MatchRating.FirstAvailable;

    // Restricted (R18) titles skip the Animation genre requirement — TMDB's community-maintained
    // genre metadata is disproportionately sparse for adult content, so requiring it here would
    // discard an otherwise-correct match. Non-restricted search keeps the genre guard to avoid
    // matching unrelated live-action/non-anime entries with a similar title.
    private static void CollectCandidates(List<SearchTv> candidates, List<SearchTv> results, HashSet<int> seen, int candidateCount, bool isRestricted = false)
    {
        foreach (var result in results)
        {
            if (candidates.Count >= candidateCount) break;
            if (!seen.Add(result.Id)) continue;
            if (!isRestricted && !result.GetGenres().Contains(AnimationGenre, StringComparer.OrdinalIgnoreCase)) continue;
            candidates.Add(result);
        }
    }

    private static void CollectMovieCandidates(List<SearchMovie> candidates, List<SearchMovie> results, HashSet<int> seen, int candidateCount, bool isRestricted = false)
    {
        foreach (var result in results)
        {
            if (candidates.Count >= candidateCount) break;
            if (!seen.Add(result.Id)) continue;
            if (!isRestricted && !result.GetGenres().Contains(AnimationGenre, StringComparer.OrdinalIgnoreCase)) continue;
            candidates.Add(result);
        }
    }

    private static bool ExactMatchesAnyName(string query, IReadOnlySet<string> names)
    {
        var normalizedQuery = SeriesSearch.NormalizeForIndex(query);
        return !string.IsNullOrWhiteSpace(normalizedQuery) &&
            names.Where(name => !string.IsNullOrWhiteSpace(name))
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
