using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Shoko.Commons.Extensions;
using Shoko.Plugin.Abstractions.DataModels;
using Shoko.Server.Models;
using Shoko.Server.Providers.TMDB;
using Shoko.Server.Repositories;
using Shoko.Server.Scheduling.Acquisition.Attributes;
using Shoko.Server.Scheduling.Attributes;
using Shoko.Server.Scheduling.Concurrency;

#pragma warning disable CS8618
namespace Shoko.Server.Scheduling.Jobs.TMDB;

[DatabaseRequired]
[NetworkRequired]
[DisallowConcurrencyGroup(ConcurrencyGroups.TMDB)]
[JobKeyGroup(JobKeyGroup.TMDB)]
public partial class SearchTmdbJob : BaseJob
{
    private readonly TmdbMetadataService _tmdbService;

    private string _animeTitle;

    public int AnimeID { get; set; }

    public virtual bool ForceRefresh { get; set; }

    public override void PostInit()
    {
        _animeTitle = RepoFactory.AniDB_Anime?.GetByAnimeID(AnimeID)?.PreferredTitle ?? AnimeID.ToString();
    }

    public override string TypeName => "Search for TMDB Entry";
    public override string Title => "Searching for TMDB Entry";
    public override Dictionary<string, object> Details => new() { { "Anime", _animeTitle } };

    public override async Task Process()
    {
        _logger.LogInformation("Processing SearchTmdbJob for {Anime}: AniDB ID {ID}", _animeTitle ?? AnimeID.ToString(), AnimeID);
        var anime = RepoFactory.AniDB_Anime.GetByAnimeID(AnimeID);
        if (anime == null)
            return;

        if (anime.AnimeType == (int)AnimeType.Movie)
        {
            await SearchForMovies(anime).ConfigureAwait(false);
            return;
        }

        await SearchForShow(anime).ConfigureAwait(false);
    }

    #region Movie

    private async Task SearchForMovies(SVR_AniDB_Anime anime)
    {
        // Find the official title in the origin language, to compare it against
        // the original language stored in the offline tmdb search dump.
        var allTitles = anime.Titles
            .Where(title => title.TitleType is TitleType.Main or TitleType.Official);
        var mainTitle = allTitles.FirstOrDefault(x => x.TitleType is TitleType.Main) ?? allTitles.FirstOrDefault();
        var language = mainTitle.Language switch
        {
            TitleLanguage.Romaji => TitleLanguage.Japanese,
            TitleLanguage.Pinyin => TitleLanguage.ChineseSimplified,
            TitleLanguage.KoreanTranscription => TitleLanguage.Korean,
            TitleLanguage.ThaiTranscription => TitleLanguage.Thai,
            _ => mainTitle.Language,
        };
        var officialTitle = language == mainTitle.Language ? mainTitle :
            allTitles.FirstOrDefault(title => title.Language == language) ?? mainTitle;

        // Try to establish a link for every movie (episode) in the movie
        // collection (anime).
        var episodes = anime.AniDBEpisodes
            .Where(episode => episode.EpisodeType == (int)EpisodeType.Episode || episode.EpisodeType == (int)EpisodeType.Special)
            .OrderBy(episode => episode.EpisodeType)
            .ThenBy(episode => episode.EpisodeNumber)
            .ToList();

        // We only have one movie in the movie collection, so don't search for
        // a sub-title.
        if (episodes.Count is 1)
        {
            // Abort if the movie have not aired yet.
            var airDate = anime.AirDate ?? episodes[0].GetAirDateAsDate() ?? null;
            if (!airDate.HasValue || airDate.Value > DateTime.Now)
                return;
            await SearchForMovie(episodes[0], officialTitle.Title, airDate.Value.Year, anime.Restricted == 1).ConfigureAwait(false);
            return;
        }

        // Find the sub title for each movie in the movie collection, then
        // search for a movie matching the combined title.
        foreach (var episode in episodes)
        {
            var allEpisodeTitles = RepoFactory.AniDB_Episode_Title.GetByEpisodeID(episode.EpisodeID);
            var isCompleteMovie = allEpisodeTitles.Any(title => title.Title.Contains("Complete Movie", StringComparison.InvariantCultureIgnoreCase));
            if (isCompleteMovie)
            {
                var airDateForAnime = anime.AirDate ?? episodes[0].GetAirDateAsDate() ?? null;
                if (!airDateForAnime.HasValue || airDateForAnime.Value > DateTime.Now)
                    continue;
                await SearchForMovie(episode, officialTitle.Title, airDateForAnime.Value.Year, anime.Restricted == 1).ConfigureAwait(false);
                continue;
            }

            var airDateForEpisode = episode.GetAirDateAsDate() ?? anime.AirDate ?? null;
            if (!airDateForEpisode.HasValue || airDateForEpisode.Value > DateTime.Now)
                continue;
            var subTitle = allEpisodeTitles.FirstOrDefault(title => title.Language == language) ??
                allEpisodeTitles.FirstOrDefault(title => title.Language == mainTitle.Language);
            if (subTitle == null)
                // TODO: Improve logic when no sub-title is found.
                continue;
            var query = $"{officialTitle.Title} {subTitle.Title}".TrimEnd();
            await SearchForMovie(episode, query, airDateForEpisode.Value.Year, anime.Restricted == 1).ConfigureAwait(false);
        }
    }

