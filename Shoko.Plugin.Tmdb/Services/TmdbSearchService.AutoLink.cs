using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Shoko.Abstractions.Metadata;
using Shoko.Abstractions.Metadata.Anidb;
using Shoko.Abstractions.Metadata.Enums;
using Shoko.Abstractions.Metadata.Matching;
using Shoko.Abstractions.Metadata.Search;
using Shoko.Abstractions.Metadata.Services;
using Shoko.Plugin.Tmdb.Mapping;
using TMDbLib.Objects.Movies;
using TMDbLib.Objects.Search;
using TMDbLib.Objects.TvShows;

namespace Shoko.Plugin.Tmdb.Services;

public sealed partial class TmdbSearchService
{
    #region Auto-linking

    /// <summary>
    ///   How far ahead of its air date an anime, or the film one of its
    ///   episodes stands for, is looked for.
    /// </summary>
    private static readonly TimeSpan _maxDaysIntoTheFuture = TimeSpan.FromDays(15);

    /// <summary>
    ///   Every show and movie the auto-search scored for an anime, taken or
    ///   turned down, without linking anything.
    /// </summary>
    /// <remarks>
    ///   Only what was fetched and judged comes back, never a hit not looked
    ///   at. The ones taken lead, each turned-down one carrying why. The
    ///   entries the anime's other sources name follow as hints, the one to
    ///   take first leading; the core takes at most one of them.
    /// </remarks>
    /// <param name="anime">The anime.</param>
    /// <param name="cancellationToken">Cancels the search.</param>
    /// <returns>The candidates, best first.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="anime"/> is <c>null</c>.</exception>
    public async Task<IReadOnlyList<MetadataAutoLinkCandidate>> FindAutoLinks(IAnidbAnime anime, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(anime);

        var search = new AutoSearch(this, anime, await _apiClient.GetImageServerUrl(cancellationToken).ConfigureAwait(false), cancellationToken);
        var results = await search.Run().ConfigureAwait(false);
        return [.. results.Select(result => result.ToCandidate())];
    }

    /// <summary>
    ///   One run of the auto-search for one anime.
    /// </summary>
    /// <param name="service">The search service.</param>
    /// <param name="anime">The anime.</param>
    /// <param name="imageServer">TMDB's image server.</param>
    /// <param name="cancellationToken">Cancels the search.</param>
    private sealed class AutoSearch(TmdbSearchService service, IAnidbAnime anime, string imageServer, CancellationToken cancellationToken)
    {
        // Each title's search often finds the same shows, so a season is fetched once for all of them.
        private readonly Dictionary<(int ShowID, int SeasonNumber), Task<IReadOnlyList<MetadataSearchResultEpisode>?>> _seasons = [];

        private readonly HashSet<int> _aligned = [];

        private ILogger Logger => service._logger;

        private IMetadataMatchingEngine Engine => service._matchingEngine;

        /// <summary>
        ///   Runs the search: the anime's hints, then movies, shows or both by
        ///   its type, then the hints judged.
        /// </summary>
        /// <returns>The results, best first.</returns>
        public async Task<IReadOnlyList<TmdbAutoSearchResult>> Run()
        {
            var hints = await FindHints().ConfigureAwait(false);
            IReadOnlyList<TmdbAutoSearchResult> results = anime.Type switch
            {
                // TMDB holds no music videos, and the other and unknown types are hard to map, so none are searched.
                AnimeType.MusicVideo or AnimeType.Other or AnimeType.Unknown => [],
                AnimeType.Movie => await SearchMovies(hints).ConfigureAwait(false),
                // A short OVA or web series may be a movie on TMDB; a show is tried when no movie is taken.
                AnimeType.OVA or AnimeType.Web when IsShortForm(anime) => await SearchMoviesThenShow(hints).ConfigureAwait(false),
                _ => await SearchShow(hints).ConfigureAwait(false),
            };
            if (hints.Count > 0)
                results = [.. results, .. await JudgeHints(hints, results).ConfigureAwait(false)];

            return BestFirst(results);
        }

