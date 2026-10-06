using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Moq;
using Shoko.Abstractions.Config;
using Shoko.Abstractions.Config.Services;
using Shoko.Abstractions.Filtering;
using Shoko.Abstractions.Filtering.Services;
using Shoko.Abstractions.Filtering.Sorting;
using Shoko.Abstractions.Metadata;
using Shoko.Abstractions.Metadata.Airing;
using Shoko.Abstractions.Metadata.Anidb;
using Shoko.Abstractions.Metadata.Anidb.Enums;
using Shoko.Abstractions.Metadata.Anidb.Models;
using Shoko.Abstractions.Metadata.Containers;
using Shoko.Abstractions.Metadata.CrossReferences;
using Shoko.Abstractions.Metadata.Enums;
using Shoko.Abstractions.Metadata.Image;
using Shoko.Abstractions.Metadata.Services;
using Shoko.Abstractions.Metadata.Shoko;
using Shoko.Abstractions.Metadata.Stub;
using Shoko.Abstractions.User;
using Shoko.Server.API.v3.Helpers;
using Shoko.Server.API.v3.Models.Airing;
using Shoko.Server.API.v3.Models.Common;
using Shoko.Server.Extensions;
using Shoko.Server.Models.AniDB;
using Shoko.Server.Models.CrossReference;
using Shoko.Server.Models.Shoko;
using Shoko.Server.Repositories.Cached;
using Shoko.Server.Repositories.Cached.AniDB;
using Shoko.Server.Repositories.Direct;
using Shoko.Server.Server;
using Shoko.Server.Services;
using Shoko.Server.Services.Airing;
using Shoko.Server.Settings;
using Shoko.Tests.Infrastructure;
using Xunit;

using AniDBExtensions = Shoko.Server.Providers.AniDB.AniDBExtensions;
using CreatorType = Shoko.Server.Providers.AniDB.CreatorType;
using SeasonRules = Shoko.Server.Utilities.SeasonCalendar;

namespace Shoko.Tests.Services;

/// <summary>
/// Covers <see cref="AnidbAnimeCatalog"/>, the listing behind the AniDB anime
/// list and its seasons, and the season view's anime <see cref="AiringCalendarService"/>
/// reads and <see cref="SeasonAnimeBuilder"/> maps from it.
/// </summary>
[Collection(nameof(RepoFactoryCollection))]
public class AnidbAnimeCatalogTests
{
    #region Fixtures

    private static AniDB_Anime Anime(
        int id,
        string title,
        PartialDateOnly? airDate,
        AnimeType type = AnimeType.TVSeries,
        bool restricted = false,
        int rating = 0,
        int votes = 0,
        PartialDateOnly? endDate = null
    )
        => new()
        {
            AniDB_AnimeID = id,
            AnimeID = id,
            MainTitle = title,
            AirDate = airDate,
            EndDate = endDate,
            AnimeType = type,
            IsRestricted = restricted,
            Rating = rating,
            VoteCount = votes,
        };

    private static PartialDateOnly Date(int year, int month, int day)
        => new(year, month, day);

    // Regular episodes of an anime, numbered in order, one per date.
    private static IEnumerable<AniDB_Episode> Episodes(int animeID, params DateOnly?[] dates)
        => dates.Select((date, index) => Episode(animeID, index + 1, date));

    private static AniDB_Episode Episode(int animeID, int number, DateOnly? date, EpisodeType type = EpisodeType.Episode)
        => new()
        {
            AniDB_EpisodeID = animeID * 1_000 + (int)type * 100 + number,
            EpisodeID = animeID * 1_000 + (int)type * 100 + number,
            AnimeID = animeID,
            EpisodeNumber = number,
            EpisodeType = type,
            AirDate = date is { } day ? AniDBExtensions.GetAniDBDateAsSeconds(day.ToDateTime(TimeOnly.MinValue)) : null,
        };

    private static DateOnly Day(int year, int month, int day)
        => new(year, month, day);

    // A day well inside a season.
    private static DateOnly MidSeason((int Year, YearlySeason Season) season)
        => new(season.Year, 2 + 3 * (int)season.Season, 1);

    // The ID of the stand-in poster of an anime.
    private static Guid PosterID(int animeID)
        => new(animeID, 0, 0, new byte[8]);

    // The ID of the stand-in backdrop of an anime.
    private static Guid BackdropID(int animeID)
        => new(animeID, 1, 0, new byte[8]);

    // The ID of the stand-in poster of an anime's series.
    private static Guid SeriesPosterID(int animeID)
        => new(animeID, 2, 0, new byte[8]);

    private sealed class Harness : IDisposable
    {
        private readonly RepoFactoryScope _scope = new();

        public AnidbAnimeCatalog Catalog { get; }

        public Harness(
            IEnumerable<AniDB_Anime> anime,
            IEnumerable<AniDB_Episode>? episodes = null,
            IEnumerable<AnimeSeries>? series = null,
            IEnumerable<AniDB_Anime_Staff>? staff = null,
            IEnumerable<AniDB_Creator>? creators = null,
            IEnumerable<AniDB_Anime_Tag>? animeTags = null,
            IEnumerable<AniDB_Tag>? tags = null,
            IEnumerable<CrossRef_File_Episode>? fileCrossReferences = null,
            IEnumerable<VideoLocal>? videos = null,
            IReadOnlySet<int>? withPosters = null,
            IReadOnlySet<int>? withBackdrops = null,
            IReadOnlySet<int>? withSeriesPosters = null,
            IReadOnlyDictionary<int, AnidbAnimeChannelAirings>? channelAirings = null,
            IEnumerable<AniDB_Anime_StartSeasonOverride>? startSeasonOverrides = null,
            IMetadataFilteringService? filtering = null
        )
        {
            var animeTagRepository = CachedRepo.Build<AniDB_Anime_TagRepository, int, AniDB_Anime_Tag>(xref => xref.AniDB_Anime_TagID, animeTags);
            var tagRepository = CachedRepo.Build<AniDB_TagRepository, int, AniDB_Tag>(tag => tag.AniDB_TagID, tags);
            // The anime read their episodes through it for their regular air dates, and the episodes their anime.
            var episodeRepository = CachedRepo.Build<AniDB_EpisodeRepository, int, AniDB_Episode>(episode => episode.AniDB_EpisodeID, episodes);
            var animeRepository = CachedRepo.Build<AniDB_AnimeRepository, int, AniDB_Anime>(entry => entry.AniDB_AnimeID, anime);
            _scope.Set(animeTagRepository).Set(tagRepository).Set(episodeRepository).Set(animeRepository);
            _scope.With<AniDB_Anime_StartSeasonOverrideRepository, int, AniDB_Anime_StartSeasonOverride>(
                row => row.AniDB_Anime_StartSeasonOverrideID,
                startSeasonOverrides
            );

            var staffList = staff?.ToList() ?? [];
            var staffRepository = new Mock<AniDB_Anime_StaffRepository>(new object[1]);
            staffRepository
                .Setup(repository => repository.GetStudiosByAnimeIDs(It.IsAny<IReadOnlyCollection<int>>()))
                .Returns((IReadOnlyCollection<int> ids) => [.. staffList.Where(xref => xref.RoleType is CreatorRoleType.Studio && ids.Contains(xref.AnimeID))]);

            var textManager = new Mock<IMetadataTextManager>();
            textManager
                .Setup(manager => manager.GetPreferredTitle(It.IsAny<IWithTitles>()))
                .Returns((IWithTitles entry) => new TitleStub
                {
                    Source = MetadataSource.AniDB,
                    Language = TitleLanguage.English,
                    LanguageCode = "en",
                    Value = entry switch
                    {
                        AnimeSeries shokoSeries => $"Series {shokoSeries.AniDB_ID}",
                        AniDB_Anime anidbAnime => anidbAnime.MainTitle,
                        _ => string.Empty,
                    },
                });
            textManager
                .Setup(manager => manager.GetPreferredOverview(It.IsAny<IWithOverviews>()))
                .Returns((IWithOverviews entry) => new TextStub
                {
                    Source = MetadataSource.AniDB,
                    Language = TitleLanguage.English,
                    LanguageCode = "en",
                    Value = entry is AnimeSeries ? "series overview" : "anime overview",
                });

            Catalog = new ImageCatalog(
                withPosters ?? new HashSet<int>(),
                withBackdrops ?? new HashSet<int>(),
                withSeriesPosters ?? new HashSet<int>(),
                channelAirings ?? new Dictionary<int, AnidbAnimeChannelAirings>(),
                animeRepository,
                episodeRepository,
                CachedRepo.Build<AnimeSeriesRepository, int, AnimeSeries>(entry => entry.AnimeSeriesID, series),
                staffRepository.Object,
                CachedRepo.Build<AniDB_CreatorRepository, int, AniDB_Creator>(creator => creator.AniDB_CreatorID, creators),
                animeTagRepository,
                tagRepository,
                CachedRepo.Build<CrossRef_File_EpisodeRepository, int, CrossRef_File_Episode>(xref => xref.CrossRef_File_EpisodeID, fileCrossReferences),
                CachedRepo.Build<VideoLocalRepository, int, VideoLocal>(video => video.VideoLocalID, videos),
                textManager.Object,
                filtering ?? Mock.Of<IMetadataFilteringService>()
            );
        }

