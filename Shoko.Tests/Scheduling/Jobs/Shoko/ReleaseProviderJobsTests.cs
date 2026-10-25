using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Shoko.Abstractions.Connectivity.Suspensions.Attributes;
using Shoko.Abstractions.Video.Release;
using Shoko.Abstractions.Video.Services;
using Shoko.Server.Providers.AniDB.Release;
using Shoko.Server.Scheduling.Concurrency;
using Shoko.Server.Scheduling.Jobs;
using Shoko.Server.Scheduling.Jobs.Metadata;
using Shoko.Server.Scheduling.Jobs.Shoko;
using Shoko.Tests.Scheduling.Jobs.Metadata;
using Xunit;

namespace Shoko.Tests.Scheduling.Jobs.Shoko;

/// <summary>
/// Covers which job a release search chain queues for each release provider,
/// and which provider each provider job is held back for.
/// </summary>
public class ReleaseProviderJobsTests
{
    #region Fixture

    /// <summary>
    /// A plugin's release provider without a job class of its own.
    /// </summary>
    public sealed class FakeReleaseProvider : IReleaseInfoProvider
    {
        public string Name => "Fake";

        public Task<ReleaseInfo?> GetReleaseInfoForVideo(ReleaseInfoContext context, CancellationToken cancellationToken)
            => Task.FromResult<ReleaseInfo?>(null);

        public Task<ReleaseInfo?> GetReleaseInfoById(string releaseId, CancellationToken cancellationToken)
            => Task.FromResult<ReleaseInfo?>(null);
    }

    /// <summary>
    /// A release provider limiting its jobs to one at a time.
    /// </summary>
    public sealed class LimitedReleaseProvider : IReleaseInfoProvider
    {
        public string Name => "Limited";

        public int? MaxConcurrentJobs => 1;

        public Task<ReleaseInfo?> GetReleaseInfoForVideo(ReleaseInfoContext context, CancellationToken cancellationToken)
            => Task.FromResult<ReleaseInfo?>(null);

        public Task<ReleaseInfo?> GetReleaseInfoById(string releaseId, CancellationToken cancellationToken)
            => Task.FromResult<ReleaseInfo?>(null);
    }

    private static ReleaseProviderInfo Info(IReleaseInfoProvider provider) => new()
    {
        ID = Guid.NewGuid(),
        Version = new(1, 0),
        Name = provider.Name,
        Description = string.Empty,
        Provider = provider,
        ConfigurationInfo = null,
        PluginInfo = null!,
        Enabled = true,
        Priority = 0,
    };

    #endregion

    #region Tests

    [Fact]
    public void AProviderWithoutAJobOfItsOwnRunsThroughTheGenericJob()
    {
        Type[] found = [typeof(FakeReleaseProvider), typeof(AnidbReleaseProvider), typeof(AnidbProcessFileJob)];
        var registered = ReleaseProviderJobs.GetJobTypes(found);

        Assert.Equal([typeof(ProcessReleaseProviderJob<FakeReleaseProvider>)], registered);

        var jobTypes = ReleaseProviderJobs.GetJobTypesByProvider([typeof(AnidbProcessFileJob), .. registered]);

        Assert.Equal(typeof(ProcessReleaseProviderJob<FakeReleaseProvider>), jobTypes[typeof(FakeReleaseProvider)]);
        Assert.Equal(typeof(AnidbProcessFileJob), jobTypes[typeof(AnidbReleaseProvider)]);
    }

    [Fact]
    public void EachProviderJobMapsToItsProviderForTheSuspensionFilter()
    {
        Assert.Equal(typeof(MetadataProviderJobTests.FakeProvider), ProviderJobs.GetProviderType(typeof(RefreshMetadataJob<MetadataProviderJobTests.FakeProvider>)));
        Assert.Equal(typeof(FakeReleaseProvider), ProviderJobs.GetProviderType(typeof(ProcessReleaseProviderJob<FakeReleaseProvider>)));
        Assert.Equal(typeof(AnidbReleaseProvider), ProviderJobs.GetProviderType(typeof(AnidbProcessFileJob)));
        Assert.Null(ProviderJobs.GetProviderType(typeof(PurgeMetadataJob)));

        // The filter attaches to a pool through the attribute, which the subclass inherits.
        Assert.Contains(typeof(AnidbProcessFileJob).GetCustomAttributes(inherit: true), attribute => attribute is ProviderJobAttribute);
    }

    [Fact]
    public void EachProviderJobFollowsTheLimitItsProviderDeclared()
    {
        var releaseService = new Mock<IVideoReleaseService>();
        releaseService.Setup(service => service.GetAvailableProviders(false))
            .Returns([Info(new LimitedReleaseProvider()), Info(new FakeReleaseProvider())]);
        var services = new ServiceCollection();
        services.AddSingleton(releaseService.Object);
        var concurrency = new ReleaseProviderJobConcurrency(services.BuildServiceProvider());

        Assert.Equal(1, concurrency.GetConcurrencyLimit(typeof(ProcessReleaseProviderJob<LimitedReleaseProvider>)));
        Assert.Null(concurrency.GetConcurrencyLimit(typeof(ProcessReleaseProviderJob<FakeReleaseProvider>)));
        Assert.Null(concurrency.GetConcurrencyLimit(typeof(PurgeMetadataJob)));
    }

    #endregion
}
