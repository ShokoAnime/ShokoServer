using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using Moq;
using Shoko.Abstractions.Metadata;
using Shoko.Abstractions.Metadata.Anidb;
using Shoko.Abstractions.Metadata.Enums;
using Shoko.Abstractions.Metadata.Matching;
using Shoko.Abstractions.Metadata.Search;
using Shoko.Abstractions.Metadata.Stub;
using Shoko.Plugin.Tmdb.Mapping;
using Shoko.Plugin.Tmdb.Services;
using Xunit;

namespace Shoko.Tests.Plugin.Tmdb;

/// <summary>
///   The plugin's searches: a person's search, and the auto-linker's
///   candidates, judged by a stand-in for the core's matching engine.
/// </summary>
public sealed class TmdbSearchServiceTests : IDisposable
{
    private readonly TmdbServiceHarness _harness = new();

    public TmdbSearchServiceTests()
        => _harness.RouteBasics().RouteShow().RouteMovie();

    public void Dispose()
        => _harness.Dispose();

    #region Searching

    [Fact]
    public async Task ASearchNamesTheGenresFromTheTagStoreAndPutsAnimationFirst()
    {
        _harness.Routes.Fixture("search/tv", "search-tv.json");

        var (page, total) = await _harness.Provider.SearchSeries(new() { Query = "Journey's End" }, TestContext.Current.CancellationToken);

        Assert.Equal(2, total);
        Assert.Equal([TmdbIds.Series(1001), TmdbIds.Series(1999)], page.Select(result => result.ID));
        // A genre joining two gives each, as upstream split them.
        Assert.Equal(["Animation", "Action", "Adventure"], page[0].Genres);
        Assert.Equal("https://image.tmdb.org/t/p/original/show-poster.jpg", page[0].PosterUrl);
        Assert.True(_harness.StoreData.Tags.ContainsKey(TmdbIds.Genre(18)));
    }

    [Fact]
    public async Task TheGenresAreFetchedOnceWhileTheStoreKnowsThem()
    {
        _harness.Routes.Fixture("search/tv", "search-tv.json");

        await _harness.Provider.SearchSeries(new() { Query = "Journey's End" }, TestContext.Current.CancellationToken);
        await _harness.Provider.SearchSeries(new() { Query = "Journey's End" }, TestContext.Current.CancellationToken);

        Assert.Equal(1, _harness.Routes.Count("genre/tv/list"));
    }

    [Fact]
    public async Task AStoredShowIsLookedUpWithoutAskingTmdb()
    {
        await _harness.Refresh.RefreshShow(1001, new() { QuickRefresh = true }, TestContext.Current.CancellationToken);
        var asked = _harness.Routes.Count("tv/1001");

        var result = await _harness.Provider.LookupSeries(TmdbIds.Series(1001), TestContext.Current.CancellationToken);

        Assert.Equal("Journey's End", result?.Title);
        Assert.Equal("旅の終わり", result?.OriginalTitle);
        Assert.Equal([1], result?.Seasons?.Select(season => season.SeasonNumber));
        Assert.Equal(asked, _harness.Routes.Count("tv/1001"));
    }

    [Fact]
    public async Task AMovieNotStoredIsLookedUpOnTmdb()
    {
        var result = await _harness.Provider.LookupMovie(TmdbIds.Movie(7001), TestContext.Current.CancellationToken);

        Assert.Equal("Journey's End: The Movie", result?.Title);
        Assert.Contains(new DateOnly(2024, 12, 20), result!.OtherReleaseDates);
    }

    #endregion

    #region Auto-linking

    [Fact]
    public async Task TheAutoSearchTakesTheAnimatedShowTheEngineRatesBest()
    {
        _harness.Routes.Fixture("search/tv", "search-tv.json");
        var anime = Anime(42, AnimeType.TV, new DateOnly(2023, 9, 29), 2);
        SetUpAnime(anime);
        JudgeShowsBy(new() { [1001] = MatchRating.DateAndTitleMatches });

        var candidates = await _harness.Provider.FindAutoLinks(42, TestContext.Current.CancellationToken);

        var taken = Assert.Single(candidates);
        Assert.Equal((TmdbIds.Series(1001), MatchRating.DateAndTitleMatches, MetadataAutoLinkOrigin.Search), (taken.ID, taken.MatchRating, taken.Origin));
        Assert.Null(taken.Rejection);
        Assert.True(taken.IsRemote);
        Assert.Null(taken.AnidbEpisodeID);
        // A hit outside the animation genre is never fetched.
        Assert.Equal(0, _harness.Routes.Count("tv/1999"));
        // The season holding the anime's start is fetched to line the episodes up.
        Assert.Contains("tv/1001/season/1", _harness.Routes.Paths);
    }

