using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
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
using Shoko.Abstractions.Metadata.CrossReferences;
using Shoko.Abstractions.Metadata.Enums;
using Shoko.Abstractions.Metadata.Providers;
using Shoko.Abstractions.Metadata.Search;
using Shoko.Abstractions.Metadata.Services;
using Shoko.Abstractions.Metadata.Storage;
using Shoko.Abstractions.Plugin;
using Shoko.Abstractions.Plugin.Models;
using Shoko.Server.API.v3.Controllers;
using Shoko.Server.API.v3.Helpers;
using Shoko.Server.API.v3.Models.Metadata;
using Shoko.Server.API.v3.Models.Metadata.Input;
using Shoko.Server.Services;
using Shoko.Server.Settings;
using Shoko.Tests.Infrastructure;
using Shoko.Tests.Services;
using Xunit;
using static Shoko.Tests.API.Metadata.FakeMetadataEntries;

namespace Shoko.Tests.API.Metadata;

/// <summary>
/// Covers the generic provider and source routes: listing and setting up
/// providers, a source's status, searching and looking entries up, the CSV
/// export and import, and the source-wide actions.
/// </summary>
public class MetadataProviderControllerTests
{
    #region Fixture

    /// <summary>
    /// A provider for the fake source, answering for series and looking
    /// them up.
    /// </summary>
    public interface ILookupProvider : IMetadataSeriesLinkingProvider, IMetadataAutoLinkingProvider;

    /// <summary>
    /// The controller over mocked services.
    /// </summary>
    private sealed class Fixture
    {
        public Mock<IMetadataProviderManager> Providers { get; } = new();

        public Mock<IMetadataRefreshService> Refresh { get; } = new();

        public Mock<ISuspensionService> Suspensions { get; } = SuspensionTestDoubles.Service();

        public Mock<IMetadataPurgeService> Purge { get; } = new();

        public Mock<IMetadataLinkingService> Linking { get; } = new();

        public Mock<IMetadataService> Metadata { get; } = new();

        public Mock<IMetadataCrossReferenceTransferService> Transfer { get; } = new();

        public Mock<IImageManager> Images { get; } = new();

        public List<MetadataProviderInfo> Registered { get; } = [];

        public IApplicationPaths Paths { get; } = Mock.Of<IApplicationPaths>(paths => paths.PluginsPath == Path.GetTempPath() && paths.ApplicationPath == AppContext.BaseDirectory);

        /// <summary>
        /// Whether the providers registered from now on are configured.
        /// </summary>
        public bool Configured { get; set; } = true;

        /// <summary>
        /// What the providers registered from now on say they are missing.
        /// </summary>
        public string? NotConfiguredReason { get; set; }

        public Fixture()
        {
            Providers.SetupGet(p => p.MetadataProviders).Returns(() => Registered);
            Providers.Setup(p => p.GetProviderInfo(It.IsAny<Guid>())).Returns((Guid id) => Registered.FirstOrDefault(info => info.ID == id));
            Providers.Setup(p => p.GetAvailableProviders(It.IsAny<MetadataEntityType>(), It.IsAny<MetadataSource?>()))
                .Returns((MetadataEntityType kind, MetadataSource? source) => Registered.Where(info => info.EnabledEntityTypes.Contains(kind) && (source is null || info.Source == source)));
            Images.Setup(i => i.GetImagesForEntity(It.IsAny<Abstractions.Metadata.Containers.IWithImages>(), It.IsAny<Abstractions.Metadata.Image.Options.ImageFilteringOptions?>())).Returns([]);
        }