        private async Task<IReadOnlyList<TmdbAutoSearchResult>> SearchMoviesThenShow(IReadOnlyList<TmdbHint> hints)
        {
            var movies = await SearchMovies(hints).ConfigureAwait(false);
            if (movies.Any(result => result.Rejection is null))
                return movies;

            // The movies turned down are still listed beside the shows.
            return [.. movies, .. await SearchShow(hints).ConfigureAwait(false)];
        }

        #region Movies

        private async Task<IReadOnlyList<TmdbAutoSearchResult>> SearchMovies(IReadOnlyList<TmdbHint> hints)
        {
            var list = new List<TmdbAutoSearchResult>();
            var allTitles = anime.Titles.Where(title => title.Type is TitleType.Main or TitleType.Official).ToList();
            if (allTitles.Count is 0)
                return list;

            var mainTitle = allTitles.FirstOrDefault(title => title.Type is TitleType.Main) ?? allTitles[0];
            var language = OriginalLanguageOf(mainTitle.Language);
            var title = mainTitle.Value;
            var officialTitle = language == mainTitle.Language ? mainTitle.Value : allTitles.FirstOrDefault(other => other.Language == language)?.Value;
            var englishTitle = allTitles.FirstOrDefault(other => other.Language == TitleLanguage.English)?.Value;

            // One movie per episode standing for one.
            var episodes = anime.Episodes
                .Where(episode => episode.Type is EpisodeType.Episode or EpisodeType.Special or EpisodeType.Other)
                .OrderBy(episode => episode.Type)
                .ThenBy(episode => episode.EpisodeNumber)
                .ToList();
            var now = service._apiClient.TimeProvider.GetLocalNow().DateTime;
            if (episodes.Count is 1)
            {
                var airDate = AirDateOf(anime) ?? AirDateOf(episodes[0]);
                if (!AiredWithin(airDate, now, _maxDaysIntoTheFuture))
                    return [];

                var year = (RegularAirDateOf(anime) ?? RegularAirDateOf(episodes[0]) ?? airDate.GetValueOrDefault()).Year;
                await SearchMovie(list, episodes[0], officialTitle, englishTitle, title, year, hints).ConfigureAwait(false);
                return list;
            }

            foreach (var episode in episodes)
            {
                var episodeTitles = episode.Titles;
                if (episodeTitles.Any(other => other.Value.Contains("Complete Movie", StringComparison.InvariantCultureIgnoreCase)))
                {
                    var airDateForAnime = AirDateOf(anime) ?? AirDateOf(episodes[0]);
                    if (!AiredWithin(airDateForAnime, now, _maxDaysIntoTheFuture))
                        continue;

                    var yearForAnime = (RegularAirDateOf(anime) ?? RegularAirDateOf(episodes[0]) ?? airDateForAnime.GetValueOrDefault()).Year;
                    await SearchMovie(list, episode, officialTitle, englishTitle, title, yearForAnime, hints).ConfigureAwait(false);
                    continue;
                }

                var airDateForEpisode = AirDateOf(episode) ?? AirDateOf(anime);
                if (!AiredWithin(airDateForEpisode, now, _maxDaysIntoTheFuture))
                    continue;

                var officialSubTitle = episodeTitles.FirstOrDefault(other => other.Language == language)?.Value ??
                    episodeTitles.FirstOrDefault(other => other.Language == mainTitle.Language)?.Value;
                var englishSubTitle = episodeTitles.FirstOrDefault(other => other.Language == TitleLanguage.English)?.Value;
                var isGenericTitle = string.Equals(englishSubTitle, $"Movie {episode.EpisodeNumber}", StringComparison.InvariantCultureIgnoreCase);
                if (IsExtra(episode, englishSubTitle))
                    continue;

                var yearForEpisode = (RegularAirDateOf(episode) ?? RegularAirDateOf(anime) ?? airDateForEpisode.GetValueOrDefault()).Year;
                await SearchMovie(
                    list,
                    episode,
                    FullMovieTitle(officialTitle, officialSubTitle, episode.EpisodeNumber, isGenericTitle),
                    FullMovieTitle(englishTitle, englishSubTitle, episode.EpisodeNumber, isGenericTitle),
                    FullMovieTitle(title, englishSubTitle, episode.EpisodeNumber, isGenericTitle),
                    yearForEpisode,
                    hints
                ).ConfigureAwait(false);
            }

            return list;
        }

