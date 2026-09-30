using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Shoko.Abstractions.Metadata;
using Shoko.Abstractions.Metadata.Anidb;
using Shoko.Abstractions.Metadata.Enums;
using Shoko.Abstractions.Metadata.Matching;
using Shoko.Abstractions.Metadata.Search;
using Shoko.Server.Filters;
using Shoko.Server.Providers.TMDB;
using Shoko.Server.Services;
using Xunit;

namespace Shoko.Tests.Providers.TMDB;

/// <summary>
/// Covers how TMDB's auto-search fetches a season's episodes so the
/// matching engine can line a show up with the anime by air date.
/// </summary>
public class TmdbEpisodeAlignmentTests
{
    #region Fixtures

    private static readonly DateOnly _start = new(2021, 1, 2);

    private static MetadataMatchingEngine Engine() => new(NullLogger<MetadataMatchingEngine>.Instance, new FuzzySearchService());

    // An anime of twelve weekly episodes from the start.
    private static IAnidbAnime Anime()
    {
        var episodes = Enumerable.Range(1, 12).Select(number =>
        {
            var episode = new Mock<IAnidbEpisode>();
            episode.SetupGet(e => e.AnidbID).Returns(number);
            episode.SetupGet(e => e.Type).Returns(EpisodeType.Episode);
            episode.SetupGet(e => e.EpisodeNumber).Returns(number);
            episode.SetupGet(e => e.AirDate).Returns(_start.AddDays(7 * (number - 1)));
            episode.SetupGet(e => e.RegularAirDate).Returns(_start.AddDays(7 * (number - 1)));
            episode.SetupGet(e => e.Titles).Returns([]);
            return episode.Object;
        }).ToList();
        var anime = new Mock<IAnidbAnime>();
        anime.SetupGet(a => a.AnidbID).Returns(1);
        anime.SetupGet(a => a.Type).Returns(AnimeType.TV);
        anime.SetupGet(a => a.AirDate).Returns(new PartialDateOnly(_start));
        anime.SetupGet(a => a.RegularAirDate).Returns(new PartialDateOnly(_start));
        anime.SetupGet(a => a.Episodes).Returns(episodes);
        anime.SetupGet(a => a.EpisodeCounts).Returns(new EpisodeCounts { Episodes = 12 });
        anime.SetupGet(a => a.Titles).Returns([]);
        return anime.Object;
    }

    // A show whose first season of 24 began twelve weeks before the anime, so
    // the anime is its second half, and whose second season is 12 long.
    private static MetadataSeriesSearchResult SplitCour(int id)
        => new()
        {
            ID = new(MetadataSource.TMDB, MetadataEntityType.Series, id.ToString()),
            Title = "Kaguya",
            FirstAiredAt = new(new DateOnly(2020, 10, 10)),
            Seasons =
            [
                new() { SeasonNumber = 1, EpisodeCount = 24, FirstAiredAt = new(new DateOnly(2020, 10, 10)) },
                new() { SeasonNumber = 2, EpisodeCount = 12, FirstAiredAt = new(new DateOnly(2022, 1, 8)) },
            ],
        };

    private static IReadOnlyList<MetadataSearchResultEpisode> Weekly(DateOnly first, int count)
        => [.. Enumerable.Range(0, count).Select(index => new MetadataSearchResultEpisode { EpisodeNumber = index + 1, AiredAt = first.AddDays(7 * index) })];

    #endregion

    #region Tests

    // The season holding the anime's start is fetched, not the one the
    // episode count points at, and the show is placed in it.
    [Fact]
    public async Task PickShow_FetchesTheSeasonHoldingTheAnimesStart()
    {
        var fetches = new List<(int Show, int Season)>();
        var ranked = await TmdbSearchService.PickShow(
            Engine(),
            Anime(),
            new SeriesMatchOptions { Query = "Kaguya" },
            [1],
            id => Task.FromResult<MetadataSeriesSearchResult?>(SplitCour(id)),
            (show, season) =>
            {
                fetches.Add((show, season));
                return Task.FromResult<IReadOnlyList<MetadataSearchResultEpisode>?>(season is 1 ? Weekly(new(2020, 10, 10), 24) : Weekly(new(2022, 1, 8), 12));
            }
        );

        var match = Assert.Single(ranked);
        Assert.Equal([(1, 1)], fetches);
        Assert.Equal((MatchRating.DateAndTitleMatches, 1, 12), (match.Rating, match.SeasonNumber, match.EpisodeAlignment?.Offset));
    }

    // The searches for one anime share the cap: a show lined up by an earlier search is
    // lined up again, a new one past the cap is not.
    [Fact]
    public async Task PickShow_SharesTheCapBetweenTheSearchesForOneAnime()
    {
        var fetches = new List<(int Show, int Season)>();
        var alignedShows = new HashSet<int>();
        Task<IReadOnlyList<SeriesMatch>> Search(IReadOnlyList<int> showIDs)
            => TmdbSearchService.PickShow(
                Engine(),
                Anime(),
                new SeriesMatchOptions { Query = "Kaguya" },
                showIDs,
                id => Task.FromResult<MetadataSeriesSearchResult?>(SplitCour(id) with
                {
                    Title = "Kaguya Sama",
                    FirstAiredAt = new(new DateOnly(2021, 1, 2)),
                    Seasons = [new() { SeasonNumber = 1, EpisodeCount = 12, FirstAiredAt = new(new DateOnly(2021, 1, 2)) }],
                }),
                (show, season) =>
                {
                    fetches.Add((show, season));
                    return Task.FromResult<IReadOnlyList<MetadataSearchResultEpisode>?>(Weekly(new(2019, 1, 5), 12));
                },
                alignedCandidates: 2,
                alignedShows: alignedShows
            );

        await Search([1, 2, 3]);
        await Search([2, 3]);

        Assert.Equal([(1, 1), (2, 1), (2, 1)], fetches);
        Assert.Equal([1, 2], alignedShows.Order());
    }

    // A show sent with its episodes, as a stored one is, needs no call.
    [Fact]
    public async Task PickShow_FetchesNothingForAShowWithItsEpisodes()
    {
        var fetches = 0;
        var show = SplitCour(1);
        show = show with { Seasons = [show.Seasons![0] with { Episodes = Weekly(new(2020, 10, 10), 24) }, show.Seasons[1]] };
        var ranked = await TmdbSearchService.PickShow(
            Engine(),
            Anime(),
            new SeriesMatchOptions { Query = "Kaguya" },
            [1],
            _ => Task.FromResult<MetadataSeriesSearchResult?>(show),
            (_, _) =>
            {
                fetches++;
                return Task.FromResult<IReadOnlyList<MetadataSearchResultEpisode>?>(null);
            }
        );

        Assert.Equal(0, fetches);
        Assert.Equal(1, Assert.Single(ranked).SeasonNumber);
    }

    // The season a date falls in is the last one begun by then, a few days'
    // grace included.
    [Theory]
    [InlineData(2021, 1, 2, 1)]
    [InlineData(2022, 1, 6, 2)]
    [InlineData(2020, 10, 8, 1)]
    [InlineData(2020, 1, 1, null)]
    public void SeasonHolding_TakesTheLastSeasonBegunByTheDate(int year, int month, int day, int? expected)
        => Assert.Equal(expected, TmdbSearchService.SeasonHolding(SplitCour(1).Seasons!, new(year, month, day)));

    #endregion
}
