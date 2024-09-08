
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Shoko.Commons.Extensions;
using Shoko.Plugin.Abstractions.DataModels;
using Shoko.Server.Models;
using TMDbLib.Objects.Search;

#nullable enable
namespace Shoko.Server.Providers.TMDB;

public partial class TmdbSearchService
{

    /// <summary>
    /// This regex might save the day if the local database doesn't contain any prequel metadata, but the title itself contains a suffix that indicates it's a sequel of sorts.
    /// </summary>
    [GeneratedRegex(@"\(\d{4}\)$|\bs(?:eason)? (?:\d+|(?=[MDCLXVI])M*(?:C[MD]|D?C{0,3})(X[CL]|L?X{0,3})(I[XV]|V?I{0,3}))$|\bs\d+$|第(零〇一二三四五六七八九十百千萬億兆京垓點)+季$|\b(?:second|2nd|third|3rd|fourth|4th|fifth|5th|sixth|6th) season$", RegexOptions.IgnoreCase | RegexOptions.Compiled | RegexOptions.Multiline)]
    private partial Regex SequelSuffixRemovalRegex();

    private readonly ILogger<TmdbSearchService> _logger;

    private readonly TmdbMetadataService _tmdbService;

    /// <summary>
    /// Max days into the future to search for matches against.
    /// </summary>
    private readonly TimeSpan _maxDaysIntoTheFuture = TimeSpan.FromDays(15);

    public TmdbSearchService(ILogger<TmdbSearchService> logger, TmdbMetadataService tmdbService)
    {
        _logger = logger;
        _tmdbService = tmdbService;
    }

    public async Task<IReadOnlyList<TmdbAutoSearchResult>> SearchForAutoMatch(SVR_AniDB_Anime anime)
    {
        if (anime.AnimeType == (int)AnimeType.Movie)
        {
            return await AutoSearchForMovies(anime).ConfigureAwait(false);
        }

        return await AutoSearchForShow(anime).ConfigureAwait(false);
    }

    #region Movie

    public async Task<(List<SearchMovie> Page, int TotalCount)> SearchMovies(string query, bool includeRestricted = false, int year = 0, int page = 1, int pageSize = 6)
    {
        var results = new List<SearchMovie>();
        var firstPage = await _tmdbService.UseClient(c => c.SearchMovieAsync(query, 1, includeRestricted, year), $"Searching{(includeRestricted ? " all" : string.Empty)} movies for \"{query}\"{(year > 0 ? $" at year {year}" : string.Empty)}").ConfigureAwait(false);
        var total = firstPage.TotalResults;
        if (total == 0)
            return (results, total);

        var lastPage = firstPage.TotalPages;
        var actualPageSize = firstPage.Results.Count;
        var startIndex = (page - 1) * pageSize;
        var startPage = (int)Math.Floor((decimal)startIndex / actualPageSize) + 1;
        var endIndex = Math.Min(startIndex + pageSize, total);
        var endPage = total == endIndex ? lastPage : Math.Min((int)Math.Floor((decimal)endIndex / actualPageSize) + (endIndex % actualPageSize > 0 ? 1 : 0), lastPage);
        for (var i = startPage; i <= endPage; i++)
        {
            var actualPage = await _tmdbService.UseClient(c => c.SearchMovieAsync(query, i, includeRestricted, year), $"Searching{(includeRestricted ? " all" : string.Empty)} movies for \"{query}\"{(year > 0 ? $" at year {year}" : string.Empty)}").ConfigureAwait(false);
            results.AddRange(actualPage.Results);
        }

        var skipCount = startIndex - (startPage - 1) * actualPageSize;
        var pagedResults = results.Skip(skipCount).Take(pageSize).ToList();

        _logger.LogTrace(
            "Got {Count} movies from {Results} total movies at {IndexRange} across {PageRange}.",
            pagedResults.Count,
            total,
            startIndex == endIndex ? $"index {startIndex}" : $"indexes {startIndex}-{endIndex}",
            startPage == endPage ? $"{startPage} actual page" : $"{startPage}-{endPage} actual pages"
        );

        return (pagedResults, total);
    }

