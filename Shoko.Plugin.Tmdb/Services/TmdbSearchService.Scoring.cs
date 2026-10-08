using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Threading.Tasks;
using Shoko.Abstractions.Metadata;
using Shoko.Abstractions.Metadata.Anidb;
using Shoko.Abstractions.Metadata.Enums;
using Shoko.Abstractions.Metadata.Matching;
using Shoko.Abstractions.Metadata.Search;
using Shoko.Abstractions.Metadata.Services;
using Shoko.Plugin.Tmdb.Mapping;
using TMDbLib.Objects.Search;

namespace Shoko.Plugin.Tmdb.Services;

public sealed partial class TmdbSearchService
{
    #region Ordering

    /// <summary>
    ///   Puts the search's candidates first, the ones taken leading, then the
    ///   links listed for context, then the hints of either origin as
    ///   <see cref="HintsInOrder"/> put them, keeping one of each candidate
    ///   per origin and AniDB episode: the one taken, else the first scored.
    /// </summary>
    /// <param name="results">The candidates, in the order they were scored.</param>
    /// <returns>The candidates, best first.</returns>
    internal static IReadOnlyList<TmdbAutoSearchResult> BestFirst(IEnumerable<TmdbAutoSearchResult> results)
        => [
            .. results
                .GroupBy(result => (result.Origin, result.IsMovie, EpisodeID: result.AnidbEpisode?.AnidbID, result.TmdbID))
                .Select(group => group.FirstOrDefault(result => result.Rejection is null) ?? group.First())
                .OrderBy(result => result.Origin is MetadataAutoLinkOrigin.CrossSourceLink ? MetadataAutoLinkOrigin.AnidbResource : result.Origin)
                .ThenBy(result => result.Rejection is null ? 0 : 1),
        ];

    /// <summary>
    ///   Puts the hints that may be taken first, in the search's own order,
    ///   and the rest after them as they came.
    /// </summary>
    /// <param name="results">The judged hints, in the order they were named.</param>
    /// <param name="filmsFirst">Whether a movie goes before a show, as for a short-form anime.</param>
    /// <returns>The hints, the one to take first leading.</returns>
    internal static IReadOnlyList<TmdbAutoSearchResult> HintsInOrder(IReadOnlyList<TmdbAutoSearchResult> results, bool filmsFirst)
        => [
            .. results
                .OrderBy(result => result.Rejection is null ? 0 : 1)
                .ThenBy(result => result.Rejection is null && filmsFirst && !result.IsMovie ? 1 : 0)
                .ThenBy(result => result.Rejection is null ? ShowMatchPriority(result.MatchRating) : 0),
        ];

    /// <summary>
    ///   A show a prequel of the anime is linked to, listed for context.
    /// </summary>
    /// <param name="anime">The anime searched for.</param>
    /// <param name="prequelAnimeID">The prequel's AniDB ID.</param>
    /// <param name="linkRating">The rating of the prequel's link.</param>
    /// <param name="show">The show.</param>
    /// <returns>The candidate, rated nothing and turned down as an existing link.</returns>
    internal static TmdbAutoSearchResult PrequelLink(IAnidbAnime anime, int prequelAnimeID, MatchRating linkRating, MetadataSeriesSearchResult show)
        => new()
        {
            AnidbAnime = anime,
            Candidate = show,
            MatchRating = MatchRating.None,
            IsLocal = true,
            Origin = MetadataAutoLinkOrigin.PrequelLink,
            LinkMatchRating = linkRating,
            PrequelAnidbAnimeID = prequelAnimeID,
            Rejection = new()
            {
                Reason = MatchRejectionReason.ExistingLink,
                Details = $"Linked to the prequel, AniDB anime {prequelAnimeID}, rated {linkRating}. Listed for context; never linked by the search.",
            },
        };

    #endregion

    #region Anime

    /// <summary>
    ///   Whether an anime has at most four regular episodes, which may make
    ///   it a movie on TMDB.
    /// </summary>
    /// <param name="anime">The anime.</param>
    /// <returns><c>true</c> for a short-form anime.</returns>
    internal static bool IsShortForm(IAnidbAnime anime)
        => IsShortFormByEpisodeCount(anime.Episodes.Count(episode => episode.Type is EpisodeType.Episode));