        public int[] IDs(AnidbAnimeListOptions? options = null)
            => [.. Catalog.GetAnime(options).Select(entry => entry.Anime.AnimeID)];

        public void Dispose()
            => _scope.Dispose();
    }

    /// <summary>
    /// A catalog whose anime have a poster or a backdrop only when listed,
    /// identified by <see cref="PosterID"/> and <see cref="BackdropID"/>, whose
    /// series have a poster only when listed, by <see cref="SeriesPosterID"/>,
    /// and whose anime air on the asked-for channels as listed.
    /// </summary>
    private sealed class ImageCatalog(
        IReadOnlySet<int> withPosters,
        IReadOnlySet<int> withBackdrops,
        IReadOnlySet<int> withSeriesPosters,
        IReadOnlyDictionary<int, AnidbAnimeChannelAirings> channelAirings,
        AniDB_AnimeRepository animeRepository,
        AniDB_EpisodeRepository episodeRepository,
        AnimeSeriesRepository seriesRepository,
        AniDB_Anime_StaffRepository staffRepository,
        AniDB_CreatorRepository creatorRepository,
        AniDB_Anime_TagRepository animeTagRepository,
        AniDB_TagRepository tagRepository,
        CrossRef_File_EpisodeRepository fileCrossReferenceRepository,
        VideoLocalRepository videoRepository,
        IMetadataTextManager textManager,
        IMetadataFilteringService filteringService
    ) : AnidbAnimeCatalog(
        animeRepository,
        episodeRepository,
        seriesRepository,
        staffRepository,
        creatorRepository,
        animeTagRepository,
        tagRepository,
        fileCrossReferenceRepository,
        videoRepository,
        textManager,
        filteringService,
        null!
    )
    {
        internal override IReadOnlyDictionary<int, AnidbAnimeChannelAirings> GetChannelAirings(IReadOnlySet<Guid> channelIDs, DateTime now)
            => channelAirings;

        protected override IImage? GetImage(IWithImages entry, ImageEntityType type)
            => (entry, type) switch
            {
                (AnimeSeries series, ImageEntityType.Primary) when withSeriesPosters.Contains(series.AniDB_ID)
                    => Mock.Of<IImage>(image => image.ID == SeriesPosterID(series.AniDB_ID)),
                (AnimeSeries, _) => null,
                (AniDB_Anime anime, ImageEntityType.Primary) when withPosters.Contains(anime.AnimeID)
                    => Mock.Of<IImage>(image => image.ID == PosterID(anime.AnimeID)),
                (AniDB_Anime anime, ImageEntityType.Backdrop) when withBackdrops.Contains(anime.AnimeID)
                    => Mock.Of<IImage>(image => image.ID == BackdropID(anime.AnimeID)),
                _ => null,
            };
    }

    private static AniDB_Anime[] PastAnime()
        =>
        [
            Anime(1, "Bravo", Date(2015, 4, 10), rating: 700),
            Anime(2, "Alpha", Date(2015, 7, 5), rating: 900),
            Anime(3, "Charlie", Date(2015, 10, 10), type: AnimeType.Movie, rating: 800),
            Anime(4, "Delta", Date(2016, 1, 15), type: AnimeType.OVA),
            // Its own date says Spring, its episodes say Summer.
            Anime(5, "Echo", Date(2015, 4, 1)),
            // Dated, but its episodes are not.
            Anime(6, "Foxtrot", Date(2015, 5, 12), endDate: Date(2015, 5, 30)),
            Anime(7, "Golf", null),
        ];

    private static AniDB_Episode[] PastEpisodes()
        =>
        [
            // Spring 2015 only.
            .. Episodes(1, Day(2015, 4, 10), Day(2015, 5, 10), Day(2015, 5, 24)),
            // Summer and Fall 2015, a show running across both.
            .. Episodes(2, Day(2015, 7, 5), Day(2015, 8, 10), Day(2015, 10, 20)),
            // Fall 2015.
            .. Episodes(3, Day(2015, 10, 10)),
            // Winter 2016.
            .. Episodes(4, Day(2016, 1, 15)),
            // Summer 2015, its Spring special left out.
            .. Episodes(5, Day(2015, 7, 10)),
            Episode(5, 1, Day(2015, 4, 1), EpisodeType.Special),
            // Undated regular episodes and a dated special: by its own dates, Spring 2015.
            .. Episodes(6, null, null),
            Episode(6, 1, Day(2015, 5, 12), EpisodeType.Special),
        ];

    private static Harness PastHarness(IReadOnlySet<int>? withPosters = null)
        => new(PastAnime(), PastEpisodes(), withPosters: withPosters);

    private static AnidbAnimeListOptions InSeasons(params (int Year, YearlySeason Season)[] seasons)
        => new() { Seasons = seasons };

    private static (int Year, YearlySeason Season) CurrentSeason()
        => SeasonRules.GetYearlySeason(DateTime.Today.ToDateOnly());

    #endregion

    #region Seasons Filter

    [Theory]
    [InlineData(2015, YearlySeason.Spring, new[] { 1, 6 })]
    [InlineData(2015, YearlySeason.Summer, new[] { 2, 5 })]
    [InlineData(2015, YearlySeason.Fall, new[] { 2, 3 })]
    [InlineData(2016, YearlySeason.Winter, new[] { 4 })]
    [InlineData(2014, YearlySeason.Fall, new int[0])]
    public void Seasons_OneSeason_TakesTheAnimeInIt(int year, YearlySeason season, int[] expected)
    {
        using var harness = PastHarness();

        Assert.Equal(expected.Order(), harness.IDs(InSeasons((year, season))).Order());
    }

    [Fact]
    public void Seasons_SeveralSeasons_MatchesAnyOfThem()
    {
        using var harness = PastHarness();

        var ids = harness.IDs(InSeasons((2015, YearlySeason.Spring), (2016, YearlySeason.Winter)));

        Assert.Equal([1, 4, 6], ids.Order());
    }

    [Fact]
    public void Seasons_BeyondTheNextSeason_MatchNothing()
    {
        var next = SeasonRules.GetNextYearlySeason(CurrentSeason());
        var afterNext = SeasonRules.GetNextYearlySeason(next);
        using var harness = new Harness([Anime(1, "Upcoming", null)], Episodes(1, MidSeason(next), MidSeason(afterNext)));

        Assert.Equal([1], harness.IDs(InSeasons(next)));
        Assert.Empty(harness.IDs(InSeasons(afterNext)));
    }

    #endregion

    #region Other Filters

    [Fact]
    public void Types_OnlyTheGivenTypes()
    {
        using var harness = PastHarness();

        Assert.Equal([3, 4], harness.IDs(new() { Types = [AnimeType.Movie, AnimeType.OVA] }).Order());
    }

    [Theory]
    [InlineData(InclusionFilter.True, new[] { 1, 2, 3 })]
    [InlineData(InclusionFilter.Only, new[] { 1 })]
    [InlineData(InclusionFilter.False, new[] { 2, 3 })]
    public void InCollection_SplitsOnTheShokoSeries(InclusionFilter inCollection, int[] expected)
    {
        using var harness = new Harness(
            [Anime(1, "A", Date(2015, 4, 1)), Anime(2, "B", Date(2015, 4, 1)), Anime(3, "C", Date(2015, 4, 1))],
            series: [new AnimeSeries { AnimeSeriesID = 10, AniDB_ID = 1 }]
        );

        Assert.Equal(expected, harness.IDs(new() { InCollection = inCollection }).Order());
    }