    private async Task<IReadOnlyList<TmdbAutoSearchResult>> AutoSearchForMovies(SVR_AniDB_Anime anime)
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
            .Where(episode => episode.EpisodeType == (int)EpisodeType.Episode || episode.EpisodeType == (int)EpisodeType.Special)
            .OrderBy(episode => episode.EpisodeType)
            .ThenBy(episode => episode.EpisodeNumber)
            .ToList();

        // We only have one movie in the movie collection, so don't search for
        // a sub-title.
        var now = DateTime.Now;
        if (episodes.Count is 1)
        {
            // Abort if the movie have not aired within the _maxDaysIntoTheFuture limit.
            var airDate = anime.AirDate ?? episodes[0].GetAirDateAsDate() ?? null;
            if (!airDate.HasValue || (airDate.Value > now && airDate.Value - now > _maxDaysIntoTheFuture))
                return [];
            await AutoSearchForMovie(list, anime, episodes[0], officialTitle, englishTitle, title, airDate.Value.Year, anime.Restricted == 1).ConfigureAwait(false);
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
                var airDateForAnime = anime.AirDate ?? episodes[0].GetAirDateAsDate() ?? null;
                if (!airDateForAnime.HasValue || (airDateForAnime.Value > now && airDateForAnime.Value - now > _maxDaysIntoTheFuture))
                    continue;
                await AutoSearchForMovie(list, anime, episode, officialTitle, englishTitle, title, airDateForAnime.Value.Year, anime.Restricted == 1).ConfigureAwait(false);
                continue;
            }

            var airDateForEpisode = episode.GetAirDateAsDate() ?? anime.AirDate ?? null;
            if (!airDateForEpisode.HasValue || (airDateForEpisode.Value > now && airDateForEpisode.Value - now > _maxDaysIntoTheFuture))
                continue;

            var officialSubTitle = allEpisodeTitles.FirstOrDefault(title => title.Language == language)?.Title ??
                allEpisodeTitles.FirstOrDefault(title => title.Language == mainTitle.Language)?.Title;
            var officialFullTitle = !string.IsNullOrEmpty(officialSubTitle)
                ? $"{officialTitle} {officialSubTitle}" : null;
            var englishSubTitle = allEpisodeTitles.FirstOrDefault(title => title.Language == TitleLanguage.English && !string.Equals(title.Title, $"Episode {episode.EpisodeNumber}", StringComparison.InvariantCultureIgnoreCase))?.Title;
            var englishFullTitle = !string.IsNullOrEmpty(englishSubTitle)
                ? $"{englishTitle} {englishSubTitle}" : null;
            var mainFullTitle = !string.IsNullOrEmpty(englishSubTitle)
                ? $"{title} {englishSubTitle}" : null;

            // ~~Stolen~~ _Borrowed_ from the Shokofin code-base since we don't want to try linking extras to movies.
            if (episode.AbstractEpisodeType is EpisodeType.Special or EpisodeType.Other && !string.IsNullOrEmpty(englishSubTitle))
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

