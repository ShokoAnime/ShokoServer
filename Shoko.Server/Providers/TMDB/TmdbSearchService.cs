using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Shoko.Abstractions.Extensions;
using Shoko.Abstractions.Metadata;
using Shoko.Abstractions.Metadata.Anidb;
using Shoko.Abstractions.Metadata.Enums;
using Shoko.Abstractions.Metadata.Matching;
using Shoko.Abstractions.Metadata.Search;
using Shoko.Abstractions.Metadata.Services;
using Shoko.Abstractions.Metadata.Tmdb;
using Shoko.Abstractions.Metadata.Tmdb.Services;
using Shoko.Server.Models.AniDB;
using Shoko.Server.Models.TMDB;
using Shoko.Server.Repositories.Cached.AniDB;
using Shoko.Server.Repositories.Cached.TMDB;
using Shoko.Server.Services;
using Shoko.Server.Settings;
using Shoko.Server.Utilities;
using TMDbLib.Objects.Find;
using TMDbLib.Objects.General;
using TMDbLib.Objects.Movies;
using TMDbLib.Objects.Search;
using TMDbLib.Objects.TvShows;

using ResourceLinkType = Shoko.Server.Providers.AniDB.ResourceLinkType;

namespace Shoko.Server.Providers.TMDB;

public class TmdbSearchService : ITmdbSearchService
{
    private const string AnimationGenre = "animation";

    private readonly ILogger<TmdbSearchService> _logger;

    private readonly TmdbApiClient _tmdbService;

    private readonly IMetadataMatchingEngine _matchingEngine;

    private readonly ISettingsProvider _settingsProvider;

    private readonly AniDB_ResourceRepository _anidbResources;

    private readonly TMDB_ShowRepository _tmdbShows;

    private readonly TMDB_MovieRepository _tmdbMovies;

    private readonly TMDB_SeasonRepository _tmdbSeasons;

    private readonly TMDB_EpisodeRepository _tmdbEpisodes;

    private readonly Lazy<IMetadataLinkingService> _linkingService;

    /// <summary>
    /// Max days into the future to search for matches against.
    /// </summary>
    private readonly TimeSpan _maxDaysIntoTheFuture = TimeSpan.FromDays(15);

    public TmdbSearchService(
        ILogger<TmdbSearchService> logger,
        TmdbApiClient tmdbService,
        IMetadataMatchingEngine matchingEngine,
        ISettingsProvider settingsProvider,
        AniDB_ResourceRepository anidbResources,
        TMDB_ShowRepository tmdbShows,
        TMDB_MovieRepository tmdbMovies,
        TMDB_SeasonRepository tmdbSeasons,
        TMDB_EpisodeRepository tmdbEpisodes,
        Lazy<IMetadataLinkingService> linkingService
    )
    {
        _logger = logger;
        _tmdbService = tmdbService;
        _matchingEngine = matchingEngine;
        _settingsProvider = settingsProvider;
        _anidbResources = anidbResources;
        _tmdbShows = tmdbShows;
        _tmdbMovies = tmdbMovies;
        _tmdbSeasons = tmdbSeasons;
        _tmdbEpisodes = tmdbEpisodes;
        _linkingService = linkingService;
    }

    async Task<IReadOnlyList<ITmdbAutoSearchResult>> ITmdbSearchService.SearchForAutoMatch(IAnidbAnime anime)
    {
        if (anime is not AniDB_Anime anidbAnime)
            return [];
        return await SearchForAutoMatch(anidbAnime).ConfigureAwait(false);
    }

    /// <summary>
    ///   The shows and movies the auto-search takes for an anime.
    /// </summary>
    /// <param name="anime">The anime.</param>
    /// <returns>The matches taken, best first.</returns>
    public async Task<IReadOnlyList<TmdbAutoSearchResult>> SearchForAutoMatch(AniDB_Anime anime)
    {
        // Weighs the hints as the core does, without the anime's links, as the search leaves them out.
        var results = await FindAutoMatches(anime).ConfigureAwait(false);
        var reviewed = results.Select(TmdbMetadataProvider.ToCandidate).ToList();
        MetadataLinkingService.ReviewHints(reviewed, MetadataSource.TMDB, null);
        return
        [
            .. results
                .Where((result, index) => reviewed[index] is
                {
                    Origin: MetadataAutoLinkOrigin.Search or MetadataAutoLinkOrigin.AnidbResource or MetadataAutoLinkOrigin.CrossSourceLink,
                    Rejection: null,
                })
                .OrderBy(result => result.Origin),
        ];
    }

    /// <summary>
    ///   Every show and movie the auto-search scored for an anime, taken or
    ///   turned down.
    /// </summary>
    /// <remarks>
    ///   Only what was fetched and judged comes back, never a search hit that
    ///   was not looked at. The ones taken come first, each turned-down one
    ///   carrying why. The entries the anime's AniDB resources or its links
    ///   on other sources name follow, the one to take first leading whatever
    ///   named it; whether the search took something is left to the core,
    ///   which takes at most one of them.
    /// </remarks>
    /// <param name="anime">The anime.</param>
    /// <returns>The candidates, best first.</returns>
    internal async Task<IReadOnlyList<TmdbAutoSearchResult>> FindAutoMatches(AniDB_Anime anime)
    {
        var hints = await FindHints(anime).ConfigureAwait(false);
        var results = anime.AnimeType switch
        {
            // Music videos are not allowed on TMDB, and the other and unknown types are hard to auto-map, so just don't.
            AnimeType.MusicVideo or AnimeType.Other or AnimeType.Unknown => [],
            AnimeType.Movie => await AutoSearchForMovies(anime, hints).ConfigureAwait(false),
            // OVA/Web entries with ≤4 main episodes may be standalone movies on TMDB even though AniDB models them as a series.
            // Try movie search first; fall back to show search if nothing matches.
            AnimeType.OVA or AnimeType.Web when IsShortFormAnime(anime) => await AutoSearchForMoviesWithShowFallback(anime, hints).ConfigureAwait(false),
            _ => await AutoSearchForShow(anime, hints).ConfigureAwait(false)
        };
        if (hints.Count > 0)
            results = [.. results, .. await JudgeHints(anime, hints, results).ConfigureAwait(false)];
        return BestFirst(results);
    }

    /// <summary>
    ///   Puts the search's candidates first, the ones taken leading, then the
    ///   links listed for context, then the hints of either origin as
    ///   <see cref="HintsInOrder"/> put them, and keeps one of each candidate
    ///   per origin and AniDB episode, the one taken or else the first scored.
    /// </summary>
    /// <param name="results">The candidates, in the order they were scored.</param>
    /// <returns>The candidates, best first.</returns>
    internal static IReadOnlyList<TmdbAutoSearchResult> BestFirst(IEnumerable<TmdbAutoSearchResult> results)
        => [
            .. results
                .GroupBy(result => (
                    result.Origin,
                    result.IsMovie,
                    EpisodeID: result.AnidbEpisode?.EpisodeID,
                    ID: result.IsMovie ? result.TmdbMovie.ID : result.TmdbShow.ID
                ))
                .Select(group => group.FirstOrDefault(result => result.Rejection is null) ?? group.First())
                .OrderBy(result => result.Origin is MetadataAutoLinkOrigin.CrossSourceLink ? MetadataAutoLinkOrigin.AnidbResource : result.Origin)
                .ThenBy(result => result.Rejection is null ? 0 : 1),
        ];

    internal static bool IsShortFormAnime(AniDB_Anime anime)
        => IsShortFormByEpisodeCount(anime.AniDBEpisodes.Count(e => e.EpisodeType is EpisodeType.Episode));

    internal static bool IsShortFormByEpisodeCount(int mainEpisodeCount) => mainEpisodeCount <= 4;

    /// <summary>
    ///   Whether something dated so aired, or airs within the window, which
    ///   is when the auto-search looks for it.
    /// </summary>
    /// <param name="airDate">When it airs, or <see langword="null"/> when that is not known.</param>
    /// <param name="now">The time now.</param>
    /// <param name="window">How far ahead of its air date it is looked for.</param>
    /// <returns><see langword="true"/> when it may be searched for.</returns>
    internal static bool AiredWithin([NotNullWhen(true)] DateTime? airDate, DateTime now, TimeSpan window)
        => airDate is { } date && (date <= now || date - now <= window);

    /// <summary>
    ///   The anime's first or second regular episode, whose date stands in
    ///   for the anime's when it has none.
    /// </summary>
    /// <param name="anime">The anime.</param>
    /// <returns>The episode, or <see langword="null"/> when it has no regular episode.</returns>
    private static AniDB_Episode? SecondEpisode(AniDB_Anime anime)
        => anime.AniDBEpisodes
            .Where(episode => episode.EpisodeType is EpisodeType.Episode)
            .OrderBy(episode => episode.EpisodeNumber)
            .Take(2)
            .LastOrDefault();

    /// <summary>
    ///   When an anime searched for as a show aired, as the search's window
    ///   is checked against.
    /// </summary>
    /// <param name="anime">The anime.</param>
    /// <returns>The date, or <see langword="null"/> when it is not known.</returns>
    internal static DateTime? ShowAirDate(AniDB_Anime anime)
        => anime.AirDate?.ToDateTime() ?? SecondEpisode(anime)?.GetAirDateAsDate();

    /// <summary>
    ///   When the film one episode of an anime stands for aired, as the
    ///   search's window is checked against: the anime's date for an anime
    ///   of one film or a complete film, the episode's otherwise.
    /// </summary>
    /// <param name="anime">The anime.</param>
    /// <param name="episode">The episode standing for the film.</param>
    /// <returns>The date, or <see langword="null"/> when it is not known.</returns>
    internal static DateTime? FilmAirDate(AniDB_Anime anime, AniDB_Episode episode)
    {
        var films = anime.AniDBEpisodes.Count(other => other.EpisodeType is EpisodeType.Episode or EpisodeType.Special or EpisodeType.Other);
        var whole = films is 1 || episode.GetTitles().Any(title => title.Value.Contains("Complete Movie", StringComparison.InvariantCultureIgnoreCase));
        return whole
            ? anime.AirDate?.ToDateTime() ?? episode.GetAirDateAsDate()
            : episode.GetAirDateAsDate() ?? anime.AirDate?.ToDateTime();
    }