        private async Task SearchMovie(
            List<TmdbAutoSearchResult> list,
            IAnidbEpisode episode,
            string? officialTitle,
            string? englishTitle,
            string? mainTitle,
            int year,
            IReadOnlyList<TmdbHint> hints
        )
        {
            foreach (var query in new[] { officialTitle, englishTitle, mainTitle }.Distinct(StringComparer.Ordinal))
            {
                if (string.IsNullOrWhiteSpace(query))
                    continue;

                var scored = await SearchMovieByTitle(episode, query, hints, anime.Restricted, year).ConfigureAwait(false);
                list.AddRange(scored);
                if (scored.Any(result => result.Rejection is null))
                    return;
            }
        }

        private async Task<IReadOnlyList<TmdbAutoSearchResult>> SearchMovieByTitle(IAnidbEpisode episode, string query, IReadOnlyList<TmdbHint> hints, bool includeRestricted, int year)
        {
            var candidateCount = service._configurationProvider.Load().AutoSearchMovieCandidateCount;
            var seen = new HashSet<int>();
            var candidates = new List<SearchMovie>();

            // The full title and its form without a sequel suffix, with the year and then without,
            // as TMDB's year is the release's and a movie dated otherwise on AniDB is missed with it.
            var stripped = TitleVariants.WithoutSequelSuffix(query);
            await CollectMovies(candidates, query, includeRestricted, year, seen, candidateCount).ConfigureAwait(false);
            if (!string.IsNullOrEmpty(stripped) && candidates.Count < candidateCount)
                await CollectMovies(candidates, stripped, includeRestricted, year, seen, candidateCount).ConfigureAwait(false);

            var yearFreeCap = candidateCount * 2;
            await CollectMovies(candidates, query, includeRestricted, 0, seen, yearFreeCap).ConfigureAwait(false);
            if (!string.IsNullOrEmpty(stripped) && candidates.Count < yearFreeCap)
                await CollectMovies(candidates, stripped, includeRestricted, 0, seen, yearFreeCap).ConfigureAwait(false);

            if (candidates.Count is 0)
                return [];

            var options = new MovieMatchOptions { Query = query, IncludeRestricted = includeRestricted, HintedIDs = HintedIDs(hints, isMovie: true) };
            var ranked = await PickMovie(
                Engine,
                anime,
                episode,
                options,
                HintedFirst(candidates.Select(candidate => candidate.Id), hints, isMovie: true),
                FetchMovie
            ).ConfigureAwait(false);
            if (ranked is [{ Rejection: MatchRejectionReason.None } best, ..])
                Logger.LogInformation("Best match for \"{Query}\": {MovieName} ({ID}) rating={Rating}", query, best.Candidate.OriginalTitle, best.Candidate.ID.ID, best.Rating);

            return
            [
                .. ranked.Select(match => new TmdbAutoSearchResult
                {
                    AnidbAnime = anime,
                    AnidbEpisode = episode,
                    Candidate = match.Candidate,
                    MatchRating = match.Rating,
                    IsRemote = true,
                    Rejection = Rejected(match.Rejection, query, match.Details),
                }),
            ];
        }

        #endregion

        #region Shows