        public MetadataProviderInfo Register(bool lookup = true, bool enabled = true, bool autoLinker = true, bool linksSeries = true, PackageImageInfo? icon = null)
        {
            IReadOnlySet<MetadataEntityType> linkable = linksSeries
                ? new HashSet<MetadataEntityType> { MetadataEntityType.Series, MetadataEntityType.Episode }
                : new HashSet<MetadataEntityType> { MetadataEntityType.Episode };
            var info = new MetadataProviderInfo
            {
                ID = Guid.NewGuid(),
                Version = new(1, 0),
                Name = "Fake",
                Description = "A fake provider.",
                Provider = Mock.Of<ILookupProvider>(provider => provider.IsConfigured == Configured && provider.NotConfiguredReason == NotConfiguredReason && provider.LinkableEntityTypes == linkable),
                ConfigurationInfo = null,
                PluginInfo = PluginTestDoubles.InstalledPluginInfo(typeof(MetadataProviderControllerTests), Guid.NewGuid()),
                SupportsSeries = true,
                SupportsMovies = false,
                SupportsCollections = false,
                SupportsAutoLinking = true,
                SupportsLookup = lookup,
                Source = Source,
                AvailableEntityTypes = new HashSet<MetadataEntityType> { MetadataEntityType.Series, MetadataEntityType.Episode },
                EnabledEntityTypes = enabled ? new HashSet<MetadataEntityType> { MetadataEntityType.Series, MetadataEntityType.Episode } : new HashSet<MetadataEntityType>(),
                IsAutoLinker = autoLinker,
                Icon = icon,
            };
            Registered.Add(info);
            return info;
        }

        public void Pause(TimeSpan left)
            => Suspensions.Suspend(Source, DateTime.UtcNow + left, "Rate limited.");

        public MetadataSourceActions Actions
            => new(Refresh.Object, Purge.Object, Linking.Object, Images.Object, NullLogger<MetadataSourceActions>.Instance);

        public MetadataProviderController Controller(IMetadataProviderManager? providers = null)
            => new(
                new StubSettingsProvider(new ServerSettings()),
                providers ?? Providers.Object,
                Suspensions.Object,
                Linking.Object,
                Metadata.Object,
                Transfer.Object,
                new MetadataModelBuilder(Metadata.Object, Mock.Of<IMetadataTextManager>(), Images.Object, Mock.Of<IMetadataStudioStore>()),
                Actions,
                Paths
            )
            {
                ControllerContext = new()
                {
                    HttpContext = new DefaultHttpContext { RequestServices = new ServiceCollection().AddLogging().AddMvcCore().Services.BuildServiceProvider() },
                },
            };

        public static MetadataSeriesSearchResult Found(string id, string title)
            => new() { ID = ID(MetadataEntityType.Series, id), Title = title, UserRating = 7.5m, UserVotes = 10, Type = AnimeType.TV };
    }

    private static T Value<T>(ActionResult<T> result) where T : class
        => result.Value ?? Assert.IsType<T>(Assert.IsType<OkObjectResult>(result.Result).Value, exactMatch: false);

    private static PackageImageInfo Icon(string path, string mimeType)
        => new() { FilePath = path, MimeType = mimeType, Width = 24, Height = 24 };

    private static int StatusOf<T>(ActionResult<T> result)
        => result.Result switch
        {
            IStatusCodeActionResult { StatusCode: { } code } => code,
            _ => 200,
        };

    #endregion

    #region Providers

    [Fact]
    public void ProvidersAreListedByPluginAndSayWhenDisabled()
    {
        var fixture = new Fixture();
        var info = fixture.Register();
        var other = fixture.Register(enabled: false);

        var all = Value(fixture.Controller().GetProviders());
        var one = Value(fixture.Controller().GetProviders(info.PluginInfo.ID));

        Assert.Equal(2, all.Count);
        Assert.Equal(info.ID, Assert.Single(one).ID);
        Assert.False(all.Single(provider => provider.ID == other.ID).IsEnabled);
        Assert.IsType<NotFoundObjectResult>(fixture.Controller().GetProvider(Guid.NewGuid()).Result);
    }

    [Fact]
    public void LinkSourcesAreOneRowPerSourceAndSayWhichKindsAreOn()
    {
        var fixture = new Fixture();
        fixture.Register(enabled: false);
        fixture.Register(linksSeries: false);

        var row = Assert.Single(Value(fixture.Controller().GetLinkSources()));
        Assert.Equal((Source, true, false, false), (row.Source, row.SupportsSeries, row.SupportsMovies, row.IsSeriesEnabled));

        fixture.Register();
        Assert.True(Assert.Single(Value(fixture.Controller().GetLinkSources())).IsSeriesEnabled);

        // A source whose providers link nothing is not listed.
        fixture.Registered.RemoveAll(info => info.Provider is IMetadataSeriesLinkingProvider { LinkableEntityTypes.Count: 2 });
        Assert.Empty(Value(fixture.Controller().GetLinkSources()));
    }

