using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Shoko.Abstractions.Metadata;
using Shoko.Abstractions.Metadata.Airing;
using Shoko.Abstractions.Metadata.Services;
using Shoko.Server.Scheduling.Concurrency;
using Shoko.Server.Scheduling.Jobs;
using Shoko.Server.Scheduling.Jobs.Airing;
using Shoko.Server.Scheduling.Jobs.Metadata;
using Shoko.Tests.Scheduling.Jobs.Metadata;
using Xunit;

namespace Shoko.Tests.Scheduling.Jobs.Airing;

/// <summary>
/// Covers which closed job types an airing schedule provider gets, and that
/// each maps back to its provider.
/// </summary>
public class AiringScheduleProviderJobsTests
{
    #region Fixtures

    /// <summary>
    /// A provider that only refreshes.
    /// </summary>
    public sealed class RefreshingProvider : IAiringScheduleProvider
    {
        public string Name => "Refreshing";

        public IReadOnlySet<AiringKind> AvailableKinds { get; } = new HashSet<AiringKind> { AiringKind.Original };

        public Task<bool> RefreshAsync(ISeries series, CancellationToken cancellationToken = default)
            => Task.FromResult(false);
    }

    /// <summary>
    /// A provider that refreshes and sweeps.
    /// </summary>
    public sealed class SweepingProvider : ISweepingAiringScheduleProvider
    {
        public string Name => "Sweeping";

        public IReadOnlySet<AiringKind> AvailableKinds { get; } = new HashSet<AiringKind> { AiringKind.Original };

        public Task<bool> RefreshAsync(ISeries series, CancellationToken cancellationToken = default)
            => Task.FromResult(false);

        public Task<string?> SweepAsync(string? cursor, CancellationToken cancellationToken)
            => Task.FromResult<string?>(null);

        public int? MaxConcurrentJobs => 3;
    }

    private static AiringScheduleProviderInfo Info(IAiringScheduleProvider provider) => new()
    {
        ID = Guid.NewGuid(),
        Version = new(1, 0),
        Name = provider.Name,
        Description = string.Empty,
        Provider = provider,
        ConfigurationInfo = null,
        PluginInfo = null!,
        Priority = 0,
        EnabledKinds = [AiringKind.Original],
        SweepInterval = TimeSpan.FromHours(1),
    };

    #endregion

    #region Types

    [Fact]
    public void ASweepingProviderGetsASweepJobBesideItsRefreshJob()
    {
        Assert.Equal([typeof(RefreshAiringScheduleJob<RefreshingProvider>)], AiringScheduleProviderJobs.GetJobTypes(typeof(RefreshingProvider)));
        Assert.Equal(
            [typeof(RefreshAiringScheduleJob<SweepingProvider>), typeof(SweepAiringScheduleProviderJob<SweepingProvider>)],
            AiringScheduleProviderJobs.GetJobTypes(typeof(SweepingProvider))
        );
        Assert.Null(AiringScheduleProviderJobs.GetSweepJobType(typeof(RefreshingProvider)));
    }

    [Fact]
    public void ATypeThatIsNotAnAiringScheduleProviderGetsNoJobs()
    {
        Assert.Empty(AiringScheduleProviderJobs.GetJobTypes(typeof(MetadataProviderJobTests.FakeProvider)));
        Assert.Empty(AiringScheduleProviderJobs.GetJobTypes(typeof(IAiringScheduleProvider)));
    }

    [Fact]
    public void EachJobMapsBackToItsProvider()
    {
        Assert.Equal(typeof(SweepingProvider), ProviderJobs.GetProviderType(typeof(SweepAiringScheduleProviderJob<SweepingProvider>)));
        Assert.Equal(typeof(RefreshingProvider), AiringScheduleProviderJobs.GetProviderType(typeof(RefreshAiringScheduleJob<RefreshingProvider>)));
        Assert.Null(AiringScheduleProviderJobs.GetProviderType(typeof(RefreshMetadataJob<MetadataProviderJobTests.FakeProvider>)));
        Assert.Null(MetadataProviderJobs.GetProviderType(typeof(RefreshAiringScheduleJob<RefreshingProvider>)));
    }

    [Fact]
    public void EachRefreshJobFollowsTheLimitItsProviderDeclared()
    {
        var airingScheduleService = new Mock<IAiringScheduleService>();
        airingScheduleService.Setup(service => service.GetAvailableProviders(false))
            .Returns([Info(new RefreshingProvider()), Info(new SweepingProvider())]);
        var services = new ServiceCollection();
        services.AddSingleton(airingScheduleService.Object);
        var concurrency = new AiringScheduleProviderJobConcurrency(services.BuildServiceProvider());

        Assert.Equal(1, concurrency.GetConcurrencyLimit(typeof(RefreshAiringScheduleJob<RefreshingProvider>)));
        Assert.Equal(3, concurrency.GetConcurrencyLimit(typeof(RefreshAiringScheduleJob<SweepingProvider>)));
        Assert.Null(concurrency.GetConcurrencyLimit(typeof(SweepAiringScheduleProviderJob<SweepingProvider>)));
    }

    #endregion
}
