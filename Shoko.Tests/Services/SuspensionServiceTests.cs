using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Shoko.Abstractions.Connectivity.Services;
using Shoko.Abstractions.Connectivity.Suspensions;
using Shoko.Abstractions.Metadata;
using Shoko.Abstractions.Metadata.Providers;
using Shoko.Abstractions.Metadata.Services;
using Shoko.Abstractions.Plugin;
using Shoko.Server.Scheduling.Acquisition.Filters;
using Shoko.Server.Scheduling.Jobs.Metadata;
using Shoko.Server.Services;
using Shoko.Tests.Actors;
using Shoko.Tests.Infrastructure;
using Shoko.Tests.Plugin.Tmdb;
using Shoko.Tests.Scheduling.Jobs.Metadata;
using Xunit;

namespace Shoko.Tests.Services;

/// <summary>
/// Covers the suspension service: what a re-report keeps, expiry, lifting,
/// an unknown provider, the held jobs and the statuses of a source.
/// </summary>
public class SuspensionServiceTests
{
    #region Fixture

    /// <summary>
    /// A provider holding back the fake metadata provider, lifting by
    /// counting.
    /// </summary>
    public sealed class HoldingProvider : ISuspensionProvider
    {
        public List<SuspensionKind> Lifted { get; } = [];

        public string Name => "Holding";

        public string? Description => null;

        public IReadOnlyList<Type> HeldProviderTypes => [typeof(MetadataProviderJobTests.FakeProvider)];

        public Task Lift(SuspensionKind kind, CancellationToken token)
        {
            Lifted.Add(kind);
            return Task.CompletedTask;
        }
    }

    /// <summary>
    /// A provider never registered.
    /// </summary>
    public sealed class UnknownProvider : ISuspensionProvider
    {
        public string Name => "Unknown";

        public string? Description => null;

        public IReadOnlyList<Type> HeldProviderTypes => [];

        public Task Lift(SuspensionKind kind, CancellationToken token) => Task.CompletedTask;
    }

    private readonly ManualTimeProvider _clock = new();

    private readonly HoldingProvider _provider = new();

    private DateTime Now => _clock.GetUtcNow().UtcDateTime;

    private static IPluginManager Plugins()
    {
        var plugins = new Mock<IPluginManager>();
        plugins.Setup(p => p.GetPluginInfo(typeof(SuspensionServiceTests).Assembly))
            .Returns(PluginTestDoubles.InstalledPluginInfo(typeof(SuspensionServiceTests), Guid.NewGuid()));
        return plugins.Object;
    }

    private static IMetadataProviderManager MetadataProviders()
    {
        var providers = new Mock<IMetadataProviderManager>();
        providers.SetupGet(p => p.MetadataProviders).Returns([
            new MetadataProviderInfo
            {
                ID = Guid.NewGuid(),
                Version = new(1, 0),
                Name = "Fake",
                Description = string.Empty,
                Provider = new MetadataProviderJobTests.FakeProvider(),
                ConfigurationInfo = null,
                PluginInfo = PluginTestDoubles.InstalledPluginInfo(typeof(SuspensionServiceTests), Guid.NewGuid()),
                SupportsSeries = true,
                SupportsMovies = false,
                SupportsCollections = false,
                SupportsAutoLinking = false,
                Source = TestSources.Plugin,
                AvailableEntityTypes = new HashSet<MetadataEntityType>(),
                EnabledEntityTypes = new HashSet<MetadataEntityType>(),
            },
        ]);
        return providers.Object;
    }

    private SuspensionService Service()
    {
        var service = new SuspensionService(Plugins(), MetadataProviders(), NullLogger<SuspensionService>.Instance, _clock);
        service.AddParts([_provider]);
        return service;
    }

    private static List<SuspensionChangedEventArgs> Changes(SuspensionService service)
    {
        var changes = new List<SuspensionChangedEventArgs>();
        service.SuspensionChanged += (_, e) => changes.Add(e);
        return changes;
    }

    #endregion

    [Fact]
    public void AReReportUpdatesTheSuspensionButKeepsWhenItWasRaised()
    {
        using var service = Service();
        var reporter = new SuspensionReporter<HoldingProvider>(service);
        var changes = Changes(service);

        reporter.Suspend(SuspensionKind.RateLimited, resumesAt: Now.AddMinutes(1));
        var raisedAt = Assert.Single(reporter.Current.Suspensions).RaisedAt;
        _clock.Advance(TimeSpan.FromSeconds(10));
        reporter.Suspend(SuspensionKind.RateLimited, "Slow down.", Now.AddMinutes(5));
        reporter.Suspend(SuspensionKind.RateLimited, "Slow down.", Now.AddMinutes(5));

        var suspension = Assert.Single(reporter.Current.Suspensions);
        Assert.Equal(raisedAt, suspension.RaisedAt);
        Assert.Equal("Slow down.", suspension.Reason);
        Assert.Equal(Now.AddMinutes(5), reporter.Current.ResumesAt);
        Assert.Equal(2, changes.Count);
        Assert.Single(changes[0].Raised);
        Assert.Single(changes[1].Updated);
    }

    [Fact]
    public void ASuspensionEndingInThePastIsIgnored()
    {
        using var service = Service();
        var reporter = new SuspensionReporter<HoldingProvider>(service);

        reporter.Suspend(SuspensionKind.ServerErrors, resumesAt: Now.AddSeconds(-1));

        Assert.False(reporter.Current.IsSuspended);
    }