        private async Task<IReadOnlyList<TmdbAutoSearchResult>> SearchShow(IReadOnlyList<TmdbHint> hints)
        {
            var storedAirDate = ShowAirDate(anime);
            if (!AiredWithin(storedAirDate, service._apiClient.TimeProvider.GetLocalNow().DateTime, _maxDaysIntoTheFuture))
                return [];

            // The regular broadcast is matched against, as the anime or its first episodes may be dated by an early showing.
            var airDate = RegularAirDateOf(anime) ?? (SecondEpisode(anime) is { } second ? RegularAirDateOf(second) : null) ?? storedAirDate.GetValueOrDefault();
            var allTitles = anime.Titles.Where(title => title.Type is TitleType.Main or TitleType.Official).ToList();
            if (allTitles.Count is 0)
                return [];

            var mainTitle = allTitles.FirstOrDefault(title => title.Type is TitleType.Main) ?? allTitles[0];
            var language = OriginalLanguageOf(mainTitle.Language);

            // Walk back to the earliest prequel, whose titles a sequel's TMDB show usually goes by.
            ISeries series = anime;
            var currentDate = airDate;
            IReadOnlyList<IRelatedMetadata<ISeries, ISeries>> relations = anime.RelatedSeries;
            while (relations.Count > 0)
            {
                var prequel = relations
                    .Where(relation => relation.RelationType == RelationType.Prequel)
                    .Select(relation => relation.Related)
                    .FirstOrDefault(related => related?.AirDate is { } prequelDate && prequelDate.ToDateTime() <= currentDate);
                if (prequel is null)
                    break;

                series = prequel;
                currentDate = prequel.AirDate!.Value.ToDateTime();
                relations = prequel.RelatedSeries;
            }

            var scored = new List<TmdbAutoSearchResult>();
            Task<TmdbAutoSearchResult?> SearchByTitle(string title, bool restricted, bool isJapanese)
                => SearchShowByTitle(scored, title, airDate, restricted, isJapanese, hints);

            var ownAnime = series.ID == anime.ID;
            var originalTitle = language == mainTitle.Language
                ? mainTitle.Value
                : (ownAnime ? allTitles : series.Titles).FirstOrDefault(title => title.Type is TitleType.Official && title.Language == language)?.Value;
            var match = !string.IsNullOrEmpty(originalTitle)
                ? await SearchByTitle(originalTitle, series.Restricted, language == TitleLanguage.Japanese).ConfigureAwait(false)
                : null;

            if (match is null)
            {
                var englishTitle = (ownAnime ? allTitles : series.Titles).FirstOrDefault(title => title is { Type: TitleType.Official, Language: TitleLanguage.English })?.Value;
                if (!string.IsNullOrEmpty(englishTitle) && (string.IsNullOrEmpty(originalTitle) || !string.Equals(englishTitle, originalTitle, StringComparison.Ordinal)))
                    match = await SearchByTitle(englishTitle, series.Restricted, false).ConfigureAwait(false);
            }

            // The last attempt is the main title, in no other language.
            match ??= await SearchByTitle(mainTitle.Value, series.Restricted, false).ConfigureAwait(false);

            // A prequel's titles find the wrong show when the anime is its own TMDB show, so its own are tried too.
            if (!ownAnime)
            {
                TmdbAutoSearchResult? ownMatch = null;
                var ownOriginalTitle = language == mainTitle.Language
                    ? mainTitle.Value
                    : allTitles.FirstOrDefault(title => title.Type is TitleType.Official && title.Language == language)?.Value;
                if (!string.IsNullOrEmpty(ownOriginalTitle) && !string.Equals(ownOriginalTitle, originalTitle, StringComparison.Ordinal))
                    ownMatch = await SearchByTitle(ownOriginalTitle, anime.Restricted, language == TitleLanguage.Japanese).ConfigureAwait(false);

                if (ownMatch is null &&
                    !string.Equals(mainTitle.Value, originalTitle, StringComparison.Ordinal) &&
                    !string.Equals(mainTitle.Value, ownOriginalTitle, StringComparison.Ordinal))
                    ownMatch = await SearchByTitle(mainTitle.Value, anime.Restricted, false).ConfigureAwait(false);

                var airedOn = DateOnly.FromDateTime(airDate);
                var episodeCount = anime.EpisodeCounts.Episodes;
                if (ownMatch is not null && (match is null || OwnTitleWins(Found(ownMatch, hints), Found(match, hints), airedOn, episodeCount)))
                {
                    if (match is not null)
                        match.Rejection = new()
                        {
                            Reason = MatchRejectionReason.Outranked,
                            Details = $"Found through the titles of the prequel, AniDB anime {series.ID.ID}, rated {match.MatchRating}; " +
                                $"{Named(ownMatch)}, found through the anime's own titles, rated {ownMatch.MatchRating}, was taken{HintNote(ownMatch, hints)}.",
                        };
                    match = ownMatch;
                }
                else if (ownMatch is not null && match is not null)
                {
                    ownMatch.Rejection = new()
                    {
                        Reason = MatchRejectionReason.Outranked,
                        Details = $"Found through the anime's own titles, rated {ownMatch.MatchRating}; " +
                            $"{Named(match)}, found through the titles of the prequel, AniDB anime {series.ID.ID}, rated {match.MatchRating}, was taken{HintNote(match, hints)}.",
                    };
                }
            }

            // A show scored more than once counts as taken if any of its scorings took it.
            List<TmdbAutoSearchResult> results =
            [
                .. scored
                    .GroupBy(result => result.TmdbID)
                    .Select(group => group.FirstOrDefault(result => result.Rejection is null) ?? group.First()),
            ];

            // The prequel's shows are listed for context, as a sequel is usually one of their seasons; never taken.
            if (!ownAnime && series is IAnidbAnime prequelAnime)
            {
                foreach (var link in service._metadataService.GetSeriesCrossReferences(prequelAnime.AnidbID, MetadataSource.TMDB).DistinctBy(link => link.ProviderID))
                {
                    if (link.ProviderID is { } providerID && service._stores.Series.GetSeries(providerID) is { } stored)
                        results.Add(PrequelLink(anime, prequelAnime.AnidbID, link.MatchRating, TmdbSearchResults.FromStored(stored, imageServer)));
                }
            }

            return results;
        }