    /// <summary>
    ///   Whether so many regular episodes make a short-form anime.
    /// </summary>
    /// <param name="mainEpisodeCount">The number of regular episodes.</param>
    /// <returns><c>true</c> for at most four.</returns>
    internal static bool IsShortFormByEpisodeCount(int mainEpisodeCount)
        => mainEpisodeCount <= 4;

    /// <summary>
    ///   Whether something dated so aired, or airs within the window, which
    ///   is when the auto-search looks for it.
    /// </summary>
    /// <param name="airDate">When it airs, or <c>null</c> when that is not known.</param>
    /// <param name="now">The time now.</param>
    /// <param name="window">How far ahead of its air date it is looked for.</param>
    /// <returns><c>true</c> when it may be searched for.</returns>
    internal static bool AiredWithin([NotNullWhen(true)] DateTime? airDate, DateTime now, TimeSpan window)
        => airDate is { } date && (date <= now || date - now <= window);

    /// <summary>
    ///   When an anime searched for as a show aired: its own date, or its
    ///   first or second regular episode's.
    /// </summary>
    /// <param name="anime">The anime.</param>
    /// <returns>The date, or <c>null</c> when it is not known.</returns>
    internal static DateTime? ShowAirDate(IAnidbAnime anime)
        => AirDateOf(anime) ?? (SecondEpisode(anime) is { } episode ? AirDateOf(episode) : null);

    /// <summary>
    ///   When the movie one episode of an anime stands for aired: the anime's
    ///   date for an anime of one movie or a complete movie, the episode's
    ///   otherwise.
    /// </summary>
    /// <param name="anime">The anime.</param>
    /// <param name="episode">The episode standing for the movie.</param>
    /// <returns>The date, or <c>null</c> when it is not known.</returns>
    internal static DateTime? FilmAirDate(IAnidbAnime anime, IAnidbEpisode episode)
    {
        var films = anime.Episodes.Count(other => other.Type is EpisodeType.Episode or EpisodeType.Special or EpisodeType.Other);
        var whole = films is 1 || episode.Titles.Any(title => title.Value.Contains("Complete Movie", StringComparison.InvariantCultureIgnoreCase));
        return whole ? AirDateOf(anime) ?? AirDateOf(episode) : AirDateOf(episode) ?? AirDateOf(anime);
    }

    private static IAnidbEpisode? SecondEpisode(IAnidbAnime anime)
        => anime.Episodes
            .Where(episode => episode.Type is EpisodeType.Episode)
            .OrderBy(episode => episode.EpisodeNumber)
            .Take(2)
            .LastOrDefault();

    private static DateTime? AirDateOf(IAnidbAnime anime)
        => anime.AirDate?.ToDateTime();

    // The first showing, early or regular, which decides whether the episode is out.
    private static DateTime? AirDateOf(IAnidbEpisode episode)
        => (episode.EarlyAirDate ?? episode.AirDate)?.ToDateTime(TimeOnly.MinValue);

    private static DateTime? RegularAirDateOf(IAnidbAnime anime)
        => RegularStartOf(anime)?.ToDateTime();

    /// <summary>
    ///   When the anime's regular broadcast started: its first normal
    ///   episode's <see cref="IEpisode.AirDate"/> when that episode was shown
    ///   early, and the anime's own date otherwise.
    /// </summary>
    /// <param name="anime">The anime.</param>
    /// <returns>The date, or <c>null</c> when neither is dated.</returns>
    private static PartialDateOnly? RegularStartOf(IAnidbAnime anime)
        => anime.Episodes.FirstOrDefault(episode => episode is { Type: EpisodeType.Episode, EpisodeNumber: 1 }) is { EarlyAirDate: not null, AirDate: { } regular }
            ? new PartialDateOnly(regular)
            : anime.AirDate;

    private static DateTime? RegularAirDateOf(IAnidbEpisode episode)
        => episode.AirDate?.ToDateTime(TimeOnly.MinValue);