            await AutoSearchForMovie(list, anime, episode, officialFullTitle, englishFullTitle, mainFullTitle, airDateForEpisode.Value.Year, anime.Restricted == 1).ConfigureAwait(false);
        }

        return list;
    }

    private async Task<bool> AutoSearchForMovie(List<TmdbAutoSearchResult> list, SVR_AniDB_Anime anime, SVR_AniDB_Episode episode, string? officialTitle, string? englishTitle, string? mainTitle, int year, bool isRestricted)
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

    private async Task<TmdbAutoSearchResult?> AutoSearchMovieUsingTitle(SVR_AniDB_Anime anime, SVR_AniDB_Episode episode, string query, bool includeRestricted = false, int year = 0)
    {
        // Brute force attempt #1: With the original title and earliest known aired year.
        var (results, totalCount) = await SearchMovies(query, includeRestricted: includeRestricted, year: year).ConfigureAwait(false);
        if (results.Count > 0)
        {
            _logger.LogTrace("Found {Count} movie results for search on {Query}, best match; {MovieName} ({ID})", totalCount, query, results[0].OriginalTitle, results[0].Id);

            return new(anime, episode, results[0]);
        }

        // Brute force attempt #2: With the original title but without the earliest known aired year.
        (results, totalCount) = await SearchMovies(query, includeRestricted: includeRestricted).ConfigureAwait(false);
        if (results.Count > 0)
        {
            _logger.LogTrace("Found {Count} movie results for search on {Query}, best match; {MovieName} ({ID})", totalCount, query, results[0].OriginalTitle, results[0].Id);

            return new(anime, episode, results[0]);
        }

        // Brute force attempt #3-4: Same as above, but after stripping the title of common "sequel endings"
        var strippedTitle = SequelSuffixRemovalRegex().Match(query) is { Success: true } regexResult
            ? query[..^regexResult.Length].TrimEnd() : null;
        if (!string.IsNullOrEmpty(strippedTitle))
        {
            (results, totalCount) = await SearchMovies(strippedTitle, includeRestricted: includeRestricted, year: year).ConfigureAwait(false);
            if (results.Count > 0)
            {
                _logger.LogTrace("Found {Count} movie results for search on {Query}, best match; {MovieName} ({ID})", totalCount, strippedTitle, results[0].OriginalTitle, results[0].Id);

                return new(anime, episode, results[0]);
            }
            (results, totalCount) = await SearchMovies(strippedTitle, includeRestricted: includeRestricted).ConfigureAwait(false);
            if (results.Count > 0)
            {
                _logger.LogTrace("Found {Count} movie results for search on {Query}, best match; {MovieName} ({ID})", totalCount, strippedTitle, results[0].OriginalTitle, results[0].Id);

                return new(anime, episode, results[0]);
            }
        }

        return null;
    }

    #endregion

    #region Show

    public async Task<(List<SearchTv> Page, int TotalCount)> SearchShows(string query, bool includeRestricted = false, int year = 0, int page = 1, int pageSize = 6)
    {
        var results = new List<SearchTv>();
        var firstPage = await _tmdbService.UseClient(c => c.SearchTvShowAsync(query, 1, includeRestricted, year), $"Searching{(includeRestricted ? " all" : "")} shows for \"{query}\"{(year > 0 ? $" at year {year}" : "")}").ConfigureAwait(false);
        var total = firstPage.TotalResults;
        if (total == 0)
            return (results, total);

        var lastPage = firstPage.TotalPages;
        var actualPageSize = firstPage.Results.Count;
        var startIndex = (page - 1) * pageSize;
        var startPage = (int)Math.Floor((decimal)startIndex / actualPageSize) + 1;
        var endIndex = Math.Min(startIndex + pageSize, total);
        var endPage = total == endIndex ? lastPage : Math.Min((int)Math.Floor((decimal)endIndex / actualPageSize) + (endIndex % actualPageSize > 0 ? 1 : 0), lastPage);
        for (var i = startPage; i <= endPage; i++)
        {
            var actualPage = await _tmdbService.UseClient(c => c.SearchTvShowAsync(query, i, includeRestricted, year), $"Searching{(includeRestricted ? " all" : "")} shows for \"{query}\"{(year > 0 ? $" at year {year}" : "")}").ConfigureAwait(false);
            results.AddRange(actualPage.Results);
        }

        var skipCount = startIndex - (startPage - 1) * actualPageSize;
        var pagedResults = results.Skip(skipCount).Take(pageSize).ToList();

        _logger.LogTrace(
            "Got {Count} shows from {Results} total shows at {IndexRange} across {PageRange}.",
            pagedResults.Count,
            total,
            startIndex == endIndex ? $"index {startIndex}" : $"indexes {startIndex}-{endIndex}",
            startPage == endPage ? $"{startPage} actual page" : $"{startPage}-{endPage} actual pages"
        );

        return (pagedResults, total);
    }

    private async Task<IReadOnlyList<TmdbAutoSearchResult>> AutoSearchForShow(SVR_AniDB_Anime anime)
    {
        // TODO: Improve this logic to take tmdb seasons into account, and maybe also take better anidb series relations into account in cases where the tmdb show name and anidb series name are too different.

        // Get the first or second episode to get the aired date if the anime is missing a date.
        var airDate = anime.AirDate;
        if (!airDate.HasValue)
        {
            airDate = anime.AniDBEpisodes
                .Where(episode => episode.EpisodeType == (int)EpisodeType.Episode)
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

        var series = anime as ISeries;
        var adjustedMainTitle = mainTitle.Title;
        var currentDate = airDate.Value;
        IReadOnlyList<IRelatedMetadata<ISeries>> currentRelations = anime.RelatedAnime;
        while (currentRelations.Count > 0)
        {
            foreach (var prequelRelation in currentRelations.Where(relation => relation.RelationType == RelationType.Prequel))
            {
                var prequelSeries = prequelRelation.Related;
                if (prequelSeries?.AirDate is not { } prequelDate || prequelDate > currentDate)
                    continue;

                series = prequelSeries;
                currentDate = prequelDate;
                currentRelations = prequelSeries.RelatedSeries;
                goto continuePrequelWhileLoop;
            }
            break;
continuePrequelWhileLoop:
            continue;
        }

        // First attempt the official title in the country of origin.
        var originalTitle = language == mainTitle.Language
            ? mainTitle.Title
            : (
                series.ID == anime.AnimeID
                    ? allTitles.FirstOrDefault(title => title.TitleType == TitleType.Official && title.Language == language)?.Title
                    : series.Titles.FirstOrDefault(title => title.Type == TitleType.Official && title.Language == language)?.Title
            );
        var match = !string.IsNullOrEmpty(originalTitle)
            ? await AutoSearchForShowUsingTitle(anime, originalTitle, airDate.Value, series.Restricted)
            : null;

        // And if that failed, then try the official english title.
        if (match is null)
        {
            var englishTitle = series.ID == anime.AnimeID
                ? allTitles.FirstOrDefault(l => l.TitleType == TitleType.Official && l.Language == TitleLanguage.English)?.Title
                : series.Titles.FirstOrDefault(l => l.Type == TitleType.Official && l.Language == TitleLanguage.English)?.Title;
            if (!string.IsNullOrEmpty(englishTitle) && (string.IsNullOrEmpty(originalTitle) || !string.Equals(englishTitle, originalTitle, StringComparison.Ordinal)))
                match = await AutoSearchForShowUsingTitle(anime, englishTitle, airDate.Value, series.Restricted);
        }

        // And the last ditch attempt will be to use the main title. We won't try other languages.
        match ??= await AutoSearchForShowUsingTitle(anime, mainTitle.Title, airDate.Value, series.Restricted);
        return match is not null ? [match] : [];
    }

    private async Task<TmdbAutoSearchResult?> AutoSearchForShowUsingTitle(SVR_AniDB_Anime anime, string title, DateTime airDate, bool restricted)
    {
        // Brute force attempt #1: With the original title and earliest known aired year.
        var (results, totalFound) = await SearchShows(title, includeRestricted: restricted, year: airDate.Year).ConfigureAwait(false);
        if (results.Count > 0)
        {
            _logger.LogTrace("Found {Count} results for search on {Query}, best match; {ShowName} ({ID})", totalFound, title, results[0].OriginalName, results[0].Id);

            return new(anime, results[0]);
        }

        // Brute force attempt #2: With the original title but without the earliest known aired year.
        (results, totalFound) = await SearchShows(title, includeRestricted: restricted).ConfigureAwait(false);
        if (totalFound > 0)
        {
            _logger.LogTrace("Found {Count} results for search on {Query}, best match; {ShowName} ({ID})", totalFound, title, results[0].OriginalName, results[0].Id);

            return new(anime, results[0]);
        }

        // Brute force attempt #3-4: Same as above, but after stripping the title of common "sequel endings"
        var strippedTitle = SequelSuffixRemovalRegex().Match(title) is { Success: true } regexResult
            ? title[..^regexResult.Length].TrimEnd() : null;
        if (!string.IsNullOrEmpty(strippedTitle))
        {
            (results, totalFound) = await SearchShows(strippedTitle, includeRestricted: restricted, year: airDate.Year).ConfigureAwait(false);
            if (results.Count > 0)
            {
                _logger.LogTrace("Found {Count} results for search on {Query}, best match; {ShowName} ({ID})", totalFound, strippedTitle, results[0].OriginalName, results[0].Id);

                return new(anime, results[0]);
            }
            (results, totalFound) = await SearchShows(strippedTitle, includeRestricted: restricted).ConfigureAwait(false);
            if (results.Count > 0)
            {
                _logger.LogTrace("Found {Count} results for search on {Query}, best match; {ShowName} ({ID})", totalFound, strippedTitle, results[0].OriginalName, results[0].Id);

                return new(anime, results[0]);
            }
        }

        return null;
    }

    #endregion
}