    [Fact]
    public void LinkSourcesSayWhetherTheSourceHasAnIconAndWhosePluginItIs()
    {
        var fixture = new Fixture();
        var info = fixture.Register();

        var plain = Assert.Single(Value(fixture.Controller().GetLinkSources()));
        fixture.Providers.Setup(p => p.GetSourceIcon(Source)).Returns(Icon("/nowhere/icon.svg", "image/svg+xml"));
        var withIcon = Assert.Single(Value(fixture.Controller().GetLinkSources()));

        Assert.Equal((false, info.PluginInfo.ID), (plain.HasIcon, plain.PluginID));
        Assert.True(withIcon.HasIcon);
    }

    [Fact]
    public void ASourceIconIsServedWithItsTypeAndSafeHeaders()
    {
        var fixture = new Fixture();
        var file = Path.GetTempFileName();
        try
        {
            File.WriteAllText(file, "<svg xmlns=\"http://www.w3.org/2000/svg\"/>");
            fixture.Providers.Setup(p => p.GetSourceIcon(Source)).Returns(Icon(file, "image/svg+xml"));
            var controller = fixture.Controller();

            var result = Assert.IsType<FileContentResult>(controller.GetSourceIcon(Source));

            Assert.Equal("image/svg+xml", result.ContentType);
            Assert.Equal(File.ReadAllBytes(file), result.FileContents);
            Assert.NotNull(result.EntityTag);
            var headers = controller.Response.Headers;
            Assert.Equal(("nosniff", "sandbox"), (headers.XContentTypeOptions.ToString(), headers.ContentSecurityPolicy.ToString()));
            Assert.StartsWith("private", headers.CacheControl.ToString());
        }
        finally
        {
            File.Delete(file);
        }
    }

    [Fact]
    public void ASourceWithoutAnIconAnswersNotFound()
    {
        var fixture = new Fixture();
        fixture.Register();

        Assert.IsType<NotFoundObjectResult>(fixture.Controller().GetSourceIcon(Source));
    }

    [Fact]
    public void AProviderIconIsItsOwnElseItsSources()
    {
        var fixture = new Fixture();
        var own = Path.GetTempFileName();
        var source = Path.GetTempFileName();
        try
        {
            File.WriteAllText(own, "<svg xmlns=\"http://www.w3.org/2000/svg\" id=\"own\"/>");
            File.WriteAllBytes(source, [0x89, 0x50, 0x4E, 0x47]);
            var withIcon = fixture.Register(icon: Icon(own, "image/svg+xml"));
            var withoutIcon = fixture.Register();
            fixture.Providers.Setup(p => p.GetSourceIcon(Source)).Returns(Icon(source, "image/png"));
            var controller = fixture.Controller();

            var ownResult = Assert.IsType<FileContentResult>(controller.GetProviderIcon(withIcon.ID));
            var sourceResult = Assert.IsType<FileContentResult>(fixture.Controller().GetProviderIcon(withoutIcon.ID));

            Assert.Equal(("image/svg+xml", "image/png"), (ownResult.ContentType, sourceResult.ContentType));
            Assert.Equal(File.ReadAllBytes(own), ownResult.FileContents);
            Assert.Equal(File.ReadAllBytes(source), sourceResult.FileContents);
            Assert.NotNull(ownResult.EntityTag);
            var headers = controller.Response.Headers;
            Assert.Equal(("nosniff", "sandbox"), (headers.XContentTypeOptions.ToString(), headers.ContentSecurityPolicy.ToString()));
            Assert.StartsWith("private", headers.CacheControl.ToString());
        }
        finally
        {
            File.Delete(own);
            File.Delete(source);
        }
    }

    [Fact]
    public void AProviderWithoutAnIconOrAnUnknownOneAnswersNotFound()
    {
        var fixture = new Fixture();
        var info = fixture.Register();

        Assert.IsType<NotFoundObjectResult>(fixture.Controller().GetProviderIcon(info.ID));
        Assert.IsType<NotFoundObjectResult>(fixture.Controller().GetProviderIcon(Guid.NewGuid()));
    }

