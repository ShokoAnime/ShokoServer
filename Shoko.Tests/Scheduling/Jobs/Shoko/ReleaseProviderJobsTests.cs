using System;
using System.Threading;
using System.Threading.Tasks;
using Shoko.Abstractions.Connectivity.Suspensions.Attributes;
using Shoko.Abstractions.Video.Release;
using Shoko.Server.Providers.AniDB.Release;
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

    #endregion
}