    private async Task<IReadOnlyList<TmdbAutoSearchResult>> AutoSearchForMoviesWithShowFallback(AniDB_Anime anime, IReadOnlyList<TmdbHint> hints)
    {
        var movieResults = await AutoSearchForMovies(anime, hints).ConfigureAwait(false);
        if (movieResults.Any(result => result.Rejection is null))
            return movieResults;

        // The films turned down are still shown beside the shows.
        return [.. movieResults, .. await AutoSearchForShow(anime, hints).ConfigureAwait(false)];
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

    private async Task<IReadOnlyList<TmdbAutoSearchResult>> AutoSearchForMovies(AniDB_Anime anime, IReadOnlyList<TmdbHint> hints)
    {
        // Find the official title in the origin language, to compare it against
        // the original language stored in the offline tmdb search dump.
        var list = new List<TmdbAutoSearchResult>();
        var allTitles = anime.Titles
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
        var title = mainTitle.Value;
        var officialTitle = language == mainTitle.Language ? mainTitle.Value :
            allTitles.FirstOrDefault(title => title.Language == language)?.Value;
        var englishTitle = allTitles.FirstOrDefault(title => title.Language == TitleLanguage.English)?.Value;

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
            if (!AiredWithin(airDate, now, _maxDaysIntoTheFuture))
                return [];
            var year = (anime.RegularAirDate?.ToDateTime() ?? episodes[0].RegularAirDate?.ToDateTime(TimeOnly.MinValue) ?? airDate.Value).Year;
            await AutoSearchForMovie(list, anime, episodes[0], officialTitle, englishTitle, title, year, anime.IsRestricted, hints).ConfigureAwait(false);
            return list;
        }

        // Find the sub title for each movie in the movie collection, then
        // search for a movie matching the combined title.
        foreach (var episode in episodes)
        {
            var allEpisodeTitles = episode.GetTitles();
            var isCompleteMovie = allEpisodeTitles.Any(title => title.Value.Contains("Complete Movie", StringComparison.InvariantCultureIgnoreCase));
            if (isCompleteMovie)
            {
                var airDateForAnime = anime.AirDate?.ToDateTime() ?? episodes[0].GetAirDateAsDate() ?? null;
                if (!AiredWithin(airDateForAnime, now, _maxDaysIntoTheFuture))
                    continue;
                var yearForAnime = (anime.RegularAirDate?.ToDateTime() ?? episodes[0].RegularAirDate?.ToDateTime(TimeOnly.MinValue) ?? airDateForAnime.Value)
                    .Year;
                await AutoSearchForMovie(list, anime, episode, officialTitle, englishTitle, title, yearForAnime, anime.IsRestricted, hints).ConfigureAwait(false);
                continue;
            }

            var airDateForEpisode = episode.GetAirDateAsDate() ?? anime.AirDate?.ToDateTime() ?? null;
            if (!AiredWithin(airDateForEpisode, now, _maxDaysIntoTheFuture))
                continue;

            var officialSubTitle = allEpisodeTitles.FirstOrDefault(title => title.Language == language)?.Value ??
                allEpisodeTitles.FirstOrDefault(title => title.Language == mainTitle.Language)?.Value;
            var englishSubTitle = episode.EnglishTitle;
            var isGenericTitle = string.Equals(englishSubTitle, $"Movie {episode.EpisodeNumber}", StringComparison.InvariantCultureIgnoreCase);
            var officialFullTitle = FullMovieTitle(officialTitle, officialSubTitle, episode.EpisodeNumber, isGenericTitle);
            var englishFullTitle = FullMovieTitle(englishTitle, englishSubTitle, episode.EpisodeNumber, isGenericTitle);
            var mainFullTitle = FullMovieTitle(title, englishSubTitle, episode.EpisodeNumber, isGenericTitle);

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

            var yearForEpisode = (episode.RegularAirDate?.ToDateTime(TimeOnly.MinValue) ?? anime.RegularAirDate?.ToDateTime() ?? airDateForEpisode.Value).Year;
            await AutoSearchForMovie(list, anime, episode, officialFullTitle, englishFullTitle, mainFullTitle, yearForEpisode, anime.IsRestricted, hints)
                .ConfigureAwait(false);
        }

        return list;
    }

    /// <summary>
    ///   The title to search for one film of several: the anime's title and
    ///   the episode's, or the film's number when the episode is only called
    ///   "Movie N".
    /// </summary>
    /// <remarks>
    ///   Without a title for the anime there is nothing to search with, as the
    ///   episode's title alone (often "Episode 1") names any film at all, so
    ///   the other titles are searched instead.
    /// </remarks>
    /// <param name="animeTitle">The anime's title in some language, if it has one.</param>
    /// <param name="subTitle">The episode's title in the same language, if it has one.</param>
    /// <param name="episodeNumber">The episode's number.</param>
    /// <param name="isGenericTitle">Whether the episode is only called "Movie N".</param>
    /// <returns>The title, or <see langword="null"/> when either part is missing.</returns>
    internal static string? FullMovieTitle(string? animeTitle, string? subTitle, int episodeNumber, bool isGenericTitle)
    {
        if (string.IsNullOrWhiteSpace(animeTitle) || string.IsNullOrWhiteSpace(subTitle))
            return null;

        return isGenericTitle ? $"{animeTitle} {episodeNumber}" : $"{animeTitle} {subTitle}";
    }

    private async Task<bool> AutoSearchForMovie(
        List<TmdbAutoSearchResult> list,
        AniDB_Anime anime,
        AniDB_Episode episode,
        string? officialTitle,
        string? englishTitle,
        string? mainTitle,
        int year,
        bool isRestricted,
        IReadOnlyList<TmdbHint> hints
    )
    {
        foreach (var query in new[] { officialTitle, englishTitle, mainTitle }.Distinct(StringComparer.Ordinal))
        {
            if (string.IsNullOrWhiteSpace(query))
                continue;

            var scored = await AutoSearchMovieUsingTitle(anime, episode, query, hints, includeRestricted: isRestricted, year: year).ConfigureAwait(false);
            list.AddRange(scored);
            if (scored.Any(result => result.Rejection is null))
                return true;
        }

        return false;
    }

    private async Task<IReadOnlyList<TmdbAutoSearchResult>> AutoSearchMovieUsingTitle(
        AniDB_Anime anime,
        AniDB_Episode episode,
        string query,
        IReadOnlyList<TmdbHint> hints,
        bool includeRestricted = false,
        int year = 0
    )
    {
        var candidateCount = _settingsProvider.GetSettings().TMDB.AutoSearchMovieCandidateCount;
        var seen = new HashSet<int>();
        var candidates = new List<SearchMovie>();

        List<SearchMovie> results;

        // Attempt #1: full title + year
        (results, _) = await SearchMoviesRaw(query, includeRestricted: includeRestricted, year: year).ConfigureAwait(false);
        CollectMovieCandidates(candidates, results, seen, candidateCount);

        // Attempt #2: sequel-suffix stripped + year
        var strippedTitle = TitleVariants.WithoutSequelSuffix(query);
        if (!string.IsNullOrEmpty(strippedTitle) && candidates.Count < candidateCount)
        {
            (results, _) = await SearchMoviesRaw(strippedTitle, includeRestricted: includeRestricted, year: year).ConfigureAwait(false);
            CollectMovieCandidates(candidates, results, seen, candidateCount);
        }

        // Attempts #3–4: year-free fallbacks mirroring #1–2.
        // Always run regardless of year-filtered candidate count — TMDB's year filter uses
        // release_date.year, so a movie whose AniDB air date differs from TMDB release year
        // will be missed entirely by year-filtered searches.
        var yearFreeCap = candidateCount * 2;

        (results, _) = await SearchMoviesRaw(query, includeRestricted: includeRestricted).ConfigureAwait(false);
        CollectMovieCandidates(candidates, results, seen, yearFreeCap);

        if (!string.IsNullOrEmpty(strippedTitle) && candidates.Count < yearFreeCap)
        {
            (results, _) = await SearchMoviesRaw(strippedTitle, includeRestricted: includeRestricted).ConfigureAwait(false);
            CollectMovieCandidates(candidates, results, seen, yearFreeCap);
        }

        if (candidates.Count == 0)
            return [];

        var options = new MovieMatchOptions { Query = query, IncludeRestricted = includeRestricted, HintedIDs = HintedIDs(hints, isMovie: true) };
        var ranked = await PickMovie(
            _matchingEngine,
            anime,
            episode,
            options,
            HintedFirst(candidates.Select(candidate => candidate.Id), hints, isMovie: true),
            async movieID => await FetchMovie(movieID, "auto-match scoring").ConfigureAwait(false) is { } full ? TmdbMetadataProvider.ToSearchResult(full) : null
        ).ConfigureAwait(false);
        if (ranked is [{ Rejection: MatchRejectionReason.None } best, ..])
            _logger.LogInformation("Best match for \"{Query}\": {MovieName} ({ID}) rating={Rating}", query, best.Candidate.OriginalTitle, best.Candidate.ID.ID, best.Rating);

        return
        [
            .. ranked.Select(match => new TmdbAutoSearchResult(anime, episode, candidates.First(candidate => candidate.Id.ToString() == match.Candidate.ID.ID), match.Rating)
            {
                IsRemote = true,
                Candidate = match.Candidate,
                Rejection = Rejected(match.Rejection, query, match.Details),
            }),
        ];
    }