    [Fact]
    public void AProviderIsSetUpAndRefusesKindsItCannotAnswer()
    {
        var fixture = new Fixture();
        var info = fixture.Register();

        var refused = fixture.Controller().UpdateProvider(info.ID, new() { EnabledEntityTypes = [MetadataEntityType.Movie] });
        var updated = fixture.Controller().UpdateProvider(info.ID, new()
        {
            EnabledEntityTypes = [MetadataEntityType.Series],
            IsAutoLinker = false,
            AutoLink = true,
            AutoLinkRestricted = false,
        });

        Assert.Equal(400, StatusOf(refused));
        Assert.Equal(200, StatusOf(updated));
        fixture.Providers.Verify(p => p.SetProviderEnabled(info.ID, It.Is<IReadOnlySet<MetadataEntityType>>(set => set.SetEquals(new[] { MetadataEntityType.Series }))), Times.Once);
        fixture.Providers.Verify(p => p.SetProviderEnabled(info.ID, It.Is<IReadOnlySet<MetadataEntityType>>(set => set.Contains(MetadataEntityType.Movie))), Times.Never);
        fixture.Providers.Verify(p => p.SetProviderAutoLinker(Source, null), Times.Once);
        fixture.Providers.Verify(p => p.SetProviderAutoLink(Source, true), Times.Once);
        fixture.Providers.Verify(p => p.SetProviderAutoLinkRestricted(Source, false), Times.Once);
        Assert.IsType<NotFoundObjectResult>(fixture.Controller().UpdateProvider(Guid.NewGuid(), new()).Result);
    }

    [Fact]
    public void AStatusTellsWhetherTheSourceIsPaused()
    {
        var fixture = new Fixture();

        var running = Value(fixture.Controller().GetStatus(Source));
        fixture.Pause(TimeSpan.FromMinutes(2));
        var paused = Value(fixture.Controller().GetStatus(Source));

        Assert.False(running.IsPaused);
        Assert.Null(running.RetryAfterSeconds);
        Assert.True(paused.IsPaused);
        Assert.Equal("Rate limited.", paused.Reason);
        Assert.InRange(paused.RetryAfterSeconds!.Value, 119, 120);
        Assert.True(running.IsConfigured);
        Assert.Null(running.NotConfiguredReason);
    }

    [Fact]
    public void AStatusTellsASourceNotConfiguredApartFromAPause()
    {
        var fixture = new Fixture { Configured = false, NotConfiguredReason = "No API key is set." };
        fixture.Register();

        var status = Value(fixture.Controller().GetStatus(Source));

        Assert.False(status.IsConfigured);
        Assert.Equal("No API key is set.", status.NotConfiguredReason);
        Assert.False(status.IsPaused);
        Assert.Null(status.Reason);
    }

    [Fact]
    public void AStatusIgnoresADisabledProviderNotConfigured()
    {
        var fixture = new Fixture { Configured = false };
        fixture.Register(enabled: false);

        Assert.True(Value(fixture.Controller().GetStatus(Source)).IsConfigured);
    }

    #endregion

    #region Provider Order

    /// <summary>
    /// A real provider manager over a folder of its own, booted with the
    /// first linker alone and then with the later one too.
    /// </summary>
    private sealed class Booted : IDisposable
    {
        private readonly string _path = Path.Join(Path.GetTempPath(), $"shoko-provider-order-tests-{Guid.NewGuid():N}");

        public Booted()
        {
            Directory.CreateDirectory(_path);
            TestProviderManagers.Boot(_path).AddParts([First]);
            Manager = TestProviderManagers.Boot(_path);
            Manager.AddParts([First, Later]);
        }

        public ProviderClaimTests.FirstLinker First { get; } = new();

        public ProviderClaimTests.LaterLinker Later { get; } = new();

        public MetadataProviderManager Manager { get; }

        public Guid FirstID => Manager.GetProviderInfo(First).ID;

        public Guid LaterID => Manager.GetProviderInfo(Later).ID;

        public void Dispose()
            => Directory.Delete(_path, recursive: true);
    }

    private static IEnumerable<(Guid, int, bool, bool)> Slots(MetadataProviderOrder order)
        => order.Providers.Select(provider => (provider.ProviderID, provider.Priority, provider.IsEnabled, provider.IsActive));

    [Fact]
    public void ASourcesProvidersAreListedPerKindInOrder()
    {
        using var booted = new Booted();

        var rows = Value(new Fixture().Controller(booted.Manager).GetProviderOrders(TestSources.Plugin));

        Assert.Equal(booted.Manager.GetProviderInfo(booted.First).AvailableEntityTypes.Order(), rows.Select(row => row.EntityType));
        Assert.All(rows, row => Assert.Equal([(booted.FirstID, 0, true, true), (booted.LaterID, 1, true, false)], Slots(row)));
        Assert.Equal(booted.Manager.GetProviderInfo(booted.First).PluginInfo.ID, rows[0].Providers[0].PluginID);
        Assert.Empty(Value(new Fixture().Controller(booted.Manager).GetProviderOrders(TestSources.AniList)));
    }