    [Fact]
    public void ASuspensionIsClearedOnceItsEndPasses()
    {
        using var service = Service();
        var reporter = new SuspensionReporter<HoldingProvider>(service);
        var changes = Changes(service);
        reporter.Suspend(SuspensionKind.RateLimited, resumesAt: Now.AddMinutes(1));
        reporter.Suspend(SuspensionKind.AuthenticationFailed);

        _clock.Advance(TimeSpan.FromMinutes(2));

        Assert.Equal(SuspensionKind.AuthenticationFailed, Assert.Single(reporter.Current.Suspensions).Kind);
        Assert.Null(reporter.Current.ResumesAt);
        var removal = Assert.Single(changes.Last().Removed);
        Assert.Equal((SuspensionKind.RateLimited, SuspensionRemovalCause.Expired), (removal.Suspension.Kind, removal.Cause));
    }

    [Fact]
    public async Task OnlyALiftableSuspensionIsLifted()
    {
        using var service = Service();
        var reporter = new SuspensionReporter<HoldingProvider>(service);
        var providerID = service.GetProviderInfo(typeof(HoldingProvider)).ID;
        var changes = Changes(service);
        reporter.Suspend(SuspensionKind.Banned, resumesAt: Now.AddHours(1), isLiftable: true);
        reporter.Suspend(SuspensionKind.Overloaded, resumesAt: Now.AddHours(1));

        Assert.False(await service.Lift(providerID, SuspensionKind.Overloaded, TestContext.Current.CancellationToken));
        Assert.False(await service.Lift(Guid.NewGuid(), SuspensionKind.Banned, TestContext.Current.CancellationToken));
        var admin = ActorContextTests.Token();
        using (ActorContext.Begin(admin))
            Assert.True(await service.Lift(providerID, SuspensionKind.Banned, TestContext.Current.CancellationToken));

        Assert.Equal([SuspensionKind.Banned], _provider.Lifted);
        Assert.Equal(SuspensionKind.Overloaded, Assert.Single(reporter.Current.Suspensions).Kind);
        Assert.Equal(SuspensionRemovalCause.Lifted, Assert.Single(changes.Last().Removed).Cause);
        Assert.Same(admin, changes.Last().Actor);
        Assert.Null(changes.First().Actor);
    }

    [Fact]
    public void ReportingForAProviderThatIsNotLoadedThrows()
    {
        using var service = Service();
        var reporter = new SuspensionReporter<UnknownProvider>(service);

        Assert.Throws<InvalidOperationException>(() => reporter.Suspend(SuspensionKind.Other));
        Assert.Throws<InvalidOperationException>(() => new SuspensionReporter<HoldingProvider>(
            new SuspensionService(Plugins(), MetadataProviders(), NullLogger<SuspensionService>.Instance, _clock)
        ).Current);
    }

    [Fact]
    public void ASuspendedProviderHoldsBackOnlyTheJobsOfItsHeldProviders()
    {
        using var service = Service();
        var reporter = new SuspensionReporter<HoldingProvider>(service);
        using var filter = new SuspensionAcquisitionFilter(service, [
            typeof(RefreshMetadataJob<MetadataProviderJobTests.FakeProvider>),
            typeof(SearchMetadataJob<MetadataProviderJobTests.FakeProvider>),
            typeof(RefreshMetadataJob<MetadataProviderJobTests.SeriesOnlyProvider>),
            typeof(PurgeMetadataJob),
        ]);
        var changes = 0;
        filter.StateChanged += (_, _) => changes++;

        reporter.Suspend(SuspensionKind.RateLimited);
        reporter.Suspend(SuspensionKind.ServerErrors);

        Assert.Equal(1, changes);
        Assert.Equal(
            [typeof(RefreshMetadataJob<MetadataProviderJobTests.FakeProvider>), typeof(SearchMetadataJob<MetadataProviderJobTests.FakeProvider>)],
            filter.GetTypesToExclude()
        );

        reporter.ResumeAll();

        Assert.Empty(filter.GetTypesToExclude());
    }

    [Fact]
    public void ASourceIsHeldByTheProvidersHoldingItsMetadataProviders()
    {
        using var service = Service();
        new SuspensionReporter<HoldingProvider>(service).Suspend(SuspensionKind.Maintenance);

        Assert.True(Assert.Single(service.GetForSource(TestSources.Plugin)).IsSuspended);
        Assert.Empty(service.GetForSource(TestSources.AniList));
    }

    [Fact]
    public void AReporterResolvedFromTheContainerReportsIntoTheService()
    {
        var services = new ServiceCollection()
            .AddLogging()
            .AddSingleton(Plugins())
            .AddSingleton(MetadataProviders())
            .AddSingleton<SuspensionService>()
            .AddSingleton<ISuspensionService>(provider => provider.GetRequiredService<SuspensionService>())
            .AddSingleton(typeof(ISuspensionReporter<>), typeof(SuspensionReporter<>));
        using var container = services.BuildServiceProvider();
        container.GetRequiredService<SuspensionService>().AddParts([_provider]);

        container.GetRequiredService<ISuspensionReporter<HoldingProvider>>().Suspend(SuspensionKind.Banned);

        Assert.True(Assert.Single(container.GetRequiredService<ISuspensionService>().GetAll()).IsSuspended);
    }
}