    /// <summary>
    ///   Fetches the candidate films one at a time and has the engine pick
    ///   one, stopping at the first that matches on both title and date.
    /// </summary>
    /// <remarks>
    ///   Nothing can outrank a film matching on both, so the ones after it are
    ///   never fetched.
    /// </remarks>
    /// <param name="engine">The matching engine.</param>
    /// <param name="anime">The anime the film belongs to.</param>
    /// <param name="episode">The episode standing for the film.</param>
    /// <param name="options">What was searched for.</param>
    /// <param name="candidateIDs">The films the searches offered, in order.</param>
    /// <param name="fetchMovie">
    ///   Fetches a film whole, with its translations and release dates, or
    ///   gives <see langword="null"/> when it cannot be had.
    /// </param>
    /// <returns>
    ///   The films fetched, best first, the first taken unless it says why
    ///   not; empty when none could be fetched.
    /// </returns>
    internal static async Task<IReadOnlyList<MovieMatch>> PickMovie(
        IMetadataMatchingEngine engine,
        IAnidbAnime anime,
        IAnidbEpisode episode,
        MovieMatchOptions options,
        IReadOnlyList<int> candidateIDs,
        Func<int, Task<MetadataMovieSearchResult?>> fetchMovie
    )
    {
        var fetched = new List<MetadataMovieSearchResult>();
        foreach (var movieID in candidateIDs)
        {
            if (await fetchMovie(movieID).ConfigureAwait(false) is not { } candidate)
                continue;

            fetched.Add(candidate);
            if (engine.MatchMovies(anime, episode, [candidate], options) is [{ Rating: MatchRating.DateAndTitleMatches }])
                break;
        }

        // Nothing fetched, as through an outage, leaves nothing to tell one
        // candidate from an unrelated one, so nothing is guessed.
        return fetched.Count > 0 ? engine.MatchMovies(anime, episode, fetched, options) : [];
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

    private async Task<IReadOnlyList<TmdbAutoSearchResult>> AutoSearchForShow(AniDB_Anime anime, IReadOnlyList<TmdbHint> hints)
    {
        // TODO: Improve this logic to take tmdb seasons into account, and maybe also take better anidb series relations into account in cases where the tmdb show name and anidb series name are too different.

        // Get the first or second episode to get the aired date if the anime is missing a date.
        var secondEpisode = SecondEpisode(anime);
        var storedAirDate = ShowAirDate(anime);

        // Abort if the show have not aired within the _maxDaysIntoTheFuture limit.
        if (!AiredWithin(storedAirDate, DateTime.Now, _maxDaysIntoTheFuture))
            return [];

        // The regular broadcast dates are matched against, since the anime or
        // its first episodes may be dated by an early showing.
        var airDate = anime.RegularAirDate?.ToDateTime() ?? secondEpisode?.RegularAirDate?.ToDateTime(TimeOnly.MinValue) ?? storedAirDate;

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

        // Each title's search often returns the same first shows, so a season is fetched once for all.
        var seasons = new Dictionary<(int ShowID, int SeasonNumber), Task<IReadOnlyList<MetadataSearchResultEpisode>?>>();
        var aligned = new HashSet<int>();
        Task<IReadOnlyList<MetadataSearchResultEpisode>?> FetchSeasonOnce(int showID, int seasonNumber)
        {
            if (!seasons.TryGetValue((showID, seasonNumber), out var fetching))
                seasons[(showID, seasonNumber)] = fetching = FetchSeasonEpisodes(showID, seasonNumber);
            return fetching;
        }

        // First attempt the official title in the country of origin.
        var scored = new List<TmdbAutoSearchResult>();
        Task<TmdbAutoSearchResult?> SearchByTitle(string title, bool restricted, bool isJapanese)
            => AutoSearchForShowUsingTitle(scored, anime, title, airDate.Value, restricted, isJapanese, hints, FetchSeasonOnce, aligned);

        var originalTitle = language == mainTitle.Language
            ? mainTitle.Value
            : (
                series.ID.ID == anime.AnimeID.ToString()
                    ? allTitles.FirstOrDefault(title => title.Type is TitleType.Official && title.Language == language)?.Value
                    : series.Titles.FirstOrDefault(title => title.Type is TitleType.Official && title.Language == language)?.Value
            );
        var match = !string.IsNullOrEmpty(originalTitle)
            ? await SearchByTitle(originalTitle, series.Restricted, language == TitleLanguage.Japanese)
            : null;

        // And if that failed, then try the official english title.
        if (match is null)
        {
            var englishTitle = series.ID.ID == anime.AnimeID.ToString()
                ? allTitles.FirstOrDefault(l => l is { Type: TitleType.Official, Language: TitleLanguage.English })?.Value
                : series.Titles.FirstOrDefault(l => l is { Type: TitleType.Official, Language: TitleLanguage.English })?.Value;
            if (!string.IsNullOrEmpty(englishTitle) && (string.IsNullOrEmpty(originalTitle) || !string.Equals(englishTitle, originalTitle, StringComparison.Ordinal)))
                match = await SearchByTitle(englishTitle, series.Restricted, false);
        }

        // And the last ditch attempt will be to use the main title. We won't try other languages.
        match ??= await SearchByTitle(mainTitle.Value, series.Restricted, false);

        // The searches above used the prequel's titles, which find the wrong show when the
        // anime is its own TMDB show, so its own title is tried and kept if it scores better.
        var prequelFollowed = series.ID.ID != anime.AnimeID.ToString();
        if (prequelFollowed)
        {
            TmdbAutoSearchResult? ownTitleMatch = null;

            // Try the current anime's own original title first, before falling back to the main title.
            var ownOriginalTitle = language == mainTitle.Language
                ? mainTitle.Value
                : allTitles.FirstOrDefault(t => t.Type is TitleType.Official && t.Language == language)?.Value;
            if (!string.IsNullOrEmpty(ownOriginalTitle) && !string.Equals(ownOriginalTitle, originalTitle, StringComparison.Ordinal))
                ownTitleMatch = await SearchByTitle(ownOriginalTitle, anime.IsRestricted, language == TitleLanguage.Japanese).ConfigureAwait(false);

            if (ownTitleMatch is null && !string.Equals(mainTitle.Value, originalTitle, StringComparison.Ordinal) && !string.Equals(mainTitle.Value, ownOriginalTitle, StringComparison.Ordinal))
                ownTitleMatch = await SearchByTitle(mainTitle.Value, anime.IsRestricted, false).ConfigureAwait(false);

            var airedOn = DateOnly.FromDateTime(airDate.Value);
            var episodeCount = ((ISeries)anime).EpisodeCounts.Episodes;
            if (ownTitleMatch is not null && (match is null || OwnTitleWins(Found(ownTitleMatch, hints), Found(match, hints), airedOn, episodeCount)))
            {
                if (match is not null)
                    match.Rejection = new()
                    {
                        Reason = MatchRejectionReason.Outranked,
                        Details = $"Found through the titles of the prequel, AniDB anime {series.ID.ID}, rated {match.MatchRating}; " +
                            $"{Named(ownTitleMatch)}, found through the anime's own titles, rated {ownTitleMatch.MatchRating}, was taken{HintNote(ownTitleMatch, hints)}.",
                    };
                match = ownTitleMatch;
            }
            else if (ownTitleMatch is not null && match is not null)
            {
                ownTitleMatch.Rejection = new()
                {
                    Reason = MatchRejectionReason.Outranked,
                    Details = $"Found through the anime's own titles, rated {ownTitleMatch.MatchRating}; " +
                        $"{Named(match)}, found through the titles of the prequel, AniDB anime {series.ID.ID}, rated {match.MatchRating}, was taken{HintNote(match, hints)}.",
                };
            }
        }

        // A show scored more than once counts as taken if any of its scorings took it.
        List<TmdbAutoSearchResult> results =
        [
            .. scored
                .GroupBy(result => result.TmdbShow!.ID)
                .Select(group => group.FirstOrDefault(result => result.Rejection is null) ?? group.First()),
        ];

        // Listed for context (a sequel is usually one of their seasons), never taken
        // and never rated for this anime.
        if (prequelFollowed && series is AniDB_Anime prequel && prequel.TmdbShowCrossReferences is { Count: > 0 } prequelXrefs)
            results.AddRange(prequelXrefs
                .DistinctBy(xref => xref.TmdbShowID)
                .Select(xref => (xref, show: xref.TmdbShow))
                .Where(pair => pair.show is not null)
                .Select(pair => PrequelLink(anime, prequel, pair.xref.MatchRating, pair.show!)));

        return results;
    }

    /// <summary>
    ///   A show a prequel of the anime is linked to, listed for context.
    /// </summary>
    /// <param name="anime">The anime searched for.</param>
    /// <param name="prequel">The prequel.</param>
    /// <param name="linkRating">The rating of the prequel's link.</param>
    /// <param name="show">The show.</param>
    /// <returns>The candidate, rated nothing and turned down as an existing link.</returns>
    internal static TmdbAutoSearchResult PrequelLink(AniDB_Anime anime, AniDB_Anime prequel, MatchRating linkRating, TMDB_Show show)
        => new(anime, RawShow(show), MatchRating.None)
        {
            IsLocal = true,
            Candidate = TmdbMetadataProvider.ToSearchResult(show),
            Origin = MetadataAutoLinkOrigin.PrequelLink,
            LinkMatchRating = linkRating,
            PrequelAnidbAnimeID = prequel.AnimeID,
            Rejection = new()
            {
                Reason = MatchRejectionReason.ExistingLink,
                Details = $"Linked to the prequel, AniDB anime {prequel.AnimeID}, rated {linkRating}. Listed for context; never linked by the search.",
            },
        };

    private async Task<TmdbAutoSearchResult?> AutoSearchForShowUsingTitle(
        List<TmdbAutoSearchResult> scored,
        AniDB_Anime anime,
        string originalTitle,
        DateTime airDate,
        bool restricted,
        bool isJapanese,
        IReadOnlyList<TmdbHint> hints,
        Func<int, int, Task<IReadOnlyList<MetadataSearchResultEpisode>?>> fetchSeason,
        ISet<int> aligned
    )
    {
        var candidateCount = _settingsProvider.GetSettings().TMDB.AutoSearchShowCandidateCount;
        var seen = new HashSet<int>();
        var candidates = new List<SearchTv>();

        List<SearchTv> results;

        // Attempt #1: full title + year
        (results, _) = await SearchShowsRaw(originalTitle, includeRestricted: restricted, year: airDate.Year).ConfigureAwait(false);
        CollectCandidates(candidates, results, seen, candidateCount);

        // Attempt #2: sequel-suffix stripped + year
        var strippedTitle = TitleVariants.WithoutSequelSuffix(originalTitle);
        if (!string.IsNullOrEmpty(strippedTitle) && candidates.Count < candidateCount)
        {
            (results, _) = await SearchShowsRaw(strippedTitle, includeRestricted: restricted, year: airDate.Year).ConfigureAwait(false);
            CollectCandidates(candidates, results, seen, candidateCount);
        }

        // Attempt #3: subtitle stripped + year. The engine rates this form a close match only,
        // so a parent show found by its short title can't outscore one matching the full title.
        var titleWithoutSubTitle = TitleVariants.WithoutSubtitle(strippedTitle ?? originalTitle, isJapanese);
        if (!string.IsNullOrEmpty(titleWithoutSubTitle) && candidates.Count < candidateCount)
        {
            (results, _) = await SearchShowsRaw(titleWithoutSubTitle, includeRestricted: restricted, year: airDate.Year).ConfigureAwait(false);
            CollectCandidates(candidates, results, seen, candidateCount);
        }

        // Attempts #4–6: year-free fallbacks mirroring #1–3.
        // These always run regardless of how many year-filtered candidates were collected, because
        // a multi-season show's first_air_date predates the current season year and will never
        // appear in year-filtered results (e.g. TenSura S2 2021 vs. main show premiered 2018).
        // The doubled cap (×2) lets year-free results fill more of the pool — root shows tend to
        // rank lower in unfiltered searches so a slightly larger window reduces missed matches.
        var yearFreeCap = candidateCount * 2;

        (results, _) = await SearchShowsRaw(originalTitle, includeRestricted: restricted).ConfigureAwait(false);
        CollectCandidates(candidates, results, seen, yearFreeCap);

        if (!string.IsNullOrEmpty(strippedTitle) && candidates.Count < yearFreeCap)
        {
            (results, _) = await SearchShowsRaw(strippedTitle, includeRestricted: restricted).ConfigureAwait(false);
            CollectCandidates(candidates, results, seen, yearFreeCap);
        }

        if (!string.IsNullOrEmpty(titleWithoutSubTitle) && candidates.Count < yearFreeCap)
        {
            (results, _) = await SearchShowsRaw(titleWithoutSubTitle, includeRestricted: restricted).ConfigureAwait(false);
            CollectCandidates(candidates, results, seen, yearFreeCap);
        }

        if (candidates.Count == 0)
            return null;

        var options = new SeriesMatchOptions
        {
            Query = originalTitle,
            QueryLanguage = isJapanese ? TitleLanguage.Japanese : null,
            IncludeRestricted = restricted,
            HintedIDs = HintedIDs(hints, isMovie: false),
        };
        var ranked = await PickShow(
            _matchingEngine,
            anime,
            options,
            HintedFirst(candidates.Select(candidate => candidate.Id), hints, isMovie: false),
            async showID => await FetchShow(showID, "auto-match scoring").ConfigureAwait(false) is { } full
                ? WithStoredEpisodes(TmdbMetadataProvider.ToSearchResult(full))
                : null,
            fetchSeason,
            alignedShows: aligned
        ).ConfigureAwait(false);

        TmdbAutoSearchResult? taken = null;
        foreach (var judged in ranked)
        {
            var result = new TmdbAutoSearchResult(anime, candidates.First(candidate => candidate.Id.ToString() == judged.Candidate.ID.ID), judged.Rating)
            {
                IsRemote = true,
                Candidate = judged.Candidate,
                Rejection = Rejected(judged.Rejection, originalTitle, judged.Details),
            };
            scored.Add(result);
            if (result.Rejection is null)
                taken ??= result;
        }

        if (taken is not null)
            _logger.LogInformation(
                "Best match for \"{Query}\": {ShowName} ({ID}) rating={Rating}",
                originalTitle, taken.TmdbShow!.OriginalTitle, taken.TmdbShow.ID, taken.MatchRating
            );
        return taken;
    }

    /// <summary>
    ///   How many of the shows fetched for one anime, over every title it is
    ///   searched by, in the order they are fetched, have a season's episodes
    ///   fetched as well to line them up with the anime by air date, a call
    ///   each.
    /// </summary>
    internal const int AlignedCandidateCount = 3;

    /// <summary>
    ///   Fetches the candidate shows one at a time and has the engine pick
    ///   one, stopping at the first that matches on both title and date.
    /// </summary>
    /// <remarks>
    ///   Nothing outranks a show matching on both, so later ones are never
    ///   fetched. For the first few rated shows, the season holding the
    ///   anime's start is fetched with its episodes so the engine can line
    ///   them up by air date (a stored show needs no call). Otherwise a season
    ///   is only fetched when the years disagree and the engine picks one.
    /// </remarks>
    /// <param name="engine">The matching engine.</param>
    /// <param name="anime">The anime being matched.</param>
    /// <param name="options">What was searched for.</param>
    /// <param name="candidateIDs">The shows the searches offered, in order.</param>
    /// <param name="fetchShow">
    ///   Fetches a show whole, with its translations and seasons, or gives
    ///   <see langword="null"/> when it cannot be had.
    /// </param>
    /// <param name="fetchSeasonEpisodes">
    ///   Fetches the episodes of a show's season, or gives
    ///   <see langword="null"/> when they cannot be had.
    /// </param>
    /// <param name="alignedCandidates">
    ///   How many shows have a season fetched to line them up by air date.
    /// </param>
    /// <param name="alignedShows">
    ///   The shows lined up so far, shared by the searches for one anime so
    ///   they count against one cap, and added to; a new set when
    ///   <see langword="null"/>. A show lined up before is lined up again.
    /// </param>
    /// <returns>
    ///   The shows fetched, best first, the first taken unless it says why
    ///   not; empty when none could be fetched.
    /// </returns>
    internal static async Task<IReadOnlyList<SeriesMatch>> PickShow(
        IMetadataMatchingEngine engine,
        IAnidbAnime anime,
        SeriesMatchOptions options,
        IReadOnlyList<int> candidateIDs,
        Func<int, Task<MetadataSeriesSearchResult?>> fetchShow,
        Func<int, int, Task<IReadOnlyList<MetadataSearchResultEpisode>?>> fetchSeasonEpisodes,
        int alignedCandidates = AlignedCandidateCount,
        ISet<int>? alignedShows = null
    )
    {
        var hasFirstEpisodeDate = anime.Episodes.Any(episode => episode is { Type: EpisodeType.Episode, EpisodeNumber: 1, RegularAirDate: not null });
        var startedOn = StartOf(anime);
        alignedShows ??= new HashSet<int>();
        var fetched = new List<MetadataSeriesSearchResult>();
        foreach (var showID in candidateIDs)
        {
            if (await fetchShow(showID).ConfigureAwait(false) is not { } candidate)
                continue;

            var judged = engine.MatchSeries(anime, [candidate], options)[0];
            if ((alignedShows.Contains(showID) || alignedShows.Count < alignedCandidates) &&
                startedOn is { } start && judged.Rating is not MatchRating.None &&
                candidate.Seasons is { Count: > 0 } seasons && seasons.All(season => season.Episodes is null) &&
                (SeasonHolding(seasons, start) ?? judged.SeasonNumber) is { } holding)
            {
                alignedShows.Add(showID);
                if (await fetchSeasonEpisodes(showID, holding).ConfigureAwait(false) is { } episodes)
                {
                    candidate = WithEpisodes(candidate, holding, episodes);
                    judged = engine.MatchSeries(anime, [candidate], options)[0];
                }
            }

            if (hasFirstEpisodeDate && !HasDate(judged.Rating) && judged.SeasonNumber is { } seasonNumber &&
                candidate.Seasons?.FirstOrDefault(season => season.SeasonNumber == seasonNumber) is { Episodes: null } &&
                await fetchSeasonEpisodes(showID, seasonNumber).ConfigureAwait(false) is { } seasonEpisodes)
            {
                candidate = WithEpisodes(candidate, seasonNumber, seasonEpisodes);
                judged = engine.MatchSeries(anime, [candidate], options)[0];
            }

            fetched.Add(candidate);
            if (judged.Rating is MatchRating.DateAndTitleMatches)
                break;
        }

        // Nothing fetched, as through an outage, leaves nothing to tell one
        // candidate from an unrelated one, so nothing is guessed.
        return fetched.Count > 0 ? engine.MatchSeries(anime, fetched, options) : [];

        static bool HasDate(MatchRating rating)
            => rating is MatchRating.DateAndTitleMatches or MatchRating.DateAndTitleKindaMatches or MatchRating.DateMatches;
    }

    /// <summary>
    ///   When the anime's regular broadcast started: its first dated regular
    ///   episode's regular date, or the anime's own.
    /// </summary>
    /// <param name="anime">The anime.</param>
    /// <returns>The date, or <see langword="null"/> when nothing is dated.</returns>
    internal static DateOnly? StartOf(IAnidbAnime anime)
        => anime.Episodes
            .Where(episode => episode is { Type: EpisodeType.Episode, RegularAirDate: not null })
            .OrderBy(episode => episode.EpisodeNumber)
            .Select(episode => episode.RegularAirDate)
            .FirstOrDefault() ?? (anime.RegularAirDate is { IsComplete: true } regular ? regular.ToDateOnly() : null);

    /// <summary>
    ///   The season a date falls in: the last one begun by it, give or take
    ///   three days.
    /// </summary>
    /// <param name="seasons">The show's regular seasons.</param>
    /// <param name="date">The date.</param>
    /// <returns>The season's number, or <see langword="null"/> when no season is dated or none began by then.</returns>
    internal static int? SeasonHolding(IReadOnlyList<MetadataSearchResultSeason> seasons, DateOnly date)
        => seasons
            .Where(season => season.FirstAiredAt is { IsComplete: true } began && began.ToDateOnly().DayNumber <= date.DayNumber + 3)
            .OrderByDescending(season => season.FirstAiredAt!.Value.ToDateOnly())
            .Select(season => (int?)season.SeasonNumber)
            .FirstOrDefault();

    /// <summary>
    ///   A show with one season's episodes filled in, and the season's first
    ///   episode dated from them.
    /// </summary>
    /// <param name="candidate">The show.</param>
    /// <param name="seasonNumber">The season.</param>
    /// <param name="episodes">The season's episodes.</param>
    /// <returns>The show.</returns>
    internal static MetadataSeriesSearchResult WithEpisodes(
        MetadataSeriesSearchResult candidate,
        int seasonNumber,
        IReadOnlyList<MetadataSearchResultEpisode> episodes
    )
        => candidate with
        {
            Seasons = [
                .. (candidate.Seasons ?? []).Select(season => season.SeasonNumber == seasonNumber
                    ? season with
                    {
                        Episodes = episodes,
                        FirstEpisodeAiredAt = episodes.OrderBy(episode => episode.EpisodeNumber).FirstOrDefault()?.AiredAt,
                    }
                    : season),
            ],
        };

    /// <summary>
    ///   A show with the episodes of every season filled in from the stored
    ///   show, when it is stored, and its seasons too when the search result
    ///   had none.
    /// </summary>
    /// <param name="candidate">The show.</param>
    /// <returns>The show, unchanged when it is not stored.</returns>
    private MetadataSeriesSearchResult WithStoredEpisodes(MetadataSeriesSearchResult candidate)
    {
        if (!candidate.ID.TryGetNumericID<int>(out var showID))
            return candidate;

        var stored = _tmdbEpisodes.GetByTmdbShowID(showID)
            .Where(episode => episode.SeasonNumber > 0)
            .GroupBy(episode => episode.SeasonNumber)
            .ToDictionary(
                group => group.Key,
                group => (IReadOnlyList<MetadataSearchResultEpisode>)[.. group.OrderBy(episode => episode.EpisodeNumber).Select(ToSearchResult)]
            );
        if (stored.Count is 0)
            return candidate;

        var seasons = candidate.Seasons ??
        [
            .. _tmdbSeasons.GetByTmdbShowID(showID)
                .Where(season => season.SeasonNumber > 0)
                .OrderBy(season => season.SeasonNumber)
                .Select(season => new MetadataSearchResultSeason
                {
                    SeasonNumber = season.SeasonNumber,
                    EpisodeCount = season.EpisodeCount,
                    FirstAiredAt = stored.TryGetValue(season.SeasonNumber, out var episodes) && episodes.Select(episode => episode.AiredAt).Min() is { } began
                        ? PartialDateOnly.FromDateOnly(began)
                        : null,
                }),
        ];
        return candidate with
        {
            Seasons = [
                .. seasons.Select(season => stored.TryGetValue(season.SeasonNumber, out var episodes)
                    ? season with { Episodes = episodes, FirstEpisodeAiredAt = episodes.FirstOrDefault()?.AiredAt }
                    : season),
            ],
        };

        static MetadataSearchResultEpisode ToSearchResult(TMDB_Episode episode)
            => new() { EpisodeNumber = episode.EpisodeNumber, AiredAt = episode.AiredAt, Title = episode.EnglishTitle };
    }

    /// <summary>
    ///   Fetches the episodes of one season of a show, to line them up with
    ///   the anime's by air date.
    /// </summary>
    /// <param name="showID">The TMDB show ID.</param>
    /// <param name="seasonNumber">The season's number.</param>
    /// <returns>The episodes, or <see langword="null"/> when TMDB has no such season.</returns>
    private async Task<IReadOnlyList<MetadataSearchResultEpisode>?> FetchSeasonEpisodes(int showID, int seasonNumber)
    {
        var season = await _tmdbService.UseClient(
            c => c.GetTvSeasonAsync(showID, seasonNumber),
            $"Fetch season {seasonNumber} of show {showID} to line its episodes up with the anime"
        ).ConfigureAwait(false);
        return season?.Episodes is { } episodes
            ?
            [
                .. episodes.Select(episode => new MetadataSearchResultEpisode
                {
                    EpisodeNumber = (int)episode.EpisodeNumber,
                    AiredAt = episode.AirDate is { } airedAt ? DateOnly.FromDateTime(airedAt) : null,
                    Title = episode.Name,
                }),
            ]
            : null;
    }

    #endregion

    #region AniDB hints

    /// <summary>
    ///   A TMDB entry the anime's AniDB resources, or its links on other
    ///   sources, name.
    /// </summary>
    /// <param name="TmdbID">The show's or film's TMDB ID.</param>
    /// <param name="IsMovie">Whether it is a film rather than a show.</param>
    /// <param name="ImdbID">
    ///   The IMDb title it was found through, when AniDB named no TMDB entry,
    ///   or <see langword="null"/>.
    /// </param>
    /// <param name="NamedBy">
    ///   The linked entries of other sources naming it, or
    ///   <see langword="null"/> for one the AniDB resources name.
    /// </param>
    /// <param name="AnidbEpisodeID">
    ///   The AniDB episode a film was named for, or <see langword="null"/>.
    /// </param>
    internal readonly record struct TmdbHint(
        int TmdbID,
        bool IsMovie,
        string? ImdbID = null,
        IReadOnlyList<MetadataGuid>? NamedBy = null,
        int? AnidbEpisodeID = null
    )
    {
        /// <summary>
        ///   The entry's identity.
        /// </summary>
        public MetadataGuid ID => new(MetadataSource.TMDB, IsMovie ? MetadataEntityType.Movie : MetadataEntityType.Series, TmdbID.ToString(CultureInfo.InvariantCulture));

        /// <summary>
        ///   Where the hint came from, as a candidate's origin.
        /// </summary>
        public MetadataAutoLinkOrigin Origin => NamedBy is null ? MetadataAutoLinkOrigin.AnidbResource : MetadataAutoLinkOrigin.CrossSourceLink;

        /// <summary>
        ///   Where the hint came from, as a clause.
        /// </summary>
        public string Source => (NamedBy, ImdbID) switch
        {
            ({ Count: > 0 } namedBy, _) =>
                $"{namedBy[0]}{(namedBy.Count > 1 ? $" and {namedBy.Count - 1} more" : string.Empty)}, linked to the anime, " +
                $"{(namedBy.Count > 1 ? "name" : "names")} TMDB {(IsMovie ? "movie" : "show")} {TmdbID}",
            (not null, _) => $"The anime's links name TMDB {(IsMovie ? "movie" : "show")} {TmdbID}",
            (_, null) => $"AniDB names TMDB {(IsMovie ? "movie" : "show")} {TmdbID}",
            _ => $"AniDB names IMDb title {ImdbID}, which is TMDB {(IsMovie ? "movie" : "show")} {TmdbID}",
        };

        /// <summary>
        ///   A clause saying what names the entry, for a show taken.
        /// </summary>
        public string Note => NamedBy is null ? "the anime's AniDB resources naming it" : "the anime's links on other sources naming it";
    }

    /// <summary>
    ///   The TMDB entries the anime's links on other sources name.
    /// </summary>
    /// <param name="hints">The hints the core read from the linked entries.</param>
    /// <returns>Each show and film once, in the core's order; an episode whose show is unknown is left out.</returns>
    internal static IReadOnlyList<TmdbHint> TmdbHintsOf(IEnumerable<MetadataAutoLinkHint> hints)
        => [
            .. hints
                .Where(hint => hint.ID.Source == MetadataSource.TMDB &&
                    (hint.ID.EntityType == MetadataEntityType.Series || hint.ID.EntityType == MetadataEntityType.Movie))
                .Select(hint => hint.ID.TryGetNumericID<int>(out var tmdbID) && tmdbID > 0
                    ? new TmdbHint(tmdbID, hint.ID.EntityType == MetadataEntityType.Movie, NamedBy: hint.NamedBy, AnidbEpisodeID: hint.AnidbEpisodeID)
                    : (TmdbHint?)null)
                .OfType<TmdbHint>()
                .DistinctBy(hint => (hint.TmdbID, hint.IsMovie)),
        ];

    /// <summary>
    ///   The TMDB entries a list of AniDB resources names.
    /// </summary>
    /// <param name="resources">The resources.</param>
    /// <returns>Each entry once, in AniDB's order.</returns>
    internal static IReadOnlyList<TmdbHint> TmdbHintsOf(IEnumerable<AniDB_Resource> resources)
        => [
            .. resources
                .Where(resource => resource.ResourceType is ResourceLinkType.TMDB)
                .Select(resource => resource.Identifiers is [var id, var kind, ..] &&
                    int.TryParse(id, NumberStyles.None, CultureInfo.InvariantCulture, out var tmdbID) && tmdbID > 0 && kind is "tv" or "movie"
                        ? new TmdbHint(tmdbID, kind is "movie")
                        : (TmdbHint?)null)
                .OfType<TmdbHint>()
                .Distinct(),
        ];

    /// <summary>
    ///   The IMDb titles a list of AniDB resources names.
    /// </summary>
    /// <param name="resources">The resources.</param>
    /// <returns>Each title ID once, in AniDB's order.</returns>
    internal static IReadOnlyList<string> ImdbIDsOf(IEnumerable<AniDB_Resource> resources)
        => [
            .. resources
                .Where(resource => resource.ResourceType is ResourceLinkType.IMDb)
                .Select(resource => resource.Identifiers.FirstOrDefault())
                .OfType<string>()
                .Where(id => id.Length > 2 && id.StartsWith("tt", StringComparison.Ordinal))
                .Distinct(StringComparer.Ordinal),
        ];

    /// <summary>
    ///   The kinds of TMDB entry a hint may be taken as for an anime: the
    ///   ones its search looks for.
    /// </summary>
    /// <param name="type">The anime's type.</param>
    /// <param name="isShortForm">Whether the anime has at most four regular episodes.</param>
    /// <returns>Whether a show, and whether a film, may be taken.</returns>
    internal static (bool Shows, bool Movies) HintKinds(AnimeType type, bool isShortForm) => type switch
    {
        AnimeType.MusicVideo => (false, false),
        AnimeType.Movie => (false, true),
        AnimeType.OVA or AnimeType.Web when isShortForm => (true, true),
        // Not searched, as they are hard to auto-map, so their hints are only listed.
        AnimeType.Other or AnimeType.Unknown => (false, false),
        _ => (true, false),
    };

    /// <summary>
    ///   Why a hinted entry is not taken, or <see langword="null"/> when it
    ///   is.
    /// </summary>
    /// <remarks>
    ///   A hint may be taken when it is of a kind the anime's search looks
    ///   for, what it would be linked to aired or airs within the search's
    ///   window, the matching engine did not filter it out, and a film can be
    ///   placed on one episode. Whether the search took something instead is
    ///   the core's to judge.
    /// </remarks>
    /// <param name="hint">The hint.</param>
    /// <param name="animeType">The anime's type.</param>
    /// <param name="kindAllowed">Whether the hint is of a kind the anime's search looks for.</param>
    /// <param name="rating">The matching engine's rating of it.</param>
    /// <param name="filter">The matching engine's reason for not taking it, if any.</param>
    /// <param name="details">What the matching engine compared, if it said.</param>
    /// <param name="unplaced">Whether it is a film no single episode of the anime could be told to stand for.</param>
    /// <param name="notAired">
    ///   Why what it would be linked to is not searched for yet, as a clause,
    ///   or <see langword="null"/> when it aired or airs within the search's
    ///   window.
    /// </param>
    /// <returns>The rejection, or <see langword="null"/>.</returns>
    internal static MetadataAutoLinkRejection? HintRejection(
        TmdbHint hint,
        AnimeType animeType,
        bool kindAllowed,
        MatchRating rating,
        MatchRejectionReason filter,
        string? details,
        bool unplaced,
        string? notAired = null
    )
    {
        var judged = $"Rated {rating}.{(string.IsNullOrEmpty(details) ? string.Empty : $" {details}")}";
        if (!kindAllowed)
            return new()
            {
                Reason = MatchRejectionReason.TypeMismatch,
                Details = $"{hint.Source}, and the anime is {animeType switch
                {
                    AnimeType.Movie => "a film",
                    AnimeType.MusicVideo => "a music video, which TMDB does not hold",
                    AnimeType.Other or AnimeType.Unknown => $"of type {animeType}, which is not auto-linked",
                    _ => "a series",
                }}. A hint of another kind is only listed. {judged}",
            };

        if (notAired is not null)
            return new()
            {
                Reason = MatchRejectionReason.Other,
                Details = $"{hint.Source}, but {notAired}, and nothing is auto-linked before the search would look for it. {judged}",
            };

        if (filter is MatchRejectionReason.Restricted or MatchRejectionReason.TypeMismatch)
            return new() { Reason = filter, Details = $"{hint.Source}. {details}" };

        if (unplaced)
            return new()
            {
                Reason = MatchRejectionReason.Other,
                Details = $"{hint.Source}, but neither titles nor dates told which of the anime's episodes the film stands for. {judged}",
            };

        return null;
    }