    [Fact]
    public void AnOrderSetPerSourceAgreesWithTheProviderRoute()
    {
        using var booted = new Booted();
        var controller = new Fixture().Controller(booted.Manager);

        var rows = Value(controller.UpdateProviderOrders(TestSources.Plugin, [
            new()
            {
                EntityType = MetadataEntityType.Series,
                Providers = [new() { ProviderID = booted.FirstID, IsEnabled = false, Priority = 1 }, new() { ProviderID = booted.LaterID, Priority = 0 }],
            },
        ]));

        var series = rows.Single(row => row.EntityType == MetadataEntityType.Series);
        Assert.Equal([(booted.LaterID, 0, true, true), (booted.FirstID, 1, false, false)], Slots(series));
        Assert.Equal([MetadataEntityType.Series], Value(controller.GetProvider(booted.LaterID)).EnabledEntityTypes);
        Assert.DoesNotContain(MetadataEntityType.Series, Value(controller.GetProvider(booted.FirstID)).EnabledEntityTypes);

        // The provider route moves it back to the front.
        controller.UpdateProvider(booted.FirstID, new() { EnabledEntityTypes = [.. booted.Manager.GetProviderInfo(booted.First).AvailableEntityTypes] });
        series = Value(controller.GetProviderOrders(TestSources.Plugin)).Single(row => row.EntityType == MetadataEntityType.Series);
        Assert.Equal([(booted.FirstID, 0, true, true), (booted.LaterID, 1, true, false)], Slots(series));

        // Turned off there, it no longer stands by for any kind.
        controller.UpdateProvider(booted.LaterID, new() { EnabledEntityTypes = [] });
        var later = Value(controller.GetProviderOrders(TestSources.Plugin)).Select(row => row.Providers.Single(provider => provider.ProviderID == booted.LaterID));
        Assert.All(later, provider => Assert.False(provider.IsEnabled));
    }

    [Fact]
    public void AnOrderForAKindNobodyAnswersOrNamingAStrangerOrTwiceIsRefused()
    {
        using var booted = new Booted();
        var controller = new Fixture().Controller(booted.Manager);
        var before = Value(controller.GetProviderOrders(TestSources.Plugin)).SelectMany(Slots).ToList();

        int Refused(List<MetadataProviderOrderBody> body)
            => StatusOf(controller.UpdateProviderOrders(TestSources.Plugin, body));

        Assert.Equal(400, Refused([new() { EntityType = MetadataEntityType.Movie, Providers = [new() { ProviderID = booted.FirstID }] }]));
        Assert.Equal(400, Refused([new() { EntityType = MetadataEntityType.Series, Providers = [new() { ProviderID = Guid.NewGuid() }] }]));
        Assert.Equal(400, Refused([
            new() { EntityType = MetadataEntityType.Series, Providers = [new() { ProviderID = booted.LaterID }] },
            new() { EntityType = MetadataEntityType.Series, Providers = [new() { ProviderID = booted.FirstID }] },
        ]));
        Assert.Equal(400, Refused([
            new()
            {
                EntityType = MetadataEntityType.Series,
                Providers = [new() { ProviderID = booted.LaterID }, new() { ProviderID = booted.LaterID, Priority = 1 }],
            },
        ]));

        Assert.Equal(before, Value(controller.GetProviderOrders(TestSources.Plugin)).SelectMany(Slots));
    }

    #endregion

    #region Search

