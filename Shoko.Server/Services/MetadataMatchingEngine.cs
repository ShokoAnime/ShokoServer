using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Logging;
using Shoko.Abstractions.Extensions;
using Shoko.Abstractions.Filtering.Services;
using Shoko.Abstractions.Metadata;
using Shoko.Abstractions.Metadata.Anidb;
using Shoko.Abstractions.Metadata.Containers;
using Shoko.Abstractions.Metadata.CrossReferences;
using Shoko.Abstractions.Metadata.Enums;
using Shoko.Abstractions.Metadata.Matching;
using Shoko.Abstractions.Metadata.Search;
using Shoko.Abstractions.Metadata.Services;
using Shoko.Server.Providers;
using Shoko.Server.Utilities;

using AnidbRegularAirDates = Shoko.Server.Providers.AniDB.AnidbRegularAirDates;

namespace Shoko.Server.Services;

/// <summary>
///   Judges how well a source's series, films and episodes line up with
///   AniDB's, over nothing but what it is handed.
/// </summary>
/// <remarks>
///   <see cref="EpisodeMatchStrategy.DateAndTitleWithinSeasons"/> serves
///   seasoned sources and <see cref="EpisodeMatchStrategy.DateThenNumber"/>
///   flat ones. It reads and writes nothing; which candidates to offer and
///   how a match becomes a link stay with the source.
/// </remarks>
/// <param name="logger">The logger.</param>
/// <param name="fuzzySearch">Tells titles that nearly match apart.</param>
public class MetadataMatchingEngine(ILogger<MetadataMatchingEngine> logger, IFuzzySearchService fuzzySearch) : IMetadataMatchingEngine
{
    #region Constants

    private readonly ILogger<MetadataMatchingEngine> _logger = logger;

    private readonly IFuzzySearchService _fuzzySearch = fuzzySearch;

    private static readonly Dictionary<char, char> _characterReplacementDict = new()
    {
        { '’', '\'' },
        { '”', '"' },
        { '‘', '\'' },
        { '“', '"' },
    };

    /// <summary>
    ///   AniDB episode titles that say nothing on their own, where the anime's
    ///   own English title is the better thing to search a source with.
    /// </summary>
    private static readonly HashSet<string> _titlesToSearch = new(StringComparer.InvariantCultureIgnoreCase)
    {
        "OAD",
        "OVA",
        "Short Movie",
        "Special",
        "TV Special",
        "Web",
    };

    /// <summary>
    ///   Placeholder titles that should never be searched for at all.
    /// </summary>
    private static readonly HashSet<string> _titlesToNotSearch = new(StringComparer.InvariantCultureIgnoreCase)
    {
        "Complete Movie",
        "Music Video",
    };

    /// <summary>
    ///   How many days after an anime's last episode a series may begin and
    ///   still rank as holding it, as through a special shown ahead of the
    ///   series.
    /// </summary>
    private const int MaxDaysStartedAfterEnd = 60;

    /// <summary>
    ///   How many days before an anime's start a series (or its season lining
    ///   up best) may begin and still rank as the anime, about two broadcast
    ///   seasons, so a two-cour entry holding the anime's second cour counts.
    /// </summary>
    private const int MaxDaysStartedBeforeStart = 180;

    /// <summary>
    ///   Ratings assigned with no title and no number evidence, and so the only
    ///   ones a coincidental air-date hit can put out of order.
    /// </summary>
    private static readonly HashSet<MatchRating> _weakOrderRatings =
        [MatchRating.DateMatches, MatchRating.DateKindaMatches, MatchRating.FirstAvailable];

    /// <summary>
    ///   How many days apart a candidate's episode and the anime's may have
    ///   aired and still line up, as two sources often date a late-night
    ///   broadcast a day apart.
    /// </summary>
    private const int AlignmentDayTolerance = 1;

    /// <summary>
    ///   How many different days have to line up before an alignment counts,
    ///   or every day the anime aired on when it has fewer.
    /// </summary>
    private const int MinAlignedDays = 3;

    /// <summary>
    ///   The share of the anime's dated episodes paired with a dated episode
    ///   of the season that has to line up before an alignment counts.
    /// </summary>
    private const double MinAlignedCoverage = 0.8;

    #endregion

    #region Series

    /// <summary>
    ///   Judge how well each series a search returned lines up with an anime,
    ///   and say which one to take.
    /// </summary>
    /// <remarks>
    ///   Titles first, then the regular broadcast's year (the best season's,
    ///   the entry's own or its broadcast season's), or first episodes within
    ///   three days or aligned air dates where the year fails. Ties go to an
    ///   entry whose titles carry the anime's sequel number, then one begun in
    ///   time, a hinted one, an aligned one, and the closest known episode
    ///   count.
    /// </remarks>
    /// <param name="anime">The anime being matched.</param>
    /// <param name="candidates">
    ///   What the source offered, in whatever order it offered them.
    /// </param>
    /// <param name="options">
    ///   What the source searched with and what it is like.
    /// </param>
    /// <exception cref="ArgumentNullException">
    ///   Thrown when <paramref name="anime"/> or
    ///   <paramref name="candidates"/> is <c>null</c>.
    /// </exception>
    /// <returns>
    ///   One entry per candidate, best first, at most the first one taken.
    /// </returns>
    public IReadOnlyList<SeriesMatch> MatchSeries(IAnidbAnime anime, IReadOnlyList<MetadataSeriesSearchResult> candidates, SeriesMatchOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(anime);
        ArgumentNullException.ThrowIfNull(candidates);

        if (candidates.Count is 0)
            return [];

        options ??= new();
        var queries = options.Query is { Length: > 0 } query
            ? [QueryVariants.Of(query, options.QueryLanguage is TitleLanguage.Japanese, withSubtitle: true)]
            : SafeTitles(anime)
                .Where(title => !string.IsNullOrWhiteSpace(title.Value))
                .Select(title => QueryVariants.Of(title.Value, title.Language is TitleLanguage.Japanese, withSubtitle: true, exactOnly: IsExactOnly(title)))
                .ToList();
        var (year, dateText) = SeriesDateOf(anime);
        var firstEpisodeDate = FirstEpisodeDateOf(anime);
        var animeStart = AnidbRegularAirDates.RegularStartOf(anime.AirDate, SafeEpisodes(anime)) is { IsComplete: true } regularStart
            ? regularStart.ToDateOnly()
            : firstEpisodeDate;
        var episodeCount = anime.EpisodeCounts.Episodes;
        var endedAt = anime.EndDate is { IsComplete: true } ended ? ended.ToDateOnly() : (DateOnly?)null;
        var datedEpisodes = DatedRegularEpisodes(anime);
        var animeNumber = SequelNumberOf(anime, options.Query);

        var judged = new List<Judgement<MetadataSeriesSearchResult>>(candidates.Count);
        for (var index = 0; index < candidates.Count; index++)
        {
            var candidate = candidates[index];
            var title = JudgeTitles(queries, NamesOf(candidate));

            // A source without seasons is one season to itself, whose first
            // episode aired when the entry started.
            var seasons = candidate.Seasons ??
            [
                new MetadataSearchResultSeason
                {
                    SeasonNumber = 0,
                    EpisodeCount = candidate.EpisodeCount,
                    FirstAiredAt = candidate.FirstAiredAt,
                    FirstEpisodeAiredAt = candidate.FirstAiredAt is { IsComplete: true } started ? started.ToDateOnly() : null,
                },
            ];
            // A season whose episodes aired on the anime's days is the one the
            // anime is, whatever its episode count says.
            var alignment = AlignEpisodes(datedEpisodes, seasons);
            var alignedSeason = alignment is { IsConclusive: true } ? seasons.FirstOrDefault(season => season.SeasonNumber == alignment.SeasonNumber) : null;
            var bestSeason = alignedSeason ?? seasons
                .OrderBy(season => EpisodeDifference(episodeCount, season.EpisodeCount) ?? int.MaxValue)
                .ThenBy(season => year is { } known && season.FirstAiredAt?.Year == known ? 0 : 1)
                .FirstOrDefault();
            var episodeDifference = bestSeason is null ? null : EpisodeDifference(episodeCount, bestSeason.EpisodeCount);
            var dateMatches = year is { } wanted && (
                bestSeason?.FirstAiredAt?.Year == wanted ||
                candidate.FirstAiredAt?.Year == wanted ||
                candidate.SeasonYear == wanted
            );

            // Where the years fail (a year-boundary premiere, a split cour),
            // first episodes airing within days of each other still agree.
            if (!dateMatches && bestSeason?.FirstEpisodeAiredAt is { } sourceDate && firstEpisodeDate is { } anidbDate)
                dateMatches = Math.Abs(sourceDate.DayNumber - anidbDate.DayNumber) <= 3;

            // And so do episodes aired on the same days as nearly all of the
            // anime's, which is the stronger evidence of the two.
            if (!dateMatches && alignedSeason is not null)
                dateMatches = true;

            // Separate-season sources keep the first season under the bare title,
            // so a match found only without the sequel suffix needs the dates to agree.
            if (title is TitleEvidence.ExactWithoutSuffix && !dateMatches && options.SeasonsAreSeparateEntries)
                title = TitleEvidence.Close;

            // An entry begun well after the anime ended holds it at most as a special,
            // so between two matching alike (shows sharing a title) the timely one wins.
            var startedAt = StartOf(candidate);
            var startedLate = startedAt is { } began && endedAt is { } lastAired && began.DayNumber - lastAired.DayNumber > MaxDaysStartedAfterEnd;

            // The season lining up best tells when the anime's part of a long show began,
            // and one whose episodes aired on the anime's days holds it however early.
            var seasonStart = candidate.Seasons is null
                ? startedAt
                : bestSeason?.FirstEpisodeAiredAt ?? (bestSeason?.FirstAiredAt is { IsComplete: true } seasonStarted ? seasonStarted.ToDateOnly() : null);
            var daysEarly = alignedSeason is null && seasonStart is { } seasonBegan && animeStart is { } animeBegan
                ? animeBegan.DayNumber - seasonBegan.DayNumber
                : 0;
            var candidateNumbers = NamesOf(candidate).Select(TitleVariants.SequelNumber).OfType<int>().ToHashSet();
            var filter = candidate switch
            {
                { IsRestricted: true } when !options.IncludeRestricted => MatchRejectionReason.Restricted,
                { Type: AnimeType.MusicVideo } when anime.Type is not AnimeType.MusicVideo => MatchRejectionReason.TypeMismatch,
                _ => MatchRejectionReason.None,
            };
            var candidateText = DescribeSeries(candidate, candidate.Seasons is null ? null : bestSeason);
            var alignmentText = datedEpisodes.Count > 0 && seasons.Any(season => season.Episodes is { Count: > 0 })
                ? $" {DescribeAlignment(alignment, datedEpisodes)}"
                : string.Empty;
            judged.Add(new(candidate, index, Rate(title, dateMatches), title, dateMatches, episodeDifference, candidate.Seasons is null ? null : bestSeason?.SeasonNumber, filter)
            {
                Hinted = options.HintedIDs.Contains(candidate.ID),
                FilterDetails = filter switch
                {
                    MatchRejectionReason.Restricted => "It is marked as adult, and adult entries were not allowed.",
                    MatchRejectionReason.TypeMismatch => "It is a music video, and the anime is not.",
                    _ => null,
                },
                StartedLate = startedLate
                    ? $"it began on {startedAt:yyyy-MM-dd}, more than {MaxDaysStartedAfterEnd} days after the anime ended on {endedAt:yyyy-MM-dd}"
                    : null,
                DaysStartedEarly = daysEarly > MaxDaysStartedBeforeStart ? daysEarly : 0,
                StartedEarly = daysEarly > MaxDaysStartedBeforeStart
                    ? $"it began on {seasonStart:yyyy-MM-dd}, {daysEarly} days before the anime started on {animeStart:yyyy-MM-dd}"
                    : null,
                Numbering = animeNumber is not { } wantedNumber || candidateNumbers.Count is 0 ? NumberMatch.Unknown
                    : candidateNumbers.Contains(wantedNumber) ? NumberMatch.Same
                    : NumberMatch.Different,
                NumberText = candidateNumbers.Count is 0
                    ? $"its titles carry no number, the anime's carrying {animeNumber}"
                    : $"its titles number it {string.Join(" or ", candidateNumbers.Order())}, the anime's {animeNumber}",
                Comparison = $"Compared {dateText} and {episodeCount} episodes with {candidateText}.{alignmentText}",
                Alignment = alignment,
            });
        }

        return [.. Rank(judged, byEpisodeCount: true).Select(ranked => new SeriesMatch
        {
            AnidbAnime = anime,
            Candidate = ranked.Judgement.Candidate,
            Rating = ranked.Judgement.Rating,
            Rejection = ranked.Rejection,
            SeasonNumber = ranked.Judgement.SeasonNumber,
            EpisodeAlignment = ranked.Judgement.Alignment,
            Details = ranked.Details,
        })];
    }