    [Fact]
    public void Filter_KeepsOnlyTheAnimeOfTheSeriesItPasses()
    {
        var series = new[] { new AnimeSeries { AnimeSeriesID = 10, AniDB_ID = 1 }, new AnimeSeries { AnimeSeriesID = 20, AniDB_ID = 2 } };
        var filter = Mock.Of<IFilter>();
        var filtering = new Mock<IMetadataFilteringService>();
        filtering
            .Setup(service => service.GetAllFilteredSeries(filter, null, null, true, It.IsAny<CancellationToken>()))
            .Returns([series[0]]);
        using var harness = new Harness(
            [Anime(1, "A", Date(2015, 4, 1)), Anime(2, "B", Date(2015, 4, 1)), Anime(3, "C", Date(2015, 4, 1))],
            series: series,
            filtering: filtering.Object
        );

        Assert.Equal([1], harness.IDs(new() { Filter = filter }));
    }

    [Theory]
    [InlineData(true, new[] { 3, 1, 2 })]
    [InlineData(false, new[] { 1, 2, 3 })]
    public void Filter_ItsSortingExpressionDecidesTheOrder(bool sorted, int[] expected)
    {
        var series = new[]
        {
            new AnimeSeries { AnimeSeriesID = 10, AniDB_ID = 1 },
            new AnimeSeries { AnimeSeriesID = 20, AniDB_ID = 2 },
            new AnimeSeries { AnimeSeriesID = 30, AniDB_ID = 3 },
        };
        var filter = Mock.Of<IFilter>(entry => entry.SortingExpression == (sorted ? Mock.Of<ISortingExpression>() : null));
        var filtering = new Mock<IMetadataFilteringService>();
        filtering
            .Setup(service => service.GetAllFilteredSeries(filter, null, null, !sorted, It.IsAny<CancellationToken>()))
            .Returns([series[2], series[0], series[1]]);
        using var harness = new Harness(
            [Anime(1, "A", Date(2015, 4, 1)), Anime(2, "B", Date(2015, 4, 1)), Anime(3, "C", Date(2015, 4, 1))],
            series: series,
            filtering: filtering.Object
        );

        Assert.Equal(expected, harness.IDs(new() { Filter = filter, OrderBy = AnidbAnimeListOrder.Title }));
    }

    [Theory]
    [InlineData(InclusionFilter.True, new[] { 1, 2 })]
    [InlineData(InclusionFilter.Only, new[] { 2 })]
    [InlineData(InclusionFilter.False, new[] { 1 })]
    public void Restricted_SplitsOnTheRestrictedFlag(InclusionFilter restricted, int[] expected)
    {
        using var harness = new Harness([Anime(1, "A", Date(2015, 4, 1)), Anime(2, "B", Date(2015, 4, 1), restricted: true)]);

        Assert.Equal(expected, harness.IDs(new() { IncludeRestricted = restricted }).Order());
    }

    [Fact]
    public void User_TheirRestrictionsAlwaysApply()
    {
        var user = new Mock<IUser>();
        user.Setup(u => u.IsAllowedToSee(It.IsAny<IAnidbAnime>())).Returns((IAnidbAnime anime) => anime.AnidbID != 2);
        using var harness = new Harness([Anime(1, "A", Date(2015, 4, 1)), Anime(2, "B", Date(2015, 4, 1))]);

        Assert.Equal([1], harness.IDs(new() { User = user.Object }));
    }

    [Fact]
    public void TitlePrefix_MatchesThePreferredTitleIgnoringCase()
    {
        using var harness = new Harness(
            [Anime(1, "Alpha", Date(2015, 4, 1)), Anime(2, "Beta", Date(2015, 4, 1))],
            series: [new AnimeSeries { AnimeSeriesID = 10, AniDB_ID = 2 }]
        );

        Assert.Equal([1], harness.IDs(new() { TitlePrefix = "alp" }));
        // A series in the collection goes by its series' title.
        Assert.Equal([2], harness.IDs(new() { TitlePrefix = "series" }));
    }

    [Fact]
    public void GetTitle_ByID_PrefersTheSeries_AndIsNullWhenUncachedOrHidden()
    {
        var user = new Mock<IUser>();
        user.Setup(u => u.IsAllowedToSee(It.IsAny<IAnidbAnime>())).Returns((IAnidbAnime anime) => anime.AnidbID != 1);
        using var harness = new Harness(
            [Anime(1, "Alpha", Date(2015, 4, 1)), Anime(2, "Beta", Date(2015, 4, 1))],
            series: [new AnimeSeries { AnimeSeriesID = 10, AniDB_ID = 2 }]
        );

        Assert.Equal("Alpha", harness.Catalog.GetTitle(1));
        Assert.Equal("Series 2", harness.Catalog.GetTitle(2));
        Assert.Null(harness.Catalog.GetTitle(3));
        Assert.Null(harness.Catalog.GetTitle(1, user.Object));
        Assert.Equal("Series 2", harness.Catalog.GetTitle(2, user.Object));
    }

    #endregion

    #region Ordering

    [Fact]
    public void OrderBy_DefaultsToTitleWithoutSeasons()
    {
        using var harness = PastHarness();

        Assert.Equal([2, 1, 3, 4, 5, 6, 7], harness.IDs());
    }

    [Fact]
    public void OrderBy_DefaultsToAirDateWithSeasons_ThenTitle()
    {
        using var harness = new Harness(
            [
                Anime(1, "Zulu", Date(2015, 10, 10)),
                Anime(2, "Yankee", Date(2015, 10, 1)),
                Anime(3, "Alpha", Date(2015, 10, 10)),
            ],
            [.. Episodes(1, Day(2015, 10, 10)), .. Episodes(2, Day(2015, 10, 1)), .. Episodes(3, Day(2015, 10, 10))]
        );

        Assert.Equal([2, 3, 1], harness.IDs(InSeasons((2015, YearlySeason.Fall))));
    }

    [Fact]
    public void OrderBy_AirDate_PutsUnknownDatesLast()
    {
        using var harness = PastHarness();

        Assert.Equal([5, 1, 6, 2, 3, 4, 7], harness.IDs(new() { OrderBy = AnidbAnimeListOrder.AirDate }));
    }

    [Fact]
    public void OrderBy_Rating_HighestFirst()
    {
        using var harness = PastHarness();

        Assert.Equal([2, 3, 1], harness.IDs(new() { OrderBy = AnidbAnimeListOrder.Rating }).Take(3));
    }

    #endregion

    #region Seasons Listing

    [Fact]
    public void GetSeasons_CountsEachSeasonNewestFirst_AsTheListDoes()
    {
        using var harness = PastHarness();

        var seasons = harness.Catalog.GetSeasons()
            .Where(season => season.Year < 2017)
            .Select(season => (season.Year, season.Season, season.Count))
            .ToList();

        Assert.Equal(
            [
                (2016, YearlySeason.Winter, 1),
                (2015, YearlySeason.Fall, 2),
                (2015, YearlySeason.Summer, 2),
                (2015, YearlySeason.Spring, 2),
            ],
            seasons
        );
        foreach (var (year, season, count) in seasons)
            Assert.Equal(count, harness.IDs(InSeasons((year, season))).Length);
    }

    [Fact]
    public void GetSeasons_AnimeWithoutDatedRegularEpisodes_GoesByItsOwnDates()
    {
        using var harness = new Harness([PastAnime()[5], PastAnime()[6]], PastEpisodes().Where(episode => episode.AnimeID == 6));

        var seasons = harness.Catalog.GetSeasons().Where(season => season.Count > 0).Select(season => (season.Year, season.Season, season.Count));

        Assert.Equal([(2015, YearlySeason.Spring, 1)], seasons);
    }

