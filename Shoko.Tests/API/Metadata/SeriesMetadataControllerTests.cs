using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Shoko.Abstractions.Connectivity.Services;
using Shoko.Abstractions.Metadata;
using Shoko.Abstractions.Metadata.Anidb;
using Shoko.Abstractions.Metadata.CrossReferences;
using Shoko.Abstractions.Metadata.Enums;
using Shoko.Abstractions.Metadata.Providers;
using Shoko.Abstractions.Metadata.Search;
using Shoko.Abstractions.Metadata.Services;
using Shoko.Abstractions.Metadata.Shoko;
using Shoko.Abstractions.Metadata.Storage;
using Shoko.Abstractions.User;
using Shoko.Abstractions.User.Services;
using Shoko.Server.API.v3.Controllers;
using Shoko.Server.API.v3.Helpers;
using Shoko.Server.Settings;
using Shoko.Tests.Infrastructure;
using Xunit;
using static Shoko.Tests.API.Metadata.FakeMetadataEntries;

namespace Shoko.Tests.API.Metadata;

/// <summary>
/// Covers the generic routes hanging a source's links off a Shoko series and
/// episode: linking, unlinking, overriding, matching and resetting episode
/// links, refreshing what is linked, and the series and sources they refuse.
/// </summary>
public class SeriesMetadataControllerTests
{
    #region Fixture

    private const int SeriesID = 1;

    private const int AnimeID = 100;

    /// <summary>
    /// The controllers over mocked services, with a Shoko series of two
    /// AniDB episodes.
    /// </summary>
    private sealed class Fixture
    {
        public Mock<IMetadataService> Metadata { get; } = new();

        public Mock<IMetadataLinkingService> Linking { get; } = new();

        public Mock<IMetadataRefreshService> Refresh { get; } = new();

        public Mock<ISuspensionService> Suspensions { get; } = SuspensionTestDoubles.Service();

        public Mock<IMetadataProviderManager> Providers { get; } = new();

        public Mock<IUserService> Users { get; } = new();

        public Mock<IShokoSeries> Series { get; } = new();

        public List<IMetadataSeriesCrossReference> SeriesLinks { get; } = [];

        public List<IMetadataEpisodeCrossReference> EpisodeLinks { get; } = [];