    #endregion

    #region Films

    /// <summary>
    ///   Judge which of the films a search returned is the one an episode
    ///   stands for.
    /// </summary>
    /// <remarks>
    ///   Titles first, then the year of any of its releases, a hinted
    ///   candidate winning a tie.
    /// </remarks>
    /// <param name="anime">The anime the film belongs to.</param>
    /// <param name="episode">The episode standing for a film.</param>
    /// <param name="candidates">
    ///   What the source offered, in whatever order it offered them.
    /// </param>
    /// <param name="options">What the source searched with.</param>
    /// <exception cref="ArgumentNullException">
    ///   Thrown when <paramref name="anime"/>, <paramref name="episode"/> or
    ///   <paramref name="candidates"/> is <c>null</c>.
    /// </exception>
    /// <exception cref="ArgumentException">
    ///   Thrown when the episode does not belong to the anime.
    /// </exception>
    /// <returns>
    ///   One entry per candidate, best first, at most the first one taken.
    /// </returns>
    public IReadOnlyList<MovieMatch> MatchMovies(IAnidbAnime anime, IAnidbEpisode episode, IReadOnlyList<MetadataMovieSearchResult> candidates, MovieMatchOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(anime);
        ArgumentNullException.ThrowIfNull(episode);
        ArgumentNullException.ThrowIfNull(candidates);

        if (episode.AnidbAnimeID != anime.AnidbID)
            throw new ArgumentException($"Episode {episode.AnidbID} does not belong to anime {anime.AnidbID}.", nameof(episode));

        if (candidates.Count is 0)
            return [];

        // A film's AniDB episode is usually "Complete Movie", so the anime's titles
        // name it; the episode's own are added for the rare named one.
        options ??= new();
        var queries = options.Query is { Length: > 0 } query
            ? [QueryVariants.Of(query, isJapanese: false, withSubtitle: false)]
            : SafeTitles(anime)
                .Concat(SafeTitles(episode).Where(title => !GenericEpisodeTitles.IsEnglishGeneric(title.Value, episode.Type, episode.EpisodeNumber)))
                .Where(title => !string.IsNullOrWhiteSpace(title.Value))
                .Select(title => QueryVariants.Of(title.Value, isJapanese: false, withSubtitle: false, exactOnly: IsExactOnly(title)))
                .ToList();
        var (year, dateText) = FilmDateOf(anime, episode);

        var judged = new List<Judgement<MetadataMovieSearchResult>>(candidates.Count);
        for (var index = 0; index < candidates.Count; index++)
        {
            var candidate = candidates[index];
            var title = JudgeTitles(queries, NamesOf(candidate));
            var dateMatches = year is { } wanted && (
                candidate.ReleasedAt?.Year == wanted ||
                candidate.OtherReleaseDates.Any(date => date.Year == wanted)
            );
            var filter = candidate.IsRestricted && !options.IncludeRestricted ? MatchRejectionReason.Restricted : MatchRejectionReason.None;
            judged.Add(new(candidate, index, Rate(title, dateMatches), title, dateMatches, null, null, filter)
            {
                Hinted = options.HintedIDs.Contains(candidate.ID),
                FilterDetails = filter is MatchRejectionReason.Restricted ? "It is marked as adult, and adult entries were not allowed." : null,
                Comparison = $"Compared {dateText} with {DescribeMovie(candidate)}.",
            });
        }

        return [.. Rank(judged, byEpisodeCount: false).Select(ranked => new MovieMatch
        {
            AnidbAnime = anime,
            AnidbEpisode = episode,
            Candidate = ranked.Judgement.Candidate,
            Rating = ranked.Judgement.Rating,
            Rejection = ranked.Rejection,
            Details = ranked.Details,
        })];
    }

    #endregion

    #region Series and film helpers

    /// <summary>
    ///   How closely a candidate's titles matched, strongest last.
    /// </summary>
    private enum TitleEvidence
    {
        None = 0,
        Close = 1,
        ExactWithoutSuffix = 2,
        Exact = 3,
    }

    /// <summary>
    ///   Whether a candidate's titles carry the anime's sequel or season
    ///   number, best first.
    /// </summary>
    private enum NumberMatch
    {
        Same = 0,
        Unknown = 1,
        Different = 2,
    }

    /// <summary>
    ///   One candidate as judged, before it is ranked.
    /// </summary>
    /// <param name="Candidate">The candidate.</param>
    /// <param name="Index">Where the source offered it.</param>
    /// <param name="Rating">How well it matched.</param>
    /// <param name="Title">How closely its titles matched.</param>
    /// <param name="DateMatches">Whether its date agreed.</param>
    /// <param name="EpisodeDifference">
    ///   How far its episode count was off, or <c>null</c> when either count
    ///   is unknown.
    /// </param>
    /// <param name="SeasonNumber">Its season that lines up best.</param>
    /// <param name="Filter">Why it may not be taken at all, if anything.</param>
    private sealed record Judgement<T>(
        T Candidate,
        int Index,
        MatchRating Rating,
        TitleEvidence Title,
        bool DateMatches,
        int? EpisodeDifference,
        int? SeasonNumber,
        MatchRejectionReason Filter
    ) where T : MetadataSearchResult
    {
        /// <summary>
        ///   What to say about the <see cref="Filter"/>, if there is one.
        /// </summary>
        public string? FilterDetails { get; init; }

        /// <summary>
        ///   When the candidate began and the anime ended, as a clause, when it
        ///   began too late to hold more than a special, or
        ///   <c>null</c>.
        /// </summary>
        public string? StartedLate { get; init; }

        /// <summary>
        ///   How many days before the anime's start the candidate (or its
        ///   season lining up best) began, when that was too long before to
        ///   be the anime, or <c>0</c>.
        /// </summary>
        public int DaysStartedEarly { get; init; }

        /// <summary>
        ///   When the candidate and the anime began, as a clause, when
        ///   <see cref="DaysStartedEarly"/> is set, or <c>null</c>.
        /// </summary>
        public string? StartedEarly { get; init; }

        /// <summary>
        ///   Whether its titles carry the anime's sequel or season number.
        /// </summary>
        public NumberMatch Numbering { get; init; } = NumberMatch.Unknown;

        /// <summary>
        ///   The numbers its titles and the anime's carry, as a clause.
        /// </summary>
        public string NumberText { get; init; } = string.Empty;

        /// <summary>
        ///   What was compared, as a sentence.
        /// </summary>
        public string Comparison { get; init; } = string.Empty;

        /// <summary>
        ///   Whether another source names the candidate as the anime's own,
        ///   which settles a tie between candidates rated alike.
        /// </summary>
        public bool Hinted { get; init; }

        /// <summary>
        ///   Where its episodes line up with the anime's by their air dates, if
        ///   anywhere. A conclusive one settles a tie the hints leave.
        /// </summary>
        public EpisodeAlignment? Alignment { get; init; }

        /// <summary>
        ///   Whether its episodes line up with the anime's conclusively.
        /// </summary>
        public bool IsAligned => Alignment is { IsConclusive: true };
    }

    /// <summary>
    ///   One title and the shorter forms it is judged by.
    /// </summary>
    /// <param name="Title">The title as it is.</param>
    /// <param name="WithoutSuffix">
    ///   The title without its sequel suffix, which may match exactly.
    /// </param>
    /// <param name="WithoutSubtitle">
    ///   The title without its subtitle, which only ever matches closely.
    /// </param>
    /// <param name="ExactOnly">
    ///   Whether the title only counts when it matches exactly as it is,
    ///   never closely and never without its suffix.
    /// </param>
    private sealed record QueryVariants(string Title, string? WithoutSuffix, string? WithoutSubtitle, bool ExactOnly = false)
    {
        /// <summary>
        ///   A title and its shorter forms.
        /// </summary>
        /// <param name="title">The title.</param>
        /// <param name="isJapanese">Whether the title is written in Japanese.</param>
        /// <param name="withSubtitle">Whether to cut the subtitle off as well.</param>
        /// <param name="exactOnly">Whether the title only counts when it matches exactly as it is.</param>
        /// <returns>The forms.</returns>
        public static QueryVariants Of(string title, bool isJapanese, bool withSubtitle, bool exactOnly = false)
        {
            if (exactOnly)
                return new(title, null, null, ExactOnly: true);

            var withoutSuffix = TitleVariants.WithoutSequelSuffix(title);
            var withoutSubtitle = withSubtitle ? TitleVariants.WithoutSubtitle(withoutSuffix ?? title, isJapanese) : null;
            return new(title, string.IsNullOrEmpty(withoutSuffix) ? null : withoutSuffix, string.IsNullOrEmpty(withoutSubtitle) ? null : withoutSubtitle);
        }
    }