        private async Task<TmdbAutoSearchResult?> SearchShowByTitle(
            List<TmdbAutoSearchResult> scored,
            string query,
            DateTime airDate,
            bool restricted,
            bool isJapanese,
            IReadOnlyList<TmdbHint> hints
        )
        {
            var candidateCount = service._configurationProvider.Load().AutoSearchShowCandidateCount;
            var seen = new HashSet<int>();
            var candidates = new List<SearchTv>();

            // The full title, without its sequel suffix and without its subtitle, with the year. The
            // engine rates the subtitle-less form a close match only, so a parent show found by its
            // short title cannot outscore one matching the full title.
            var stripped = TitleVariants.WithoutSequelSuffix(query);
            var withoutSubtitle = TitleVariants.WithoutSubtitle(stripped ?? query, isJapanese);
            await CollectShows(candidates, query, restricted, airDate.Year, seen, candidateCount).ConfigureAwait(false);
            if (!string.IsNullOrEmpty(stripped) && candidates.Count < candidateCount)
                await CollectShows(candidates, stripped, restricted, airDate.Year, seen, candidateCount).ConfigureAwait(false);
            if (!string.IsNullOrEmpty(withoutSubtitle) && candidates.Count < candidateCount)
                await CollectShows(candidates, withoutSubtitle, restricted, airDate.Year, seen, candidateCount).ConfigureAwait(false);

            // The same without the year, always: a later season's year never finds its show, which
            // first aired before it. The pool is doubled, as root shows rank lower without the year.
            var yearFreeCap = candidateCount * 2;
            await CollectShows(candidates, query, restricted, 0, seen, yearFreeCap).ConfigureAwait(false);
            if (!string.IsNullOrEmpty(stripped) && candidates.Count < yearFreeCap)
                await CollectShows(candidates, stripped, restricted, 0, seen, yearFreeCap).ConfigureAwait(false);
            if (!string.IsNullOrEmpty(withoutSubtitle) && candidates.Count < yearFreeCap)
                await CollectShows(candidates, withoutSubtitle, restricted, 0, seen, yearFreeCap).ConfigureAwait(false);

            if (candidates.Count is 0)
                return null;

            var options = new SeriesMatchOptions
            {
                Query = query,
                QueryLanguage = isJapanese ? TitleLanguage.Japanese : null,
                IncludeRestricted = restricted,
                HintedIDs = HintedIDs(hints, isMovie: false),
            };
            var ranked = await PickShow(
                Engine,
                anime,
                options,
                HintedFirst(candidates.Select(candidate => candidate.Id), hints, isMovie: false),
                async showID => await FetchShow(showID).ConfigureAwait(false) is { } full ? WithStoredEpisodes(full) : null,
                FetchSeasonOnce,
                alignedShows: _aligned
            ).ConfigureAwait(false);

            TmdbAutoSearchResult? taken = null;
            foreach (var judged in ranked)
            {
                var result = new TmdbAutoSearchResult
                {
                    AnidbAnime = anime,
                    Candidate = judged.Candidate,
                    MatchRating = judged.Rating,
                    IsRemote = true,
                    Rejection = Rejected(judged.Rejection, query, judged.Details),
                };
                scored.Add(result);
                if (result.Rejection is null)
                    taken ??= result;
            }

            if (taken is not null)
                Logger.LogInformation("Best match for \"{Query}\": {ShowName} ({ID}) rating={Rating}", query, taken.Candidate.OriginalTitle, taken.TmdbID, taken.MatchRating);
            return taken;
        }