    /// <summary>
    ///   Why an anime, or the film one of its episodes stands for, is not
    ///   searched for yet, as a clause.
    /// </summary>
    /// <param name="airDate">When it airs, or <see langword="null"/> when that is not known.</param>
    /// <param name="isMovie">Whether it is a film standing for one episode.</param>
    /// <param name="now">The time now.</param>
    /// <param name="window">How far ahead of its air date the search looks for it.</param>
    /// <returns>The clause, or <see langword="null"/> when it is searched for.</returns>
    internal static string? NotAired(DateTime? airDate, bool isMovie, DateTime now, TimeSpan window)
    {
        if (AiredWithin(airDate, now, window))
            return null;

        var what = isMovie ? "the episode the film stands for" : "the anime";
        return airDate is { } date
            ? $"{what} airs on {date:yyyy-MM-dd}, more than {window.TotalDays:0} days from now"
            : $"{what} has no air date yet";
    }

    /// <summary>
    ///   The TMDB entries the anime's AniDB resources name, or, when they
    ///   name none, the ones TMDB finds for the IMDb titles they name, and
    ///   after them the ones the anime's links on other sources name.
    /// </summary>
    /// <param name="anime">The anime.</param>
    /// <returns>The hints, each entry once, as the first to name it named it.</returns>
    private async Task<IReadOnlyList<TmdbHint>> FindHints(AniDB_Anime anime)
    {
        var resources = _anidbResources.GetByAnimeID(anime.AnimeID);
        var found = new List<TmdbHint>(TmdbHintsOf(resources));
        if (found.Count is 0)
        {
            foreach (var imdbID in ImdbIDsOf(resources))
            {
                var result = await _tmdbService
                    .UseClient(c => c.FindAsync(FindExternalSource.Imdb, imdbID), $"Find the TMDB entries of IMDb title {imdbID}")
                    .ConfigureAwait(false);
                found.AddRange((result?.MovieResults ?? []).Select(movie => new TmdbHint(movie.Id, true, imdbID)));
                found.AddRange((result?.TvResults ?? []).Select(show => new TmdbHint(show.Id, false, imdbID)));
            }
        }

        found.AddRange(TmdbHintsOf(_linkingService.Value.GetCrossSourceHints(MetadataSource.TMDB, anime.AnimeID)));
        return [.. found.DistinctBy(hint => (hint.TmdbID, hint.IsMovie))];
    }