    [Fact]
    public async Task ASearchAsksTheProviderAndTellsWhatIsStored()
    {
        var fixture = new Fixture();
        fixture.Register();
        fixture.Linking.Setup(l => l.SearchSeries(Source, It.IsAny<MetadataSearchOptions>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(([Fixture.Found("21", "Stored"), Fixture.Found("22", "Remote")], 40));
        fixture.Metadata.Setup(m => m.GetEntry(ID(MetadataEntityType.Series, "21"))).Returns(new FakeSeries("21", "Stored"));

        var result = Value(await fixture.Controller().Search(Source, "query", year: 2020, type: [AnimeType.TV], pageSize: 2, page: 3, cancellationToken: TestContext.Current.CancellationToken));

        Assert.Equal(40, result.Total);
        Assert.Equal([("21", true), ("22", false)], result.List.Select(found => (found.ID, found.IsLocal)));
        Assert.Equal(7.5, result.List[0].Rating!.Value);
        fixture.Linking.Verify(l => l.SearchSeries(Source, It.Is<MetadataSearchOptions>(options =>
            options.Query == "query" && options.Year == 2020 && options.Page == 3 && options.PageSize == 2 && options.Types!.Single() == AnimeType.TV), It.IsAny<CancellationToken>()), Times.Once);
        Assert.Equal(400, StatusOf(await fixture.Controller().Search(Source, "query", MetadataEntityType.Episode, cancellationToken: TestContext.Current.CancellationToken)));
    }

    [Fact]
    public async Task ASearchIsRefusedWhileTheProviderIsNotConfiguredEvenWhenItAlsoSaysPaused()
    {
        var fixture = new Fixture { Configured = false, NotConfiguredReason = "No API key is set." };
        fixture.Register();
        fixture.Pause(TimeSpan.FromMinutes(1));

        var series = await fixture.Controller().Search(Source, "query", cancellationToken: TestContext.Current.CancellationToken);
        var lookup = await fixture.Controller().LookupSeries(Source, "21", TestContext.Current.CancellationToken);

        Assert.Equal(503, StatusOf(series));
        Assert.Equal(503, StatusOf(lookup));
        fixture.Linking.Verify(l => l.SearchSeries(It.IsAny<MetadataSource>(), It.IsAny<MetadataSearchOptions>(), It.IsAny<CancellationToken>()), Times.Never);
        fixture.Linking.Verify(l => l.LookupSeries(It.IsAny<MetadataGuid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ASearchIsRefusedWhileTheSourceIsPaused()
    {
        var fixture = new Fixture();
        fixture.Register();
        fixture.Pause(TimeSpan.FromSeconds(10));

        var refused = await fixture.Controller().Search(Source, "query", cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal(503, StatusOf(refused));
        fixture.Linking.Verify(l => l.SearchSeries(It.IsAny<MetadataSource>(), It.IsAny<MetadataSearchOptions>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ALookupAnswersAStoredSeriesWithoutAskingTheProvider()
    {
        var fixture = new Fixture();
        fixture.Register(lookup: false);
        fixture.Pause(TimeSpan.FromMinutes(1));
        fixture.Metadata.Setup(m => m.GetEntry(ID(MetadataEntityType.Series, "21"))).Returns(new FakeSeries("21", "Stored Title") { YearlySeasons = [(2020, YearlySeason.Spring)] });

        var result = Value(await fixture.Controller().LookupSeries(Source, "21", TestContext.Current.CancellationToken));

        Assert.Equal("Stored Title", result.Title);
        Assert.True(result.IsLocal);
        Assert.Equal((YearlySeason.Spring, 2020), (result.Season!.Value, result.SeasonYear!.Value));
        fixture.Linking.Verify(l => l.LookupSeries(It.IsAny<MetadataGuid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ALookupIsRefusedWhenTheProviderCannotAnswerOrIsPaused()
    {
        var fixture = new Fixture();
        fixture.Register(lookup: false);
        var unsupported = await fixture.Controller().LookupSeries(Source, "21", TestContext.Current.CancellationToken);

        fixture.Registered.Clear();
        fixture.Register();
        fixture.Pause(TimeSpan.FromSeconds(10));
        var paused = await fixture.Controller().LookupSeries(Source, "21", TestContext.Current.CancellationToken);

        Assert.Equal(400, StatusOf(unsupported));
        Assert.Equal(503, StatusOf(paused));
        fixture.Linking.Verify(l => l.LookupSeries(It.IsAny<MetadataGuid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ASearchAndALookupJudgeTheProviderTheLinkingServiceAsks()
    {
        var fixture = new Fixture { Configured = false };
        fixture.Register(linksSeries: false);
        fixture.Configured = true;
        fixture.Register(lookup: false);
        fixture.Register();
        fixture.Linking.Setup(l => l.SearchSeries(Source, It.IsAny<MetadataSearchOptions>(), It.IsAny<CancellationToken>())).ReturnsAsync(([Fixture.Found("22", "Remote")], 1));

        var search = await fixture.Controller().Search(Source, "query", cancellationToken: TestContext.Current.CancellationToken);
        var lookup = await fixture.Controller().LookupSeries(Source, "21", TestContext.Current.CancellationToken);

        Assert.Equal(1, Value(search).Total);
        Assert.Equal(400, StatusOf(lookup));
        fixture.Linking.Verify(l => l.LookupSeries(It.IsAny<MetadataGuid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ALookupAsksTheProviderForWhatIsNotStored()
    {
        var fixture = new Fixture();
        fixture.Register();
        fixture.Linking.Setup(l => l.LookupSeries(ID(MetadataEntityType.Series, "22"), It.IsAny<CancellationToken>())).ReturnsAsync(Fixture.Found("22", "Remote"));

        var found = Value(await fixture.Controller().LookupSeries(Source, "22", TestContext.Current.CancellationToken));
        var missing = await fixture.Controller().LookupSeries(Source, "23", TestContext.Current.CancellationToken);

        Assert.Equal(("22", "Remote", false), (found.ID, found.Title, found.IsLocal));
        Assert.IsType<NotFoundObjectResult>(missing.Result);
    }

    [Fact]
    public async Task ABulkLookupKeepsTheOrderAndNamesWhatIsMissing()
    {
        var fixture = new Fixture();
        fixture.Register();
        fixture.Metadata.Setup(m => m.GetEntry(ID(MetadataEntityType.Series, "21"))).Returns(new FakeSeries("21", "Stored"));
        fixture.Linking.Setup(l => l.LookupSeries(ID(MetadataEntityType.Series, "22"), It.IsAny<CancellationToken>())).ReturnsAsync(Fixture.Found("22", "Remote"));

        var found = Value(await fixture.Controller().LookupSeriesInBulk(Source, new() { IDs = ["22", $"{Source.Value}://series/21", "22"] }, TestContext.Current.CancellationToken));
        var controller = fixture.Controller();
        var missing = await controller.LookupSeriesInBulk(Source, new() { IDs = ["21", "404"] }, TestContext.Current.CancellationToken);

        Assert.Equal(["22", "21", "22"], found.Select(result => result.ID));
        Assert.Equal(400, StatusOf(missing));
        Assert.Contains("'404'", Assert.Single(controller.ModelState["IDs"]!.Errors).ErrorMessage);
        fixture.Linking.Verify(l => l.LookupSeries(ID(MetadataEntityType.Series, "22"), It.IsAny<CancellationToken>()), Times.Once);
    }

    #endregion

    #region Cross-References

    [Fact]
    public async Task LinksAreExportedAndImportedForTheSource()
    {
        var fixture = new Fixture();
        fixture.Transfer.Setup(t => t.Export(Source, It.IsAny<MetadataCrossReferenceExportOptions?>())).Returns("AnidbAnimeId,SomeShowId,Rating\n1,21,UserVerified\n");
        string? imported = null;
        fixture.Transfer.Setup(t => t.Import(Source, It.IsAny<TextReader>(), It.IsAny<MetadataCrossReferenceImportOptions?>(), It.IsAny<CancellationToken>()))
            .Callback((MetadataSource _, TextReader reader, MetadataCrossReferenceImportOptions? _, CancellationToken _) => imported = reader.ReadToEnd())
            .ReturnsAsync(new MetadataCrossReferenceImportResult { LinkCount = 1, SeriesAdded = 1 });

        var export = Assert.IsType<FileContentResult>(fixture.Controller().ExportCrossReferences(Source, new() { Sections = [MetadataCrossReferenceSection.Series], AnidbAnimeID = 1 }));
        var bytes = export.FileContents;
        var file = new FormFile(new MemoryStream(bytes), 0, bytes.Length, "file", "links.csv");
        var summary = Value(await fixture.Controller().ImportCrossReferences(Source, file, removeExisting: false, cancellationToken: TestContext.Current.CancellationToken));

        Assert.Equal("text/csv", export.ContentType);
        Assert.Equal(Encoding.UTF8.GetString(bytes), imported);
        Assert.Equal((1, 1), (summary.LinkCount, summary.SeriesAdded));
        fixture.Transfer.Verify(t => t.Export(Source, It.Is<MetadataCrossReferenceExportOptions?>(options =>
            options!.Sections == MetadataCrossReferenceSections.Series && options.AnidbAnimeID == 1 && options.Automatic == null)), Times.Once);
        fixture.Transfer.Verify(t => t.Import(Source, It.IsAny<TextReader>(), It.Is<MetadataCrossReferenceImportOptions?>(options => !options!.RemoveExisting && options.AddMissingSeries),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task AniDBHasNoLinksToExportOrImport()
    {
        var fixture = new Fixture();

        Assert.IsType<ObjectResult>(fixture.Controller().ExportCrossReferences(MetadataSource.AniDB), exactMatch: false);
        Assert.Equal(400, StatusOf(await fixture.Controller().ImportCrossReferences(MetadataSource.AniDB, null!, cancellationToken: TestContext.Current.CancellationToken)));
        fixture.Transfer.VerifyNoOtherCalls();
    }

    #endregion

    #region Actions

    [Fact]
    public void ActionsAreAcceptedForASourceWithAProviderOnly()
    {
        var fixture = new Fixture();
        var controller = fixture.Controller();

        Assert.Equal(400, Assert.IsType<ObjectResult>(controller.RefreshAllLinked(Source), exactMatch: false).StatusCode);
        fixture.Register();
        Assert.IsType<AcceptedResult>(fixture.Controller().RefreshAllLinked(Source, MetadataEntityType.Series));
        Assert.Equal(400, Assert.IsType<ObjectResult>(fixture.Controller().RefreshAllLinked(Source, MetadataEntityType.Episode), exactMatch: false).StatusCode);
        Assert.Equal(400, Assert.IsType<ObjectResult>(fixture.Controller().RemoveAllLinks(MetadataSource.AniDB), exactMatch: false).StatusCode);
        Assert.IsType<AcceptedResult>(fixture.Controller().AutoSearchAll(Source));
        Assert.IsType<AcceptedResult>(fixture.Controller().PurgeUnused(Source));
    }

    [Fact]
    public void SearchingEveryAnimeIsRefusedWhileTheAutoLinkerIsNotConfigured()
    {
        var fixture = new Fixture { Configured = false };
        fixture.Register();

        var refused = Assert.IsType<ObjectResult>(fixture.Controller().AutoSearchAll(Source), exactMatch: false);

        Assert.Equal(503, refused.StatusCode);
        fixture.Refresh.Verify(r => r.AutoSearchAll(
            It.IsAny<MetadataSource>(),
            It.IsAny<bool>(),
            It.IsAny<IProgress<decimal>?>(),
            It.IsAny<CancellationToken>()
        ), Times.Never);
    }

    [Fact]
    public async Task RemovingEveryLinkResetsTheAutoLinkingStateOnlyWhenAsked()
    {
        var fixture = new Fixture();
        var actions = fixture.Actions;

        await actions.RemoveAllLinks(Source, true, false, false, false, TestContext.Current.CancellationToken);
        await actions.RemoveAllLinks(Source, false, false, false, null, TestContext.Current.CancellationToken);

        fixture.Linking.Verify(l => l.RemoveAllLinks(Source, true, false, false, It.IsAny<IProgress<decimal>?>(), It.IsAny<CancellationToken>()), Times.Once);
        fixture.Linking.Verify(l => l.RemoveAllLinks(
            It.IsAny<MetadataSource>(),
            It.IsAny<bool>(),
            It.IsAny<bool>(),
            It.IsAny<bool>(),
            It.IsAny<IProgress<decimal>?>(),
            It.IsAny<CancellationToken>()
        ), Times.Once);
        fixture.Linking.Verify(l => l.ResetAutoLinkingState(Source, false), Times.Once);
        fixture.Linking.Verify(l => l.ResetAutoLinkingState(It.IsAny<MetadataSource>(), It.IsAny<bool>()), Times.Once);
    }

    [Fact]
    public async Task AFailedActionIsLoggedAndNotThrown()
    {
        var fixture = new Fixture();
        fixture.Refresh.Setup(r => r.AutoSearchAll(
            Source,
            false,
            It.IsAny<IProgress<decimal>?>(),
            It.IsAny<CancellationToken>()
        )).ThrowsAsync(new InvalidOperationException("Broken."));
        var actions = fixture.Actions;

        await actions.Start("Searching", () => actions.AutoSearchAll(Source, false));

        fixture.Refresh.Verify(r => r.AutoSearchAll(Source, false, It.IsAny<IProgress<decimal>?>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    #endregion
}