    [Fact]
    public async Task AShowTheAnimesCrossSourceIDsNameIsAHint()
    {
        _harness.Routes.Json("search/tv", """{"page":1,"results":[],"total_pages":0,"total_results":0}""");
        var anime = Anime(42, AnimeType.TV, new DateOnly(2023, 9, 29), 2, crossSourceIDs: [TmdbIds.Series(1001)]);
        SetUpAnime(anime);
        JudgeShowsBy(new() { [1001] = MatchRating.TitleMatches });

        var candidates = await _harness.Provider.FindAutoLinks(42, TestContext.Current.CancellationToken);

        var hint = Assert.Single(candidates);
        Assert.Equal((TmdbIds.Series(1001), MetadataAutoLinkOrigin.AnidbResource), (hint.ID, hint.Origin));
        Assert.Null(hint.Rejection);
    }

    [Fact]
    public async Task AnImdbIDIsFoundOnTmdbWhenNoTmdbIDIsNamed()
    {
        _harness.Routes
            .Json("search/tv", """{"page":1,"results":[],"total_pages":0,"total_results":0}""")
            .Fixture("find/tt22248376", "find-tt22248376.json");
        var imdb = new MetadataGuid(MetadataSource.Parse("imdb"), MetadataEntityType.Series, "tt22248376");
        var anime = Anime(42, AnimeType.TV, new DateOnly(2023, 9, 29), 2, crossSourceIDs: [imdb]);
        SetUpAnime(anime);
        JudgeShowsBy(new() { [1001] = MatchRating.None });

        var candidates = await _harness.Provider.FindAutoLinks(42, TestContext.Current.CancellationToken);

        var hint = Assert.Single(candidates);
        Assert.Equal(TmdbIds.Series(1001), hint.ID);
        // Nothing agreed, but a hint of the kind searched for may still be taken.
        Assert.Equal(MatchRating.FirstAvailable, hint.MatchRating);
        Assert.Null(hint.Rejection);
    }

    [Fact]
    public async Task AMovieIsFoundForTheEpisodeItStandsFor()
    {
        _harness.Routes.Fixture("search/movie", "search-movie.json");
        var anime = Anime(43, AnimeType.Movie, new DateOnly(2025, 1, 10), 1);
        SetUpAnime(anime);
        _harness.Engine
            .Setup(engine => engine.MatchMovies(It.IsAny<IAnidbAnime>(), It.IsAny<IAnidbEpisode>(), It.IsAny<IReadOnlyList<MetadataMovieSearchResult>>(), It.IsAny<MovieMatchOptions?>()))
            .Returns((IAnidbAnime animeArg, IAnidbEpisode episode, IReadOnlyList<MetadataMovieSearchResult> movies, MovieMatchOptions? _) =>
                [.. movies.Select((movie, index) => new MovieMatch
                {
                    AnidbAnime = animeArg,
                    AnidbEpisode = episode,
                    Candidate = movie,
                    Rating = MatchRating.DateAndTitleMatches,
                    Rejection = index is 0 ? MatchRejectionReason.None : MatchRejectionReason.Outranked,
                })]);

        var candidates = await _harness.Provider.FindAutoLinks(43, TestContext.Current.CancellationToken);

        var taken = Assert.Single(candidates);
        Assert.Equal((TmdbIds.Movie(7001), 4301), (taken.ID, taken.AnidbEpisodeID));
        Assert.Null(taken.Rejection);
    }

    [Fact]
    public async Task AMusicVideoIsNotSearched()
    {
        SetUpAnime(Anime(44, AnimeType.MusicVideo, new DateOnly(2023, 9, 29), 1));

        Assert.Empty(await _harness.Provider.FindAutoLinks(44, TestContext.Current.CancellationToken));
        Assert.DoesNotContain(_harness.Routes.Paths, path => path.StartsWith("search/", StringComparison.Ordinal));
    }