    /// <summary>
    ///   Whether one of the anime's own titles only counts when it matches a
    ///   name exactly: a short title or a synonym, which are often a word or
    ///   two that begins, or is spelt nearly like, the name of another work.
    /// </summary>
    /// <param name="title">The title.</param>
    /// <returns><c>true</c> when a close match of it counts for nothing.</returns>
    private static bool IsExactOnly(ITitle title)
        => title.Type is not (TitleType.Main or TitleType.Official or TitleType.None);

    /// <summary>
    ///   Where a rating sits when choosing between candidates, lower first.
    /// </summary>
    /// <remarks>
    ///   Not <see cref="MatchRatingExtensions"/>' score: when choosing a
    ///   series or a film a title agreeing on its own beats a date doing so,
    ///   a search by title offering plenty of entries from the same year.
    /// </remarks>
    /// <param name="rating">The rating.</param>
    /// <returns>Its priority.</returns>
    internal static int Priority(MatchRating rating) => rating switch
    {
        MatchRating.UserVerified => 0,
        MatchRating.DateAndTitleMatches => 1,
        MatchRating.TitleMatches => 2,
        MatchRating.DateAndTitleKindaMatches => 3,
        MatchRating.DateMatches => 4,
        MatchRating.TitleKindaMatches => 5,
        _ => 6,
    };

    private static MatchRating Rate(TitleEvidence title, bool dateMatches)
        => (title >= TitleEvidence.ExactWithoutSuffix, title is TitleEvidence.Close, dateMatches) switch
        {
            (true, _, true) => MatchRating.DateAndTitleMatches,
            (true, _, false) => MatchRating.TitleMatches,
            (_, true, true) => MatchRating.DateAndTitleKindaMatches,
            (_, _, true) => MatchRating.DateMatches,
            (_, true, false) => MatchRating.TitleKindaMatches,
            _ => MatchRating.None,
        };

    /// <summary>
    ///   Puts the judged candidates best first and says why each one after
    ///   the first, and the first when nothing about it agreed, was not
    ///   taken.
    /// </summary>
    /// <remarks>
    ///   Between candidates rated alike: the anime's sequel number, then
    ///   beginning neither after the anime ended nor long before it began
    ///   (closest first), a hint, aligned episodes, the episode count and the
    ///   source's order. An unknown episode count takes the best known one
    ///   of its tie, so it neither wins nor loses on the count.
    /// </remarks>
    /// <param name="judged">The candidates as judged, in the source's order.</param>
    /// <param name="byEpisodeCount">
    ///   Whether the episode count settles a tie before the source's order.
    /// </param>
    /// <returns>
    ///   Each candidate, best first, with why it was not taken and what was
    ///   compared.
    /// </returns>
    private static IEnumerable<(Judgement<T> Judgement, MatchRejectionReason Rejection, string? Details)> Rank<T>(List<Judgement<T>> judged, bool byEpisodeCount)
        where T : MetadataSearchResult
    {
        var countKeys = new int[judged.Count];
        if (byEpisodeCount)
        {
            foreach (var tie in judged.GroupBy(TieOf))
            {
                var best = tie.Min(judgement => judgement.EpisodeDifference) ?? 0;
                foreach (var judgement in tie)
                    countKeys[judgement.Index] = judgement.EpisodeDifference ?? best;
            }
        }

        var ordered = judged
            .OrderBy(judgement => judgement.Filter is not MatchRejectionReason.None)
            .ThenBy(judgement => Priority(judgement.Rating))
            .ThenBy(judgement => judgement.Numbering)
            .ThenBy(judgement => judgement.StartedLate is not null)
            .ThenBy(judgement => judgement.DaysStartedEarly)
            .ThenBy(judgement => !judgement.Hinted)
            .ThenBy(judgement => !judgement.IsAligned)
            .ThenBy(judgement => countKeys[judgement.Index])
            .ThenBy(judgement => judgement.Index)
            .ToList();
        var winner = ordered[0] is { Filter: MatchRejectionReason.None, Rating: not MatchRating.None } first ? first : null;
        var taken = winner is null ? null : $"\"{winner.Candidate.Title}\" ({winner.Candidate.ID.ID}), rated {winner.Rating}";
        foreach (var judgement in ordered)
        {
            if (ReferenceEquals(judgement, winner))
                yield return (judgement, MatchRejectionReason.None, null);
            else if (judgement.Filter is not MatchRejectionReason.None)
                yield return (judgement, judgement.Filter, judgement.FilterDetails);
            else if (winner is null || judgement.Rating is MatchRating.None)
                yield return (judgement, MatchRejectionReason.TitleMismatch, $"Neither its titles nor its date matched. {judgement.Comparison}");
            else if (Priority(judgement.Rating) == Priority(winner.Rating))
                yield return ExplainTie(judgement, winner, $"Rated {judgement.Rating} like {taken}");
            else if (Strength(judgement.Title) < Strength(winner.Title))
                yield return (judgement, MatchRejectionReason.TitleMismatch, $"Rated {judgement.Rating}, its titles matching less closely than those of {taken}. {judgement.Comparison}");
            else if (winner.DateMatches && !judgement.DateMatches)
                yield return (judgement, MatchRejectionReason.DateMismatch, $"Rated {judgement.Rating}, its titles matching as well as {taken}, whose date agreed. {judgement.Comparison}");
            else
                yield return (judgement, MatchRejectionReason.Outranked, $"Rated {judgement.Rating}, below {taken}. {judgement.Comparison}");
        }

        // Everything that settles a tie before the episode count.
        static (bool, int, NumberMatch, bool, int, bool, bool) TieOf(Judgement<T> judgement)
            => (
                judgement.Filter is not MatchRejectionReason.None,
                Priority(judgement.Rating),
                judgement.Numbering,
                judgement.StartedLate is not null,
                judgement.DaysStartedEarly,
                judgement.Hinted,
                judgement.IsAligned
            );

        // The first tie-break, in the order above, the one taken won on.
        (Judgement<T>, MatchRejectionReason, string?) ExplainTie(Judgement<T> loser, Judgement<T> chosen, string rated)
        {
            if (loser.Numbering != chosen.Numbering)
            {
                var chosenTitles = chosen.Numbering is NumberMatch.Same ? "whose titles carry the anime's number" : "whose titles carry no other number";
                return (loser, MatchRejectionReason.TitleMismatch, $"{rated}, {chosenTitles}, where {loser.NumberText}. {loser.Comparison}");
            }

            if (loser.StartedLate is { } late && chosen.StartedLate is null)
                return (loser, MatchRejectionReason.DateMismatch, $"{rated}, but {late}, where the one taken did not. {loser.Comparison}");

            if (loser.DaysStartedEarly != chosen.DaysStartedEarly)
            {
                var than = chosen.DaysStartedEarly is 0 ? "where the one taken did not begin that early" : "earlier than the one taken";
                return (loser, MatchRejectionReason.DateMismatch, $"{rated}, but {loser.StartedEarly}, {than}. {loser.Comparison}");
            }

            if (chosen.Hinted && !loser.Hinted)
                return (loser, MatchRejectionReason.Outranked, $"{rated}, which another source names as the anime's own. {loser.Comparison}");

            if (chosen.IsAligned && !loser.IsAligned)
                return (loser, MatchRejectionReason.DateMismatch, $"{rated}, whose episodes aired on the anime's days. {loser.Comparison}");

            if (countKeys[loser.Index] != countKeys[chosen.Index])
            {
                var closer = chosen.EpisodeDifference is null
                    ? "whose episode count is unknown, and another candidate's is closer than its own"
                    : "whose episode count is closer";
                return (loser, MatchRejectionReason.EpisodeCountMismatch, $"{rated}, {closer}. {loser.Comparison}");
            }

            return (loser, MatchRejectionReason.Outranked, $"{rated}, which came first. {loser.Comparison}");
        }

        static int Strength(TitleEvidence title) => title switch
        {
            TitleEvidence.Exact or TitleEvidence.ExactWithoutSuffix => 2,
            TitleEvidence.Close => 1,
            _ => 0,
        };
    }

    /// <summary>
    ///   How closely any of the titles matches any of a candidate's names:
    ///   exactly once both are folded, or closely by a prefix on a word
    ///   boundary or a near miss in spelling.
    /// </summary>
    /// <param name="queries">The titles to try, each with its shorter forms.</param>
    /// <param name="names">The candidate's names.</param>
    /// <returns>The strongest evidence any title gave.</returns>
    private TitleEvidence JudgeTitles(IReadOnlyList<QueryVariants> queries, IReadOnlySet<string> names)
    {
        if (names.Count is 0)
            return TitleEvidence.None;

        var best = TitleEvidence.None;
        foreach (var query in queries)
        {
            if (ExactMatchesAnyName(query.Title, names))
                return TitleEvidence.Exact;

            if (best < TitleEvidence.ExactWithoutSuffix && query.WithoutSuffix is { } withoutSuffix && ExactMatchesAnyName(withoutSuffix, names))
            {
                best = TitleEvidence.ExactWithoutSuffix;
                continue;
            }

            if (best < TitleEvidence.Close && !query.ExactOnly && (IsClose(query.Title, names) || (query.WithoutSuffix is { } shorter && IsClose(shorter, names)) || (query.WithoutSubtitle is { } shortest && IsClose(shortest, names))))
                best = TitleEvidence.Close;
        }

        return best;
    }

    private bool IsClose(string query, IReadOnlySet<string> names)
        => _fuzzySearch.FuzzyScoreAnyName(query, names) is { isNotExact: true } || PrefixMatchesAnyName(query, names);