        private Task<IReadOnlyList<MetadataSearchResultEpisode>?> FetchSeasonOnce(int showID, int seasonNumber)
        {
            if (!_seasons.TryGetValue((showID, seasonNumber), out var fetching))
                _seasons[(showID, seasonNumber)] = fetching = FetchSeasonEpisodes(showID, seasonNumber);
            return fetching;
        }

        /// <summary>
        ///   A stored show's episodes filled in for every season, so the
        ///   matching engine can line them up without asking TMDB.
        /// </summary>
        /// <param name="candidate">The show, as TMDB answered it.</param>
        /// <returns>The show, unchanged when it is not stored.</returns>
        private MetadataSeriesSearchResult WithStoredEpisodes(MetadataSeriesSearchResult candidate)
        {
            if (service._stores.Series.GetSeries(candidate.ID) is not { } stored)
                return candidate;

            var fromStore = TmdbSearchResults.FromStored(stored, imageServer);
            if (fromStore.Seasons is not { Count: > 0 } storedSeasons || storedSeasons.All(season => season.Episodes is null))
                return candidate;

            var bySeason = storedSeasons.Where(season => season.Episodes is not null).ToDictionary(season => season.SeasonNumber);
            return candidate with
            {
                Seasons =
                [
                    .. (candidate.Seasons ?? storedSeasons).Select(season => bySeason.TryGetValue(season.SeasonNumber, out var storedSeason)
                        ? season with { Episodes = storedSeason.Episodes, FirstEpisodeAiredAt = storedSeason.FirstEpisodeAiredAt }
                        : season),
                ],
            };
        }

        #endregion

        #region Hints

        /// <summary>
        ///   The TMDB entries the anime's cross-source IDs name, or, when they
        ///   name none, the ones TMDB finds for the IMDb titles they name, and
        ///   after them the ones the anime's links on other sources name.
        /// </summary>
        /// <returns>The hints, each entry once.</returns>
        private async Task<IReadOnlyList<TmdbHint>> FindHints()
        {
            var found = new List<TmdbHint>(TmdbHintsOf(anime.CrossSourceIDs));
            if (found.Count is 0)
            {
                foreach (var imdbID in ImdbIDsOf(anime.CrossSourceIDs))
                {
                    var result = await service._apiClient.FindByImdbID(imdbID, cancellationToken).ConfigureAwait(false);
                    found.AddRange((result?.MovieResults ?? []).Select(movie => new TmdbHint(movie.Id, true, imdbID)));
                    found.AddRange((result?.TvResults ?? []).Select(show => new TmdbHint(show.Id, false, imdbID)));
                }
            }

            found.AddRange(TmdbHintsOf(service._linkingService.GetCrossSourceHints(MetadataSource.TMDB, anime.AnidbID)));
            return [.. found.DistinctBy(hint => (hint.TmdbID, hint.IsMovie))];
        }

        private async Task<IReadOnlyList<TmdbAutoSearchResult>> JudgeHints(IReadOnlyList<TmdbHint> hints, IReadOnlyList<TmdbAutoSearchResult> searched)
        {
            var (shows, movies) = HintKinds(anime.Type, IsShortForm(anime));
            var now = service._apiClient.TimeProvider.GetLocalNow().DateTime;
            var results = new List<TmdbAutoSearchResult>();
            foreach (var hint in hints)
            {
                var judged = hint.IsMovie
                    ? await JudgeMovieHint(hint, searched).ConfigureAwait(false)
                    : await JudgeShowHint(hint, searched).ConfigureAwait(false);
                if (judged is not { } verdict)
                {
                    Logger.LogDebug("{Hint} for AniDB anime {AnimeID}, which could not be had from TMDB.", hint.Source, anime.AnidbID);
                    continue;
                }

                var result = verdict.Result;
                result.Rejection = HintRejection(
                    hint,
                    anime.Type,
                    hint.IsMovie ? movies : shows,
                    result.MatchRating,
                    verdict.Filter,
                    verdict.Details,
                    verdict.Unplaced,
                    NotAired(hint.IsMovie ? FilmAirDate(anime, result.AnidbEpisode!) : ShowAirDate(anime), hint.IsMovie, now, _maxDaysIntoTheFuture)
                );
                if (result.Rejection is null)
                {
                    result.MatchRating = result.MatchRating is MatchRating.None ? MatchRating.FirstAvailable : result.MatchRating;
                    Logger.LogDebug("{Hint} for AniDB anime {AnimeID}, which may be taken, rated {Rating}.", hint.Source, anime.AnidbID, result.MatchRating);
                }

                results.Add(result);
            }

            return HintsInOrder(results, filmsFirst: shows && movies);
        }