    [Fact]
    public void GetSeasons_AnimeBefore1970_StartsOnItsOwnDate()
    {
        var anime = Anime(1, "Old", Date(1965, 4, 20));
        anime.EndDate = Date(1965, 8, 1);
        using var harness = new Harness([anime], Episodes(1, null, null));

        var seasons = harness.Catalog.GetSeasons().Where(season => season.Count > 0).Select(season => (season.Year, season.Season));

        Assert.Equal([(1965, YearlySeason.Spring)], seasons);
    }

    [Fact]
    public void GetSeasons_APlaceholderTheAnimesDateStandsIn_CountsAsDated()
    {
        // A single episode on 1970-01-01 is stored as AniDB's placeholder, 0.
        using var harness = new Harness([Anime(1, "Epoch", Date(1970, 1, 1))], Episodes(1, Day(1970, 1, 1)));

        var seasons = harness.Catalog.GetSeasons().Where(season => season.Count > 0).Select(season => (season.Year, season.Season));

        Assert.Equal([SeasonRules.GetYearlySeason(Day(1970, 1, 1))], seasons);
    }

    [Fact]
    public void GetSeasons_ListsAtMostOneSeasonAhead()
    {
        var current = CurrentSeason();
        var next = SeasonRules.GetNextYearlySeason(current);
        var afterNext = SeasonRules.GetNextYearlySeason(next);
        using var harness = new Harness(
            [Anime(1, "Running", null), Anime(2, "Later", null)],
            [.. Episodes(1, MidSeason(current), MidSeason(next), MidSeason(afterNext)), .. Episodes(2, MidSeason(afterNext))]
        );

        var seasons = harness.Catalog.GetSeasons().Select(season => (season.Year, season.Season, season.Count));

        Assert.Equal([(next.Year, next.Season, 1), (current.Year, current.Season, 1)], seasons);
    }

    [Fact]
    public void GetSeasons_At_DecidesTheSeasonUnderWay()
    {
        using var harness = PastHarness();

        var seasons = harness.Catalog.GetSeasons(new() { At = new DateTime(2015, 8, 1, 12, 0, 0, DateTimeKind.Utc) });

        // Winter 2016 is two seasons ahead of Summer 2015, so it is left out.
        Assert.Equal((2015, YearlySeason.Fall), (seasons[0].Year, seasons[0].Season));
        Assert.Equal((2015, YearlySeason.Summer), seasons.Where(season => season.IsCurrent).Select(season => (season.Year, season.Season)).Single());
    }

    [Fact]
    public void GetSeasons_AppliesTheOtherFilters()
    {
        using var harness = PastHarness();

        var seasons = harness.Catalog.GetSeasons(new() { Types = [AnimeType.Movie] }).Where(season => season.Count > 0);

        Assert.Equal([(2015, YearlySeason.Fall)], seasons.Select(season => (season.Year, season.Season)));
    }

    #endregion

    #region Channels

    // The asked-for channels; the stand-in catalog answers for any.
    private static readonly IReadOnlySet<Guid> _channels = new HashSet<Guid> { Guid.NewGuid() };

    // Where an anime's airings on the channels fall.
    private static AnidbAnimeChannelAirings OnChannels(bool hasUpcoming, params (int Year, YearlySeason Season)[] seasons)
    {
        var airings = new AnidbAnimeChannelAirings { HasUpcoming = hasUpcoming };
        airings.Seasons.UnionWith(seasons);
        return airings;
    }

    // Anime 2 airs on the channels in Summer 2015, anime 5 only in Fall 2015.
    private static Harness ChannelHarness()
        => new(
            PastAnime(),
            PastEpisodes(),
            channelAirings: new Dictionary<int, AnidbAnimeChannelAirings>
            {
                [2] = OnChannels(false, (2015, YearlySeason.Summer)),
                [5] = OnChannels(false, (2015, YearlySeason.Fall)),
            }
        );

    [Fact]
    public void Channels_ListOnlyTheAnimeAiringOnThemInTheSeason()
    {
        using var harness = ChannelHarness();

        Assert.Equal([2, 5], harness.IDs(InSeasons((2015, YearlySeason.Summer))).Order());
        Assert.Equal([2], harness.IDs(new() { Seasons = [(2015, YearlySeason.Summer)], ChannelIDs = _channels }));
    }

    [Fact]
    public void Channels_CountOnlyTheAnimeAiringOnThemInEachSeason()
    {
        using var harness = ChannelHarness();

        var all = harness.Catalog.GetSeasons().Where(season => season.Year == 2015).Select(season => (season.Season, season.Count));
        var onChannels = harness.Catalog.GetSeasons(new() { ChannelIDs = _channels })
            .Where(season => season.Count > 0)
            .Select(season => (season.Year, season.Season, season.Count));

        Assert.Equal([(YearlySeason.Fall, 2), (YearlySeason.Summer, 2), (YearlySeason.Spring, 2)], all);
        // Anime 2 is in Fall too, but airs on the channels in Summer only.
        Assert.Equal([(2015, YearlySeason.Summer, 1)], onChannels);
    }

    [Fact]
    public void Channels_AnUpcomingAiringCountsFromTheSeasonUnderWayOn()
    {
        var current = CurrentSeason();
        var next = SeasonRules.GetNextYearlySeason(current);
        using var harness = new Harness(
            [Anime(1, "Running", null), Anime(2, "Old", null)],
            [.. Episodes(1, MidSeason(current), MidSeason(next)), .. Episodes(2, Day(2015, 4, 10))],
            channelAirings: new Dictionary<int, AnidbAnimeChannelAirings>
            {
                [1] = OnChannels(true),
                [2] = OnChannels(true),
            }
        );

        Assert.Equal([1], harness.IDs(new() { Seasons = [current], ChannelIDs = _channels }));
        Assert.Equal([1], harness.IDs(new() { Seasons = [next], ChannelIDs = _channels }));
        Assert.Empty(harness.IDs(new() { Seasons = [(2015, YearlySeason.Spring)], ChannelIDs = _channels }));
    }

    #endregion

    #region Season Images

    // The images a season gets, as poster and backdrop IDs.
    private static (Guid? Poster, Guid? Backdrop) ImagesOf(
        Harness harness,
        (int Year, YearlySeason Season) season,
        AnidbAnimeListOptions? options = null
    )
    {
        var entry = harness.Catalog.GetSeasons(options, includeImages: true).Single(entry => (entry.Year, entry.Season) == season);
        return (entry.Poster?.ID, entry.Backdrop?.ID);
    }

    [Fact]
    public void GetSeasons_Images_OnlyWhenAsked()
    {
        var all = new HashSet<int> { 1, 2, 3, 4, 5 };
        using var harness = new Harness(PastAnime(), PastEpisodes(), withPosters: all, withBackdrops: all);

        Assert.All(harness.Catalog.GetSeasons(), season => Assert.True(season is { Poster: null, Backdrop: null }));
        Assert.All(harness.Catalog.GetSeasons(includeImages: true).Where(season => season.Count > 0), season => Assert.NotNull(season.Poster));
    }

    [Theory]
    [InlineData(new[] { 1, 2, 3 }, 2)]
    [InlineData(new[] { 1, 3 }, 1)]
    [InlineData(new[] { 3 }, 3)]
    public void GetSeasons_Images_FromStartersFirst_ThenTheNewestCarryOvers(int[] withPosters, int expected)
    {
        AniDB_Anime[] anime =
        [
            Anime(1, "Carried Over", null, rating: 700, votes: 1_000),
            Anime(2, "New", null, rating: 500, votes: 10),
            Anime(3, "Carried Over Longer", null, rating: 900, votes: 10_000),
        ];
        AniDB_Episode[] episodes =
        [
            .. Episodes(1, Day(2015, 7, 5), Day(2015, 10, 20)),
            .. Episodes(2, Day(2015, 10, 10)),
            .. Episodes(3, Day(2015, 4, 10), Day(2015, 7, 10), Day(2015, 10, 15)),
        ];
        using var harness = new Harness(anime, episodes, withPosters: withPosters.ToHashSet());

        Assert.Equal(PosterID(expected), ImagesOf(harness, (2015, YearlySeason.Fall)).Poster);
    }