    // Equal once folded, also without unspaced-script spaces or decorative marks (middle dot, CJK bracket,
    // dash); a '?' or an apostrophe may be all that sets a sequel apart, so those still count.
    private static bool ExactMatchesAnyName(string query, IReadOnlySet<string> names)
    {
        var normalizedQuery = SeriesSearch.NormalizeForIndex(query);
        if (string.IsNullOrWhiteSpace(normalizedQuery))
            return false;

        var joinedQuery = SeriesSearch.JoinUnspacedScripts(normalizedQuery);
        var bareQuery = SeriesSearch.JoinUnspacedScripts(SeriesSearch.WithoutDecorativeMarks(normalizedQuery));
        return names.Any(name =>
        {
            var normalizedName = SeriesSearch.NormalizeForIndex(name);
            return string.Equals(normalizedQuery, normalizedName, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(joinedQuery, SeriesSearch.JoinUnspacedScripts(normalizedName), StringComparison.OrdinalIgnoreCase) ||
                (bareQuery.Length > 0 && string.Equals(bareQuery, SeriesSearch.JoinUnspacedScripts(SeriesSearch.WithoutDecorativeMarks(normalizedName)), StringComparison.OrdinalIgnoreCase));
        });
    }

    // Whole words only: the query has to end where a word of the name does.
    private static bool PrefixMatchesAnyName(string query, IReadOnlySet<string> names)
    {
        var normalizedQuery = SeriesSearch.NormalizeForIndex(query);
        if (string.IsNullOrWhiteSpace(normalizedQuery))
            return false;

        return names
            .Select(SeriesSearch.NormalizeForIndex)
            .Any(name => name.StartsWith(normalizedQuery, StringComparison.OrdinalIgnoreCase) &&
                (name.Length == normalizedQuery.Length || name[normalizedQuery.Length] == ' '));
    }

    /// <summary>
    ///   Every name a candidate goes by, each once.
    /// </summary>
    /// <param name="candidate">The candidate.</param>
    /// <returns>Its names.</returns>
    private static HashSet<string> NamesOf(MetadataSearchResult candidate)
        => [.. new[] { candidate.Title, candidate.OriginalTitle }
            .Concat(candidate.AlternateTitles ?? [])
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Select(name => name!)];

    /// <summary>
    ///   How far a candidate's episode count is off the anime's.
    /// </summary>
    /// <param name="anidbCount">The anime's episode count, <c>0</c> when unknown.</param>
    /// <param name="sourceCount">The candidate's, <c>null</c> or <c>0</c> when unknown.</param>
    /// <returns>The difference, or <c>null</c> when either count is unknown.</returns>
    private static int? EpisodeDifference(int anidbCount, int? sourceCount)
        => anidbCount > 0 && sourceCount is > 0 ? Math.Abs(anidbCount - sourceCount.Value) : null;

    /// <summary>
    ///   The year an anime's regular broadcast started, falling back on its
    ///   second episode (or its first, when it has only one) for an anime
    ///   AniDB has not dated.
    /// </summary>
    /// <param name="anime">The anime.</param>
    /// <returns>
    ///   The year, or <c>null</c> when nothing is dated, and the
    ///   date it was read from, in words.
    /// </returns>
    private static (int? Year, string Text) SeriesDateOf(IAnidbAnime anime)
    {
        var episodes = SafeEpisodes(anime);
        if (AnidbRegularAirDates.RegularStartOf(anime.AirDate, episodes) is { } regular)
            return (regular.Year, regular != anime.AirDate
                ? $"the anime's regular broadcast from {regular}" + (anime.AirDate is { } stored ? $" (stored as {stored})" : string.Empty)
                : $"the anime's start on {regular}");

        var secondEpisode = episodes
            .Where(episode => episode.Type is EpisodeType.Episode)
            .OrderBy(episode => episode.EpisodeNumber)
            .Take(2)
            .LastOrDefault();
        if (secondEpisode?.AirDate is { } episodeAirDate)
            return (episodeAirDate.Year, secondEpisode.EarlyAirDate is { } early
                ? $"the regular broadcast of episode {secondEpisode.EpisodeNumber} on {episodeAirDate:yyyy-MM-dd} (stored as {early:yyyy-MM-dd})"
                : $"episode {secondEpisode.EpisodeNumber} airing on {episodeAirDate:yyyy-MM-dd}");
        return (null, "the anime, undated,");
    }

    /// <summary>
    ///   The sequel or season number an anime goes by: the highest its query
    ///   and its own full titles carry, as a search by its prequel's title says
    ///   nothing of the anime's own number.
    /// </summary>
    /// <param name="anime">The anime.</param>
    /// <param name="query">The title the source was searched with, if any.</param>
    /// <returns>The number, or <c>null</c> when no title tells.</returns>
    private static int? SequelNumberOf(IAnidbAnime anime, string? query)
        => SafeTitles(anime)
            .Where(title => !IsExactOnly(title))
            .Select(title => title.Value)
            .Append(query)
            .Select(TitleVariants.SequelNumber)
            .Max();

    /// <summary>
    ///   When a candidate series began: its own date, or else its earliest
    ///   season's, where the day is known.
    /// </summary>
    /// <param name="candidate">The candidate.</param>
    /// <returns>The date, or <c>null</c> when no full date is known.</returns>
    private static DateOnly? StartOf(MetadataSeriesSearchResult candidate)
    {
        if (candidate.FirstAiredAt is { IsComplete: true } started)
            return started.ToDateOnly();

        return (candidate.Seasons ?? [])
            .Select(season => season.FirstAiredAt is { IsComplete: true } seasonStarted ? seasonStarted.ToDateOnly() : (DateOnly?)null)
            .Where(date => date.HasValue)
            .Min();
    }

    /// <summary>
    ///   A candidate series' dates and episode count, in words.
    /// </summary>
    /// <param name="candidate">The candidate.</param>
    /// <param name="bestSeason">Its season lining up best, if it has seasons.</param>
    /// <returns>The words.</returns>
    private static string DescribeSeries(MetadataSeriesSearchResult candidate, MetadataSearchResultSeason? bestSeason)
    {
        var started = candidate.FirstAiredAt is { } aired ? $"its start on {aired}" : "it, undated,";
        if (bestSeason is not null)
            return $"{started} and its closest season {bestSeason.SeasonNumber}" +
                (bestSeason.FirstAiredAt is { } seasonAired ? $" from {seasonAired}" : string.Empty) +
                (bestSeason.EpisodeCount is { } seasonCount ? $" with {seasonCount} episodes" : " of unknown length");
        return candidate.EpisodeCount is { } count ? $"{started} and {count} episodes" : $"{started}, of unknown length";
    }

    /// <summary>
    ///   A candidate film's release dates, in words.
    /// </summary>
    /// <param name="candidate">The candidate.</param>
    /// <returns>The words.</returns>
    private static string DescribeMovie(MetadataMovieSearchResult candidate)
    {
        var released = candidate.ReleasedAt is { } date ? $"its release on {date}" : "it, undated";
        var otherYears = candidate.OtherReleaseDates
            .Select(other => other.Year)
            .Where(otherYear => otherYear != candidate.ReleasedAt?.Year)
            .Distinct()
            .Order()
            .ToList();
        return otherYears.Count > 0 ? $"{released} (and other releases in {string.Join(", ", otherYears)})" : released;
    }

    /// <summary>
    ///   When the anime's first normal episode was regularly broadcast.
    /// </summary>
    /// <param name="anime">The anime.</param>
    /// <returns>The date, or <c>null</c> when it is not known.</returns>
    private static DateOnly? FirstEpisodeDateOf(IAnidbAnime anime)
        => SafeEpisodes(anime)
            .Where(episode => episode.Type is EpisodeType.Episode && episode.EpisodeNumber is 1)
            .Select(episode => episode.AirDate)
            .FirstOrDefault();

    /// <summary>
    ///   The year a film came out: the anime's when the film is its only
    ///   entry or its complete film, and the episode's otherwise, regular
    ///   broadcast dates first.
    /// </summary>
    /// <param name="anime">The anime the film belongs to.</param>
    /// <param name="episode">The episode standing for the film.</param>
    /// <returns>
    ///   The year, or <c>null</c> when nothing is dated, and where
    ///   it was read from, in words.
    /// </returns>
    private static (int? Year, string Text) FilmDateOf(IAnidbAnime anime, IAnidbEpisode episode)
    {
        var films = SafeEpisodes(anime)
            .Where(film => film.Type is EpisodeType.Episode or EpisodeType.Special or EpisodeType.Other)
            .OrderBy(film => film.Type)
            .ThenBy(film => film.EpisodeNumber)
            .ToList();
        var first = films.FirstOrDefault();
        var isAnimeWide = films.Count is 1 ||
            SafeTitles(episode).Any(title => title.Value.Contains("Complete Movie", StringComparison.InvariantCultureIgnoreCase));
        (int? Year, string From) found = (null, string.Empty);
        var animeStart = AnidbRegularAirDates.RegularStartOf(anime.AirDate, films);
        if (isAnimeWide)
        {
            if (animeStart is { } regular)
                found = (regular.Year, regular != anime.AirDate ? "the anime's regular broadcast" : "the anime's start");
            else if (first?.AirDate is { } firstAirDate)
                found = (firstAirDate.Year, first.EarlyAirDate is not null ? "its first episode's regular broadcast" : "its first episode's air date");
        }
        else
        {
            if (episode.AirDate is { } airDate)
                found = (airDate.Year, episode.EarlyAirDate is not null ? "the episode's regular broadcast" : "the episode's air date");
            else if (animeStart is { } regular)
                found = (regular.Year, regular != anime.AirDate ? "the anime's regular broadcast" : "the anime's stored start");
        }

        var (year, from) = found;
        return year is { } known ? (known, $"the year {known} (from {from})") : (null, "no year");
    }

    private static IReadOnlyList<IAnidbEpisode> SafeEpisodes(IAnidbAnime anime)
    {
        try
        {
            return anime.Episodes ?? [];
        }
        catch
        {
            return [];
        }
    }

    #endregion

    #region Episode alignment

    /// <summary>
    ///   One of the anime's regular episodes with its regular broadcast date.
    /// </summary>
    /// <param name="Number">The episode's number.</param>
    /// <param name="Date">When it was regularly broadcast.</param>
    private readonly record struct DatedEpisode(int Number, DateOnly Date);

    /// <summary>
    ///   Every regular episode of the anime with a regular broadcast date,
    ///   each number once, in order.
    /// </summary>
    /// <param name="anime">The anime.</param>
    /// <returns>The episodes.</returns>
    private static IReadOnlyList<DatedEpisode> DatedRegularEpisodes(IAnidbAnime anime)
        => [
            .. SafeEpisodes(anime)
                .Where(episode => episode.Type is EpisodeType.Episode && episode.AirDate is not null)
                .GroupBy(episode => episode.EpisodeNumber)
                .Select(group => new DatedEpisode(group.Key, group.First().AirDate!.Value))
                .OrderBy(episode => episode.Number),
        ];

    /// <summary>
    ///   Lines the episodes a candidate sent with its seasons up with the
    ///   anime's by their air dates.
    /// </summary>
    /// <remarks>
    ///   Each episode aired within a day of one of the anime's proposes a
    ///   season and numbering offset, checked over pairs dated on both sides.
    ///   Most distinct days wins, then most on the very day, then coverage:
    ///   days keep a double episode from outvoting the rest, and coverage
    ///   keeps a long show from winning by its length.
    /// </remarks>
    /// <param name="anidb">The anime's dated regular episodes.</param>
    /// <param name="seasons">The candidate's seasons.</param>
    /// <returns>The best alignment, or <c>null</c> when no episode aired on one of the anime's days.</returns>
    private static EpisodeAlignment? AlignEpisodes(IReadOnlyList<DatedEpisode> anidb, IReadOnlyList<MetadataSearchResultSeason> seasons)
    {
        if (anidb.Count is 0)
            return null;

        var byDay = anidb
            .GroupBy(episode => episode.Date.DayNumber)
            .ToDictionary(group => group.Key, group => group.ToList());
        var minDays = Math.Min(MinAlignedDays, byDay.Count);
        (EpisodeAlignment Alignment, int ExactDays)? best = null;
        foreach (var season in seasons)
        {
            if (season.Episodes is not { Count: > 0 } episodes)
                continue;

            var dated = episodes
                .Where(episode => episode.AiredAt is not null)
                .GroupBy(episode => episode.EpisodeNumber)
                .ToDictionary(group => group.Key, group => group.First().AiredAt!.Value);
            if (dated.Count is 0)
                continue;

            var offsets = new HashSet<int>();
            foreach (var (number, airedAt) in dated)
            {
                for (var delta = -AlignmentDayTolerance; delta <= AlignmentDayTolerance; delta++)
                {
                    if (byDay.TryGetValue(airedAt.DayNumber + delta, out var sameDay))
                        offsets.UnionWith(sameDay.Select(episode => number - episode.Number));
                }
            }

            foreach (var offset in offsets)
            {
                // Only an episode dated on both sides can tell for or against.
                var stretch = anidb
                    .Where(episode => dated.ContainsKey(episode.Number + offset))
                    .Select(episode => (Episode: episode, Apart: Math.Abs(dated[episode.Number + offset].DayNumber - episode.Date.DayNumber)))
                    .ToList();
                var matched = stretch.Where(pair => pair.Apart <= AlignmentDayTolerance).ToList();
                var days = matched.Select(pair => pair.Episode.Date).Distinct().Count();
                var exactDays = matched.Where(pair => pair.Apart is 0).Select(pair => pair.Episode.Date).Distinct().Count();
                var alignment = new EpisodeAlignment
                {
                    SeasonNumber = season.SeasonNumber,
                    Offset = offset,
                    MatchedEpisodes = matched.Count,
                    DatedEpisodes = stretch.Count,
                    MatchedDays = days,
                    IsConclusive = days >= minDays && stretch.Count > 0 && (double)matched.Count / stretch.Count >= MinAlignedCoverage,
                };
                if (best is not { } current ||
                    days > current.Alignment.MatchedDays ||
                    (days == current.Alignment.MatchedDays && exactDays > current.ExactDays) ||
                    (days == current.Alignment.MatchedDays && exactDays == current.ExactDays && alignment.Coverage > current.Alignment.Coverage))
                    best = (alignment, exactDays);
            }
        }

        return best?.Alignment;
    }

    /// <summary>
    ///   How a candidate's episodes lined up with the anime's, as a sentence.
    /// </summary>
    /// <param name="alignment">The best alignment, if there was one.</param>
    /// <param name="anidb">The anime's dated regular episodes.</param>
    /// <returns>The sentence.</returns>
    private static string DescribeAlignment(EpisodeAlignment? alignment, IReadOnlyList<DatedEpisode> anidb)
    {
        if (alignment is null)
            return "None of its listed episodes aired within a day of the anime's.";

        var from = anidb
            .Where(episode => episode.Number + alignment.Offset >= 1)
            .Select(episode => episode.Number + alignment.Offset)
            .DefaultIfEmpty(1 + alignment.Offset)
            .Min();
        var placed = $"{alignment.MatchedEpisodes} of {alignment.DatedEpisodes} dated episodes aired the same day, " +
            $"season {alignment.SeasonNumber} from episode {from}";
        return alignment.IsConclusive
            ? $"{placed}."
            : $"Only {placed}, too few to count.";
    }

    #endregion

    #region Entry point

    /// <inheritdoc/>
    /// <exception cref="ArgumentNullException">
    ///   Either list is <c>null</c>.
    /// </exception>
    public IReadOnlyList<EpisodeMatch> MatchEpisodes(
        IReadOnlyList<IAnidbEpisode> anidbEpisodes,
        IReadOnlyList<IEpisode> providerEpisodes,
        IReadOnlyList<IMetadataEpisodeCrossReference>? existing = null,
        EpisodeMatchOptions? options = null
    )
    {
        ArgumentNullException.ThrowIfNull(anidbEpisodes);
        ArgumentNullException.ThrowIfNull(providerEpisodes);

        var existingByEpisode = existing?
            .GroupBy(crossReference => crossReference.AnidbEpisodeID)
            .ToDictionary(group => group.Key, group => (IReadOnlyList<IMetadataEpisodeCrossReference>)[.. group]);

        options ??= new EpisodeMatchOptions();
        var strategy = options.Strategy is EpisodeMatchStrategy.Auto ? SelectStrategy(providerEpisodes) : options.Strategy;

        var anidb = anidbEpisodes
            .Where(episode => episode.Type is EpisodeType.Episode or EpisodeType.Special)
            .Where(episode => options.IncludeSpecials || episode.Type is not EpisodeType.Special)
            .OrderBy(episode => episode.Type)
            .ThenBy(episode => episode.EpisodeNumber)
            .ToList();
        if (anidb.Count == 0)
            return [];

        var context = BuildContext(strategy, anidb, providerEpisodes);
        _logger.LogTrace(
            "Matching {AnidbCount} AniDB episodes against {ProviderCount} provider episodes. (Strategy: {Strategy}, Use Existing: {UseExisting}, Include Specials: {IncludeSpecials})",
            anidb.Count, providerEpisodes.Count, strategy, existingByEpisode is not null, options.IncludeSpecials);

        var firstPass = new List<Pairing>();
        foreach (var pairing in context.Pairings.ToList())
        {
            // A person's link is honoured even on a hidden episode; hiding only
            // stops a new search and drops an automatic link.
            var isHidden = IsHidden(pairing.Anidb);
            if (existingByEpisode is not null && TryUseExisting(pairing, context, existingByEpisode, isHidden))
                continue;

            // Otherwise a hidden episode is an explicit miss, not linked to
            // whatever happens to be free.
            if (isHidden)
            {
                _logger.LogTrace("Skipping hidden episode. (AniDB ID: {AnidbEpisodeID})", pairing.Anidb.AnidbID);
                pairing.IsFixed = true;
                continue;
            }

            firstPass.Add(pairing);
        }

        // Pass 1 takes only the strategy's strongest rating, so certain episodes
        // claim their candidate before weaker ones compete for it.
        var secondPass = new List<Pairing>();
        var thirdPass = new List<Pairing>();
        var fourthPass = new List<Pairing>();
        var bestRating = strategy is EpisodeMatchStrategy.DateAndTitleWithinSeasons
            ? MatchRating.DateAndTitleMatches
            : MatchRating.DateAndNumberMatches;
        var secondRating = strategy is EpisodeMatchStrategy.DateAndTitleWithinSeasons
            ? MatchRating.TitleMatches
            : MatchRating.DateMatches;
        if (firstPass.Count > 0)
            RunPass(1, firstPass, rating => rating == bestRating, secondPass, context);

        if (secondPass.Count > 0)
            RunPass(2, secondPass, rating => rating == secondRating, thirdPass, context);

        // Flat sources place episodes without date evidence on the offset the
        // date-matched anchors agree on; seasoned ones have seasons to scope the guess.
        if (thirdPass.Count > 0 && strategy is EpisodeMatchStrategy.DateThenNumber)
            thirdPass = RunOffsetPass(thirdPass, context);

        if (thirdPass.Count > 0)
            RunPass(3, thirdPass, rating => rating is not MatchRating.FirstAvailable and not MatchRating.None, fourthPass, context);

        // Everything is accepted here, a miss included, so nothing overflows
        // past this pass.
        if (fourthPass.Count > 0)
            RunPass(4, fourthPass, _ => true, [], context);

        ReconcileOrderInversions(context.Pairings);

        // An episode settled outside the candidates is left out, so a caller
        // saving the result keeps its links as they are.
        return context.Pairings
            .Where(pairing => !pairing.IsLeftAlone)
            .OrderBy(pairing => pairing.Anidb.Type)
            .ThenBy(pairing => pairing.Anidb.EpisodeNumber)
            .ThenBy(pairing => pairing.Ordering)
            .Select(pairing => new EpisodeMatch
            {
                AnidbEpisode = pairing.Anidb,
                Candidate = pairing.Provider,
                Rating = pairing.Provider is null ? MatchRating.None : pairing.Rating,
                Ordering = pairing.Ordering,
            })
            .ToList();
    }

    #endregion

    #region Strategy selection

    /// <summary>
    ///   Picks a strategy from the shape of the source's episodes.
    /// </summary>
    /// <remarks>
    ///   Any episode with a <see cref="IEpisode.SeasonNumber"/> means
    ///   <see cref="EpisodeMatchStrategy.DateAndTitleWithinSeasons"/>, else
    ///   <see cref="EpisodeMatchStrategy.DateThenNumber"/> (an empty source
    ///   too). Season 0 is a real season, so the test is <c>HasValue</c>.
    /// </remarks>
    /// <param name="providerEpisodes">The source's episodes.</param>
    /// <returns>The strategy to match with.</returns>
    private static EpisodeMatchStrategy SelectStrategy(IReadOnlyList<IEpisode> providerEpisodes)
        => providerEpisodes.Any(episode => episode.SeasonNumber.HasValue)
            ? EpisodeMatchStrategy.DateAndTitleWithinSeasons
            : EpisodeMatchStrategy.DateThenNumber;

    #endregion

    #region Context

    // Mutable on purpose: the passes claim and re-claim candidates, and the
    // order reconciliation swaps them outright.
    private sealed class Pairing(IAnidbEpisode anidb)
    {
        public IAnidbEpisode Anidb { get; } = anidb;

        public IEpisode? Provider { get; set; }

        public MatchRating Rating { get; set; } = MatchRating.None;

        public int Ordering { get; set; }

        /// <summary>
        ///   Set for a pairing the matcher did not arrive at itself (a kept
        ///   link or a hidden episode), which the order reconciliation skips.
        /// </summary>
        public bool IsFixed { get; set; }

        /// <summary>
        ///   Set for a pairing an existing link settles outside the candidates
        ///   (a person's link to nothing, or one to an entry not offered). It
        ///   still counts towards the seasons and anchors, but not the result.
        /// </summary>
        public bool IsLeftAlone { get; set; }
    }

    // NormalPool and SpecialPool are one list when there is one pool (a flat
    // source or an OVA), so a claim from either is taken from both.
    private sealed class MatchContext
    {
        public required EpisodeMatchStrategy Strategy { get; init; }

        public required AnimeType? AnimeType { get; init; }

        public required bool IsOva { get; init; }

        public required List<Pairing> Pairings { get; init; }

        public required IReadOnlyDictionary<(EpisodeType, int), IAnidbEpisode> ByTypeNumber { get; init; }

        public required List<IEpisode> NormalPool { get; set; }

        public required List<IEpisode> SpecialPool { get; set; }

        public Dictionary<int, Pairing> PrimaryByAnidbID { get; } = [];

        /// <summary>
        ///   Each candidate's searchable titles, worked out once per run.
        ///   Concurrent, since a search reads its candidates in parallel.
        /// </summary>
        public ConcurrentDictionary<IEpisode, IReadOnlyList<string>> TitleCandidates { get; } = new(ReferenceEqualityComparer.Instance);

        public List<IEpisode> GetPool(IAnidbEpisode episode)
            => IsSpecialEpisode(episode, this) ? SpecialPool : NormalPool;

        public IReadOnlyList<string> GetTitleCandidates(IEpisode episode)
            => TitleCandidates.GetOrAdd(episode, static candidate => TitleCandidatesOf(candidate, OriginalLanguageOf(candidate)));
    }

    private static MatchContext BuildContext(EpisodeMatchStrategy strategy, List<IAnidbEpisode> anidb, IReadOnlyList<IEpisode> providerEpisodes)
    {
        var animeType = anidb
            .Select(episode => episode.Series?.Type)
            .FirstOrDefault(type => type.HasValue && type.Value is not AnimeType.Unknown);
        var isOva = animeType is AnimeType.OVA;
        var flat = strategy is EpisodeMatchStrategy.DateThenNumber || isOva;
        var candidates = providerEpisodes.Where(episode => episode is not null).ToList();

        // The specials season goes last, so the positional fallback reaches
        // for an ordinary episode before a special.
        var normalPool = flat
            ? candidates
                .OrderBy(episode => episode.SeasonNumber is 0)
                .ThenBy(episode => episode.SeasonNumber ?? 0)
                .ThenBy(episode => episode.EpisodeNumber)
                .ToList()
            : candidates
                .Where(episode => (episode.SeasonNumber ?? 0) != 0)
                .OrderBy(episode => episode.SeasonNumber ?? 0)
                .ThenBy(episode => episode.EpisodeNumber)
                .ToList();
        var specialPool = flat
            ? normalPool
            : candidates
                .Where(episode => (episode.SeasonNumber ?? 0) == 0)
                .OrderBy(episode => episode.EpisodeNumber)
                .ToList();

        return new MatchContext
        {
            Strategy = strategy,
            AnimeType = animeType,
            IsOva = isOva,
            Pairings = anidb.Select(episode => new Pairing(episode)).ToList(),
            // Not ToDictionary: AniDB data can repeat a (type, number) pair, and
            // this only answers whether a neighbour exists, so the first will do.
            ByTypeNumber = anidb
                .GroupBy(episode => (episode.Type, episode.EpisodeNumber))
                .ToDictionary(group => group.Key, group => group.First()),
            NormalPool = normalPool,
            SpecialPool = specialPool,
        };
    }

    /// <summary>
    ///   Whether an episode belongs in the specials pool.
    /// </summary>
    /// <remarks>
    ///   An episode AniDB typed as a special, or any episode of an anime that
    ///   is not a TV or web series, as an OVA or a movie sits outside a
    ///   source's ordinary numbering. An unknown anime type counts for nothing.
    /// </remarks>
    /// <param name="episode">The AniDB episode.</param>
    /// <param name="context">The run, which knows the anime's type.</param>
    /// <returns><c>true</c> when the episode is matched from the specials pool.</returns>
    private static bool IsSpecialEpisode(IAnidbEpisode episode, MatchContext context)
        => episode.Type is EpisodeType.Special ||
            (context.AnimeType is { } animeType &&
                animeType is not AnimeType.TV and not AnimeType.Web);

    private static bool IsHidden(IAnidbEpisode episode)
    {
        try
        {
            return episode.ShokoEpisodes is { Count: > 0 } shokoEpisodes && shokoEpisodes.All(shoko => shoko.IsHidden);
        }
        catch
        {
            return false;
        }
    }

    #endregion

    #region Existing links

    // Keeps a caller's user-verified or top-rated links and takes their candidates out of the pool. A link to
    // nothing, or any link on a hidden episode, is only kept when a person made it.
    private bool TryUseExisting(
        Pairing pairing,
        MatchContext context,
        IReadOnlyDictionary<int, IReadOnlyList<IMetadataEpisodeCrossReference>> existing,
        bool isHidden
    )
    {
        if (!existing.TryGetValue(pairing.Anidb.AnidbID, out var crossReferences) || crossReferences.Count == 0)
            return false;

        var links = crossReferences
            .DistinctBy(link => link.ProviderID)
            .OrderBy(link => link.Ordering)
            .ToList();
        if (links.Any(link => link.ProviderID is not null))
            links = [.. links.Where(link => link.ProviderID is not null)];

        var bestRating = context.Strategy is EpisodeMatchStrategy.DateAndTitleWithinSeasons
            ? MatchRating.DateAndTitleMatches
            : MatchRating.DateAndNumberMatches;
        if (!links.Any(link => link.MatchRating is MatchRating.UserVerified || link.MatchRating == bestRating))
            return false;

        var isUserVerified = links.Any(link => link.MatchRating is MatchRating.UserVerified);
        if ((isHidden || links.All(link => link.ProviderID is null)) && !isUserVerified)
            return false;

        var byID = context.NormalPool
            .Concat(context.SpecialPool)
            .DistinctBy(episode => episode.ID)
            .ToDictionary(episode => episode.ID);
        var offered = links
            .Where(link => link.ProviderID is { } providerID && byID.ContainsKey(providerID))
            .ToList();
        // A link the candidates cannot stand for (to nothing, or to an entry not offered) leaves the
        // episode alone: it keeps its candidates but stays out of the result.
        var isLeftAlone = offered.Count != links.Count;
        pairing.IsFixed = true;
        pairing.IsLeftAlone = isLeftAlone;
        if (isLeftAlone)
            _logger.LogTrace(
                "Leaving an episode settled outside the candidates alone. (AniDB ID: {AnidbEpisodeID}, Links: {Links}, Offered: {Offered})",
                pairing.Anidb.AnidbID, links.Count, offered.Count);

        for (var index = 0; index < offered.Count; index++)
        {
            var link = offered[index];
            var candidate = byID[link.ProviderID!];
            var target = index == 0 ? pairing : new Pairing(pairing.Anidb) { IsFixed = true, IsLeftAlone = isLeftAlone };
            target.Provider = candidate;
            target.Rating = link.MatchRating;
            target.Ordering = index;
            if (index > 0)
                context.Pairings.Add(target);
            if (index == 0)
                context.PrimaryByAnidbID[pairing.Anidb.AnidbID] = target;

            context.NormalPool.Remove(candidate);
            context.SpecialPool.Remove(candidate);
            _logger.LogTrace(
                "Keeping existing link for episode. (AniDB ID: {AnidbEpisodeID}, Provider ID: {ProviderID}, Rating: {MatchRating})",
                pairing.Anidb.AnidbID, link.ProviderID, link.MatchRating);
        }

        return true;
    }

    #endregion

    #region Passes

    // Every pairing whose match `accepts` claims its candidate; the rest
    // overflow to the next pass.
    private void RunPass(int passNumber, List<Pairing> queue, Func<MatchRating, bool> accepts, List<Pairing> overflow, MatchContext context)
    {
        // Weaker passes stay within the seasons an earlier pass settled.
        if (passNumber > 1 && context.Strategy is EpisodeMatchStrategy.DateAndTitleWithinSeasons)
            FilterToCurrentSeasons(context, passNumber);

        foreach (var (pairing, cached, cachedAnchor) in RankByConfidence(queue, context))
        {
            var result = ResolveRankedMatch(pairing, cached, cachedAnchor, context);
            if (!accepts(result.Rating))
            {
                overflow.Add(pairing);
                continue;
            }

            Claim(pairing, result, context);
            _logger.LogTrace(
                "Linked episode. (AniDB ID: {AnidbEpisodeID}, Rating: {MatchRating}, Pass: {PassNumber}/4)",
                pairing.Anidb.AnidbID, result.Rating, passNumber);
        }
    }

    private static void Claim(Pairing pairing, MatchResult result, MatchContext context)
    {
        if (result.Episode is { } episode)
        {
            context.NormalPool.Remove(episode);
            if (!ReferenceEquals(context.NormalPool, context.SpecialPool))
                context.SpecialPool.Remove(episode);
        }

        pairing.Provider = result.Episode;
        pairing.Rating = result.Rating;
        if (result.Episode is not null && pairing.Ordering == 0)
            context.PrimaryByAnidbID[pairing.Anidb.AnidbID] = pairing;
    }

    private void FilterToCurrentSeasons(MatchContext context, int passNumber)
    {
        var currentSeasons = context.Pairings
            .Where(pairing => pairing.Provider is not null && (context.IsOva || pairing.Anidb.Type is EpisodeType.Episode))
            .Select(pairing => pairing.Provider!.SeasonNumber ?? 0)
            .ToHashSet();
        if (currentSeasons.Count == 0)
            return;

        if (!context.IsOva)
            currentSeasons.Add(0);

        _logger.LogTrace(
            "Filtering available episodes by the seasons currently in use. (Seasons: {Seasons}, Pass: {PassNumber}/4)",
            string.Join(", ", currentSeasons), passNumber);
        var remaining = (ReferenceEquals(context.NormalPool, context.SpecialPool)
            ? context.NormalPool
            : context.NormalPool.Concat(context.SpecialPool))
            .Where(episode => currentSeasons.Contains(episode.SeasonNumber ?? 0))
            .ToList();
        if (context.IsOva)
        {
            context.NormalPool = remaining;
            context.SpecialPool = remaining;
            return;
        }

        context.NormalPool = remaining
            .Where(episode => (episode.SeasonNumber ?? 0) != 0)
            .OrderBy(episode => episode.SeasonNumber ?? 0)
            .ThenBy(episode => episode.EpisodeNumber)
            .ToList();
        context.SpecialPool = remaining
            .Where(episode => (episode.SeasonNumber ?? 0) == 0)
            .OrderBy(episode => episode.EpisodeNumber)
            .ToList();
    }

    // Links each remaining normal episode on the offset the date-matched anchors
    // agree on and returns the rest; the offset itself comes only from dates.
    private List<Pairing> RunOffsetPass(List<Pairing> queue, MatchContext context)
    {
        var offsets = context.Pairings
            .Where(pairing => pairing.Provider is not null &&
                pairing.Rating is MatchRating.UserVerified or MatchRating.DateAndNumberMatches or MatchRating.DateMatches)
            .Where(pairing => pairing.Anidb.Type is EpisodeType.Episode)
            .Select(pairing => pairing.Provider!.EpisodeNumber - pairing.Anidb.EpisodeNumber)
            .ToList();
        if (offsets.Count < 2 || offsets.Distinct().Count() != 1)
        {
            _logger.LogTrace("Skipping the offset pass. (Anchors: {Anchors}, Distinct Offsets: {Offsets})", offsets.Count, offsets.Distinct().Count());
            return queue;
        }

        var offset = offsets[0];
        var overflow = new List<Pairing>();
        foreach (var pairing in queue)
        {
            if (pairing.Anidb.Type is not EpisodeType.Episode || IsSpecialEpisode(pairing.Anidb, context))
            {
                overflow.Add(pairing);
                continue;
            }

            var pool = context.GetPool(pairing.Anidb);
            var index = pool.FindIndex(candidate => candidate.EpisodeNumber == pairing.Anidb.EpisodeNumber + offset);
            if (index == -1)
            {
                overflow.Add(pairing);
                continue;
            }

            Claim(pairing, new MatchResult(pool[index], MatchRating.DateOffsetMatches, 0), context);
            _logger.LogTrace(
                "Linked episode on the agreed offset. (AniDB ID: {AnidbEpisodeID}, Offset: {Offset})", pairing.Anidb.AnidbID, offset);
        }

        return overflow;
    }

    #endregion

    #region Ranking

    private readonly record struct MatchResult(IEpisode? Episode, MatchRating Rating, double Confidence)
    {
        public static readonly MatchResult Miss = new(null, MatchRating.None, 0);
    }

    // Ranks pairings by their best candidate without claiming it, so a pass works its strongest matches
    // first rather than in AniDB episode order. The match is cached for the claiming loop.
    private List<(Pairing Pairing, MatchResult Cached, int? Anchor)> RankByConfidence(IEnumerable<Pairing> pairings, MatchContext context)
        => pairings
            .Select(pairing =>
            {
                var anchor = ResolveAnchorSeason(pairing.Anidb, context);
                return (Pairing: pairing, Cached: FindMatch(pairing.Anidb, context, anchor), Anchor: anchor);
            })
            .OrderByDescending(ranked => ranked.Cached.Confidence)
            .ToList();

    // Reuses the cached match unless its candidate was claimed or the anchor season moved,
    // as a neighbour linked earlier in the same loop can resolve a new anchor.
    private MatchResult ResolveRankedMatch(Pairing pairing, MatchResult cached, int? cachedAnchor, MatchContext context)
    {
        var anchor = ResolveAnchorSeason(pairing.Anidb, context);
        return cached.Episode is { } episode && anchor == cachedAnchor && context.GetPool(pairing.Anidb).Contains(episode)
            ? cached
            : FindMatch(pairing.Anidb, context, anchor);
    }

    // The season an adjacent neighbour linked into, scoping the nearest-date fallback so back-to-back
    // cours cannot pull an episode into the wrong season. Null for flat sources and OVAs.
    private static int? ResolveAnchorSeason(IAnidbEpisode episode, MatchContext context)
    {
        if (context.IsOva || context.Strategy is not EpisodeMatchStrategy.DateAndTitleWithinSeasons)
            return null;

        (int Season, DateOnly? AiredAt)? SeasonOfNeighbour(int neighbourNumber)
        {
            if (!context.ByTypeNumber.TryGetValue((episode.Type, neighbourNumber), out var neighbour))
                return null;
            if (!context.PrimaryByAnidbID.TryGetValue(neighbour.AnidbID, out var pairing) || pairing.Provider is not { } provider)
                return null;

            return (provider.SeasonNumber ?? 0, provider.AirDate);
        }

        var previous = SeasonOfNeighbour(episode.EpisodeNumber - 1);
        var next = SeasonOfNeighbour(episode.EpisodeNumber + 1);
        if (previous is null)
            return next?.Season;
        if (next is null || previous.Value.Season == next.Value.Season)
            return previous.Value.Season;

        // A season boundary sits between the neighbours: the closer air date wins,
        // so a new season's first episode anchors to the new season.
        var anidbDate = episode.AirDate;
        if (anidbDate is null)
            return previous.Value.Season;

        var previousDistance = EpisodeMatchingUtility.CalculateAirDateDistance(anidbDate, previous.Value.AiredAt) ?? int.MaxValue;
        var nextDistance = EpisodeMatchingUtility.CalculateAirDateDistance(anidbDate, next.Value.AiredAt) ?? int.MaxValue;
        return nextDistance < previousDistance ? next.Value.Season : previous.Value.Season;
    }

    private MatchResult FindMatch(IAnidbEpisode episode, MatchContext context, int? anchorSeason)
        => context.Strategy is EpisodeMatchStrategy.DateAndTitleWithinSeasons
            ? FindWithinSeasons(episode, context, anchorSeason)
            : FindByDateThenNumber(episode, context);

    #endregion

    #region Date and title, within seasons

    // Gathers the title and air-date evidence, then picks the strongest tier
    // that holds.
    private MatchResult FindWithinSeasons(IAnidbEpisode episode, MatchContext context, int? anchorSeason)
    {
        var pool = context.GetPool(episode);
        var isSpecial = IsSpecialEpisode(episode, context) && !context.IsOva;

        var anidbTitle = ResolveSearchTitle(episode, out var isExcludedTitle);
        if (isExcludedTitle)
            return MatchResult.Miss;

        // An episode shown early is out already, so its earliest date decides
        // what is still to come; the regular date is what the source has.
        if ((episode.EarlyAirDate ?? episode.AirDate) is { } shownOn && shownOn > DateOnly.FromDateTime(DateTime.UtcNow.AddDays(1)))
        {
            _logger.LogTrace("Skipping future episode {AnidbEpisodeID}", episode.AnidbID);
            return MatchResult.Miss;
        }

        var anidbDate = episode.AirDate;

        var airdateProbability = pool
            .Select(candidate => (episode: candidate, probability: EpisodeMatchingUtility.CalculateAirDateProbability(anidbDate, candidate.AirDate)))
            .Where(result => result.probability != 0)
            .OrderByDescending(result => result.probability)
            .ThenBy(result => (result.episode.SeasonNumber ?? 0) == 0)
            .ThenBy(result => result.episode.SeasonNumber ?? 0)
            .ThenBy(result => result.episode.EpisodeNumber)
            .ToList();

        // Nothing in the strict window: try the closest air date before a blind
        // positional guess, within the anchor season when there is one.
        var nearestAirdate = airdateProbability.Count > 0 || anidbDate is null
            ? []
            : pool
                .Where(candidate => anchorSeason is null || (candidate.SeasonNumber ?? 0) == anchorSeason.Value)
                .Select(candidate => (episode: candidate, distance: EpisodeMatchingUtility.CalculateAirDateDistance(anidbDate, candidate.AirDate)))
                .Where(result => result.distance is not null)
                .Select(result => (result.episode, distance: result.distance!.Value))
                .OrderBy(result => result.distance)
                .ThenBy(result => (result.episode.SeasonNumber ?? 0) == 0)
                .ThenBy(result => result.episode.SeasonNumber ?? 0)
                .ThenBy(result => result.episode.EpisodeNumber)
                .ToList();

        var titleSearchResults = !string.IsNullOrEmpty(anidbTitle)
            ? pool.Search(anidbTitle, context.GetTitleCandidates, true).OrderBy(result => result).ToList()
            : [];

        return SelectWithinSeasons(pool, isSpecial, titleSearchResults, airdateProbability, nearestAirdate);
    }

    private static MatchResult SelectWithinSeasons(
        IReadOnlyList<IEpisode> pool,
        bool isSpecial,
        List<SeriesSearch.SearchResult<IEpisode>> titleSearchResults,
        List<(IEpisode episode, double probability)> airdateProbability,
        List<(IEpisode episode, int distance)> nearestAirdate
    )
    {
        // An exact title, raised when the air date corroborates it.
        if (TryTitleMatch(titleSearchResults, airdateProbability,
                result => result.ExactMatch && result.LengthDifference < 3,
                (MatchRating.TitleMatches, MatchRating.DateAndTitleMatches)) is { } exact)
            return exact;

        // A near-enough title, same shape one tier down.
        if (TryTitleMatch(titleSearchResults, airdateProbability,
                result => result.Distance < 0.2D && result.LengthDifference < 6,
                (MatchRating.TitleKindaMatches, MatchRating.DateAndTitleKindaMatches)) is { } kinda)
            return kinda;

        if (airdateProbability.Count > 0)
        {
            var matched = airdateProbability.FirstOrDefault(result => titleSearchResults.Any(title => ReferenceEquals(title.Result, result.episode)));
            var episode = matched.episode ?? airdateProbability[0].episode;
            var rating = matched.episode is null ? MatchRating.DateMatches : MatchRating.DateAndTitleKindaMatches;
            var confidence = matched.episode is null ? airdateProbability[0].probability : matched.probability;
            return new MatchResult(episode, rating, confidence);
        }

        if (titleSearchResults.Count > 0)
            return new MatchResult(titleSearchResults[0].Result, MatchRating.TitleKindaMatches, 1 - titleSearchResults[0].Distance);

        // The loose fallback reaches 120 days, a coin flip for an uncorroborated
        // special, so specials skip it like the positional fallback below.
        if (!isSpecial && nearestAirdate.Count > 0 && nearestAirdate[0].distance <= EpisodeMatchingUtility.MaxFallbackDifferenceInDays)
            return new MatchResult(nearestAirdate[0].episode, MatchRating.DateKindaMatches, 0.5 / (1 + nearestAirdate[0].distance));

        if (!isSpecial && pool.Count > 0)
            return new MatchResult(pool[0], MatchRating.FirstAvailable, 0);

        return MatchResult.Miss;
    }

    // Takes the best title result past a threshold, raised to the date-and-title
    // rating when the air date corroborates it.
    private static MatchResult? TryTitleMatch(
        List<SeriesSearch.SearchResult<IEpisode>> titleSearchResults,
        List<(IEpisode episode, double probability)> airdateProbability,
        Func<SeriesSearch.SearchResult<IEpisode>, bool> isCandidate,
        (MatchRating TitleOnly, MatchRating DateAndTitle) ratings
    )
    {
        if (titleSearchResults.Count == 0 || !isCandidate(titleSearchResults[0]))
            return null;

        var titleMatch = titleSearchResults[0];
        var episode = titleMatch.Result;
        var dateMatches = airdateProbability.Any(result => ReferenceEquals(result.episode, episode));
        return new MatchResult(
            episode,
            dateMatches ? ratings.DateAndTitle : ratings.TitleOnly,
            (dateMatches ? 1 : 0) + (1 - titleMatch.Distance));
    }

    #endregion

    #region Date, then number

    // Only the date places an episode here; the number just breaks ties the dates
    // leave, and the offset pass reads numbers only from date-placed anchors.
    private MatchResult FindByDateThenNumber(IAnidbEpisode episode, MatchContext context)
    {
        var pool = context.GetPool(episode);
        var isSpecial = IsSpecialEpisode(episode, context);

        // An episode shown early is out already, so its earliest date decides
        // what is still to come; the regular date is what the source has.
        if ((episode.EarlyAirDate ?? episode.AirDate) is { } shownOn && shownOn > DateOnly.FromDateTime(DateTime.UtcNow.AddDays(1)))
        {
            _logger.LogTrace("Skipping future episode {AnidbEpisodeID}", episode.AnidbID);
            return MatchResult.Miss;
        }

        var anidbDate = episode.AirDate;

        var airdateProbability = pool
            .Select(candidate => (episode: candidate, probability: EpisodeMatchingUtility.CalculateAirDateProbability(anidbDate, candidate.AirDate)))
            .Where(result => result.probability != 0)
            .OrderByDescending(result => result.probability)
            .ThenBy(result => result.episode.EpisodeNumber)
            .ToList();
        var nearestAirdate = airdateProbability.Count > 0 || anidbDate is null
            ? []
            : pool
                .Select(candidate => (episode: candidate, distance: EpisodeMatchingUtility.CalculateAirDateDistance(anidbDate, candidate.AirDate)))
                .Where(result => result.distance is not null)
                .Select(result => (result.episode, distance: result.distance!.Value))
                .OrderBy(result => result.distance)
                .ThenBy(result => result.episode.EpisodeNumber)
                .ToList();

        if (airdateProbability.Count > 0)
        {
            // A candidate in the window whose number agrees rates higher. A special
            // never shares the source's numbering, so it only gets the date match.
            if (!isSpecial && episode.Type is EpisodeType.Episode &&
                airdateProbability.FirstOrDefault(result => result.episode.EpisodeNumber == episode.EpisodeNumber) is { episode: not null } corroborated)
                return new MatchResult(corroborated.episode, MatchRating.DateAndNumberMatches, 1 + corroborated.probability);

            return new MatchResult(airdateProbability[0].episode, MatchRating.DateMatches, airdateProbability[0].probability);
        }

        if (!isSpecial && nearestAirdate.Count > 0 && nearestAirdate[0].distance <= EpisodeMatchingUtility.MaxFallbackDifferenceInDays)
            return new MatchResult(nearestAirdate[0].episode, MatchRating.DateKindaMatches, 0.5 / (1 + nearestAirdate[0].distance));

        if (!isSpecial && pool.Count > 0)
            return new MatchResult(pool[0], MatchRating.FirstAvailable, 0);

        return MatchResult.Miss;
    }

    #endregion

    #region Titles

    // English first, then the anime's main-title language, skipping generic "Episode 5" placeholders.
    // Only AniDB's own titles count, so a title another source added cannot vouch for itself.
    private static string? ResolveSearchTitle(IAnidbEpisode episode, out bool isExcludedTitle)
    {
        isExcludedTitle = false;

        var seriesTitles = AnidbTitles(episode.Series);
        var mainTitle = seriesTitles.FirstOrDefault(title => title.Type is TitleType.Main);
        var fallbackLanguage = mainTitle?.Language switch
        {
            TitleLanguage.Romaji => TitleLanguage.Japanese,
            TitleLanguage.Pinyin => TitleLanguage.ChineseSimplified,
            TitleLanguage.KoreanTranscription => TitleLanguage.Korean,
            TitleLanguage.ThaiTranscription => TitleLanguage.Thai,
            _ => mainTitle?.Language ?? TitleLanguage.English,
        };
        var episodeTitles = AnidbTitles(episode);
        var anidbTitle = episodeTitles
                .FirstOrDefault(title => title.Language is TitleLanguage.English && !GenericEpisodeTitles.IsEnglishGeneric(title.Value, episode.Type, episode.EpisodeNumber))?.Value
            ?? (fallbackLanguage is not TitleLanguage.English
                ? episodeTitles.FirstOrDefault(title => title.Language == fallbackLanguage && !GenericEpisodeTitles.IsEnglishGeneric(title.Value, episode.Type, episode.EpisodeNumber))?.Value
                : null);
        if (string.IsNullOrEmpty(anidbTitle))
            return anidbTitle;

        if (_titlesToNotSearch.Any(title => anidbTitle.Contains(title, StringComparison.InvariantCultureIgnoreCase)))
        {
            isExcludedTitle = true;
            return null;
        }

        // A bare "OVA"/"Special"/… says nothing about which episode this is, so
        // the anime's own English title is the better thing to search with.
        if (_titlesToSearch.Contains(anidbTitle) &&
            seriesTitles.FirstOrDefault(title => title.Type is TitleType.Official && title.Language is TitleLanguage.English)?.Value is { } englishAnimeTitle)
        {
            var index = englishAnimeTitle.IndexOf(':', StringComparison.Ordinal);
            anidbTitle = index > 0 && index < englishAnimeTitle.Length - 1 ? englishAnimeTitle[(index + 1)..].TrimStart() : englishAnimeTitle;
        }

        return anidbTitle;
    }

    /// <summary>
    ///   The titles of a source's episode worth searching with an AniDB title.
    /// </summary>
    /// <remarks>
    ///   With a known original language, only American (or countryless)
    ///   English and original-language titles are kept, the only languages
    ///   the AniDB title is taken in. "Episode N" placeholders are dropped and
    ///   curly quotes straightened.
    /// </remarks>
    /// <param name="episode">The source's episode.</param>
    /// <param name="originalLanguageCode">
    ///   The language code of the series' original language, or
    ///   <c>null</c> when it is not known.
    /// </param>
    /// <returns>The titles to search, each once.</returns>
    internal static IReadOnlyList<string> TitleCandidatesOf(IEpisode episode, string? originalLanguageCode)
        => SafeTitles(episode)
            .Where(title => string.IsNullOrEmpty(originalLanguageCode) ||
                title.LanguageCode == originalLanguageCode ||
                (title.LanguageCode == "en" && (string.IsNullOrEmpty(title.CountryCode) || title.CountryCode == "US")))
            .Where(title => !title.Value.Trim().Equals($"Episode {episode.EpisodeNumber}", StringComparison.InvariantCultureIgnoreCase))
            .Select(title => _characterReplacementDict.Aggregate(title.Value, (current, kv) => current.Replace(kv.Key, kv.Value)))
            .Where(title => !string.IsNullOrEmpty(title))
            .Distinct()
            .ToList();

    // The original language of the series a source's episode belongs to, or
    // null when the episode names no series or the series no language.
    private static string? OriginalLanguageOf(IEpisode episode)
    {
        try
        {
            return episode.Series?.OriginalLanguageCode is { Length: > 0 } languageCode ? languageCode : null;
        }
        catch
        {
            return null;
        }
    }

    // The titles AniDB itself gave an entry, leaving out any another source or
    // a person added to it.
    private static IReadOnlyList<ITitle> AnidbTitles(IWithTitles? entity)
        => [.. SafeTitles(entity).Where(title => title.Source == MetadataSource.AniDB)];

    private static IReadOnlyList<ITitle> SafeTitles(IWithTitles? entity)
    {
        if (entity is null)
            return [];

        try
        {
            return entity.Titles ?? [];
        }
        catch
        {
            return [];
        }
    }

    #endregion

    #region Order reconciliation

    // Swaps adjacent weak matches back into AniDB order; strong matches never
    // move, only guesses without title or number evidence.
    private static void ReconcileOrderInversions(List<Pairing> pairings)
    {
        foreach (var group in pairings.Where(pairing => pairing.Provider is not null && !pairing.IsFixed).GroupBy(pairing => pairing.Anidb.Type))
        {
            var ordered = group.OrderBy(pairing => pairing.Anidb.EpisodeNumber).ToList();

            // Repeat until a full pass makes no swaps, to untangle a run of
            // three or more reversed weak matches.
            while (BubbleSwapPass(ordered))
            {
                // Intentionally empty: the pass applies its swaps in place.
            }
        }
    }

    private static bool BubbleSwapPass(List<Pairing> ordered)
    {
        var swapped = false;
        for (var index = 0; index < ordered.Count - 1; index++)
        {
            var a = ordered[index];
            var b = ordered[index + 1];
            if (!ShouldSwap(a, b))
                continue;

            (a.Provider, b.Provider) = (b.Provider, a.Provider);
            (a.Rating, b.Rating) = (b.Rating, a.Rating);
            swapped = true;
        }

        return swapped;
    }

    private static bool ShouldSwap(Pairing a, Pairing b)
        => _weakOrderRatings.Contains(a.Rating) && _weakOrderRatings.Contains(b.Rating) &&
            a.Provider is { } providerA && b.Provider is { } providerB &&
            (providerA.SeasonNumber ?? 0, providerA.EpisodeNumber).CompareTo((providerB.SeasonNumber ?? 0, providerB.EpisodeNumber)) > 0;

    #endregion
}
