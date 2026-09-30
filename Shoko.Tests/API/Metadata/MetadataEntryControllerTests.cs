using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Shoko.Abstractions.Filtering.Services;
using Shoko.Abstractions.Metadata;
using Shoko.Abstractions.Metadata.Anidb;
using Shoko.Abstractions.Metadata.CrossReferences;
using Shoko.Abstractions.Metadata.Enums;
using Shoko.Abstractions.Metadata.Providers;
using Shoko.Abstractions.Metadata.Services;
using Shoko.Abstractions.Metadata.Shoko;
using Shoko.Abstractions.Metadata.Storage;
using Shoko.Abstractions.User;
using Shoko.Abstractions.Video;
using Shoko.Server.API.ActionConstraints;
using Shoko.Server.API.Resolvers;
using Shoko.Server.API.v3.Controllers;
using Shoko.Server.API.v3.Helpers;
using Shoko.Server.API.v3.Models.Common;
using Shoko.Server.API.v3.Models.Metadata;
using Shoko.Server.API.v3.Models.Metadata.Input;
using Shoko.Server.Settings;
using Shoko.Tests.Infrastructure;
using Xunit;
using static Shoko.Tests.API.Metadata.FakeMetadataEntries;

namespace Shoko.Tests.API.Metadata;

/// <summary>
/// Covers the generic <c>Metadata/{source}</c> routes against a fake plugin
/// source: lookups, waiting out refreshes, lists, bulk reads, include flags,
/// text, tags, episode searches and groups, the admin actions, the match
/// ratings of links, and the route constraints.
/// </summary>
public class MetadataEntryControllerTests
{
    #region Fixture

    private static readonly JsonSerializerSettings _apiSettings = new() { ContractResolver = new ApiContractResolver() };

    /// <summary>
    /// The controller over mocked services, with helpers to store entries.
    /// </summary>
    private sealed class Fixture
    {
        public Mock<IMetadataService> Metadata { get; } = new();

        public Mock<IMetadataTextManager> Text { get; } = new();

        public Mock<IImageManager> Images { get; } = new();

        public Mock<IMetadataRefreshService> Refresh { get; } = new();

        public Mock<IMetadataPurgeService> Purge { get; } = new();

        public Mock<IMetadataTagStore> Tags { get; } = new();

        public Mock<IMetadataStudioStore> Studios { get; } = new();

        public Mock<IMetadataCollectionStore> Collections { get; } = new();

        public Mock<IFuzzySearchService> Fuzzy { get; } = new();

        public Mock<IMetadataLinkingService> Linking { get; } = new();

        public WritableLinkStore Links { get; } = new();