        private async Task<HintVerdict?> JudgeShowHint(TmdbHint hint, IReadOnlyList<TmdbAutoSearchResult> searched)
        {
            var options = new SeriesMatchOptions { IncludeRestricted = anime.Restricted, HintedIDs = [hint.ID] };
            MetadataSeriesSearchResult candidate;
            bool isLocal;
            SeriesMatch judged;
            if (searched.FirstOrDefault(result => !result.IsMovie && result.TmdbID == hint.TmdbID) is { Candidate: MetadataSeriesSearchResult found } known)
            {
                (candidate, isLocal) = (found, known.IsLocal);
                judged = Engine.MatchSeries(anime, [candidate], options)[0];
            }
            else
            {
                if (service._stores.Series.GetSeries(hint.ID) is { } stored)
                    (candidate, isLocal) = (TmdbSearchResults.FromStored(stored, imageServer), true);
                else if (await FetchShow(hint.TmdbID).ConfigureAwait(false) is { } remote)
                    (candidate, isLocal) = (remote, false);
                else
                    return null;

                // Judged as the search judges a show it fetched, the season holding the anime's start lined up.
                var shown = candidate;
                var picked = await PickShow(
                    Engine,
                    anime,
                    options,
                    [hint.TmdbID],
                    _ => Task.FromResult<MetadataSeriesSearchResult?>(shown),
                    FetchSeasonEpisodes,
                    alignedCandidates: 1
                ).ConfigureAwait(false);
                if (picked.Count is 0)
                    return null;

                judged = picked[0];
                candidate = judged.Candidate;
            }

            var result = new TmdbAutoSearchResult
            {
                AnidbAnime = anime,
                Candidate = candidate,
                MatchRating = judged.Rating,
                IsLocal = isLocal,
                IsRemote = !isLocal,
                Origin = hint.Origin,
            };
            return new(result, judged.Rejection, judged.Details, false);
        }

        private async Task<HintVerdict?> JudgeMovieHint(TmdbHint hint, IReadOnlyList<TmdbAutoSearchResult> searched)
        {
            MetadataMovieSearchResult candidate;
            bool isLocal;
            IReadOnlyList<IAnidbEpisode> episodes;
            var fetched = searched.Where(result => result.IsMovie && result.TmdbID == hint.TmdbID).ToList();
            if (fetched.FirstOrDefault(result => result.Rejection is null) is { Candidate: MetadataMovieSearchResult took } taken)
                (candidate, isLocal, episodes) = (took, taken.IsLocal, [taken.AnidbEpisode!]);
            else if (fetched.FirstOrDefault() is { Candidate: MetadataMovieSearchResult found } known)
                (candidate, isLocal, episodes) = (found, known.IsLocal, FilmEpisodes(anime, hint));
            else if (service._stores.Movies.GetMovie(hint.ID) is { } stored)
                (candidate, isLocal, episodes) = (TmdbSearchResults.FromStored(stored, imageServer), true, FilmEpisodes(anime, hint));
            else if (await FetchMovie(hint.TmdbID).ConfigureAwait(false) is { } remote)
                (candidate, isLocal, episodes) = (remote, false, FilmEpisodes(anime, hint));
            else
                return null;

            if (episodes.Count is 0)
                return null;

            var options = new MovieMatchOptions { IncludeRestricted = anime.Restricted, HintedIDs = [hint.ID] };
            var judged = episodes
                .Select(episode => (Episode: episode, Match: Engine.MatchMovies(anime, episode, [candidate], options)[0]))
                .ToList();
            IReadOnlyList<DateOnly> releasedOn = candidate.ReleasedAt is { IsComplete: true } released
                ? [released.ToDateOnly(), .. candidate.OtherReleaseDates]
                : candidate.OtherReleaseDates;
            var (index, unplaced) = PlaceFilm([.. judged.Select(pair => (pair.Match.Rating, AiredOn(pair.Episode)))], releasedOn);
            var best = judged[index];
            var result = new TmdbAutoSearchResult
            {
                AnidbAnime = anime,
                AnidbEpisode = best.Episode,
                Candidate = candidate,
                MatchRating = best.Match.Rating,
                IsLocal = isLocal,
                IsRemote = !isLocal,
                Origin = hint.Origin,
            };
            return new(result, best.Match.Rejection, best.Match.Details, unplaced);
        }