    private async Task<bool> SearchForMovie(SVR_AniDB_Episode episode, string query, int year, bool isRestricted)
    {
        var (results, _) = await _tmdbService.SearchMovies(query, includeRestricted: isRestricted, year: year).ConfigureAwait(false);
        if (results.Count == 0)
            return false;

        _logger.LogTrace("Found {Count} results for search on {Query} --- Linked to {MovieName} ({ID})", results.Count, query, results[0].OriginalTitle, results[0].Id);

        await _tmdbService.AddMovieLink(AnimeID, results[0].Id, episode.EpisodeID, additiveLink: true, isAutomatic: true).ConfigureAwait(false);
        await _tmdbService.ScheduleUpdateOfMovie(results[0].Id, forceRefresh: ForceRefresh, downloadImages: true).ConfigureAwait(false);

        return true;
    }

    #endregion

    #region Show

    /// <summary>
    /// This regex might save the day if the local database doesn't contain any prequel metadata, but the title itself contains a suffix that indicates it's a sequel of sorts.
    /// </summary>
    [GeneratedRegex(@"\(\d{4}\)$|\bs(?:eason)? (?:\d+|(?=[MDCLXVI])M*(?:C[MD]|D?C{0,3})(X[CL]|L?X{0,3})(I[XV]|V?I{0,3}))$|\bs\d+$|第(零〇一二三四五六七八九十百千萬億兆京垓點)+季$|\b(?:second|2nd|third|3rd|fourth|4th|fifth|5th|sixth|6th) season$", RegexOptions.IgnoreCase | RegexOptions.Compiled | RegexOptions.Multiline)]
    private partial Regex SequelSuffixRemovalRegex();