    /// <summary>
    ///   Judges each entry the anime's AniDB resources or its links on other
    ///   sources name, the one to take first leading.
    /// </summary>
    /// <remarks>
    ///   An entry not fetched by the search comes from storage or TMDB, and is
    ///   left out when neither has it. A takeable one keeps the engine's rating
    ///   (<see cref="MatchRating.FirstAvailable"/> when nothing agreed) and comes
    ///   first: a film before a show for a short-form anime, then the best
    ///   rated, then the order named, AniDB resources first.
    /// </remarks>
    /// <param name="anime">The anime.</param>
    /// <param name="hints">The hints.</param>
    /// <param name="searched">What the search scored.</param>
    /// <returns>
    ///   One candidate per hint, as <see cref="MetadataAutoLinkOrigin.AnidbResource"/>
    ///   or <see cref="MetadataAutoLinkOrigin.CrossSourceLink"/>.
    /// </returns>
    private async Task<IReadOnlyList<TmdbAutoSearchResult>> JudgeHints(AniDB_Anime anime, IReadOnlyList<TmdbHint> hints, IReadOnlyList<TmdbAutoSearchResult> searched)
    {
        var (shows, movies) = HintKinds(anime.AnimeType, IsShortFormAnime(anime));
        var results = new List<TmdbAutoSearchResult>();
        foreach (var hint in hints)
        {
            var judged = hint.IsMovie
                ? await JudgeMovieHint(anime, hint, searched).ConfigureAwait(false)
                : await JudgeShowHint(anime, hint, searched).ConfigureAwait(false);
            if (judged is not { } verdict)
            {
                _logger.LogDebug("{Hint} for AniDB anime {AnimeID}, which could not be had from TMDB.", hint.Source, anime.AnimeID);
                continue;
            }

            var result = verdict.Result;
            result.Rejection = HintRejection(
                hint,
                anime.AnimeType,
                hint.IsMovie ? movies : shows,
                result.MatchRating,
                verdict.Filter,
                verdict.Details,
                verdict.Unplaced,
                NotAired(hint.IsMovie ? FilmAirDate(anime, result.AnidbEpisode!) : ShowAirDate(anime), hint.IsMovie, DateTime.Now, _maxDaysIntoTheFuture)
            );
            if (result.Rejection is null)
            {
                result.MatchRating = result.MatchRating is MatchRating.None ? MatchRating.FirstAvailable : result.MatchRating;
                _logger.LogDebug("{Hint} for AniDB anime {AnimeID}, which may be taken, rated {Rating}.", hint.Source, anime.AnimeID, result.MatchRating);
            }

            results.Add(result);
        }

        return HintsInOrder(results, filmsFirst: shows && movies);
    }