    [Fact]
    public async Task WithoutAnApiKeyNothingIsSearched()
    {
        _harness.Configuration.UserApiKey = null;
        SetUpAnime(Anime(42, AnimeType.TV, new DateOnly(2023, 9, 29), 2));

        Assert.Empty(await _harness.Provider.FindAutoLinks(42, TestContext.Current.CancellationToken));
        Assert.Empty(_harness.Routes.Paths);
    }

    #endregion

    #region Judging

    [Theory]
    [InlineData(MatchRating.DateAndTitleMatches, MatchRating.TitleMatches, true)]
    [InlineData(MatchRating.DateAndTitleKindaMatches, MatchRating.TitleKindaMatches, true)]
    [InlineData(MatchRating.DateMatches, MatchRating.TitleMatches, false)]
    [InlineData(MatchRating.DateAndTitleKindaMatches, MatchRating.DateAndTitleMatches, false)]
    public void TheAnimesOwnShowNeedsADateOverATitleAlone(MatchRating own, MatchRating prequels, bool wins)
        => Assert.Equal(wins, TmdbSearchService.OwnTitleWins(new(own, null, null), new(prequels, null, null), new DateOnly(2008, 4, 2), 12));

    [Theory]
    [InlineData(true, false, true)]
    [InlineData(false, true, false)]
    public void AHintSettlesATie(bool ownHinted, bool prequelsHinted, bool wins)
        => Assert.Equal(
            wins,
            TmdbSearchService.OwnTitleWins(new(MatchRating.TitleMatches, null, null, ownHinted), new(MatchRating.TitleMatches, null, null, prequelsHinted), new DateOnly(2008, 4, 2), 12)
        );

    [Fact]
    public void TheOnlyTiedEpisodeAiredWithTheMovieTakesIt()
        => Assert.Equal(
            (0, false),
            TmdbSearchService.PlaceFilm(
                [(MatchRating.DateAndTitleMatches, [new DateOnly(1998, 2, 25)]), (MatchRating.DateAndTitleMatches, [new DateOnly(1998, 10, 25)])],
                [new DateOnly(1998, 2, 25)]
            )
        );

    [Fact]
    public void SeveralTiedEpisodesAiredWithTheMovieLeaveItUnplaced()
    {
        var day = new DateOnly(1990, 1, 25);

        Assert.True(TmdbSearchService.PlaceFilm([(MatchRating.DateAndTitleMatches, [day]), (MatchRating.DateAndTitleMatches, [day])], [day]).Unplaced);
    }

    [Fact]
    public void AHintOfAnotherKindIsOnlyListed()
    {
        var movie = new TmdbSearchService.TmdbHint(7001, true);

        Assert.Equal(
            MatchRejectionReason.TypeMismatch,
            TmdbSearchService.HintRejection(movie, AnimeType.TV, false, MatchRating.TitleMatches, MatchRejectionReason.None, null, false)?.Reason
        );
        Assert.Null(TmdbSearchService.HintRejection(movie, AnimeType.Movie, true, MatchRating.TitleMatches, MatchRejectionReason.None, null, false));
    }

    [Fact]
    public void AHintForAnAnimeNotAiredYetIsRefused()
    {
        var now = new DateTime(2026, 10, 1, 12, 0, 0);
        var notAired = TmdbSearchService.NotAired(now.AddDays(40), false, now, TimeSpan.FromDays(15));

        Assert.Equal(
            MatchRejectionReason.Other,
            TmdbSearchService.HintRejection(new(1001, false), AnimeType.TV, true, MatchRating.DateAndTitleMatches, MatchRejectionReason.None, null, false, notAired)?.Reason
        );
    }

    [Fact]
    public void TheHintsAreReadFromTmdbsOwnCrossSourceIDs()
    {
        var imdb = new MetadataGuid(MetadataSource.Parse("imdb"), MetadataEntityType.Series, "tt22248376");
        IReadOnlyList<MetadataGuid> ids = [TmdbIds.Series(1001), TmdbIds.Movie(7001), TmdbIds.Series(1001), imdb, TmdbIds.Episode(3001)];

        Assert.Equal([(1001, false), (7001, true)], TmdbSearchService.TmdbHintsOf(ids).Select(hint => (hint.TmdbID, hint.IsMovie)));
        Assert.Equal(["tt22248376"], TmdbSearchService.ImdbIDsOf(ids));
    }

    [Fact]
    public void TheHintedCandidatesAreFetchedFirst()
        => Assert.Equal([3, 1, 2], TmdbSearchService.HintedFirst([1, 2, 3], [new(3, false), new(1, true)], isMovie: false));