    [Fact]
    public void GetSeasons_Images_RankByWeightedRating()
    {
        // Neither the highest rating on few votes nor the most votes on a poor
        // rating beats a solid rating on many votes.
        AniDB_Anime[] anime =
        [
            Anime(1, "Few Votes", null, rating: 1_000, votes: 5),
            Anime(2, "Solid", null, rating: 880, votes: 2_000),
            Anime(3, "Middling", null, rating: 600, votes: 100),
            Anime(4, "Most Votes", null, rating: 500, votes: 3_000),
        ];
        AniDB_Episode[] episodes =
        [
            .. Episodes(1, Day(2015, 4, 10)),
            .. Episodes(2, Day(2015, 4, 20)),
            .. Episodes(3, Day(2015, 4, 5)),
            .. Episodes(4, Day(2015, 4, 5)),
        ];
        using var harness = new Harness(anime, episodes, withPosters: new HashSet<int> { 1, 2, 3, 4 });

        Assert.Equal(PosterID(2), ImagesOf(harness, (2015, YearlySeason.Spring)).Poster);
    }

    [Theory]
    [InlineData(new[] { 1, 2, 3 }, 2)]
    [InlineData(new[] { 1, 3 }, 3)]
    [InlineData(new[] { 1 }, 1)]
    public void GetSeasons_Images_TiesGoToTheEarlierStartThenTheLowerID(int[] withPosters, int expected)
    {
        // No votes, as in a season yet to air.
        AniDB_Anime[] anime = [Anime(1, "Later", null), Anime(2, "Early", null), Anime(3, "Early Too", null)];
        AniDB_Episode[] episodes = [.. Episodes(1, Day(2015, 5, 10)), .. Episodes(2, Day(2015, 4, 10)), .. Episodes(3, Day(2015, 4, 10))];
        using var harness = new Harness(anime, episodes, withPosters: withPosters.ToHashSet());

        Assert.Equal(PosterID(expected), ImagesOf(harness, (2015, YearlySeason.Spring)).Poster);
    }

    [Fact]
    public void GetSeasons_Images_BackdropOfTheAnimeThePosterComesFrom()
    {
        AniDB_Anime[] anime = [Anime(1, "Top", null, rating: 900, votes: 1_000), Anime(2, "Second", null, rating: 500, votes: 1_000)];
        AniDB_Episode[] episodes = [.. Episodes(1, Day(2015, 4, 10)), .. Episodes(2, Day(2015, 4, 10))];

        using (var harness = new Harness(anime, episodes, withPosters: new HashSet<int> { 1, 2 }, withBackdrops: new HashSet<int> { 1, 2 }))
            Assert.Equal(((Guid?)PosterID(1), (Guid?)BackdropID(1)), ImagesOf(harness, (2015, YearlySeason.Spring)));

        using (var harness = new Harness(anime, episodes, withPosters: new HashSet<int> { 1, 2 }, withBackdrops: new HashSet<int> { 2 }))
            Assert.Equal(((Guid?)PosterID(1), (Guid?)null), ImagesOf(harness, (2015, YearlySeason.Spring)));
    }

    [Fact]
    public void GetSeasons_Images_LeaveOutFilteredAnime()
    {
        AniDB_Anime[] anime =
        [
            Anime(1, "Open", null, rating: 100, votes: 10),
            Anime(2, "Restricted", null, restricted: true, rating: 900, votes: 1_000),
        ];
        using var harness = new Harness(
            anime,
            [.. Episodes(1, Day(2015, 4, 10)), .. Episodes(2, Day(2015, 4, 10))],
            withPosters: new HashSet<int> { 1, 2 }
        );

        Assert.Equal(PosterID(1), ImagesOf(harness, (2015, YearlySeason.Spring), new() { IncludeRestricted = InclusionFilter.False }).Poster);
    }

    #endregion

    #region Start Season Overrides

    // Anime 1 moved from Spring to Summer 2015, anime 6 back to Winter 2015.
    private static Harness OverriddenHarness()
        => new(
            PastAnime(),
            PastEpisodes(),
            startSeasonOverrides:
            [
                new AniDB_Anime_StartSeasonOverride { AniDB_Anime_StartSeasonOverrideID = 1, AnimeID = 1, Year = 2015, Season = YearlySeason.Summer },
                new AniDB_Anime_StartSeasonOverride { AniDB_Anime_StartSeasonOverrideID = 2, AnimeID = 6, Year = 2015, Season = YearlySeason.Winter },
            ]
        );

    [Theory]
    [InlineData(2015, YearlySeason.Winter, new[] { 6 })]
    [InlineData(2015, YearlySeason.Spring, new[] { 6 })]
    [InlineData(2015, YearlySeason.Summer, new[] { 1, 2, 5 })]
    public void StartSeasonOverride_TheSeasonsFilterGoesByIt(int year, YearlySeason season, int[] expected)
    {
        using var harness = OverriddenHarness();

        Assert.Equal(expected.Order(), harness.IDs(InSeasons((year, season))).Order());
    }

    [Fact]
    public void StartSeasonOverride_TheSeasonCountsGoByIt()
    {
        using var harness = OverriddenHarness();

        var counts = harness.Catalog.GetSeasons()
            .Where(season => season.Year is 2015)
            .ToDictionary(season => season.Season, season => season.Count);

        Assert.Equal(1, counts[YearlySeason.Winter]);
        Assert.Equal(1, counts[YearlySeason.Spring]);
        Assert.Equal(3, counts[YearlySeason.Summer]);
    }

    [Fact]
    public void StartSeasonOverride_TheStartSeasonIsItAndFlagged()
    {
        using var harness = OverriddenHarness();
        var anime = harness.Catalog.GetAnime().ToDictionary(entry => entry.Anime.AnimeID, entry => entry.Anime);

        Assert.Equal((2015, YearlySeason.Summer), harness.Catalog.GetStartSeason(anime[1]));
        Assert.True(harness.Catalog.IsStartSeasonOverridden(anime[1]));
        Assert.Equal((2015, YearlySeason.Summer), harness.Catalog.GetStartSeason(anime[2]));
        Assert.False(harness.Catalog.IsStartSeasonOverridden(anime[2]));
    }

    [Fact]
    public void StartSeasonOverride_TheCalendarServiceSeesIt()
    {
        using var harness = OverriddenHarness();
        var calendar = new AiringCalendarService(harness.Catalog, AiringService(_ => []).Object);

        var summer = calendar.GetSeasonAnime(2015, YearlySeason.Summer, airingOptions: new() { At = _readAt }).ToDictionary(entry => entry.Anime.AnidbID);
        var sections = calendar.GetSeasonSections(2015, YearlySeason.Spring, airingOptions: new() { At = _readAt });
        var byYear = calendar.GetSeasonsByYear().Single(year => year.Year is 2015);

        Assert.Equal((2015, YearlySeason.Summer), summer[1].StartSeason);
        Assert.True(summer[1].IsStartSeasonOverridden);
        Assert.False(summer[2].IsStartSeasonOverridden);
        // Started in Winter by hand, so it carries on into Spring.
        Assert.Equal([6], sections.Single(section => section.Definition.ID is "continuing").Anime.Select(entry => entry.Anime.AnidbID));
        Assert.Equal(1, byYear.Seasons.Single(season => season.Season is YearlySeason.Winter).Count);
    }

    [Fact]
    public void StartSeasonOverride_SeasonAnimeCarriesIt()
    {
        using var harness = OverriddenHarness();

        var models = Build(harness, AiringService(_ => [])).ToDictionary(anime => anime.ID);

        Assert.Equal(new SeasonWithYear(2015, YearlySeason.Summer), models[1].StartSeason);
        Assert.True(models[1].IsStartSeasonOverridden);
        Assert.False(models[2].IsStartSeasonOverridden);
    }

    #endregion

    #region Season Anime

    // The time the season view is read at, and its day.
    private static readonly DateTime _readAt = new(2026, 10, 5, 0, 0, 0, DateTimeKind.Utc);

    private static readonly DateOnly _today = DateOnly.FromDateTime(_readAt);