    /// <summary>
    ///   Puts the hints that may be taken first, in the search's own order,
    ///   and the rest after them as they came.
    /// </summary>
    /// <param name="results">The judged hints, in the order they were named, the AniDB resources' first.</param>
    /// <param name="filmsFirst">Whether a film goes before a show, as for a short-form anime.</param>
    /// <returns>The hints, the one to take first leading.</returns>
    internal static IReadOnlyList<TmdbAutoSearchResult> HintsInOrder(IReadOnlyList<TmdbAutoSearchResult> results, bool filmsFirst)
        => [
            .. results
                .OrderBy(result => result.Rejection is null ? 0 : 1)
                .ThenBy(result => result.Rejection is null && filmsFirst && !result.IsMovie ? 1 : 0)
                .ThenBy(result => result.Rejection is null ? ShowMatchPriority(result.MatchRating) : 0),
        ];

    /// <summary>
    ///   A hinted show, judged by the matching engine against every title of
    ///   the anime.
    /// </summary>
    /// <param name="anime">The anime.</param>
    /// <param name="hint">The hint.</param>
    /// <param name="searched">What the search scored.</param>
    /// <returns>The candidate and the engine's verdict, or <see langword="null"/> when the show cannot be had.</returns>
    private async Task<(TmdbAutoSearchResult Result, MatchRejectionReason Filter, string? Details, bool Unplaced)?> JudgeShowHint(
        AniDB_Anime anime,
        TmdbHint hint,
        IReadOnlyList<TmdbAutoSearchResult> searched
    )
    {
        SearchTv raw;
        MetadataSeriesSearchResult candidate;
        bool isLocal;
        var options = new SeriesMatchOptions { IncludeRestricted = anime.IsRestricted, HintedIDs = [hint.ID] };
        SeriesMatch judged;
        if (searched.FirstOrDefault(result => result is { IsMovie: false, Candidate: MetadataSeriesSearchResult } && result.TmdbShow.ID == hint.TmdbID) is { } found)
        {
            (raw, candidate, isLocal) = (found.TmdbShowRaw!, (MetadataSeriesSearchResult)found.Candidate!, found.IsLocal);
            judged = _matchingEngine.MatchSeries(anime, [candidate], options)[0];
        }
        else
        {
            if (_tmdbShows.GetByTmdbShowID(hint.TmdbID) is { } stored)
                (raw, candidate, isLocal) = (RawShow(stored), WithStoredEpisodes(TmdbMetadataProvider.ToSearchResult(stored)), true);
            else if (await FetchShow(hint.TmdbID, "a hint").ConfigureAwait(false) is { } remote)
                (raw, candidate, isLocal) = (RawShow(remote), TmdbMetadataProvider.ToSearchResult(remote), false);
            else
                return null;

            // Judged as the search judges a show it fetched, its season
            // holding the anime's start fetched with its episodes.
            var shown = candidate;
            var picked = await PickShow(
                _matchingEngine,
                anime,
                options,
                [hint.TmdbID],
                _ => Task.FromResult<MetadataSeriesSearchResult?>(shown),
                FetchSeasonEpisodes,
                alignedCandidates: 1
            ).ConfigureAwait(false);
            judged = picked[0];
            candidate = judged.Candidate;
        }

        var result = new TmdbAutoSearchResult(anime, raw, judged.Rating)
        {
            IsLocal = isLocal,
            IsRemote = !isLocal,
            Candidate = candidate,
            Origin = hint.Origin,
        };
        return (result, judged.Rejection, judged.Details, false);
    }