    #endregion

    #region Helpers

    private void SetUpAnime(IAnidbAnime anime)
        => _harness.MetadataService
            .Setup(service => service.GetSeries(new MetadataGuid(MetadataSource.AniDB, MetadataEntityType.Series, anime.AnidbID.ToString(CultureInfo.InvariantCulture))))
            .Returns(anime);

    private void JudgeShowsBy(Dictionary<int, MatchRating> ratings)
        => _harness.Engine
            .Setup(engine => engine.MatchSeries(It.IsAny<IAnidbAnime>(), It.IsAny<IReadOnlyList<MetadataSeriesSearchResult>>(), It.IsAny<SeriesMatchOptions?>()))
            .Returns((IAnidbAnime anime, IReadOnlyList<MetadataSeriesSearchResult> candidates, SeriesMatchOptions? _) =>
            {
                var judged = candidates
                    .Select(candidate => (Candidate: candidate, Rating: ratings.GetValueOrDefault(candidate.ID.GetNumericID<int>())))
                    .OrderBy(pair => pair.Rating is MatchRating.None)
                    .ToList();
                return
                [
                    .. judged.Select((pair, index) => new SeriesMatch
                    {
                        AnidbAnime = anime,
                        Candidate = pair.Candidate,
                        Rating = pair.Rating,
                        Rejection = pair.Rating is MatchRating.None ? MatchRejectionReason.TitleMismatch : index is 0 ? MatchRejectionReason.None : MatchRejectionReason.Outranked,
                    }),
                ];
            });

    private static IAnidbAnime Anime(int anidbID, AnimeType type, DateOnly airedOn, int episodeCount, IReadOnlyList<MetadataGuid>? crossSourceIDs = null)
    {
        var episodes = Enumerable.Range(1, episodeCount)
            .Select(number =>
            {
                var episode = new Mock<IAnidbEpisode> { DefaultValue = DefaultValue.Empty };
                episode.SetupGet(mock => mock.AnidbID).Returns((anidbID * 100) + number);
                episode.SetupGet(mock => mock.Type).Returns(EpisodeType.Episode);
                episode.SetupGet(mock => mock.EpisodeNumber).Returns(number);
                episode.SetupGet(mock => mock.AirDate).Returns(airedOn.AddDays(7 * (number - 1)));
                episode.SetupGet(mock => mock.RegularAirDate).Returns(airedOn.AddDays(7 * (number - 1)));
                episode.SetupGet(mock => mock.Titles).Returns([Title($"Episode {number}", TitleLanguage.English, TitleType.Main)]);
                return episode.Object;
            })
            .ToList();
        var anime = new Mock<IAnidbAnime> { DefaultValue = DefaultValue.Empty };
        anime.SetupGet(mock => mock.AnidbID).Returns(anidbID);
        anime.SetupGet(mock => mock.ID).Returns(new MetadataGuid(MetadataSource.AniDB, MetadataEntityType.Series, anidbID.ToString(CultureInfo.InvariantCulture)));
        anime.SetupGet(mock => mock.Type).Returns(type);
        anime.SetupGet(mock => mock.AirDate).Returns(PartialDateOnly.FromDateOnly(airedOn));
        anime.SetupGet(mock => mock.RegularAirDate).Returns(PartialDateOnly.FromDateOnly(airedOn));
        anime.SetupGet(mock => mock.Episodes).Returns(episodes);
        anime.SetupGet(mock => mock.EpisodeCounts).Returns(new EpisodeCounts { Episodes = episodeCount });
        anime.SetupGet(mock => mock.CrossSourceIDs).Returns(crossSourceIDs ?? []);
        anime.SetupGet(mock => mock.RelatedSeries).Returns([]);
        anime.SetupGet(mock => mock.Titles).Returns(
        [
            Title("Tabi no Owari", TitleLanguage.Romaji, TitleType.Main),
            Title("旅の終わり", TitleLanguage.Japanese, TitleType.Official),
            Title("Journey's End", TitleLanguage.English, TitleType.Official),
        ]);
        return anime.Object;
    }

    private static ITitle Title(string value, TitleLanguage language, TitleType type)
        => new TitleStub { Source = MetadataSource.AniDB, Language = language, LanguageCode = language.ToString(), Value = value, Type = type };

    #endregion
}