    private static Harness CardHarness()
        => new(
            [Anime(1, "Alpha", Date(2015, 4, 1)), Anime(2, "Beta", Date(2015, 4, 1))],
            // Starts in Spring, its Winter special left out; Beta has no episodes.
            [.. Episodes(1, Day(2015, 4, 1), Day(2015, 7, 1)), Episode(1, 1, Day(2015, 1, 10), EpisodeType.Special)],
            series: [new AnimeSeries { AnimeSeriesID = 10, AniDB_ID = 1 }],
            staff:
            [
                new AniDB_Anime_Staff { AniDB_Anime_StaffID = 1, AnimeID = 1, CreatorID = 100, RoleType = CreatorRoleType.Studio, Role = "Animation Work", Ordering = 2 },
                new AniDB_Anime_Staff { AniDB_Anime_StaffID = 2, AnimeID = 1, CreatorID = 101, RoleType = CreatorRoleType.Studio, Role = "Animation Work", Ordering = 1 },
                new AniDB_Anime_Staff { AniDB_Anime_StaffID = 3, AnimeID = 1, CreatorID = 102, RoleType = CreatorRoleType.Director, Role = "Direction", Ordering = 0 },
            ],
            creators:
            [
                new AniDB_Creator { AniDB_CreatorID = 1, CreatorID = 100, Name = "Studio One", Type = CreatorType.Company },
                new AniDB_Creator { AniDB_CreatorID = 2, CreatorID = 101, Name = "Studio Two", Type = CreatorType.Company },
                new AniDB_Creator { AniDB_CreatorID = 3, CreatorID = 102, Name = "A Director", Type = CreatorType.Person },
            ],
            animeTags:
            [
                new AniDB_Anime_Tag { AniDB_Anime_TagID = 1, AnimeID = 1, TagID = 1, Weight = 300 },
                new AniDB_Anime_Tag { AniDB_Anime_TagID = 2, AnimeID = 1, TagID = 2, Weight = 600 },
                new AniDB_Anime_Tag { AniDB_Anime_TagID = 3, AnimeID = 1, TagID = 3, Weight = 500 },
                new AniDB_Anime_Tag { AniDB_Anime_TagID = 4, AnimeID = 1, TagID = 4, Weight = 600, LocalSpoiler = true },
                new AniDB_Anime_Tag { AniDB_Anime_TagID = 5, AnimeID = 1, TagID = 5, Weight = 600 },
                new AniDB_Anime_Tag { AniDB_Anime_TagID = 6, AnimeID = 1, TagID = 6, Weight = 200 },
                new AniDB_Anime_Tag { AniDB_Anime_TagID = 7, AnimeID = 1, TagID = 2798, Weight = 0 },
            ],
            tags:
            [
                new AniDB_Tag { AniDB_TagID = 1, TagID = 1, TagNameSource = "comedy", Verified = true },
                new AniDB_Tag { AniDB_TagID = 2, TagID = 2, TagNameSource = "action", Verified = true },
                // Not a genre.
                new AniDB_Tag { AniDB_TagID = 3, TagID = 3, TagNameSource = "school", Verified = true },
                // A genre, but a spoiler for this anime.
                new AniDB_Tag { AniDB_TagID = 4, TagID = 4, TagNameSource = "romance", Verified = true },
                // A genre, but a spoiler for every anime.
                new AniDB_Tag { AniDB_TagID = 5, TagID = 5, TagNameSource = "tragedy", Verified = true, GlobalSpoiler = true },
                new AniDB_Tag { AniDB_TagID = 6, TagID = 6, TagNameSource = "fantasy", Verified = true },
                new AniDB_Tag { AniDB_TagID = 7, TagID = 2798, TagNameSource = "manga", Verified = true },
            ],
            fileCrossReferences:
            [
                // One file covering two episodes counts once.
                new CrossRef_File_Episode { CrossRef_File_EpisodeID = 1, AnimeID = 1, EpisodeID = 1, Hash = "AAA", FileSize = 1 },
                new CrossRef_File_Episode { CrossRef_File_EpisodeID = 2, AnimeID = 1, EpisodeID = 2, Hash = "AAA", FileSize = 1 },
                new CrossRef_File_Episode { CrossRef_File_EpisodeID = 3, AnimeID = 1, EpisodeID = 3, Hash = "BBB", FileSize = 2 },
                // Linked, but not on disk.
                new CrossRef_File_Episode { CrossRef_File_EpisodeID = 4, AnimeID = 1, EpisodeID = 4, Hash = "CCC", FileSize = 3 },
            ],
            videos:
            [
                new VideoLocal { VideoLocalID = 1, Hash = "AAA", FileSize = 1 },
                new VideoLocal { VideoLocalID = 2, Hash = "BBB", FileSize = 2 },
            ]
        );

    // An airing of an episode at a time on a channel, or a date-only entry when it has no time.
    private static IEpisodeAiring Airing(int episodeID, DateTime? airedAt, string? channel = null, DateOnly? airDate = null)
        => new FakeAiring
        {
            ID = Guid.NewGuid(),
            EpisodeID = new MetadataGuid(MetadataSource.AniDB, MetadataEntityType.Episode, episodeID.ToString()),
            AiredAt = airedAt,
            IsDateOnly = airedAt is null,
            AirDate = airDate,
            Channel = channel is null
                ? null
                : Mock.Of<IAiringChannel>(entry => entry.ChannelID == Guid.NewGuid() && entry.Name == channel && entry.Aliases == Array.Empty<string>()),
        };

    // An airing schedule service answering the single next airing and the next airing per channel of each series.
    private static Mock<IAiringScheduleService> AiringService(
        Func<ISeries, IReadOnlyList<IEpisodeAiring>> next,
        Func<ISeries, IReadOnlyList<IEpisodeAiring>>? perChannel = null
    )
    {
        var service = new Mock<IAiringScheduleService>();
        service
            .Setup(entry => entry.GetAiringsForSeries(It.IsAny<IEnumerable<ISeries>>(), It.IsAny<EpisodeAiringFilteringOptions?>()))
            .Returns((IEnumerable<ISeries> series, EpisodeAiringFilteringOptions? options) =>
                series.ToDictionary(
                    entry => entry.ID,
                    entry => options!.NextPer!.Contains(AiringNextGrouping.Channel) ? (perChannel ?? (_ => []))(entry) : next(entry)
                ));
        return service;
    }

    private static List<SeasonAnime> Build(Harness harness, Mock<IAiringScheduleService> airingService)
        => Build(harness, airingService, Links(), []);

    // Builds with the given linked series and the season view's sources ranked as given.
    private static List<SeasonAnime> Build(
        Harness harness,
        Mock<IAiringScheduleService> airingService,
        IMetadataService metadataService,
        List<MetadataSource> sourceOrder
    )
    {
        var configurationService = new Mock<IConfigurationService>();
        configurationService
            .Setup(service => service.Load(It.IsAny<ConfigurationInfo>(), It.IsAny<bool>()))
            .Returns(new AiringScheduleServiceSettings { SeasonDetailSourceOrder = sourceOrder });
        var builder = new SeasonAnimeBuilder(
            harness.Catalog,
            metadataService,
            new ConfigurationProvider<AiringScheduleServiceSettings>(configurationService.Object)
        );
        var calendar = new AiringCalendarService(harness.Catalog, airingService.Object);
        return builder.Build(calendar.BuildEntries(harness.Catalog.GetAnime(), new EpisodeAiringFilteringOptions(), _readAt));
    }

    // A metadata service linking anime 1 to the given series, and nothing else.
    private static IMetadataService Links(params SeasonAnimeBuilder.LinkedEntry[] series)
    {
        var service = new Mock<IMetadataService>();
        service
            .Setup(entry => entry.GetSeriesCrossReferences(It.IsAny<int>(), It.IsAny<MetadataSource?>()))
            .Returns((int animeID, MetadataSource? source) => series
                .Where(link => animeID == 1 && (source is null || link.Source == source))
                .Select(link => Mock.Of<IMetadataSeriesCrossReference>(xref => xref.Provider == Mock.Of<ISeries>(entry =>
                    entry.Studios == link.Studios && entry.Tags == link.Tags
                )))
                .ToList());
        service
            .Setup(entry => entry.GetMovieCrossReferencesForSeries(It.IsAny<int>(), It.IsAny<MetadataSource?>()))
            .Returns([]);
        return service.Object;
    }

    private static IStudio LinkedStudio(MetadataSource source, string name, StudioType type = StudioType.Animation)
        => Mock.Of<IStudio>(studio => studio.ID == new MetadataGuid(source, MetadataEntityType.Studio, name)
            && studio.Source == source
            && studio.Name == name
            && studio.StudioType == type);