    /// <summary>
    ///   The language a transcription of a title is a transcription of.
    /// </summary>
    /// <param name="language">The title's language.</param>
    /// <returns>The original language.</returns>
    private static TitleLanguage OriginalLanguageOf(TitleLanguage language)
        => language switch
        {
            TitleLanguage.Romaji => TitleLanguage.Japanese,
            TitleLanguage.Pinyin => TitleLanguage.ChineseSimplified,
            TitleLanguage.KoreanTranscription => TitleLanguage.Korean,
            TitleLanguage.ThaiTranscription => TitleLanguage.Thai,
            _ => language,
        };

    /// <summary>
    ///   Whether a special or other episode is an extra no movie stands
    ///   for: an interview, a cinema intro, a making-of and the like.
    /// </summary>
    /// <param name="episode">The episode.</param>
    /// <param name="englishTitle">Its English title.</param>
    /// <returns><c>true</c> for an extra.</returns>
    private static bool IsExtra(IAnidbEpisode episode, string? englishTitle)
    {
        if (episode.Type is not (EpisodeType.Special or EpisodeType.Other) || string.IsNullOrEmpty(englishTitle))
            return false;

        if (englishTitle.Contains("interview", StringComparison.InvariantCultureIgnoreCase))
            return true;

        var cinema = englishTitle.StartsWith("cinema ", StringComparison.InvariantCultureIgnoreCase) ||
            englishTitle.StartsWith("theatrical ", StringComparison.InvariantCultureIgnoreCase);
        if ((cinema && (englishTitle.Contains("intro", StringComparison.InvariantCultureIgnoreCase) || englishTitle.Contains("outro", StringComparison.InvariantCultureIgnoreCase))) ||
            englishTitle.Contains("manners movie", StringComparison.InvariantCultureIgnoreCase))
            return true;

        return englishTitle.Contains("behind the scenes", StringComparison.InvariantCultureIgnoreCase) ||
            englishTitle.Contains("making of", StringComparison.InvariantCultureIgnoreCase) ||
            englishTitle.Contains("music in", StringComparison.InvariantCultureIgnoreCase) ||
            englishTitle.Contains("advance screening", StringComparison.InvariantCultureIgnoreCase) ||
            englishTitle.Contains("premiere", StringComparison.InvariantCultureIgnoreCase);
    }

    /// <summary>
    ///   The title to search for one movie of several: the anime's title and
    ///   the episode's, or the movie's number when the episode is only called
    ///   "Movie N".
    /// </summary>
    /// <param name="animeTitle">The anime's title in some language.</param>
    /// <param name="subTitle">The episode's title in the same language.</param>
    /// <param name="episodeNumber">The episode's number.</param>
    /// <param name="isGenericTitle">Whether the episode is only called "Movie N".</param>
    /// <returns>The title, or <c>null</c> when either part is missing.</returns>
    internal static string? FullMovieTitle(string? animeTitle, string? subTitle, int episodeNumber, bool isGenericTitle)
        => string.IsNullOrWhiteSpace(animeTitle) || string.IsNullOrWhiteSpace(subTitle)
            ? null
            : isGenericTitle ? $"{animeTitle} {episodeNumber}" : $"{animeTitle} {subTitle}";

    #endregion

    #region Picking

    /// <summary>
    ///   How many shows fetched for one anime have a season's episodes
    ///   fetched as well, to line them up with the anime by air date.
    /// </summary>
    internal const int AlignedCandidateCount = 3;

    /// <summary>
    ///   How many days apart an episode and a movie's release may be for the
    ///   movie to be placed on the episode by date.
    /// </summary>
    private const int FilmPlacementDays = 3;

    /// <summary>
    ///   Fetches the candidate movies one at a time and has the engine pick
    ///   one, stopping at the first that matches on both title and date.
    /// </summary>
    /// <param name="engine">The matching engine.</param>
    /// <param name="anime">The anime the movie belongs to.</param>
    /// <param name="episode">The episode standing for the movie.</param>
    /// <param name="options">What was searched for.</param>
    /// <param name="candidateIDs">The movies the searches offered, in order.</param>
    /// <param name="fetchMovie">Fetches a movie whole, or gives <c>null</c>.</param>
    /// <returns>The movies fetched, best first; empty when none could be fetched.</returns>
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