    /// <summary>
    ///   A hinted film, judged by the matching engine against each regular
    ///   episode of the anime, and placed on the one it matches best.
    /// </summary>
    /// <remarks>
    ///   A film the search took stays on its episode. One it fetched but
    ///   turned down is judged afresh against every episode it may stand for.
    ///   Where several episodes match alike, the one aired within three days
    ///   of a release of the film takes it when no other is, since the
    ///   matching engine rates a film's date by its year alone. Otherwise,
    ///   and where none matches, the film cannot be placed.
    /// </remarks>
    /// <param name="anime">The anime.</param>
    /// <param name="hint">The hint.</param>
    /// <param name="searched">What the search scored.</param>
    /// <returns>The candidate and the engine's verdict, or <see langword="null"/> when the film cannot be had or the anime has no episode.</returns>
    private async Task<(TmdbAutoSearchResult Result, MatchRejectionReason Filter, string? Details, bool Unplaced)?> JudgeMovieHint(
        AniDB_Anime anime,
        TmdbHint hint,
        IReadOnlyList<TmdbAutoSearchResult> searched
    )
    {
        SearchMovie raw;
        MetadataMovieSearchResult candidate;
        bool isLocal;
        IReadOnlyList<AniDB_Episode> episodes;
        var fetched = searched.Where(result => result is { IsMovie: true, Candidate: MetadataMovieSearchResult } && result.TmdbMovie.ID == hint.TmdbID).ToList();
        if (fetched.FirstOrDefault(result => result.Rejection is null) is { } took)
            (raw, candidate, isLocal, episodes) = (took.TmdbMovieRaw!, (MetadataMovieSearchResult)took.Candidate!, took.IsLocal, [took.AnidbEpisode!]);
        else if (fetched.FirstOrDefault() is { } found)
            (raw, candidate, isLocal, episodes) = (found.TmdbMovieRaw!, (MetadataMovieSearchResult)found.Candidate!, found.IsLocal, FilmEpisodes(anime, hint));
        else if (_tmdbMovies.GetByTmdbMovieID(hint.TmdbID) is { } stored)
            (raw, candidate, isLocal, episodes) = (RawMovie(stored), TmdbMetadataProvider.ToSearchResult(stored), true, FilmEpisodes(anime, hint));
        else if (await FetchMovie(hint.TmdbID, "a hint").ConfigureAwait(false) is { } remote)
            (raw, candidate, isLocal, episodes) = (RawMovie(remote), TmdbMetadataProvider.ToSearchResult(remote), false, FilmEpisodes(anime, hint));
        else
            return null;

        if (episodes.Count is 0)
            return null;

        var options = new MovieMatchOptions { IncludeRestricted = anime.IsRestricted, HintedIDs = [hint.ID] };
        var judged = episodes
            .Select(episode => (Episode: episode, Match: _matchingEngine.MatchMovies(anime, episode, [candidate], options)[0]))
            .ToList();
        var releasedOn = candidate.ReleasedAt is { IsComplete: true } released
            ? [released.ToDateOnly(), .. candidate.OtherReleaseDates]
            : candidate.OtherReleaseDates;
        var (index, unplaced) = PlaceFilm([.. judged.Select(pair => (pair.Match.Rating, AiredOn(pair.Episode)))], releasedOn);
        var best = judged[index];
        var result = new TmdbAutoSearchResult(anime, best.Episode, raw, best.Match.Rating)
        {
            IsLocal = isLocal,
            IsRemote = !isLocal,
            Candidate = candidate,
            Origin = hint.Origin,
        };
        return (result, best.Match.Rejection, best.Match.Details, unplaced);
    }

    /// <summary>
    ///   Which of the episodes a hinted film was judged against it stands for:
    ///   the best rated, or, where several are rated alike, the only one of
    ///   them aired within three days of a release of the film.
    /// </summary>
    /// <param name="episodes">The engine's rating of the film against each episode, with the days the episode aired on, in order.</param>
    /// <param name="releasedOn">The days the film was released on.</param>
    /// <returns>The episode's place in <paramref name="episodes"/>, and whether the film could not be placed after all.</returns>
    /// <exception cref="ArgumentException"><paramref name="episodes"/> is empty.</exception>
    internal static (int Index, bool Unplaced) PlaceFilm(IReadOnlyList<(MatchRating Rating, IReadOnlyList<DateOnly> AiredOn)> episodes, IReadOnlyList<DateOnly> releasedOn)
    {
        if (episodes.Count is 0)
            throw new ArgumentException("A film needs an episode to be placed on.", nameof(episodes));

        var priority = episodes.Min(episode => ShowMatchPriority(episode.Rating));
        var tied = Enumerable.Range(0, episodes.Count).Where(index => ShowMatchPriority(episodes[index].Rating) == priority).ToList();
        if (episodes.Count is 1 || (tied.Count is 1 && episodes[tied[0]].Rating is not MatchRating.None))
            return (tied[0], false);
        if (episodes[tied[0]].Rating is MatchRating.None)
            return (tied[0], true);

        // The engine rates a film's date by its year, so same-year episodes tie;
        // the one aired with the film settles it.
        var close = tied
            .Where(index => episodes[index].AiredOn.Any(aired => releasedOn.Any(released => Math.Abs(aired.DayNumber - released.DayNumber) <= FilmPlacementDays)))
            .ToList();
        return close.Count is 1 ? (close[0], false) : (tied[0], true);
    }

    /// <summary>
    ///   How many days apart an episode and a film's release may be for the
    ///   film to be placed on the episode by date, as for a show's first
    ///   episode.
    /// </summary>
    private const int FilmPlacementDays = 3;

    /// <summary>
    ///   The days an episode aired on: its stored air date and its regular
    ///   broadcast, once each.
    /// </summary>
    /// <param name="episode">The episode.</param>
    /// <returns>The days, or none when it is not dated.</returns>
    private static IReadOnlyList<DateOnly> AiredOn(AniDB_Episode episode)
        => [.. new[] { episode.GetAirDateAsDateOnly(), episode.RegularAirDate }.OfType<DateOnly>().Distinct()];

    /// <summary>
    ///   The episodes of an anime a hinted film may stand for: the one it was
    ///   named for, when it was named for one of the anime's, or else those a
    ///   film named for the whole anime may stand for.
    /// </summary>
    /// <param name="anime">The anime.</param>
    /// <param name="hint">The hint.</param>
    /// <returns>The episodes, in order.</returns>
    private static IReadOnlyList<AniDB_Episode> FilmEpisodes(AniDB_Anime anime, TmdbHint hint)
        => hint.AnidbEpisodeID is { } episodeID && anime.AniDBEpisodes.FirstOrDefault(episode => episode.EpisodeID == episodeID) is { } named
            ? [named]
            : FilmEpisodes(anime);

    /// <summary>
    ///   The episodes of an anime a film named for the whole anime may stand
    ///   for: its regular episodes, or its specials and other episodes when
    ///   it has none.
    /// </summary>
    /// <param name="anime">The anime.</param>
    /// <returns>The episodes, in order.</returns>
    private static IReadOnlyList<AniDB_Episode> FilmEpisodes(AniDB_Anime anime)
    {
        var episodes = anime.AniDBEpisodes;
        var regular = episodes.Where(episode => episode.EpisodeType is EpisodeType.Episode).OrderBy(episode => episode.EpisodeNumber).ToList();
        return regular.Count > 0
            ? regular
            : [.. episodes.Where(episode => episode.EpisodeType is EpisodeType.Special or EpisodeType.Other).OrderBy(episode => episode.EpisodeType).ThenBy(episode => episode.EpisodeNumber)];
    }

    /// <summary>
    ///   The identities of the hinted entries of one kind.
    /// </summary>
    /// <param name="hints">The hints.</param>
    /// <param name="isMovie">Whether to give the films rather than the shows.</param>
    /// <returns>The identities.</returns>
    private static IReadOnlyCollection<MetadataGuid> HintedIDs(IReadOnlyList<TmdbHint> hints, bool isMovie)
        => [.. hints.Where(hint => hint.IsMovie == isMovie).Select(hint => hint.ID)];

    /// <summary>
    ///   The candidates a search offered, the hinted ones first, so the
    ///   fetching stops early at none of them before they are judged.
    /// </summary>
    /// <param name="candidateIDs">The candidates, in the search's order.</param>
    /// <param name="hints">The hints.</param>
    /// <param name="isMovie">Whether the candidates are films rather than shows.</param>
    /// <returns>The candidates, hinted first, otherwise in the same order.</returns>
    internal static IReadOnlyList<int> HintedFirst(IEnumerable<int> candidateIDs, IReadOnlyList<TmdbHint> hints, bool isMovie)
        => [.. candidateIDs.OrderBy(id => hints.Any(hint => hint.IsMovie == isMovie && hint.TmdbID == id) ? 0 : 1)];

    /// <summary>
    ///   A show the search found, as <see cref="OwnTitleWins"/> weighs it.
    /// </summary>
    /// <param name="result">The show.</param>
    /// <param name="hints">The hints.</param>
    /// <returns>Its rating, start, episode count, and whether it is hinted.</returns>
    private static FoundShow Found(TmdbAutoSearchResult result, IReadOnlyList<TmdbHint> hints)
        => FoundShow.Of(result) with { IsHinted = hints.Any(hint => !hint.IsMovie && hint.TmdbID == result.TmdbShow!.ID) };

    /// <summary>
    ///   A clause saying a hint names a taken show, or nothing when none
    ///   does.
    /// </summary>
    /// <param name="result">The show taken.</param>
    /// <param name="hints">The hints.</param>
    /// <returns>The clause, or an empty string.</returns>
    private static string HintNote(TmdbAutoSearchResult result, IReadOnlyList<TmdbHint> hints)
        => hints.FirstOrDefault(hint => !hint.IsMovie && hint.TmdbID == result.TmdbShow!.ID) is { TmdbID: > 0 } hint ? $", {hint.Note}" : string.Empty;

    /// <summary>
    ///   Fetches a show whole, with its translations and seasons.
    /// </summary>
    /// <param name="showID">The TMDB show ID.</param>
    /// <param name="purpose">What it is fetched for, for the logs.</param>
    /// <returns>The show, or <see langword="null"/> when TMDB has none.</returns>
    private Task<TvShow?> FetchShow(int showID, string purpose)
        => _tmdbService.UseClient(c => c.GetTvShowAsync(showID, TvShowMethods.Translations), $"Fetch candidate show {showID} for {purpose}");

    /// <summary>
    ///   Fetches a film whole, with its translations and release dates.
    /// </summary>
    /// <param name="movieID">The TMDB movie ID.</param>
    /// <param name="purpose">What it is fetched for, for the logs.</param>
    /// <returns>The film, or <see langword="null"/> when TMDB has none.</returns>
    private Task<Movie?> FetchMovie(int movieID, string purpose)
        => _tmdbService.UseClient(c => c.GetMovieAsync(movieID, "en-US", null, MovieMethods.Translations | MovieMethods.ReleaseDates), $"Fetch candidate movie {movieID} for {purpose}");