    private static ITag LinkedTag(MetadataSource source, string name, TagKind kind = TagKind.Genre, bool spoiler = false)
        => Mock.Of<ITag>(tag => tag.ID == new MetadataGuid(source, MetadataEntityType.Tag, name)
            && tag.Source == source
            && tag.Name == name
            && tag.Kind == kind
            && tag.IsSpoiler == spoiler);

    private static SeasonAnime.Studio AnidbStudio(string name)
        => new() { Source = MetadataSource.AniDB, ID = name, Name = name };

    private static SeasonAnime.Tag AnidbGenre(string name)
        => new() { Source = MetadataSource.AniDB, ID = name, Name = name };

    // AniDB first, then the linked entries' sources in their order.
    private static IReadOnlyList<MetadataSource> AnidbFirst(IEnumerable<SeasonAnimeBuilder.LinkedEntry> linked)
        => [MetadataSource.AniDB, .. linked.Select(entry => entry.Source).Distinct()];

    [Fact]
    public void MergeStudios_AppendsAnimationStudiosOnlyTheLinkedSourcesName()
    {
        var linked = new List<SeasonAnimeBuilder.LinkedEntry>
        {
            new(TestSources.AniList, [LinkedStudio(TestSources.AniList, "STUDIO ONE"), LinkedStudio(TestSources.AniList, "Financier", StudioType.Production)], []),
            new(MetadataSource.TMDB, [LinkedStudio(MetadataSource.TMDB, "Studio Three"), LinkedStudio(MetadataSource.TMDB, "Untyped", StudioType.None)], []),
        };

        var merged = SeasonAnimeBuilder.MergeStudios([AnidbStudio("Studio One")], linked, AnidbFirst(linked));

        Assert.Equal(["Studio One", "Studio Three"], merged.Select(studio => studio.Name));
        Assert.Equal(MetadataSource.TMDB, merged[1].Source);
    }

    [Fact]
    public void MergeStudios_UntypedStudiosOnlyFillInFromTheTopRankedSourceWithAny()
    {
        var linked = new List<SeasonAnimeBuilder.LinkedEntry>
        {
            new(TestSources.AniList, [LinkedStudio(TestSources.AniList, "Financier", StudioType.Production)], []),
            new(MetadataSource.TMDB, [LinkedStudio(MetadataSource.TMDB, "Publisher", StudioType.None)], []),
            // Another entry of the same source, such as a linked movie.
            new(MetadataSource.TMDB, [LinkedStudio(MetadataSource.TMDB, "Distributor", StudioType.None)], []),
            new(TestSources.Plugin, [LinkedStudio(TestSources.Plugin, "Lower Ranked", StudioType.None)], []),
        };

        Assert.Equal(["Publisher", "Distributor"], SeasonAnimeBuilder.MergeStudios([], linked, AnidbFirst(linked)).Select(studio => studio.Name));
        // AniDB naming an animation studio is enough to keep them out.
        Assert.Equal(["Studio One"], SeasonAnimeBuilder.MergeStudios([AnidbStudio("Studio One")], linked, AnidbFirst(linked)).Select(studio => studio.Name));
    }

    [Fact]
    public void MergeGenres_RanksTheGenresMostSourcesAgreeOn()
    {
        var linked = new List<SeasonAnimeBuilder.LinkedEntry>
        {
            new(
                TestSources.Plugin,
                [],
                [LinkedTag(TestSources.Plugin, "Animation"), LinkedTag(TestSources.Plugin, "Drama"), LinkedTag(TestSources.Plugin, "Comedy", spoiler: true)]
            ),
            new(
                TestSources.AniList,
                [],
                [LinkedTag(TestSources.AniList, "Drama"), LinkedTag(TestSources.AniList, "Action"), LinkedTag(TestSources.AniList, "isekai", TagKind.Keyword)]
            ),
        };

        var merged = SeasonAnimeBuilder.MergeGenres([AnidbGenre("comedy"), AnidbGenre("action"), AnidbGenre("fantasy")], linked, AnidbFirst(linked), 3);

        Assert.Equal(["action", "Drama", "comedy"], merged.Select(tag => tag.Name));
    }

    [Theory]
    [InlineData("8bit", "8-bit")]
    [InlineData("Liden Films", "LIDENFILMS")]
    [InlineData("J.C.STAFF", "J.C.Staff")]
    [InlineData("studio MOTHER", "Studio Mother")]
    [InlineData("Studio_Deen", "Studio Deen")]
    [InlineData("Kyoto Animation, Inc.", "Kyoto Animation Inc")]
    [InlineData("IKIF+", "IKIF +")]
    public void MergeStudios_MatchesNamesWithoutCaseSpacesOrPunctuation(string anidbName, string linkedName)
    {
        var linked = new List<SeasonAnimeBuilder.LinkedEntry> { new(TestSources.AniList, [LinkedStudio(TestSources.AniList, linkedName)], []) };

        var merged = SeasonAnimeBuilder.MergeStudios([AnidbStudio(anidbName)], linked, AnidbFirst(linked));

        Assert.Equal([(MetadataSource.AniDB, anidbName)], merged.Select(studio => (studio.Source, studio.Name)));
    }

    [Fact]
    public void MergeStudios_NamesWithoutLettersOrDigitsMatchOnlyThemselves()
    {
        var linked = new List<SeasonAnimeBuilder.LinkedEntry>
        {
            new(TestSources.AniList, [LinkedStudio(TestSources.AniList, "!!!"), LinkedStudio(TestSources.AniList, "???")], []),
        };

        var merged = SeasonAnimeBuilder.MergeStudios([AnidbStudio("!!!")], linked, AnidbFirst(linked));

        Assert.Equal(["!!!", "???"], merged.Select(studio => studio.Name));
    }

    [Fact]
    public void MergeStudios_KeepsPlusApart()
    {
        var linked = new List<SeasonAnimeBuilder.LinkedEntry> { new(TestSources.AniList, [LinkedStudio(TestSources.AniList, "IKIF")], []) };

        var merged = SeasonAnimeBuilder.MergeStudios([AnidbStudio("IKIF+")], linked, AnidbFirst(linked));

        Assert.Equal(["IKIF+", "IKIF"], merged.Select(studio => studio.Name));
    }

    [Fact]
    public void MergeGenres_CountsPunctuatedSpellingsAsOneGenre()
    {
        var linked = new List<SeasonAnimeBuilder.LinkedEntry>
        {
            new(TestSources.AniList, [], [LinkedTag(TestSources.AniList, "Slice-of-Life"), LinkedTag(TestSources.AniList, "ANIMATION!")]),
        };

        var merged = SeasonAnimeBuilder.MergeGenres([AnidbGenre("comedy"), AnidbGenre("Slice of Life")], linked, AnidbFirst(linked), 3);

        Assert.Equal(["Slice of Life", "comedy"], merged.Select(tag => tag.Name));
    }

    [Fact]
    public void SeasonAnime_StudiosAndGenres_UseOnlyTheListedSourcesInTheirOrder()
    {
        using var harness = CardHarness();
        var links = Links(
            // Not listed: its studio and its vote for comedy are left out.
            new(TestSources.Plugin, [LinkedStudio(TestSources.Plugin, "Plugin Studio")], [LinkedTag(TestSources.Plugin, "comedy")]),
            new(TestSources.AniList, [LinkedStudio(TestSources.AniList, "AniList Studio")], [LinkedTag(TestSources.AniList, "Mystery")]),
            new(MetadataSource.TMDB, [LinkedStudio(MetadataSource.TMDB, "TMDB Studio")], [LinkedTag(MetadataSource.TMDB, "Drama")])
        );
        SeasonAnime Alpha(List<MetadataSource> sourceOrder)
            => Build(harness, AiringService(_ => []), links, sourceOrder).Single(anime => anime.ID == 1);

        // AniDB is first unless listed.
        var aniListFirst = Alpha([TestSources.AniList, MetadataSource.TMDB]);
        var tmdbFirst = Alpha([MetadataSource.TMDB, TestSources.AniList]);
        var anidbSecond = Alpha([MetadataSource.TMDB, MetadataSource.AniDB, TestSources.AniList]);

        Assert.Equal(["Studio Two", "Studio One", "AniList Studio", "TMDB Studio"], aniListFirst.Studios.Select(studio => studio.Name));
        Assert.Equal(["action", "comedy", "fantasy", "Mystery", "Drama"], aniListFirst.Tags.Select(tag => tag.Name));
        Assert.Equal(["Studio Two", "Studio One", "TMDB Studio", "AniList Studio"], tmdbFirst.Studios.Select(studio => studio.Name));
        Assert.Equal(["action", "comedy", "fantasy", "Drama", "Mystery"], tmdbFirst.Tags.Select(tag => tag.Name));
        Assert.Equal(["TMDB Studio", "Studio Two", "Studio One", "AniList Studio"], anidbSecond.Studios.Select(studio => studio.Name));
        Assert.Equal(["Drama", "action", "comedy", "fantasy", "Mystery"], anidbSecond.Tags.Select(tag => tag.Name));
    }