        public Fixture()
        {
            Refresh.Setup(r => r.WaitForRefresh(It.IsAny<MetadataGuid>(), It.IsAny<CancellationToken>())).ReturnsAsync(false);
            Linking.Setup(l => l.MatchEpisodes(It.IsAny<int>(), It.IsAny<MetadataGuid>(), It.IsAny<MetadataGuid?>(), It.IsAny<bool>(), It.IsAny<bool>(), It.IsAny<bool?>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync([]);
            Providers.SetupGet(p => p.MetadataProviders).Returns([]);

            var anime = new Mock<IAnidbAnime>();
            anime.SetupGet(a => a.Episodes).Returns([AnidbEpisode(1001, 1), AnidbEpisode(1002, 2)]);
            Series.SetupGet(s => s.AnidbAnimeID).Returns(AnimeID);
            Series.SetupGet(s => s.AnidbAnime).Returns(anime.Object);
            Metadata.Setup(m => m.GetShokoSeriesByID(SeriesID)).Returns(Series.Object);
            Metadata.Setup(m => m.GetSeriesCrossReferences(AnimeID, It.IsAny<MetadataSource?>())).Returns(() => SeriesLinks);
            Metadata.Setup(m => m.GetEpisodeCrossReferencesForSeries(AnimeID, It.IsAny<MetadataSource?>())).Returns(() => EpisodeLinks);
            Metadata.Setup(m => m.GetMovieCrossReferencesForSeries(AnimeID, It.IsAny<MetadataSource?>())).Returns([]);
            foreach (var (episodeID, number) in new[] { (1001, 1), (1002, 2) })
            {
                var episode = new Mock<IShokoEpisode>();
                episode.SetupGet(e => e.ShokoSeriesID).Returns(SeriesID);
                episode.SetupGet(e => e.AnidbEpisodeID).Returns(episodeID);
                episode.SetupGet(e => e.AnidbEpisode).Returns(AnidbEpisode(episodeID, number));
                Metadata.Setup(m => m.GetShokoEpisodeByAnidbID(episodeID)).Returns(episode.Object);
                Metadata.Setup(m => m.GetShokoEpisodeByID(episodeID - 1000)).Returns(episode.Object);
            }
        }

        private static IAnidbEpisode AnidbEpisode(int id, int number)
        {
            var episode = new Mock<IAnidbEpisode>();
            episode.SetupGet(e => e.AnidbID).Returns(id);
            episode.SetupGet(e => e.AnidbAnimeID).Returns(AnimeID);
            episode.SetupGet(e => e.EpisodeNumber).Returns(number);
            episode.SetupGet(e => e.Type).Returns(EpisodeType.Episode);
            return episode.Object;
        }

        /// <summary>
        /// Stores a series of the fake source, refreshed in full or only
        /// fetched in part.
        /// </summary>
        public FakeSeries StoreSeries(string id, bool fullyRefreshed)
        {
            var series = new FakeSeries(id, $"Series {id}");
            Metadata.Setup(m => m.GetEntry(series.ID)).Returns(series);
            Metadata.Setup(m => m.GetEntry<ISeries>(series.ID)).Returns(series);
            Refresh.Setup(r => r.GetLastRefreshedAt(series.ID)).Returns(fullyRefreshed ? DateTime.Now.AddDays(-1) : null);
            return series;
        }

        public FakeEpisode StoreEpisode(string id, string seriesID, int number)
        {
            var episode = new FakeEpisode(id, seriesID, number);
            Metadata.Setup(m => m.GetEntry(episode.ID)).Returns(episode);
            Metadata.Setup(m => m.GetEntry<IEpisode>(episode.ID)).Returns(episode);
            return episode;
        }

        public void Link(string seriesID)
            => SeriesLinks.Add(Mock.Of<IMetadataSeriesCrossReference>(link =>
                link.AnidbAnimeID == AnimeID && link.ProviderID == ID(MetadataEntityType.Series, seriesID) && link.Source == Source && link.EntityType == MetadataEntityType.Series));

        public void Pause(TimeSpan left)
            => Suspensions.Suspend(Source, DateTime.UtcNow + left);

        /// <summary>
        /// Registers an auto-linker for the fake source.
        /// </summary>
        /// <param name="configured">Whether the auto-linker is configured.</param>
        public void AddAutoLinker(bool configured)
            => Providers.SetupGet(p => p.MetadataProviders).Returns([new MetadataProviderInfo
            {
                ID = Guid.NewGuid(),
                Version = new(1, 0),
                Name = "Fake",
                Description = string.Empty,
                Provider = Mock.Of<IMetadataAutoLinkingProvider>(provider => provider.IsConfigured == configured),
                ConfigurationInfo = null,
                PluginInfo = null!,
                SupportsSeries = true,
                SupportsMovies = false,
                SupportsCollections = false,
                SupportsAutoLinking = true,
                Source = Source,
                AvailableEntityTypes = new HashSet<MetadataEntityType> { MetadataEntityType.Series },
                EnabledEntityTypes = new HashSet<MetadataEntityType> { MetadataEntityType.Series },
                IsAutoLinker = true,
            }]);

        public void Hide()
        {
            var user = new Mock<IUser>();
            user.Setup(u => u.IsAllowedToSee(It.IsAny<IShokoSeries>())).Returns(false);
            Users.Setup(u => u.GetUserFromHttpContext(It.IsAny<HttpContext>())).Returns(user.Object);
        }

        private MetadataModelBuilder Models
            => new(Metadata.Object, Mock.Of<IMetadataTextManager>(), Mock.Of<IImageManager>(), Mock.Of<IMetadataStudioStore>());

        private static ControllerContext Context()
            => new() { HttpContext = new DefaultHttpContext { RequestServices = new ServiceCollection().AddLogging().AddMvcCore().Services.BuildServiceProvider() } };

        public SeriesMetadataController Controller()
            => new(
                new StubSettingsProvider(new ServerSettings()),
                NullLogger<SeriesMetadataController>.Instance,
                Users.Object,
                Metadata.Object,
                Linking.Object,
                Refresh.Object,
                Providers.Object,
                Models,
                Suspensions.Object
            )
            {
                ControllerContext = Context(),
            };

        public EpisodeMetadataController EpisodeController()
            => new(
                new StubSettingsProvider(new ServerSettings()),
                NullLogger<EpisodeMetadataController>.Instance,
                Users.Object,
                Metadata.Object,
                Linking.Object,
                Refresh.Object,
                Models,
                Suspensions.Object
            )
            {
                ControllerContext = Context(),
            };

        public void VerifyNoRefresh()
            => Refresh.Verify(r => r.RefreshEntry(It.IsAny<MetadataGuid>(), It.IsAny<bool>(), It.IsAny<MetadataRefreshOptions?>(), It.IsAny<bool>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    private static int StatusOf(IActionResult result)
        => result switch
        {
            IStatusCodeActionResult { StatusCode: { } code } => code,
            _ => 200,
        };

    private static int StatusOf<T>(ActionResult<T> result)
        => result.Result is null ? 200 : StatusOf(result.Result);

    #endregion

    #region Access

    [Fact]
    public async Task AniDBIsNotALinkTarget()
    {
        var fixture = new Fixture();

        Assert.Equal(400, StatusOf(await fixture.Controller().LinkSeries(SeriesID, MetadataSource.AniDB, new() { ID = "1" }, TestContext.Current.CancellationToken)));
        Assert.Equal(400, StatusOf(fixture.Controller().GetCrossReferences(SeriesID, MetadataSource.Shoko)));
        fixture.Linking.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task AnUnknownOrHiddenSeriesIsRefused()
    {
        var fixture = new Fixture();

        Assert.IsType<NotFoundObjectResult>(await fixture.Controller().LinkSeries(2, Source, new() { ID = "21" }, TestContext.Current.CancellationToken));
        fixture.Hide();
        Assert.Equal(403, StatusOf(await fixture.Controller().LinkSeries(SeriesID, Source, new() { ID = "21" }, TestContext.Current.CancellationToken)));
        Assert.Equal(403, StatusOf(await fixture.EpisodeController().GetLinkedEpisodes(1, Source, cancellationToken: TestContext.Current.CancellationToken)));
        fixture.Linking.Verify(l => l.AddSeriesLink(It.IsAny<MetadataSeriesLinkRequest>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    #endregion

    #region Reads

    [Fact]
    public async Task TheLinkedSeriesAreListed_LeavingOutOnesNotStored()
    {
        var fixture = new Fixture();
        fixture.StoreSeries("21", fullyRefreshed: true);
        fixture.Link("21");
        fixture.Link("999");

        var linked = await fixture.Controller().GetLinkedSeries(SeriesID, Source, cancellationToken: TestContext.Current.CancellationToken);
        var unknown = await fixture.Controller().GetLinkedSeries(2, Source, cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal(["21"], linked.Value!.Select(series => series.ID));
        Assert.IsType<NotFoundObjectResult>(unknown.Result);
    }

    #endregion

    #region Link

    [Fact]
    public async Task AddLink_RefreshesASeriesOnlyQuickFetched()
    {
        var fixture = new Fixture();
        var series = fixture.StoreSeries("21", fullyRefreshed: false);

        Assert.IsType<NoContentResult>(await fixture.Controller().LinkSeries(SeriesID, Source, new() { ID = "21" }, TestContext.Current.CancellationToken));

        fixture.Linking.Verify(l => l.AddSeriesLink(It.Is<MetadataSeriesLinkRequest>(request =>
            request.Source == Source && request.ProviderID == series.ID && request.AnidbAnimeID == AnimeID && request.Additive && request.MatchRating == MatchRating.UserVerified),
            It.IsAny<CancellationToken>()), Times.Once);
        fixture.Refresh.Verify(r => r.RefreshEntry(series.ID, false, It.IsAny<MetadataRefreshOptions?>(), false, false, It.IsAny<CancellationToken>()), Times.Once);
        fixture.Refresh.Verify(r => r.RefreshEntry(series.ID, true, It.IsAny<MetadataRefreshOptions?>(), It.IsAny<bool>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task AddLink_LeavesAFetchedSeriesAlone_AndItsEpisodesToTheLinkingService()
    {
        var fixture = new Fixture();
        var series = fixture.StoreSeries("21", fullyRefreshed: true);

        Assert.IsType<NoContentResult>(await fixture.Controller().LinkSeries(SeriesID, Source, new() { ID = "21", Replace = true }, TestContext.Current.CancellationToken));

        fixture.Linking.Verify(l => l.AddSeriesLink(It.Is<MetadataSeriesLinkRequest>(request => !request.Additive), It.IsAny<CancellationToken>()), Times.Once);
        fixture.Linking.Verify(l => l.MatchEpisodes(AnimeID, series.ID, null, true, true, null, It.IsAny<CancellationToken>()), Times.Never);
        fixture.VerifyNoRefresh();
    }

    [Fact]
    public async Task AddLink_ForcesARefreshOnlyWhenAskedTo()
    {
        var fixture = new Fixture();
        fixture.StoreSeries("22", fullyRefreshed: true);

        Assert.IsType<NoContentResult>(await fixture.Controller().LinkSeries(SeriesID, Source, new() { ID = "21" }, TestContext.Current.CancellationToken));
        Assert.IsType<NoContentResult>(await fixture.Controller().LinkSeries(SeriesID, Source, new() { ID = "22", Refresh = true }, TestContext.Current.CancellationToken));

        fixture.Refresh.Verify(r => r.RefreshEntry(ID(MetadataEntityType.Series, "21"), false, It.IsAny<MetadataRefreshOptions?>(), false, false, It.IsAny<CancellationToken>()), Times.Once);
        fixture.Refresh.Verify(r => r.RefreshEntry(ID(MetadataEntityType.Series, "21"), true, It.IsAny<MetadataRefreshOptions?>(), It.IsAny<bool>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()), Times.Never);
        fixture.Refresh.Verify(r => r.RefreshEntry(ID(MetadataEntityType.Series, "22"), true, It.IsAny<MetadataRefreshOptions?>(), false, false, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task AFailedMatchStillLinks()
    {
        var fixture = new Fixture();
        fixture.Linking.Setup(l => l.MatchEpisodes(It.IsAny<int>(), It.IsAny<MetadataGuid>(), It.IsAny<MetadataGuid?>(), It.IsAny<bool>(), It.IsAny<bool>(), It.IsAny<bool?>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new NotSupportedException("No episodes."));

        Assert.IsType<NoContentResult>(await fixture.Controller().LinkSeries(SeriesID, Source, new() { ID = "21" }, TestContext.Current.CancellationToken));
        fixture.Linking.Verify(l => l.AddSeriesLink(It.IsAny<MetadataSeriesLinkRequest>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task UnlinkRemovesOneSeriesOrEveryOneAndStopsAutoLinking()
    {
        var fixture = new Fixture();

        Assert.IsType<NoContentResult>(await fixture.Controller().UnlinkSeries(SeriesID, Source, new() { ID = "21", Purge = true }, TestContext.Current.CancellationToken));
        Assert.IsType<NoContentResult>(await fixture.Controller().UnlinkSeries(SeriesID, Source, null, TestContext.Current.CancellationToken));

        fixture.Linking.Verify(l => l.RemoveSeriesLink(It.Is<MetadataSeriesLinkRequest>(request =>
            request.ProviderID == ID(MetadataEntityType.Series, "21") && request.Purge && request.DisableAutoLinking && request.AnidbAnimeID == AnimeID), It.IsAny<CancellationToken>()), Times.Once);
        fixture.Linking.Verify(l => l.RemoveLinksForAnime(Source, AnimeID, MetadataEntityType.Series, false, true, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task AMovieIsLinkedToTheFirstEpisodeUnlessOneIsNamed()
    {
        var fixture = new Fixture();

        Assert.IsType<NoContentResult>(await fixture.Controller().LinkMovie(SeriesID, Source, new() { ID = "m1" }, TestContext.Current.CancellationToken));
        Assert.IsType<NoContentResult>(await fixture.Controller().LinkMovie(SeriesID, Source, new() { ID = "m2", EpisodeID = 1002 }, TestContext.Current.CancellationToken));
        Assert.Equal(400, StatusOf(await fixture.Controller().LinkMovie(SeriesID, Source, new() { ID = "m3", EpisodeID = 9999 }, TestContext.Current.CancellationToken)));
        Assert.IsType<NoContentResult>(await fixture.EpisodeController().LinkMovie(2, Source, new() { ID = "m4" }, TestContext.Current.CancellationToken));

        fixture.Linking.Verify(l => l.AddMovieLink(It.Is<MetadataEpisodeLinkRequest>(request => request.ProviderID == ID(MetadataEntityType.Movie, "m1") && request.AnidbEpisodeID == 1001), It.IsAny<CancellationToken>()), Times.Once);
        fixture.Linking.Verify(l => l.AddMovieLink(It.Is<MetadataEpisodeLinkRequest>(request => request.ProviderID == ID(MetadataEntityType.Movie, "m2") && request.AnidbEpisodeID == 1002), It.IsAny<CancellationToken>()), Times.Once);
        fixture.Linking.Verify(l => l.AddMovieLink(It.Is<MetadataEpisodeLinkRequest>(request =>
            request.ProviderID == ID(MetadataEntityType.Movie, "m4") && request.AnidbEpisodeID == 1002 && request.AnidbAnimeID == AnimeID), It.IsAny<CancellationToken>()), Times.Once);
        fixture.Linking.Verify(l => l.AddMovieLink(It.IsAny<MetadataEpisodeLinkRequest>(), It.IsAny<CancellationToken>()), Times.Exactly(3));
    }

    [Fact]
    public async Task AnIDTheStoreRefusesIsABadRequest_AndNothingIsRefreshed()
    {
        // Such as a TMDB 0, which the route reads as an ID like any other.
        var fixture = new Fixture();
        fixture.Linking.Setup(l => l.AddSeriesLink(It.IsAny<MetadataSeriesLinkRequest>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ArgumentException("\"0\" is not an ID the source gives.", "links"));
        fixture.Linking.Setup(l => l.AddMovieLink(It.IsAny<MetadataEpisodeLinkRequest>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ArgumentException("\"0\" is not an ID the source gives.", "links"));

        Assert.Equal(400, StatusOf(await fixture.Controller().LinkSeries(SeriesID, Source, new() { ID = "0" }, TestContext.Current.CancellationToken)));
        Assert.Equal(400, StatusOf(await fixture.Controller().LinkMovie(SeriesID, Source, new() { ID = "0" }, TestContext.Current.CancellationToken)));
        Assert.Equal(400, StatusOf(await fixture.EpisodeController().LinkMovie(2, Source, new() { ID = "0" }, TestContext.Current.CancellationToken)));

        fixture.VerifyNoRefresh();
        fixture.Linking.Verify(l => l.MatchEpisodes(It.IsAny<int>(), It.IsAny<MetadataGuid>(), It.IsAny<MetadataGuid?>(), It.IsAny<bool>(), It.IsAny<bool>(), It.IsAny<bool?>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    #endregion

    #region Episode Links

    [Fact]
    public async Task AnOverrideLinksTheMissingSeriesFirstAndSetsReplacingLinksFirst()
    {
        var fixture = new Fixture();
        fixture.Link("21");
        fixture.StoreSeries("21", fullyRefreshed: true);
        fixture.StoreEpisode("2101", "21", 1);
        fixture.StoreEpisode("2202", "22", 2);
        List<(int AnidbEpisodeID, MetadataGuid? ProviderID, bool Additive)> calls = [];
        fixture.Linking.Setup(l => l.SetEpisodeLink(Source, It.IsAny<int>(), It.IsAny<MetadataGuid?>(), It.IsAny<bool>(), It.IsAny<int?>(), It.IsAny<MetadataGuid?>(), It.IsAny<CancellationToken>()))
            .Callback((MetadataSource _, int anidbEpisodeID, MetadataGuid? providerID, bool additive, int? _, MetadataGuid? _, CancellationToken _) => calls.Add((anidbEpisodeID, providerID, additive)))
            .ReturnsAsync(true);

        var result = await fixture.Controller().OverrideEpisodeCrossReferences(SeriesID, Source, new()
        {
            UnsetAll = true,
            Mapping =
            [
                new() { AniDBID = 1001, ID = "2101" },
                new() { AniDBID = 1002, ID = "2202", Replace = true },
                new() { AniDBID = 1001, ID = "0" },
            ],
        }, TestContext.Current.CancellationToken);

        // The series of episode 2202 was not linked or stored, so it is
        // linked first and refreshed after.
        Assert.IsType<CreatedResult>(result);
        fixture.Linking.Verify(l => l.AddSeriesLink(It.Is<MetadataSeriesLinkRequest>(request => request.ProviderID == ID(MetadataEntityType.Series, "22") && request.Additive), It.IsAny<CancellationToken>()), Times.Once);
        fixture.Linking.Verify(l => l.ResetEpisodeLinks(Source, AnimeID, false, It.IsAny<CancellationToken>()), Times.Once);
        Assert.Equal(
            [(1002, ID(MetadataEntityType.Episode, "2202"), false), (1001, ID(MetadataEntityType.Episode, "2101"), true), (1001, null, false)],
            calls
        );
        fixture.Refresh.Verify(r => r.RefreshEntry(ID(MetadataEntityType.Series, "22"), false, It.IsAny<MetadataRefreshOptions?>(), false, false, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task AnOverrideNamingUnknownEpisodesWritesNothing()
    {
        var fixture = new Fixture();

        var empty = await fixture.Controller().OverrideEpisodeCrossReferences(SeriesID, Source, new(), TestContext.Current.CancellationToken);
        var unknown = await fixture.Controller().OverrideEpisodeCrossReferences(SeriesID, Source, new()
        {
            Mapping = [new() { AniDBID = 5555 }, new() { AniDBID = 1001, ID = "not-stored" }],
        }, TestContext.Current.CancellationToken);

        Assert.Equal(400, StatusOf(empty));
        Assert.Equal(400, StatusOf(unknown));
        fixture.Linking.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task AnAutoMatchPreviewWritesNothing()
    {
        var fixture = new Fixture();
        var none = await fixture.Controller().PreviewAutoMatchEpisodes(SeriesID, Source, cancellationToken: TestContext.Current.CancellationToken);
        fixture.Link("21");
        fixture.Linking.Setup(l => l.MatchEpisodes(AnimeID, ID(MetadataEntityType.Series, "21"), null, false, false, true, It.IsAny<CancellationToken>()))
            .ReturnsAsync([new FakeEpisodeLink(AnimeID, 1001, ID(MetadataEntityType.Episode, "2101")) { ProviderParentID = ID(MetadataEntityType.Series, "21") }]);

        var preview = await fixture.Controller().PreviewAutoMatchEpisodes(SeriesID, Source, keepExisting: false, considerExistingOtherLinks: true, cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal(400, StatusOf(none));
        var link = Assert.Single(preview.Value!.List);
        Assert.Equal(("2101", "21", 1001), (link.ID, link.ParentID, link.AnidbEpisodeID));
    }

    [Fact]
    public async Task AnAutoMatchLinksANewSeriesOrMatchesALinkedOne()
    {
        var fixture = new Fixture();
        fixture.StoreSeries("21", fullyRefreshed: true);
        fixture.StoreSeries("22", fullyRefreshed: false);
        fixture.Link("21");

        var missing = await fixture.Controller().AutoMatchEpisodes(SeriesID, Source, new() { ParentID = "404" }, TestContext.Current.CancellationToken);
        var linked = await fixture.Controller().AutoMatchEpisodes(SeriesID, Source, new() { KeepExisting = false }, TestContext.Current.CancellationToken);
        var added = await fixture.Controller().AutoMatchEpisodes(SeriesID, Source, new() { ParentID = "22" }, TestContext.Current.CancellationToken);

        Assert.Equal(400, StatusOf(missing));
        Assert.IsType<NoContentResult>(linked);
        Assert.IsType<CreatedResult>(added);
        fixture.Linking.Verify(l => l.MatchEpisodes(AnimeID, ID(MetadataEntityType.Series, "21"), null, false, true, null, It.IsAny<CancellationToken>()), Times.Once);
        fixture.Linking.Verify(l => l.AddSeriesLink(It.Is<MetadataSeriesLinkRequest>(request => request.ProviderID == ID(MetadataEntityType.Series, "22")), It.IsAny<CancellationToken>()), Times.Once);
        fixture.Refresh.Verify(r => r.RefreshEntry(ID(MetadataEntityType.Series, "22"), false, It.IsAny<MetadataRefreshOptions?>(), false, false, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public void EpisodeLinksCanBeLimitedToALinkedSeries()
    {
        var fixture = new Fixture();
        fixture.Link("21");
        fixture.EpisodeLinks.AddRange([
            new FakeEpisodeLink(AnimeID, 1001, ID(MetadataEntityType.Episode, "2101")) { ProviderParentID = ID(MetadataEntityType.Series, "21") },
            new FakeEpisodeLink(AnimeID, 1002, ID(MetadataEntityType.Episode, "2202")) { ProviderParentID = ID(MetadataEntityType.Series, "22") },
            new FakeEpisodeLink(AnimeID, 1002, null),
        ]);

        var all = fixture.Controller().GetEpisodeCrossReferences(SeriesID, Source).Value!;
        var one = fixture.Controller().GetEpisodeCrossReferences(SeriesID, Source, "21").Value!;
        var notLinked = fixture.Controller().GetEpisodeCrossReferences(SeriesID, Source, "22");

        Assert.Equal(3, all.Total);
        Assert.Equal(["2101", null], one.List.Select(link => link.ID));
        Assert.Equal(400, StatusOf(notLinked));
    }

    #endregion

    #region Search and Refresh

    [Fact]
    public async Task AnAutoSearchPreviewNeedsAnAutoLinkerAndShowsWhyCandidatesLost()
    {
        var fixture = new Fixture();
        var unsupported = await fixture.Controller().PreviewAutoSearch(SeriesID, Source, TestContext.Current.CancellationToken);
        fixture.AddAutoLinker(configured: true);
        fixture.Linking.Setup(l => l.PreviewAutoLink(Source, AnimeID, It.IsAny<CancellationToken>())).ReturnsAsync([new MetadataAutoLinkCandidate
        {
            AnidbAnimeID = AnimeID,
            Result = new MetadataSeriesSearchResult { ID = ID(MetadataEntityType.Series, "22"), Title = "Lost" },
            MatchRating = MatchRating.TitleKindaMatches,
            IsRemote = true,
            Rejection = new() { Reason = MatchRejectionReason.TitleMismatch },
        }]);

        var found = await fixture.Controller().PreviewAutoSearch(SeriesID, Source, TestContext.Current.CancellationToken);
        fixture.Pause(TimeSpan.FromSeconds(5));
        var paused = await fixture.Controller().PreviewAutoSearch(SeriesID, Source, TestContext.Current.CancellationToken);

        Assert.Equal(400, StatusOf(unsupported));
        var lost = Assert.Single(found.Value!);
        Assert.Equal(("22", MatchRejectionReason.TitleMismatch), (lost.ID, lost.Rejection?.Reason));
        Assert.Equal(503, StatusOf(paused));
        fixture.Linking.Verify(l => l.PreviewAutoLink(Source, AnimeID, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Refresh_WhilePaused_QueuesAtTheFront_AndAnswers503()
    {
        var fixture = new Fixture();
        fixture.Link("21");
        fixture.Pause(TimeSpan.FromSeconds(30));
        var controller = fixture.Controller();

        var result = await controller.RefreshLinked(SeriesID, Source, new() { Immediate = false }, TestContext.Current.CancellationToken);

        Assert.Equal(503, StatusOf(result));
        Assert.InRange(int.Parse(controller.Response.Headers.RetryAfter.ToString()), 29, 30);
        fixture.Refresh.Verify(r => r.RefreshEntry(ID(MetadataEntityType.Series, "21"), false, It.Is<MetadataRefreshOptions?>(options => options!.Reason == MetadataRefreshReason.Requested),
            false, true, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Refresh_Immediate_RunsTheRefreshAndWaits()
    {
        var fixture = new Fixture();
        fixture.Link("21");

        var result = await fixture.Controller().RefreshLinked(SeriesID, Source, new() { Immediate = true, QuickRefresh = true, Force = true }, TestContext.Current.CancellationToken);

        Assert.IsType<OkResult>(result);
        fixture.Refresh.Verify(r => r.RefreshEntry(ID(MetadataEntityType.Series, "21"), false, It.Is<MetadataRefreshOptions?>(options => options!.QuickRefresh && options.Reason == MetadataRefreshReason.Requested),
            true, false, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task AnAutoSearchIsRefusedWhileTheAutoLinkerIsNotConfigured()
    {
        var fixture = new Fixture();
        fixture.AddAutoLinker(configured: false);

        var preview = await fixture.Controller().PreviewAutoSearch(SeriesID, Source, TestContext.Current.CancellationToken);
        var search = await fixture.Controller().ScheduleAutoSearch(SeriesID, Source, force: true, TestContext.Current.CancellationToken);

        Assert.Equal(503, StatusOf(preview));
        Assert.Equal(503, StatusOf(search));
        fixture.Linking.Verify(l => l.PreviewAutoLink(It.IsAny<MetadataSource>(), It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
        fixture.Refresh.Verify(r => r.AutoSearch(It.IsAny<MetadataSource>(), It.IsAny<int>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    #endregion
}