        /// <summary>
        ///   What judging a hint came to.
        /// </summary>
        /// <param name="Result">The hinted entry, as a candidate.</param>
        /// <param name="Filter">The engine's reason for not taking it, if any.</param>
        /// <param name="Details">What the engine compared, if it said.</param>
        /// <param name="Unplaced">Whether it is a movie no single episode could be told to stand for.</param>
        private readonly record struct HintVerdict(TmdbAutoSearchResult Result, MatchRejectionReason Filter, string? Details, bool Unplaced);

        #endregion

        #region Fetching

        /// <summary>
        ///   Searches TMDB's shows and adds the animated hits not seen yet to
        ///   the candidates, up to a cap.
        /// </summary>
        /// <param name="candidates">The candidates so far.</param>
        /// <param name="query">What to search for.</param>
        /// <param name="restricted">Whether to include adult shows.</param>
        /// <param name="year">The year the show first aired, or <c>0</c> for any.</param>
        /// <param name="seen">The hits seen so far.</param>
        /// <param name="cap">How many candidates to collect at most.</param>
        /// <returns>A task that completes once the hits are collected.</returns>
        private async Task CollectShows(List<SearchTv> candidates, string query, bool restricted, int year, HashSet<int> seen, int cap)
            => Collect(candidates, (await service.SearchShowsRaw(query, restricted, year, cancellationToken: cancellationToken).ConfigureAwait(false)).Page, seen, cap, restricted);

        /// <summary>
        ///   Searches TMDB's movies and adds the animated hits not seen yet to
        ///   the candidates, up to a cap.
        /// </summary>
        /// <param name="candidates">The candidates so far.</param>
        /// <param name="query">What to search for.</param>
        /// <param name="restricted">Whether to include adult movies.</param>
        /// <param name="year">The year the movie was released, or <c>0</c> for any.</param>
        /// <param name="seen">The hits seen so far.</param>
        /// <param name="cap">How many candidates to collect at most.</param>
        /// <returns>A task that completes once the hits are collected.</returns>
        private async Task CollectMovies(List<SearchMovie> candidates, string query, bool restricted, int year, HashSet<int> seen, int cap)
            => Collect(candidates, (await service.SearchMoviesRaw(query, restricted, year, cancellationToken: cancellationToken).ConfigureAwait(false)).Page, seen, cap, restricted);

        private async Task<MetadataSeriesSearchResult?> FetchShow(int showID)
            => await service._apiClient.GetShow(showID, TvShowMethods.Translations, cancellationToken).ConfigureAwait(false) is { } show
                ? TmdbSearchResults.FromShow(show, imageServer)
                : null;

        private async Task<MetadataMovieSearchResult?> FetchMovie(int movieID)
            => await service._apiClient.GetMovie(movieID, MovieMethods.Translations | MovieMethods.ReleaseDates, cancellationToken).ConfigureAwait(false) is { } movie
                ? TmdbSearchResults.FromMovie(movie, imageServer)
                : null;

        private async Task<IReadOnlyList<MetadataSearchResultEpisode>?> FetchSeasonEpisodes(int showID, int seasonNumber)
        {
            var season = await service._apiClient.GetSeason(showID, seasonNumber, TvSeasonMethods.Undefined, cancellationToken).ConfigureAwait(false);
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
    }

    #endregion
}