    [Fact]
    public void SeasonAnime_NextAiring_LeadsTheOtherUpcomingAiringsOfItsEpisode()
    {
        using var harness = CardHarness();
        var lead = Airing(1, new DateTime(2026, 10, 6, 14, 30, 0, DateTimeKind.Utc), "BS11");
        var later = Airing(1, new DateTime(2026, 10, 9, 13, 30, 0, DateTimeKind.Utc), "AT-X");
        var otherEpisode = Airing(2, new DateTime(2026, 10, 8, 14, 30, 0, DateTimeKind.Utc), "TOKYO MX");
        // Read through the series for the anime in the collection.
        var service = AiringService(
            series => series is AnimeSeries ? [lead] : [],
            series => series is AnimeSeries ? [lead, otherEpisode, later] : []
        );

        var alpha = Build(harness, service).Single(anime => anime.ID == 1);

        Assert.Equal(SeasonAnime.NextAiringStatus.Upcoming, alpha.AiringStatus);
        Assert.Equal(lead.ID, alpha.NextAiring?.ID);
        Assert.Equal([later.ID], alpha.OtherAirings.Select(airing => airing.ID));
    }

    [Fact]
    public void SeasonAnime_DateOnlyNextAiring_HasNoOtherAirings()
    {
        using var harness = CardHarness();
        var dateOnly = Airing(1, null, airDate: new DateOnly(2026, 10, 10));
        var service = AiringService(series => series is AnimeSeries ? [dateOnly] : []);

        var alpha = Build(harness, service).Single(anime => anime.ID == 1);

        Assert.True(alpha.NextAiring?.IsDateOnly);
        Assert.Equal(new DateOnly(2026, 10, 10), alpha.NextAiring?.AirDate);
        Assert.Empty(alpha.OtherAirings);
        // Nothing has a time, so the airings per channel are never read.
        service.Verify(entry => entry.GetAiringsForSeries(It.IsAny<IEnumerable<ISeries>>(), It.IsAny<EpisodeAiringFilteringOptions?>()), Times.Once);
    }

    [Theory]
    [InlineData(null, SeasonAnimeAiringStatus.Unknown)]
    [InlineData("2026-10-04", SeasonAnimeAiringStatus.Finished)]
    [InlineData("2026-10-05", SeasonAnimeAiringStatus.Unknown)]
    [InlineData("2026-09", SeasonAnimeAiringStatus.Finished)]
    [InlineData("2026-10", SeasonAnimeAiringStatus.Unknown)]
    public void SeasonAnime_WithoutANextAiring_IsFinishedOnceItsEndHasPassed(string? endDate, SeasonAnimeAiringStatus expected)
    {
        var anime = Anime(1, "Alpha", Date(2026, 7, 1), endDate: endDate is null ? null : PartialDateOnly.Parse(endDate));

        Assert.Equal(expected, AiringCalendarService.GetStatus(anime, null, _today));
        Assert.Equal(SeasonAnimeAiringStatus.Upcoming, AiringCalendarService.GetStatus(anime, Airing(1, null, airDate: _today), _today));
    }

    [Fact]
    public void SeasonAnime_Poster_PrefersTheSeriesOne()
    {
        using var harness = new Harness(
            [Anime(1, "Alpha", null), Anime(2, "Beta", null), Anime(3, "Gamma", null)],
            series: [new AnimeSeries { AnimeSeriesID = 10, AniDB_ID = 1 }, new AnimeSeries { AnimeSeriesID = 20, AniDB_ID = 2 }],
            withPosters: new HashSet<int> { 1, 2, 3 },
            withSeriesPosters: new HashSet<int> { 1 }
        );
        Guid? PosterOf(int animeID)
        {
            var (anime, series) = harness.Catalog.GetAnime().Single(entry => entry.Anime.AnimeID == animeID);
            return harness.Catalog.GetPoster(anime, series)?.ID;
        }

        Assert.Equal(SeriesPosterID(1), PosterOf(1));
        // A series without one falls back to its anime's.
        Assert.Equal(PosterID(2), PosterOf(2));
        Assert.Equal(PosterID(3), PosterOf(3));
    }

    [Fact]
    public void SeasonAnime_CardDetails_ComeFromTheCatalog()
    {
        using var harness = CardHarness();

        var models = Build(harness, AiringService(_ => []));
        var alpha = models.Single(anime => anime.ID == 1);
        var beta = models.Single(anime => anime.ID == 2);

        Assert.Equal("series overview", alpha.Overview);
        Assert.Equal(["Studio Two", "Studio One"], alpha.Studios.Select(studio => studio.Name));
        Assert.Equal(SourceMaterial.Manga, alpha.SourceMaterial);
        Assert.Equal(["action", "comedy", "fantasy"], alpha.Tags.Select(tag => tag.Name));
        Assert.Equal(2, alpha.VideoCount);
        Assert.Equal(new SeasonWithYear(2015, YearlySeason.Spring), alpha.StartSeason);
        // Outside the collection: no files, no studios, and the anime's own overview.
        Assert.Equal(0, beta.VideoCount);
        Assert.Empty(beta.Studios);
        Assert.Equal("anime overview", beta.Overview);
    }

    [Fact]
    public void EpisodeDuration_IsTheMedianKnownRegularLength()
    {
        var episodes = Episodes(1, Day(2015, 4, 1), Day(2015, 4, 8), Day(2015, 4, 15)).ToList();
        episodes[0].LengthSeconds = 1440;
        episodes[1].LengthSeconds = 1500;
        var special = Episode(1, 1, Day(2015, 4, 2), EpisodeType.Special);
        special.LengthSeconds = 300;
        using var harness = new Harness([Anime(1, "Alpha", Date(2015, 4, 1))], [.. episodes, special]);

        Assert.Equal(TimeSpan.FromSeconds(1470), harness.Catalog.GetEpisodeDuration(1));
    }

    /// <summary>
    /// An airing with only what the season view reads set.
    /// </summary>
    private sealed class FakeAiring : IEpisodeAiring
    {
        public required Guid ID { get; init; }

        public string Key => ID.ToString();

        public IAiringSchedule? Schedule => null;

        public Guid? ProviderID => null;

        public string? ProviderName => null;

        public required bool IsDateOnly { get; init; }

        public DateOnly? AirDate { get; init; }

        public required MetadataGuid EpisodeID { get; init; }

        public IEpisode? Episode => null;

        public IAnidbEpisode? AnidbEpisode => null;

        public IShokoEpisode? ShokoEpisode => null;

        public IAiringChannel? Channel { get; init; }

        public string? Url => null;

        public IReadOnlyList<IAiringTrack> Tracks => [];

        public DateTime? AiredAt { get; init; }

        public DateTime? OriginalAiredAt => null;

        public TimeSpan? Duration => null;

        public DateTime? EndsAt => null;

        public bool IsDelayed => false;

        public bool IsEstimated => false;

        public bool IsPreferred => true;

        public EpisodeAiringKind Kind => EpisodeAiringKind.Normal;

        public TimeSpan? OffsetFromOriginal => null;

        public Guid? LinkID => null;

        public DateTime CreatedAt => DateTime.UnixEpoch;

        public DateTime LastUpdatedAt => DateTime.UnixEpoch;
    }

    #endregion
}