    /// <summary>
    ///   A stored show, the way TMDB's search offers one.
    /// </summary>
    /// <param name="show">The show.</param>
    /// <returns>The search result.</returns>
    private static SearchTv RawShow(TMDB_Show show)
        => new()
        {
            Id = show.Id,
            OriginalName = show.OriginalTitle,
            Name = show.EnglishTitle,
            FirstAirDate = show.FirstAiredAt?.ToDateTime(),
            BackdropPath = show.BackdropPath,
            GenreIds = [],
            MediaType = MediaType.Tv,
            OriginalLanguage = show.OriginalLanguageCode,
            Overview = show.EnglishOverview,
            PosterPath = show.PosterPath,
            Popularity = show.UserRating,
            VoteAverage = show.UserRating,
            VoteCount = show.UserVotes,
        };

    /// <summary>
    ///   A show as TMDB answered it, the way TMDB's search offers one.
    /// </summary>
    /// <param name="show">The show.</param>
    /// <returns>The search result.</returns>
    private static SearchTv RawShow(TvShow show)
        => new()
        {
            Id = show.Id,
            OriginalName = show.OriginalName,
            Name = show.Name,
            FirstAirDate = show.FirstAirDate,
            BackdropPath = show.BackdropPath,
            GenreIds = [.. (show.Genres ?? []).Select(genre => genre.Id)],
            MediaType = MediaType.Tv,
            OriginalLanguage = show.OriginalLanguage,
            OriginCountry = show.OriginCountry,
            Overview = show.Overview,
            PosterPath = show.PosterPath,
            Popularity = show.Popularity,
            VoteAverage = show.VoteAverage,
            VoteCount = show.VoteCount,
        };

    /// <summary>
    ///   A stored film, the way TMDB's search offers one.
    /// </summary>
    /// <param name="movie">The film.</param>
    /// <returns>The search result.</returns>
    private static SearchMovie RawMovie(TMDB_Movie movie)
        => new()
        {
            Id = movie.TmdbMovieID,
            OriginalTitle = movie.OriginalTitle,
            Title = movie.EnglishTitle,
            ReleaseDate = movie.ReleasedAt?.ToDateTime(TimeOnly.MinValue),
            Adult = movie.IsRestricted,
            Video = movie.IsVideo,
            BackdropPath = movie.BackdropPath,
            GenreIds = [],
            MediaType = MediaType.Movie,
            OriginalLanguage = movie.OriginalLanguageCode,
            Overview = movie.EnglishOverview,
            PosterPath = movie.PosterPath,
            Popularity = movie.UserRating,
            VoteAverage = movie.UserRating,
            VoteCount = movie.UserVotes,
        };

    /// <summary>
    ///   A film as TMDB answered it, the way TMDB's search offers one.
    /// </summary>
    /// <param name="movie">The film.</param>
    /// <returns>The search result.</returns>
    private static SearchMovie RawMovie(Movie movie)
        => new()
        {
            Id = movie.Id,
            OriginalTitle = movie.OriginalTitle,
            Title = movie.Title,
            ReleaseDate = movie.ReleaseDate,
            Adult = movie.Adult,
            Video = movie.Video,
            BackdropPath = movie.BackdropPath,
            GenreIds = [.. (movie.Genres ?? []).Select(genre => genre.Id)],
            MediaType = MediaType.Movie,
            OriginalLanguage = movie.OriginalLanguage,
            Overview = movie.Overview,
            PosterPath = movie.PosterPath,
            Popularity = movie.Popularity ?? 0,
            VoteAverage = movie.VoteAverage,
            VoteCount = movie.VoteCount,
        };

    #endregion

    #region Helpers

    /// <summary>
    ///   The engine's reason for turning a candidate down, as a rejection.
    /// </summary>
    /// <param name="reason">The engine's reason.</param>
    /// <param name="query">The title searched for.</param>
    /// <param name="details">What the engine compared, if it said.</param>
    /// <returns>The rejection, or <see langword="null"/> for the candidate taken.</returns>
    internal static MetadataAutoLinkRejection? Rejected(MatchRejectionReason reason, string query, string? details)
        => reason is MatchRejectionReason.None
            ? null
            : new() { Reason = reason, Details = string.IsNullOrEmpty(details) ? $"Searched for \"{query}\"." : $"Searched for \"{query}\". {details}" };

    /// <summary>
    ///   A show as the auto-search names it in a rejection.
    /// </summary>
    /// <param name="result">The show.</param>
    /// <returns>Its title and ID.</returns>
    private static string Named(TmdbAutoSearchResult result)
        => $"\"{result.TmdbShow!.Title}\" ({result.TmdbShow.ID})";

    /// <summary>
    ///   How many days apart a show found through the anime's own titles and
    ///   the anime may begin for the show to stand as the anime's own on its
    ///   date alone.
    /// </summary>
    private const int MaxDaysOwnShowStartApart = 3;

    /// <summary>
    ///   How many days before the anime a show found through its prequel's
    ///   titles must have begun for its title alone to lose to a show begun
    ///   with the anime.
    /// </summary>
    private const int MinDaysPrequelShowStartedBefore = 365;

    /// <summary>
    ///   How many times a show's episodes an anime may have for the show to
    ///   hold it on a title that only loosely agrees.
    /// </summary>
    private const int MaxEpisodeRatioForLooseTitle = 3;

    /// <summary>
    ///   A show found for an anime, as <see cref="OwnTitleWins"/> weighs it.
    /// </summary>
    /// <param name="Rating">How well it matched the anime.</param>
    /// <param name="BeganOn">When it began, if known.</param>
    /// <param name="EpisodeCount">How many regular episodes it has, if known.</param>
    /// <param name="IsHinted">Whether the anime's AniDB resources name it.</param>
    internal readonly record struct FoundShow(MatchRating Rating, DateOnly? BeganOn, int? EpisodeCount, bool IsHinted = false)
    {
        /// <summary>
        ///   The show a search result stands for.
        /// </summary>
        /// <param name="result">The search result.</param>
        /// <returns>Its rating, start and episode count.</returns>
        public static FoundShow Of(TmdbAutoSearchResult result)
            => result.Candidate is MetadataSeriesSearchResult candidate
                ? Of(result.MatchRating, candidate)
                : new(result.MatchRating, result.TmdbShowRaw?.FirstAirDate is { } firstAired ? DateOnly.FromDateTime(firstAired) : null, null);

        /// <summary>
        ///   The show a candidate the engine rated stands for.
        /// </summary>
        /// <param name="rating">How well it matched the anime.</param>
        /// <param name="candidate">The candidate.</param>
        /// <returns>Its rating, start and episode count.</returns>
        public static FoundShow Of(MatchRating rating, MetadataSeriesSearchResult candidate)
            => new(rating, candidate.FirstAiredAt is { IsComplete: true } began ? began.ToDateOnly() : null, candidate.EpisodeCount);
    }

    /// <summary>
    ///   Whether a show found through the anime's own titles wins over one
    ///   found through its prequel's.
    /// </summary>
    /// <remarks>
    ///   Rated higher, it wins. Rated alike, the one the anime's AniDB
    ///   resources name wins. Against a prequel's show agreeing on the title
    ///   alone, it also wins when it agrees on the date and the title, unless
    ///   its title only loosely agrees and it has under a third of the anime's
    ///   episodes, or on the date alone when it began with the anime and the
    ///   prequel's show over a year before.
    /// </remarks>
    /// <param name="own">The show found through the anime's own titles.</param>
    /// <param name="prequels">The show found through the prequel's titles.</param>
    /// <param name="airedOn">When the anime began.</param>
    /// <param name="episodeCount">How many regular episodes the anime has.</param>
    /// <returns><see langword="true"/> when the anime's own wins.</returns>
    internal static bool OwnTitleWins(FoundShow own, FoundShow prequels, DateOnly airedOn, int episodeCount)
    {
        if (ShowMatchPriority(own.Rating) < ShowMatchPriority(prequels.Rating))
            return true;

        if (ShowMatchPriority(own.Rating) == ShowMatchPriority(prequels.Rating) && own.IsHinted != prequels.IsHinted)
            return own.IsHinted;

        if (prequels.Rating is not (MatchRating.TitleMatches or MatchRating.TitleKindaMatches))
            return false;

        return own.Rating switch
        {
            MatchRating.DateAndTitleMatches => true,
            MatchRating.DateAndTitleKindaMatches => own.EpisodeCount is not { } count || count * MaxEpisodeRatioForLooseTitle >= episodeCount,
            MatchRating.DateMatches => own.BeganOn is { } ownBegan && Math.Abs(ownBegan.DayNumber - airedOn.DayNumber) <= MaxDaysOwnShowStartApart &&
                prequels.BeganOn is { } prequelBegan && airedOn.DayNumber - prequelBegan.DayNumber > MinDaysPrequelShowStartedBefore,
            _ => false,
        };
    }

    private static int ShowMatchPriority(MatchRating r) => r switch
    {
        MatchRating.UserVerified => 0,
        MatchRating.DateAndTitleMatches => 1,
        MatchRating.TitleMatches => 2,
        MatchRating.DateAndTitleKindaMatches => 3,
        MatchRating.DateMatches => 4,
        MatchRating.TitleKindaMatches => 5,
        _ => 6,
    };

    private static void CollectCandidates(List<SearchTv> candidates, List<SearchTv> results, HashSet<int> seen, int candidateCount)
    {
        foreach (var result in results)
        {
            if (candidates.Count >= candidateCount) break;
            if (!seen.Add(result.Id)) continue;
            if (!result.GetGenres().Contains(AnimationGenre, StringComparer.OrdinalIgnoreCase)) continue;
            candidates.Add(result);
        }
    }

    private static void CollectMovieCandidates(List<SearchMovie> candidates, List<SearchMovie> results, HashSet<int> seen, int candidateCount)
    {
        foreach (var result in results)
        {
            if (candidates.Count >= candidateCount) break;
            if (!seen.Add(result.Id)) continue;
            if (!result.GetGenres().Contains(AnimationGenre, StringComparer.OrdinalIgnoreCase)) continue;
            candidates.Add(result);
        }
    }

    #endregion
}