        public Fixture()
        {
            Linking.Setup(l => l.SetMatchRating(It.IsAny<IEnumerable<IMetadataCrossReference>>(), It.IsAny<MatchRating>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((IEnumerable<IMetadataCrossReference> links, MatchRating _, CancellationToken _) => [.. links]);
            Refresh.Setup(r => r.GetPauseStatus(It.IsAny<MetadataSource>())).Returns(MetadataProviderPauseStatus.NotPaused);
            Refresh.Setup(r => r.WaitForRefresh(It.IsAny<MetadataGuid>(), It.IsAny<CancellationToken>())).ReturnsAsync(false);
            Text.Setup(t => t.GetTitles(It.IsAny<MetadataGuid>(), It.IsAny<Abstractions.Metadata.Text.Options.TextFilteringOptions?>())).Returns([]);
            Text.Setup(t => t.GetOverviews(It.IsAny<MetadataGuid>(), It.IsAny<Abstractions.Metadata.Text.Options.TextFilteringOptions?>())).Returns([]);
            Metadata.Setup(m => m.GatherResourcesForEntity(It.IsAny<Abstractions.Metadata.Containers.IWithResources>())).Returns([]);
        }

        public MetadataModelBuilder Models
            => new(Metadata.Object, Text.Object, Images.Object, Refresh.Object, Studios.Object);

        public MetadataEntryController Controller()
            => new(
                new StubSettingsProvider(new ServerSettings()),
                NullLogger<MetadataEntryController>.Instance,
                Metadata.Object,
                Refresh.Object,
                Purge.Object,
                Mock.Of<IMetadataOrderingService>(),
                Tags.Object,
                Collections.Object,
                Fuzzy.Object,
                Linking.Object,
                Links.Store,
                Models
            )
            {
                ControllerContext = new()
                {
                    HttpContext = new DefaultHttpContext { RequestServices = new ServiceCollection().AddLogging().AddMvcCore().Services.BuildServiceProvider() },
                },
            };

        public TEntry Store<TEntry>(TEntry entry) where TEntry : class, IMetadata
        {
            Metadata.Setup(m => m.GetEntry(entry.ID)).Returns(entry);
            Metadata.Setup(m => m.GetEntry<TEntry>(entry.ID)).Returns(entry);
            return entry;
        }

        public FakeSeries StoreSeries(FakeSeries series)
        {
            Store(series);
            Metadata.Setup(m => m.GetEntry<ISeries>(series.ID)).Returns(series);
            return series;
        }
    }

    private static T Value<T>(ActionResult<T> result) where T : class
        => result.Value ?? Assert.IsType<T>(Assert.IsType<OkObjectResult>(result.Result).Value, exactMatch: false);

    #endregion

    #region Lookup

    [Fact]
    public async Task ASeriesIsSentWithWhoItIsAndItsDetails()
    {
        var fixture = new Fixture();
        var series = fixture.StoreSeries(new FakeSeries("21", "Show")
        {
            Tags = [new FakeTag("1", "Drama") { Kind = TagKind.Genre }, new FakeTag("2", "Isekai")],
        });
        var refreshedAt = new DateTime(2026, 1, 2, 3, 4, 5, DateTimeKind.Utc);
        fixture.Refresh.Setup(r => r.GetLastRefreshedAt(series.ID)).Returns(refreshedAt);

        var model = Value(await fixture.Controller().GetSeriesByID(Source, "21", cancellationToken: TestContext.Current.CancellationToken));

        Assert.Equal(("21", "test-plugin://series/21", "Metadata/test-plugin/Series/21"), (model.ID, model.Guid, model.Path));
        Assert.Equal(["Drama"], model.Genres);
        Assert.Equal(refreshedAt, model.LastRefreshedAt);
        // Nothing extra unless asked for.
        Assert.Null(model.Titles);
        Assert.Null(model.Tags);
        Assert.Null(model.Images);
        Assert.False(JObject.Parse(JsonConvert.SerializeObject(model, _apiSettings)).ContainsKey("Titles"));
    }

    [Fact]
    public async Task AnEntryNotStoredIsNotFound()
    {
        var fixture = new Fixture();

        var result = await fixture.Controller().GetSeriesByID(Source, "404", cancellationToken: TestContext.Current.CancellationToken);

        Assert.IsType<NotFoundObjectResult>(result.Result);
    }

    [Theory]
    [InlineData("a%2Fb", "a/b")]
    [InlineData("a%2fb", "a/b")]
    [InlineData("plain", "plain")]
    public void AnEscapedSlashInAnIDIsPutBack(string routeValue, string id)
        => Assert.Equal(id, MetadataEntryController.ToGuid(Source, MetadataEntityType.Series, routeValue)!.ID);

    [Theory]
    [InlineData("")]
    [InlineData(" padded ")]
    public void AnInvalidIDNamesNothing(string routeValue)
        => Assert.Null(MetadataEntryController.ToGuid(Source, MetadataEntityType.Series, routeValue));

    [Fact]
    public async Task AnEntryIsReadAgainOnceARunningRefreshEnds()
    {
        var fixture = new Fixture();
        var stale = new FakeSeries("21", "Old");
        var fresh = new FakeSeries("21", "New");
        fixture.Metadata.SetupSequence(m => m.GetEntry<ISeries>(stale.ID)).Returns(stale).Returns(fresh);
        fixture.Refresh.Setup(r => r.WaitForRefresh(stale.ID, It.IsAny<CancellationToken>())).ReturnsAsync(true);

        var model = Value(await fixture.Controller().GetSeriesByID(Source, "21", cancellationToken: TestContext.Current.CancellationToken));

        Assert.Equal("New", model.Title);
    }

    [Fact]
    public async Task AnEpisodeWaitsOnTheRefreshOfItsSeries()
    {
        var fixture = new Fixture();
        var episode = fixture.Store<IEpisode>(new FakeEpisode("e1", "21", 1));

        await fixture.Controller().GetEpisodeByID(Source, "e1", cancellationToken: TestContext.Current.CancellationToken);

        fixture.Refresh.Verify(r => r.WaitForRefresh(ID(MetadataEntityType.Series, "21"), It.IsAny<CancellationToken>()), Times.Once);
        fixture.Refresh.Verify(r => r.WaitForRefresh(episode.ID, It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public void TheMinimalRouteRefusesUsersAndFilters()
    {
        var fixture = new Fixture();

        var result = fixture.Controller().GetEntry(MetadataSource.Shoko, MetadataEntityType.User, "1");

        Assert.IsType<NotFoundObjectResult>(result.Result);
        fixture.Metadata.Verify(m => m.GetEntry(It.IsAny<MetadataGuid>()), Times.Never);
        Assert.False(MetadataEntryController.IsOpenKind(MetadataEntityType.Filter));
        Assert.True(MetadataEntryController.IsOpenKind(MetadataEntityType.Ordering));
    }

    [Fact]
    public void TheFullIDRouteResolvesAnyEntryButRefusesBadIDsUsersAndFilters()
    {
        var fixture = new Fixture();
        fixture.StoreSeries(new FakeSeries("21", "Show"));
        var controller = new MetadataController(new StubSettingsProvider(new ServerSettings()), Mock.Of<IMetadataOrderingService>(), fixture.Metadata.Object, fixture.Models)
        {
            ControllerContext = new()
            {
                HttpContext = new DefaultHttpContext { RequestServices = new ServiceCollection().AddLogging().AddMvcCore().Services.BuildServiceProvider() },
            },
        };

        var found = Value(controller.GetEntry("test-plugin://series/21"));
        var user = controller.GetEntry("shoko://user/1");
        var invalid = controller.GetEntry("not an id");

        Assert.Equal(("21", "Show", "Metadata/test-plugin/Series/21"), (found.ID, found.Title, found.Path));
        Assert.IsType<NotFoundObjectResult>(user.Result);
        Assert.Equal(400, Assert.IsType<ObjectResult>(invalid.Result, exactMatch: false).StatusCode);
    }

    [Fact]
    public async Task TheSeasonCountLeavesTheSpecialsOut()
    {
        var fixture = new Fixture();
        var regular = Mock.Of<ISeason>(season => season.SeasonNumber == 1 && season.IsSpecial == false);
        var specials = Mock.Of<ISeason>(season => season.SeasonNumber == 0 && season.IsSpecial == true);
        fixture.StoreSeries(new FakeSeries("21", "Show") { Seasons = [regular, specials] });

        var model = Value(await fixture.Controller().GetSeriesByID(Source, "21", cancellationToken: TestContext.Current.CancellationToken));

        Assert.Equal(1, model.SeasonCount);
    }

    [Theory]
    [InlineData(DateTimeKind.Utc)]
    [InlineData(DateTimeKind.Unspecified)]
    public void AnAirTimeIsSentAsUtc(DateTimeKind kind)
    {
        var time = MetadataModelBuilder.AsUtc(new DateTime(2026, 9, 26, 16, 50, 0, kind));

        Assert.Equal(DateTimeKind.Utc, time.Kind);
        Assert.Equal(new DateTime(2026, 9, 26, 16, 50, 0, DateTimeKind.Utc), time);
    }

    [Fact]
    public void AniDBAndShokoEntriesFollowTheUsersRestrictions()
    {
        var hiddenAnime = Mock.Of<IAnidbAnime>();
        var shownAnime = Mock.Of<IAnidbAnime>();
        var hiddenSeries = Mock.Of<IShokoSeries>();
        var user = new Mock<IUser>();
        user.Setup(u => u.IsAllowedToSee(It.IsAny<IAnidbAnime>())).Returns((IAnidbAnime anime) => anime != hiddenAnime);
        user.Setup(u => u.IsAllowedToSee(It.IsAny<IShokoSeries>())).Returns((IShokoSeries series) => series != hiddenSeries);
        var anidbEpisode = Mock.Of<IAnidbEpisode>(episode => episode.Series == hiddenAnime);
        var shokoEpisode = Mock.Of<IShokoEpisode>(episode => episode.Series == hiddenSeries);
        var video = Mock.Of<IVideo>(video => video.Series == new List<IShokoSeries> { hiddenSeries });

        Assert.False(MetadataEntryController.MaySee(hiddenAnime, () => user.Object));
        Assert.True(MetadataEntryController.MaySee(shownAnime, () => user.Object));
        Assert.False(MetadataEntryController.MaySee(anidbEpisode, () => user.Object));
        Assert.False(MetadataEntryController.MaySee(hiddenSeries, () => user.Object));
        Assert.False(MetadataEntryController.MaySee(shokoEpisode, () => user.Object));
        Assert.False(MetadataEntryController.MaySee(video, () => user.Object));
    }

    [Fact]
    public void OtherSourcesAreShownWithoutAskingWhoIsLooking()
    {
        var series = new FakeSeries("21", "Show");

        Assert.True(MetadataEntryController.MaySee(series, () => throw new InvalidOperationException("The user was asked for.")));
    }

    #endregion

    #region Lists and Bulk

    [Fact]
    public async Task AListIsFilteredSearchedAndPaged()
    {
        var fixture = new Fixture();
        fixture.Metadata.Setup(m => m.GetAllSeriesForSource(Source)).Returns([
            new FakeSeries("1", "Beta Show"),
            new FakeSeries("2", "Alpha Show"),
            new FakeSeries("3", "Gamma Show") { Restricted = true },
            new FakeSeries("4", "Delta") { Titles = [new FakeTitle("Delta"), new FakeTitle("Show, Another", TitleLanguage.Japanese)] },
        ]);
        var controller = fixture.Controller();

        var all = Value(await controller.GetSeries(Source, pageSize: 2, cancellationToken: TestContext.Current.CancellationToken));
        var safe = Value(await controller.GetSeries(Source, restricted: IncludeOnlyFilter.False, pageSize: 0, cancellationToken: TestContext.Current.CancellationToken));
        var only = Value(await controller.GetSeries(Source, restricted: IncludeOnlyFilter.Only, cancellationToken: TestContext.Current.CancellationToken));
        var searched = Value(await controller.GetSeries(Source, search: "show", fuzzy: false, pageSize: 0, cancellationToken: TestContext.Current.CancellationToken));

        Assert.Equal(4, all.Total);
        Assert.Equal(["Alpha Show", "Beta Show"], all.List.Select(series => series.Title));
        Assert.Equal(3, safe.Total);
        Assert.Equal(["3"], only.List.Select(series => series.ID));
        // Any title may match, and an earlier match ranks first.
        Assert.Equal(["4", "1", "2", "3"], searched.List.Select(series => series.ID));
    }

    [Fact]
    public async Task ABulkReadTakesSourceIDsAndFullIDsAndSkipsTheRest()
    {
        var fixture = new Fixture();
        fixture.StoreSeries(new FakeSeries("1", "One"));
        fixture.StoreSeries(new FakeSeries("a/b", "Slashed"));

        var models = Value(await fixture.Controller().GetSeriesInBulk(Source, new MetadataBulkFetchBody
        {
            IDs = ["test-plugin://series/a/b", "1", "missing", "test-plugin://episode/1", "tmdb://series/1", " "],
            Include = [MetadataIncludeDetails.Titles],
        }, TestContext.Current.CancellationToken));

        Assert.Equal(["a/b", "1"], models.Select(model => model.ID));
        Assert.All(models, model => Assert.NotNull(model.Titles));
    }

    #endregion

    #region Include Flags and Text

    [Fact]
    public async Task TheIncludeFlagsAddTheirBlocks()
    {
        var fixture = new Fixture();
        fixture.StoreSeries(new FakeSeries("21", "Show")
        {
            Tags = [new FakeTag("1", "Drama") { Kind = TagKind.Genre, Weight = 300 }],
            YearlySeasons = [(2020, YearlySeason.Spring)],
            MetadataSeriesCrossReferences = [Mock.Of<IMetadataSeriesCrossReference>(link => link.AnidbAnimeID == 5 && link.Source == Source && link.EntityType == MetadataEntityType.Series && link.ProviderID == ID(MetadataEntityType.Series, "21"))],
        });

        var model = Value(await fixture.Controller().GetSeriesByID(
            Source,
            "21",
            include: [MetadataIncludeDetails.Tags, MetadataIncludeDetails.YearlySeasons, MetadataIncludeDetails.CrossReferences, MetadataIncludeDetails.Titles, MetadataIncludeDetails.Resources],
            cancellationToken: TestContext.Current.CancellationToken
        ));

        var tag = Assert.Single(model.Tags!);
        Assert.Equal(("1", 300), (tag.ID, tag.Weight));
        Assert.Equal(2020, Assert.Single(model.YearlySeasons!).Year);
        var link = Assert.Single(model.CrossReferences!);
        Assert.Equal((5, "21"), (link.AnidbAnimeID, link.ID));
        Assert.NotNull(model.Titles);
        Assert.NotNull(model.Resources);
        Assert.Null(model.Cast);
    }

    [Fact]
    public void TheTitlesComeFromTheTextManager_PreferredFirst()
    {
        var fixture = new Fixture();
        var id = ID(MetadataEntityType.Series, "21");
        var main = new FakeTitle("Main", TitleLanguage.Japanese, TitleType.Main);
        var english = new FakeTitle("English", TitleLanguage.English, TitleType.Official, MetadataSource.User);
        var other = new FakeTitle("Other", TitleLanguage.English, TitleType.Synonym);
        fixture.Text.Setup(t => t.GetTitles(id, null)).Returns([main, other, english]);
        fixture.Text.Setup(t => t.GetDefaultTitle(id)).Returns(main);
        fixture.Text.Setup(t => t.GetPreferredTitle(id)).Returns(english);

        var titles = fixture.Models.Titles(id);
        var japanese = fixture.Models.Titles(id, new HashSet<TitleLanguage> { TitleLanguage.Japanese });

        Assert.Equal(["English", "Main", "Other"], titles.Select(title => title.Name));
        Assert.True(titles[0].Preferred);
        Assert.True(titles[1].Default);
        Assert.Equal("User", titles[0].Source);
        Assert.Equal("test-plugin", titles[1].Source);
        Assert.Equal(TitleType.Main, titles[1].Type);
        Assert.Equal(["Main"], japanese.Select(title => title.Name));
    }

    [Fact]
    public void AMadeUpEpisodeTitleIsOnlyListedWhenAskedFor()
    {
        var fixture = new Fixture();
        var id = ID(MetadataEntityType.Episode, "e1");
        fixture.Text.Setup(t => t.GetTitles(id, null)).Returns([]);
        fixture.Text.Setup(t => t.GetPreferredTitle(id)).Returns(new FakeTitle("Episode 1") { IsSynthesized = true });

        Assert.Empty(fixture.Models.Titles(id));
        var title = Assert.Single(fixture.Models.Titles(id, includeSynthesized: true));
        Assert.True(title.Synthesized);
        Assert.True(title.Preferred);

        var json = JObject.Parse(JsonConvert.SerializeObject(new Title(new FakeTitle("Given"), null, (ITitle?)null), _apiSettings));
        Assert.False(json.ContainsKey("Synthesized"));
    }

    [Fact]
    public void ATagReadThroughAnEntrySaysHowItAppliesThere()
    {
        var fixture = new Fixture();
        var stored = fixture.Store<ITag>(new FakeTag("7", "Twist") { IsSpoiler = false, Overview = "What it is." });
        var onEntry = new FakeTag("7", "Twist") { IsSpoiler = true, Weight = 200 };

        var tag = fixture.Models.Tag(stored, onEntry);
        var plain = fixture.Models.Tag(stored, excludeOverview: true, size: 4);

        Assert.Equal((false, true, 200), (tag.IsSpoiler, tag.IsLocalSpoiler, tag.Weight));
        Assert.Equal("What it is.", tag.Overview);
        Assert.Null(plain.Overview);
        Assert.Null(plain.IsLocalSpoiler);
        Assert.Equal(4, plain.Size);
    }

    #endregion

    #region Episodes

    [Theory]
    [InlineData("1", new[] { 1, 10, 11 })]
    [InlineData("E1", new[] { 1 })]
    [InlineData("#10", new[] { 10 })]
    [InlineData("x", null)]
    [InlineData(null, new[] { 1, 2, 10, 11 })]
    public void EpisodesAreSearchedByNumber(string? search, int[]? numbers)
    {
        IEpisode[] episodes = [.. new[] { 1, 2, 10, 11 }.Select(number => new FakeEpisode($"e{number}", "21", number))];

        var found = MetadataEntryController.SearchEpisodes(episodes, search);

        if (numbers is null)
            Assert.Null(found);
        else
            Assert.Equal(numbers, found!.Select(episode => episode.EpisodeNumber));
    }

    [Fact]
    public void EpisodesAreOrderedBySeasonWithSpecialsLast()
    {
        IEpisode[] episodes =
        [
            new FakeEpisode("s1", "21", 1, EpisodeType.Special, seasonNumber: 0),
            new FakeEpisode("e2", "21", 2),
            new FakeEpisode("b1", "21", 1, seasonNumber: 2),
            new FakeEpisode("e1", "21", 1),
        ];

        Assert.Equal(["e1", "e2", "b1", "s1"], MetadataEntryController.InOrder(episodes).Select(episode => episode.ID.ID));
    }

    [Fact]
    public void TheDaysOfWeekComeFromTheAirDates()
    {
        IEpisode[] episodes =
        [
            new FakeEpisode("e1", "21", 1) { AirDateWithTime = new DateTime(2026, 9, 26, 12, 0, 0) },
            new FakeEpisode("e2", "21", 2) { AirDate = new DateOnly(2026, 9, 20) },
            new FakeEpisode("e3", "21", 3) { AirDate = new DateOnly(2026, 9, 27) },
            new FakeEpisode("e4", "21", 4),
        ];

        Assert.Equal(["Saturday", "Sunday"], MetadataEntryController.DaysOfWeek(episodes));
    }

    [Fact]
    public async Task TheEpisodeLinksAreGroupedAsTmdbAndAniListGroupThem()
    {
        var fixture = new Fixture();
        var one = ID(MetadataEntityType.Episode, "e1");
        var two = ID(MetadataEntityType.Episode, "e2");
        fixture.StoreSeries(new FakeSeries("21", "Show")
        {
            MetadataEpisodeCrossReferences =
            [
                // Two AniDB episodes sharing one episode, and one AniDB episode split over two.
                new FakeEpisodeLink(5, 101, one),
                new FakeEpisodeLink(5, 102, one),
                new FakeEpisodeLink(5, 103, two, ordering: 1),
                new FakeEpisodeLink(5, 103, ID(MetadataEntityType.Episode, "e3"), ordering: 0),
            ],
        });

        var groups = Value(await fixture.Controller().GetSeriesEpisodeCrossReferenceGroups(Source, "21", cancellationToken: TestContext.Current.CancellationToken));

        Assert.Equal(2, groups.Total);
        Assert.Equal([101, 102], groups.List[0].Select(link => link.AnidbEpisodeID!.Value));
        Assert.Equal(["e3", "e2"], groups.List[1].Select(link => link.ID));
        Assert.Equal([0, 1], groups.List[1].Select(link => link.Index));
    }

    #endregion

    #region Actions

    [Fact]
    public async Task APurgeGoesToThePurgeService_ButNotForAniDB()
    {
        var fixture = new Fixture();
        var controller = fixture.Controller();

        var purged = await controller.DeleteSeries(Source, "a%2Fb", TestContext.Current.CancellationToken);
        var refused = await controller.DeleteSeries(MetadataSource.AniDB, "1", TestContext.Current.CancellationToken);

        Assert.IsType<NoContentResult>(purged);
        Assert.Equal(400, Assert.IsType<ObjectResult>(refused, exactMatch: false).StatusCode);
        fixture.Purge.Verify(p => p.PurgeEntry(ID(MetadataEntityType.Series, "a/b"), true, It.IsAny<CancellationToken>()), Times.Once);
        fixture.Purge.Verify(p => p.PurgeEntry(It.Is<MetadataGuid>(id => id.Source == MetadataSource.AniDB), It.IsAny<bool>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ARefreshWhilePausedIsQueuedAtTheFrontAndAnswers503()
    {
        var fixture = new Fixture();
        var series = fixture.StoreSeries(new FakeSeries("21", "Show"));
        fixture.Refresh.Setup(r => r.GetPauseStatus(Source)).Returns(new MetadataProviderPauseStatus { IsPaused = true, ResumesAt = DateTime.UtcNow.AddSeconds(30) });
        var controller = fixture.Controller();

        var queued = await controller.RefreshSeries(Source, "21", new MetadataRefreshBody { Force = true }, TestContext.Current.CancellationToken);

        Assert.Equal(503, Assert.IsType<ObjectResult>(queued).StatusCode);
        Assert.InRange(int.Parse(controller.Response.Headers.RetryAfter.ToString()), 1, 31);
        fixture.Refresh.Verify(r => r.RefreshEntry(series.ID, true, It.Is<MetadataRefreshOptions>(o => o.Reason == MetadataRefreshReason.Requested), false, true, It.IsAny<CancellationToken>()), Times.Once);

        var refused = await fixture.Controller().RefreshSeries(Source, "21", new MetadataRefreshBody { Immediate = true }, TestContext.Current.CancellationToken);

        Assert.Equal(503, Assert.IsType<ObjectResult>(refused).StatusCode);
        fixture.Refresh.Verify(r => r.RefreshEntry(It.IsAny<MetadataGuid>(), It.IsAny<bool>(), It.IsAny<MetadataRefreshOptions?>(), It.IsAny<bool>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ARefreshOfAnEntryNotStoredFetchesIt_AndSkipIfExistsLeavesAStoredOneAlone()
    {
        var fixture = new Fixture();
        fixture.StoreSeries(new FakeSeries("21", "Show"));
        var controller = fixture.Controller();

        var fetched = await controller.RefreshSeries(Source, "99", new MetadataRefreshBody(), TestContext.Current.CancellationToken);
        var skipped = await controller.RefreshSeries(Source, "21", new MetadataRefreshBody { SkipIfExists = true }, TestContext.Current.CancellationToken);

        Assert.IsType<NoContentResult>(fetched);
        Assert.IsType<OkResult>(skipped);
        fixture.Refresh.Verify(r => r.RefreshEntry(ID(MetadataEntityType.Series, "99"), false, It.IsAny<MetadataRefreshOptions?>(), false, false, It.IsAny<CancellationToken>()), Times.Once);
        fixture.Refresh.Verify(r => r.RefreshEntry(ID(MetadataEntityType.Series, "21"), It.IsAny<bool>(), It.IsAny<MetadataRefreshOptions?>(), It.IsAny<bool>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    #endregion

    #region Match Ratings

    /// <summary>
    /// Links from two anime to series "s", a film "f" claiming the whole of
    /// the first anime and linked from one of its episodes, and an episode
    /// link to nothing.
    /// </summary>
    private static async Task<Fixture> RatedLinks()
    {
        var fixture = new Fixture();
        var cancellationToken = TestContext.Current.CancellationToken;
        await fixture.Links.Store.MergeSeriesLinks(
            [
                new() { Source = Source, AnidbAnimeID = 10, ProviderID = ID(MetadataEntityType.Series, "s"), MatchRating = MatchRating.TitleMatches },
                new() { Source = Source, AnidbAnimeID = 20, ProviderID = ID(MetadataEntityType.Series, "s"), MatchRating = MatchRating.TitleMatches },
                new() { Source = Source, AnidbAnimeID = 10, ProviderID = ID(MetadataEntityType.Movie, "f"), MatchRating = MatchRating.TitleMatches },
            ],
            cancellationToken: cancellationToken
        );
        await fixture.Links.Store.MergeMovieLinks(
            [new() { Source = Source, AnidbAnimeID = 10, AnidbEpisodeID = 101, ProviderID = ID(MetadataEntityType.Movie, "f"), MatchRating = MatchRating.DateMatches }],
            cancellationToken: cancellationToken
        );
        await fixture.Links.Store.MergeEpisodeLinks(
            [new() { Source = Source, AnidbAnimeID = 10, AnidbEpisodeID = 102, ProviderID = null, MatchRating = MatchRating.None }],
            cancellationToken: cancellationToken
        );
        return fixture;
    }

    [Fact]
    public async Task PatchingASeriesLinksVerifiesEveryLinkToIt_OrOnlyOneAnimesWhenAsked()
    {
        var fixture = await RatedLinks();
        var controller = fixture.Controller();

        var all = await controller.SetSeriesMatchRating(Source, "s", null, TestContext.Current.CancellationToken);
        var one = await controller.SetSeriesMatchRating(
            Source, "s", new MetadataSetMatchRatingBody { MatchRating = MatchRating.FirstAvailable, AnidbAnimeID = 20 }, TestContext.Current.CancellationToken
        );

        Assert.Equal([10, 20], Value(all).Select(x => x.AnidbAnimeID));
        Assert.Equal([20], Value(one).Select(x => x.AnidbAnimeID));
        fixture.Linking.Verify(l => l.SetMatchRating(
            It.Is<IEnumerable<IMetadataCrossReference>>(links => links.Count() == 2),
            MatchRating.UserVerified,
            It.IsAny<CancellationToken>()
        ), Times.Once);
        fixture.Linking.Verify(l => l.SetMatchRating(
            It.Is<IEnumerable<IMetadataCrossReference>>(links => links.Count() == 1 && links.First().AnidbAnimeID == 20),
            MatchRating.FirstAvailable,
            It.IsAny<CancellationToken>()
        ), Times.Once);
    }

    [Fact]
    public async Task PatchingAMoviesLinksCoversItsClaimOnAWholeAnime()
    {
        var fixture = await RatedLinks();
        var controller = fixture.Controller();

        var all = await controller.SetMovieMatchRating(Source, "f", null, TestContext.Current.CancellationToken);
        var episode = await controller.SetMovieMatchRating(Source, "f", new MetadataSetMatchRatingBody { AnidbEpisodeID = 101 }, TestContext.Current.CancellationToken);

        Assert.Equal([MetadataEntityType.Series, MetadataEntityType.Movie], Value(all).Select(x => x.EntityType).Order());
        Assert.Equal([101], Value(episode).Select(x => x.AnidbEpisodeID));
    }

    [Fact]
    public async Task PatchingAnEntryNobodyLinksAnswers404()
    {
        var fixture = await RatedLinks();
        var controller = fixture.Controller();

        var unknown = await controller.SetEpisodeMatchRating(Source, "e", null, TestContext.Current.CancellationToken);
        var otherAnime = await controller.SetSeriesMatchRating(Source, "s", new MetadataSetMatchRatingBody { AnidbAnimeID = 30 }, TestContext.Current.CancellationToken);

        Assert.IsType<NotFoundObjectResult>(unknown.Result);
        Assert.IsType<NotFoundObjectResult>(otherAnime.Result);
        fixture.Linking.Verify(l => l.SetMatchRating(It.IsAny<IEnumerable<IMetadataCrossReference>>(), It.IsAny<MatchRating>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task TheBulkRouteTakesTheLinksAsTheCrossReferenceRoutesSendThem()
    {
        var fixture = await RatedLinks();
        var controller = fixture.Controller();

        var result = await controller.SetMatchRatings(Source, new MetadataBulkSetMatchRatingBody
        {
            CrossReferences =
            [
                new() { EntityType = MetadataEntityType.Series, AnidbAnimeID = 20, ID = "s" },
                new() { EntityType = MetadataEntityType.Series, AnidbAnimeID = 10, ID = ID(MetadataEntityType.Movie, "f").ToString() },
                new() { EntityType = MetadataEntityType.Episode, AnidbAnimeID = 10, AnidbEpisodeID = 102 },
            ],
        }, TestContext.Current.CancellationToken);

        Assert.Equal(
            // Sent back in the cross-reference routes' order, by AniDB anime.
            [(MetadataEntityType.Series, 10, (int?)null), (MetadataEntityType.Episode, 10, 102), (MetadataEntityType.Series, 20, null)],
            Value(result).Select(x => (x.EntityType, x.AnidbAnimeID, x.AnidbEpisodeID))
        );
    }

    [Fact]
    public async Task TheBulkRouteRefusesEverythingWhenOneLinkIsNotStored()
    {
        var fixture = await RatedLinks();
        var controller = fixture.Controller();

        var result = await controller.SetMatchRatings(Source, new MetadataBulkSetMatchRatingBody
        {
            CrossReferences =
            [
                new() { EntityType = MetadataEntityType.Series, AnidbAnimeID = 20, ID = "s" },
                new() { EntityType = MetadataEntityType.Movie, AnidbAnimeID = 10, ID = "f" },
                new() { EntityType = MetadataEntityType.Series, AnidbAnimeID = 20, ID = "x" },
            ],
        }, TestContext.Current.CancellationToken);

        Assert.Equal(400, Assert.IsType<ObjectResult>(result.Result, exactMatch: false).StatusCode);
        Assert.Equal(2, controller.ModelState.ErrorCount);
        fixture.Linking.Verify(l => l.SetMatchRating(It.IsAny<IEnumerable<IMetadataCrossReference>>(), It.IsAny<MatchRating>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task AnUndefinedRatingIsABadRequest()
    {
        var fixture = await RatedLinks();
        var controller = fixture.Controller();

        var result = await controller.SetSeriesMatchRating(Source, "s", new MetadataSetMatchRatingBody { MatchRating = (MatchRating)6 }, TestContext.Current.CancellationToken);

        Assert.Equal(400, Assert.IsType<ObjectResult>(result.Result, exactMatch: false).StatusCode);
    }

    #endregion

    #region Routing

    [Theory]
    [InlineData("test-plugin", true)]
    [InlineData("TEST_PLUGIN", true)]
    [InlineData("AniDB", true)]
    [InlineData("themoviedb", true)]
    [InlineData("Episode", false)]
    [InlineData("nothing-registered", false)]
    public void TheSourceConstraintMatchesRegisteredSourcesOnly(string value, bool matches)
    {
        var values = new RouteValueDictionary { ["source"] = value };

        Assert.Equal(matches, new MetadataSourceRouteConstraint().Match(null, null, "source", values, RouteDirection.IncomingRequest));
    }

    [Theory]
    [InlineData("ordering", true)]
    [InlineData("Show", true)]
    [InlineData("nothing", false)]
    public void TheKindConstraintMatchesRegisteredKindsOnly(string value, bool matches)
    {
        var values = new RouteValueDictionary { ["kind"] = value };

        Assert.Equal(matches, new MetadataEntityTypeRouteConstraint().Match(null, null, "kind", values, RouteDirection.IncomingRequest));
    }

    [Fact]
    public void ThePathOfAnEntryEscapesItsID()
    {
        Assert.Equal("Metadata/test-plugin/Series/a%2Fb", MetadataModelBuilder.PathOf(ID(MetadataEntityType.Series, "a/b")));
        Assert.Equal("Metadata/tmdb/Movie/600", MetadataModelBuilder.PathOf(new(MetadataSource.TMDB, MetadataEntityType.Movie, "600")));
        Assert.Equal("Metadata/test-plugin/ordering/o1", MetadataModelBuilder.PathOf(ID(MetadataEntityType.Ordering, "o1")));
    }

    #endregion
}