    private async Task SearchForShow(SVR_AniDB_Anime anime)
    {
        // TODO: Improve this logic to take tmdb seasons into account, and maybe also take better anidb series relations into account in cases where the tmdb show name and anidb series name are too different.

        // Find the official title in the origin language, to compare it against
        // the original language stored in the offline tmdb search dump.
        var allTitles = anime.Titles
            .Where(title => title.TitleType is TitleType.Main or TitleType.Official);
        var mainTitle = allTitles.FirstOrDefault(x => x.TitleType is TitleType.Main) ?? allTitles.FirstOrDefault();
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
        var currentDate = anime.AirDate.Value;
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
        // Get the first or second episode to get the aired date if the anime is missing a date.
        var airDate = series.AirDate;
        if (!airDate.HasValue)
        {
            airDate = series.EpisodeList
                .Where(episode => episode.Type == EpisodeType.Episode)
                .OrderBy(episode => episode.Type)
                .ThenBy(episode => episode.EpisodeNumber)
                .Take(2)
                .LastOrDefault()
                ?.AirDate;
        }

        // Abort if the show have not aired yet.
        if (!airDate.HasValue || airDate.Value > DateTime.Now)
            return;
        var officialTitle = language == mainTitle.Language
            ? mainTitle.Title
            : (
                series.ID == AnimeID
                    ? allTitles.FirstOrDefault(title => title.Language == language)?.Title
                    : series.Titles.FirstOrDefault(title => title.Language == language)?.Title
            ) ?? mainTitle.Title;

        // Brute force attempt #1: With the original title and earliest known aired year.
        var (results, totalFound) = await _tmdbService.SearchShows(officialTitle, includeRestricted: anime.Restricted == 1, year: airDate.Value.Year).ConfigureAwait(false);
        if (results.Count > 0)
        {
            _logger.LogTrace("Found {Count} results for search on {Query} --- Linked to {ShowName} ({ID})", totalFound, officialTitle, results[0].OriginalName, results[0].Id);

            await _tmdbService.AddShowLink(AnimeID, results[0].Id, additiveLink: true, isAutomatic: true).ConfigureAwait(false);
            await _tmdbService.ScheduleUpdateOfShow(results[0].Id, forceRefresh: ForceRefresh, downloadImages: true).ConfigureAwait(false);
            return;
        }

        // Brute force attempt #2: With the original title but without the earliest known aired year.
        (results, totalFound) = await _tmdbService.SearchShows(officialTitle, includeRestricted: series.Restricted).ConfigureAwait(false);
        if (totalFound > 0)
        {
            _logger.LogTrace("Found {Count} results for search on {Query} --- Linked to {ShowName} ({ID})", totalFound, officialTitle, results[0].OriginalName, results[0].Id);

            await _tmdbService.AddShowLink(AnimeID, results[0].Id, additiveLink: true, isAutomatic: true).ConfigureAwait(false);
            await _tmdbService.ScheduleUpdateOfShow(results[0].Id, forceRefresh: ForceRefresh, downloadImages: true).ConfigureAwait(false);
            return;
        }

        // Brute force attempt #3-4: Same as above, but after stripping the title of common "sequel endings"
        var strippedTitle = SequelSuffixRemovalRegex().Match(officialTitle) is { Success: true } regexResult
            ? officialTitle[..^regexResult.Length].TrimEnd() : null;
        if (!string.IsNullOrEmpty(strippedTitle))
        {
            (results, totalFound) = await _tmdbService.SearchShows(strippedTitle, includeRestricted: series.Restricted, year: airDate.Value.Year).ConfigureAwait(false);
            if (results.Count > 0)
            {
                _logger.LogTrace("Found {Count} results for search on {Query} --- Linked to {ShowName} ({ID})", totalFound, strippedTitle, results[0].OriginalName, results[0].Id);

                await _tmdbService.AddShowLink(AnimeID, results[0].Id, additiveLink: true, isAutomatic: true).ConfigureAwait(false);
                await _tmdbService.ScheduleUpdateOfShow(results[0].Id, forceRefresh: ForceRefresh, downloadImages: true).ConfigureAwait(false);
                return;
            }
            (results, totalFound) = await _tmdbService.SearchShows(strippedTitle, includeRestricted: series.Restricted).ConfigureAwait(false);
            if (results.Count > 0)
            {
                _logger.LogTrace("Found {Count} results for search on {Query} --- Linked to {ShowName} ({ID})", totalFound, strippedTitle, results[0].OriginalName, results[0].Id);

                await _tmdbService.AddShowLink(AnimeID, results[0].Id, additiveLink: true, isAutomatic: true).ConfigureAwait(false);
                await _tmdbService.ScheduleUpdateOfShow(results[0].Id, forceRefresh: ForceRefresh, downloadImages: true).ConfigureAwait(false);
                return;
            }
        }
    }

    #endregion

    public SearchTmdbJob(TmdbMetadataService tmdbService)
    {
        _tmdbService = tmdbService;
    }

    protected SearchTmdbJob() { }
}