        // Nothing fetched, as through an outage, leaves nothing to tell one candidate from another.
        return fetched.Count > 0 ? engine.MatchMovies(anime, episode, fetched, options) : [];
    }

    /// <summary>
    ///   Fetches the candidate shows one at a time and has the engine pick
    ///   one, stopping at the first that matches on both title and date.
    /// </summary>
    /// <remarks>
    ///   For the first few rated shows, the season holding the anime's start
    ///   is fetched with its episodes so the engine can line them up by air
    ///   date. Otherwise a season is only fetched when the years disagree and
    ///   the engine picks one.
    /// </remarks>
    /// <param name="engine">The matching engine.</param>
    /// <param name="anime">The anime being matched.</param>
    /// <param name="options">What was searched for.</param>
    /// <param name="candidateIDs">The shows the searches offered, in order.</param>
    /// <param name="fetchShow">Fetches a show whole, or gives <c>null</c>.</param>
    /// <param name="fetchSeasonEpisodes">Fetches the episodes of a show's season, or gives <c>null</c>.</param>
    /// <param name="alignedCandidates">How many shows have a season fetched to line them up.</param>
    /// <param name="alignedShows">The shows lined up so far, shared by one anime's searches; a new set when <c>null</c>.</param>
    /// <returns>The shows fetched, best first; empty when none could be fetched.</returns>
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
        var hasFirstEpisodeDate = anime.Episodes.Any(episode => episode is { Type: EpisodeType.Episode, EpisodeNumber: 1, AirDate: not null });
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

        return fetched.Count > 0 ? engine.MatchSeries(anime, fetched, options) : [];

        static bool HasDate(MatchRating rating)
            => rating is MatchRating.DateAndTitleMatches or MatchRating.DateAndTitleKindaMatches or MatchRating.DateMatches;
    }

    /// <summary>
    ///   When the anime's regular broadcast started: its first dated regular
    ///   episode's regular date, or the anime's own.
    /// </summary>
    /// <param name="anime">The anime.</param>
    /// <returns>The date, or <c>null</c> when nothing is dated.</returns>
    internal static DateOnly? StartOf(IAnidbAnime anime)
        => anime.Episodes
            .Where(episode => episode is { Type: EpisodeType.Episode, AirDate: not null })
            .OrderBy(episode => episode.EpisodeNumber)
            .Select(episode => episode.AirDate)
            .FirstOrDefault() ?? (anime.AirDate is { IsComplete: true } airDate ? airDate.ToDateOnly() : null);

    /// <summary>
    ///   The season a date falls in: the last one begun by it, give or take
    ///   three days.
    /// </summary>
    /// <param name="seasons">The show's regular seasons.</param>
    /// <param name="date">The date.</param>
    /// <returns>The season's number, or <c>null</c> when none began by then.</returns>
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
    internal static MetadataSeriesSearchResult WithEpisodes(MetadataSeriesSearchResult candidate, int seasonNumber, IReadOnlyList<MetadataSearchResultEpisode> episodes)
        => candidate with
        {
            Seasons =
            [
                .. (candidate.Seasons ?? []).Select(season => season.SeasonNumber == seasonNumber
                    ? season with { Episodes = episodes, FirstEpisodeAiredAt = episodes.OrderBy(episode => episode.EpisodeNumber).FirstOrDefault()?.AiredAt }
                    : season),
            ],
        };

    /// <summary>
    ///   Which of the episodes a hinted movie was judged against it stands
    ///   for: the best rated, or, where several are rated alike, the only one
    ///   of them aired within three days of a release of the movie.
    /// </summary>
    /// <param name="episodes">The engine's rating against each episode, with the days it aired on.</param>
    /// <param name="releasedOn">The days the movie was released on.</param>
    /// <returns>The episode's place, and whether the movie could not be placed after all.</returns>
    /// <exception cref="ArgumentException"><paramref name="episodes"/> is empty.</exception>
    internal static (int Index, bool Unplaced) PlaceFilm(IReadOnlyList<(MatchRating Rating, IReadOnlyList<DateOnly> AiredOn)> episodes, IReadOnlyList<DateOnly> releasedOn)
    {
        if (episodes.Count is 0)
            throw new ArgumentException("A movie needs an episode to be placed on.", nameof(episodes));

        var priority = episodes.Min(episode => ShowMatchPriority(episode.Rating));
        var tied = Enumerable.Range(0, episodes.Count).Where(index => ShowMatchPriority(episodes[index].Rating) == priority).ToList();
        if (episodes.Count is 1 || (tied.Count is 1 && episodes[tied[0]].Rating is not MatchRating.None))
            return (tied[0], false);
        if (episodes[tied[0]].Rating is MatchRating.None)
            return (tied[0], true);

        // The engine rates a movie's date by its year, so same-year episodes tie; the one aired with it settles it.
        var close = tied
            .Where(index => episodes[index].AiredOn.Any(aired => releasedOn.Any(released => Math.Abs(aired.DayNumber - released.DayNumber) <= FilmPlacementDays)))
            .ToList();
        return close.Count is 1 ? (close[0], false) : (tied[0], true);
    }

    private static IReadOnlyList<DateOnly> AiredOn(IAnidbEpisode episode)
        => [.. new[] { episode.EarlyAirDate, episode.AirDate }.OfType<DateOnly>().Distinct()];

    private static IReadOnlyList<IAnidbEpisode> FilmEpisodes(IAnidbAnime anime, TmdbHint hint)
    {
        if (hint.AnidbEpisodeID is { } episodeID && anime.Episodes.FirstOrDefault(episode => episode.AnidbID == episodeID) is { } named)
            return [named];

        var regular = anime.Episodes.Where(episode => episode.Type is EpisodeType.Episode).OrderBy(episode => episode.EpisodeNumber).ToList();
        return regular.Count > 0
            ? regular
            : [.. anime.Episodes.Where(episode => episode.Type is EpisodeType.Special or EpisodeType.Other).OrderBy(episode => episode.Type).ThenBy(episode => episode.EpisodeNumber)];
    }

    // TMDB's genre tags are sparse for adult titles, so a restricted anime also takes an East Asian original language.
    private static bool IsAnimation(List<int>? genreIDs, string? originalLanguage, bool restricted)
        => (genreIDs?.Contains(AnimationGenreID) ?? false) || (restricted && originalLanguage is not null && RestrictedLanguages.Contains(originalLanguage));

    internal static void Collect(List<SearchTv> candidates, List<SearchTv> results, HashSet<int> seen, int candidateCount, bool restricted)
    {
        foreach (var result in results)
        {
            if (candidates.Count >= candidateCount)
                break;
            if (seen.Add(result.Id) && IsAnimation(result.GenreIds, result.OriginalLanguage, restricted))
                candidates.Add(result);
        }
    }

    internal static void Collect(List<SearchMovie> candidates, List<SearchMovie> results, HashSet<int> seen, int candidateCount, bool restricted)
    {
        foreach (var result in results)
        {
            if (candidates.Count >= candidateCount)
                break;
            if (seen.Add(result.Id) && IsAnimation(result.GenreIds, result.OriginalLanguage, restricted))
                candidates.Add(result);
        }
    }

    #endregion

    #region Hints

    /// <summary>
    ///   A TMDB entry the anime's other sources name.
    /// </summary>
    /// <param name="TmdbID">The show's or movie's TMDB ID.</param>
    /// <param name="IsMovie">Whether it is a movie rather than a show.</param>
    /// <param name="ImdbID">The IMDb title it was found through, or <c>null</c>.</param>
    /// <param name="NamedBy">The linked entries of other sources naming it, or <c>null</c> for the anime's own cross-source IDs.</param>
    /// <param name="AnidbEpisodeID">The AniDB episode a movie was named for, or <c>null</c>.</param>
    internal readonly record struct TmdbHint(int TmdbID, bool IsMovie, string? ImdbID = null, IReadOnlyList<MetadataGuid>? NamedBy = null, int? AnidbEpisodeID = null)
    {
        /// <summary>
        ///   The entry's identity.
        /// </summary>
        public MetadataGuid ID => IsMovie ? TmdbIds.Movie(TmdbID) : TmdbIds.Series(TmdbID);

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
    ///   The TMDB shows and movies among an anime's cross-source IDs, which
    ///   carry its AniDB resources.
    /// </summary>
    /// <param name="crossSourceIDs">The cross-source IDs.</param>
    /// <returns>Each entry once, in order.</returns>
    internal static IReadOnlyList<TmdbHint> TmdbHintsOf(IEnumerable<MetadataGuid> crossSourceIDs)
        => [
            .. crossSourceIDs
                .Select(id => TmdbIds.TryGetID(id, MetadataEntityType.Series, out var showID)
                    ? new TmdbHint(showID, false)
                    : TmdbIds.TryGetID(id, MetadataEntityType.Movie, out var movieID) ? new TmdbHint(movieID, true) : (TmdbHint?)null)
                .OfType<TmdbHint>()
                .Distinct(),
        ];

    /// <summary>
    ///   The TMDB entries the anime's links on other sources name.
    /// </summary>
    /// <param name="hints">The hints the core read from the linked entries.</param>
    /// <returns>Each show and movie once, in the core's order.</returns>
    internal static IReadOnlyList<TmdbHint> TmdbHintsOf(IEnumerable<MetadataAutoLinkHint> hints)
        => [
            .. hints
                .Select(hint => TmdbIds.TryGetID(hint.ID, MetadataEntityType.Series, out var showID)
                    ? new TmdbHint(showID, false, NamedBy: hint.NamedBy, AnidbEpisodeID: hint.AnidbEpisodeID)
                    : TmdbIds.TryGetID(hint.ID, MetadataEntityType.Movie, out var movieID)
                        ? new TmdbHint(movieID, true, NamedBy: hint.NamedBy, AnidbEpisodeID: hint.AnidbEpisodeID)
                        : (TmdbHint?)null)
                .OfType<TmdbHint>()
                .DistinctBy(hint => (hint.TmdbID, hint.IsMovie)),
        ];

    /// <summary>
    ///   The IMDb titles among an anime's cross-source IDs.
    /// </summary>
    /// <param name="crossSourceIDs">The cross-source IDs.</param>
    /// <returns>Each title ID once, in order.</returns>
    internal static IReadOnlyList<string> ImdbIDsOf(IEnumerable<MetadataGuid> crossSourceIDs)
        => [
            .. crossSourceIDs
                .Where(id => string.Equals(id.Source.Value, "imdb", StringComparison.Ordinal) && id.ID.Length > 2 && id.ID.StartsWith("tt", StringComparison.Ordinal))
                .Select(id => id.ID)
                .Distinct(StringComparer.Ordinal),
        ];

    /// <summary>
    ///   The kinds of TMDB entry a hint may be taken as for an anime: the
    ///   ones its search looks for.
    /// </summary>
    /// <param name="type">The anime's type.</param>
    /// <param name="isShortForm">Whether the anime has at most four regular episodes.</param>
    /// <returns>Whether a show, and whether a movie, may be taken.</returns>
    internal static (bool Shows, bool Movies) HintKinds(AnimeType type, bool isShortForm) => type switch
    {
        AnimeType.MusicVideo => (false, false),
        AnimeType.Movie => (false, true),
        AnimeType.OVA or AnimeType.Web when isShortForm => (true, true),
        AnimeType.Other or AnimeType.Unknown => (false, false),
        _ => (true, false),
    };

    /// <summary>
    ///   Why a hinted entry is not taken, or <c>null</c> when it
    ///   may be.
    /// </summary>
    /// <param name="hint">The hint.</param>
    /// <param name="animeType">The anime's type.</param>
    /// <param name="kindAllowed">Whether the hint is of a kind the anime's search looks for.</param>
    /// <param name="rating">The engine's rating of it.</param>
    /// <param name="filter">The engine's reason for not taking it, if any.</param>
    /// <param name="details">What the engine compared, if it said.</param>
    /// <param name="unplaced">Whether it is a movie no single episode could be told to stand for.</param>
    /// <param name="notAired">Why what it would be linked to is not searched for yet, or <c>null</c>.</param>
    /// <returns>The rejection, or <c>null</c>.</returns>
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
    ///   Why an anime, or the movie one of its episodes stands for, is not
    ///   searched for yet, as a clause.
    /// </summary>
    /// <param name="airDate">When it airs, or <c>null</c> when that is not known.</param>
    /// <param name="isMovie">Whether it is a movie standing for one episode.</param>
    /// <param name="now">The time now.</param>
    /// <param name="window">How far ahead of its air date the search looks for it.</param>
    /// <returns>The clause, or <c>null</c> when it is searched for.</returns>
    internal static string? NotAired(DateTime? airDate, bool isMovie, DateTime now, TimeSpan window)
    {
        if (AiredWithin(airDate, now, window))
            return null;

        var what = isMovie ? "the episode the film stands for" : "the anime";
        return airDate is { } date
            ? $"{what} airs on {date:yyyy-MM-dd}, more than {window.TotalDays:0} days from now"
            : $"{what} has no air date yet";
    }

    private static IReadOnlyCollection<MetadataGuid> HintedIDs(IReadOnlyList<TmdbHint> hints, bool isMovie)
        => [.. hints.Where(hint => hint.IsMovie == isMovie).Select(hint => hint.ID)];

    /// <summary>
    ///   The candidates a search offered, the hinted ones first, so the
    ///   fetching does not stop early before reaching them.
    /// </summary>
    /// <param name="candidateIDs">The candidates, in the search's order.</param>
    /// <param name="hints">The hints.</param>
    /// <param name="isMovie">Whether the candidates are movies.</param>
    /// <returns>The candidates, hinted first, otherwise in the same order.</returns>
    internal static IReadOnlyList<int> HintedFirst(IEnumerable<int> candidateIDs, IReadOnlyList<TmdbHint> hints, bool isMovie)
        => [.. candidateIDs.OrderBy(id => hints.Any(hint => hint.IsMovie == isMovie && hint.TmdbID == id) ? 0 : 1)];

    private static FoundShow Found(TmdbAutoSearchResult result, IReadOnlyList<TmdbHint> hints)
        => FoundShow.Of(result.MatchRating, (MetadataSeriesSearchResult)result.Candidate) with
        {
            IsHinted = hints.Any(hint => !hint.IsMovie && hint.TmdbID == result.TmdbID),
        };

    private static string HintNote(TmdbAutoSearchResult result, IReadOnlyList<TmdbHint> hints)
        => hints.FirstOrDefault(hint => !hint.IsMovie && hint.TmdbID == result.TmdbID) is { TmdbID: > 0 } hint ? $", {hint.Note}" : string.Empty;

    #endregion

    #region Judging

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
    /// <param name="IsHinted">Whether a hint names it.</param>
    internal readonly record struct FoundShow(MatchRating Rating, DateOnly? BeganOn, int? EpisodeCount, bool IsHinted = false)
    {
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
    ///   Rated higher, it wins. Rated alike, the hinted one wins. Against a
    ///   prequel's show agreeing on the title alone, it also wins when it
    ///   agrees on the date and the title, unless its title only loosely
    ///   agrees and it has under a third of the anime's episodes, or on the
    ///   date alone when it began with the anime and the prequel's show over
    ///   a year before.
    /// </remarks>
    /// <param name="own">The show found through the anime's own titles.</param>
    /// <param name="prequels">The show found through the prequel's titles.</param>
    /// <param name="airedOn">When the anime began.</param>
    /// <param name="episodeCount">How many regular episodes the anime has.</param>
    /// <returns><c>true</c> when the anime's own wins.</returns>
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

    /// <summary>
    ///   The engine's reason for turning a candidate down, as a rejection.
    /// </summary>
    /// <param name="reason">The engine's reason.</param>
    /// <param name="query">The title searched for.</param>
    /// <param name="details">What the engine compared, if it said.</param>
    /// <returns>The rejection, or <c>null</c> for the candidate taken.</returns>
    internal static MetadataAutoLinkRejection? Rejected(MatchRejectionReason reason, string query, string? details)
        => reason is MatchRejectionReason.None
            ? null
            : new() { Reason = reason, Details = string.IsNullOrEmpty(details) ? $"Searched for \"{query}\"." : $"Searched for \"{query}\". {details}" };

    private static string Named(TmdbAutoSearchResult result)
        => $"\"{result.Candidate.Title}\" ({result.TmdbID})";

    private static int ShowMatchPriority(MatchRating rating) => rating switch
    {
        MatchRating.UserVerified => 0,
        MatchRating.DateAndTitleMatches => 1,
        MatchRating.TitleMatches => 2,
        MatchRating.DateAndTitleKindaMatches => 3,
        MatchRating.DateMatches => 4,
        MatchRating.TitleKindaMatches => 5,
        _ => 6,
    };

    #endregion
}
