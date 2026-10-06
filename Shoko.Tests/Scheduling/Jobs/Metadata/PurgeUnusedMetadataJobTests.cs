using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Shoko.Abstractions.Metadata;
using Shoko.Abstractions.Metadata.CrossReferences;
using Shoko.Abstractions.Metadata.Providers;
using Shoko.Abstractions.Metadata.Services;
using Shoko.Abstractions.Metadata.Storage;
using Shoko.QueueProcessor.Abstractions;
using Shoko.QueueProcessor.Workers;
using Shoko.Server.Scheduling.Jobs.Metadata;
using Shoko.Server.Services;
using Shoko.Server.Settings;
using Shoko.Tests.Infrastructure;
using Xunit;

namespace Shoko.Tests.Scheduling.Jobs.Metadata;

/// <summary>
/// Covers the daily purge of unused entries: which sources it purges, the
/// cutoff it passes, the setting that turns it off, and that a linked entry is
/// never among them.
/// </summary>
public class PurgeUnusedMetadataJobTests
{
    #region Helpers

    private static MetadataProviderInfo Info(MetadataSource source)
        => new()
        {
            ID = Guid.NewGuid(),
            Version = new(1, 0),
            Name = source.Value,
            Description = string.Empty,
            Provider = null!,
            ConfigurationInfo = null,
            PluginInfo = null!,
            SupportsSeries = true,
            SupportsMovies = true,
            SupportsCollections = true,
            SupportsAutoLinking = false,
            Source = source,
            AvailableEntityTypes = new HashSet<MetadataEntityType> { MetadataEntityType.Series },
            EnabledEntityTypes = new HashSet<MetadataEntityType> { MetadataEntityType.Series },
        };

    private static IMetadataProviderManager Manager(params MetadataProviderInfo[] providers)
        => Mock.Of<IMetadataProviderManager>(manager => manager.MetadataProviders == providers);

    private static PurgeUnusedMetadataJob MakeJob(int days, out Mock<IMetadataPurgeService> purge, params MetadataProviderInfo[] providers)
    {
        purge = new Mock<IMetadataPurgeService>();
        purge.Setup(service => service.PurgeUnused(
            It.IsAny<MetadataSource>(),
            It.IsAny<DateTime?>(),
            It.IsAny<MetadataEntityType?>(),
            It.IsAny<IProgress<decimal>?>(),
            It.IsAny<CancellationToken>()
        ))
            .ReturnsAsync(0);
        return MakeJob(days, Manager(providers), purge.Object);
    }

    private static PurgeUnusedMetadataJob MakeJob(int days, IMetadataProviderManager manager, IMetadataPurgeService purge)
    {
        var settings = new ServerSettings { Metadata = new MetadataSettings { AutoPurgeUnlinkedAfterDays = days } };
        var settingsProvider = new Mock<ISettingsProvider>();
        settingsProvider.Setup(provider => provider.GetSettings()).Returns(settings);

        var cancellation = new Mock<IJobCancellationAccessor>();
        cancellation.SetupGet(accessor => accessor.Token).Returns(CancellationToken.None);

        var services = new Mock<IServiceProvider>();
        services.Setup(provider => provider.GetService(typeof(ILoggerFactory))).Returns(NullLoggerFactory.Instance);

        var job = new PurgeUnusedMetadataJob(settingsProvider.Object, manager, purge, cancellation.Object, Mock.Of<IJobProgressAccessor>());
        job.Setup(services.Object);
        return job;
    }

    #endregion

    #region Execution

    [Fact]
    public async Task AZeroSetting_PurgesNothing()
    {
        var job = MakeJob(0, out var purge, Info(MetadataSource.TMDB));

        await job.Execute();

        purge.Verify(
            service => service.PurgeUnused(
                It.IsAny<MetadataSource>(),
                It.IsAny<DateTime?>(),
                It.IsAny<MetadataEntityType?>(),
                It.IsAny<IProgress<decimal>?>(),
                It.IsAny<CancellationToken>()
            ),
            Times.Never
        );
    }

    [Fact]
    public async Task APurgeableSource_IsPurgedWholeByTheCutoff()
    {
        var before = DateTime.Now.AddDays(-14);
        var job = MakeJob(14, out var purge, Info(MetadataSource.TMDB));

        await job.Execute();

        purge.Verify(
            service => service.PurgeUnused(
                TestSources.Plugin,
                It.Is<DateTime?>(cutoff => cutoff.HasValue && cutoff.Value >= before && cutoff.Value <= DateTime.Now.AddDays(-14)),
                null,
                It.IsAny<IProgress<decimal>?>(),
                It.IsAny<CancellationToken>()
            ),
            Times.Once
        );
    }

    [Fact]
    public async Task ALinkedEntry_IsNotPurged_WhileAnUnlinkedOneIs()
    {
        var linked = new MetadataGuid(TestSources.Plugin, MetadataEntityType.Series, "linked");
        var unlinked = new MetadataGuid(TestSources.Plugin, MetadataEntityType.Series, "unlinked");
        var metadata = new Mock<IMetadataService>();
        metadata.Setup(service => service.GetAllSeriesForSource(It.IsAny<MetadataSource>()))
            .Returns((MetadataSource source) => source == TestSources.Plugin ? [Mock.Of<ISeries>(series => series.ID == linked), Mock.Of<ISeries>(series => series.ID == unlinked)] : []);
        metadata.Setup(service => service.GetAllMoviesForSource(It.IsAny<MetadataSource>())).Returns([]);
        var links = new Mock<IMetadataCrossReferenceStore>();
        links.Setup(store => store.GetLinksTo(It.IsAny<MetadataGuid>()))
            .Returns((MetadataGuid id) => id == linked ? [Mock.Of<IMetadataCrossReference>()] : []);
        links.Setup(store => store.GetEpisodeLinksInto(It.IsAny<MetadataGuid>())).Returns([]);
        var queued = new List<string>();
        var queue = new Mock<IQueueScheduler>();
        queue.Setup(scheduler => scheduler.Enqueue(It.IsAny<Action<PurgeMetadataJob>?>(), It.IsAny<bool>(), It.IsAny<DateTimeOffset?>(), It.IsAny<CancellationToken>()))
            .Returns((Action<PurgeMetadataJob>? configure, bool _, DateTimeOffset? _, CancellationToken _) =>
            {
                var job = (PurgeMetadataJob)RuntimeHelpers.GetUninitializedObject(typeof(PurgeMetadataJob));
                configure?.Invoke(job);
                queued.Add(job.EntryID);
                return Task.CompletedTask;
            });
        var manager = Manager();
        var scheduler = new MetadataProviderScheduler(
            manager,
            links.Object,
            Mock.Of<IMetadataRefreshState>(),
            queue.Object,
            Mock.Of<IJobFactory>(),
            NullLogger<MetadataProviderScheduler>.Instance
        );

        // The unused purge reads none of the orphan purge's parts.
        var purge = new MetadataPurgeService(
            links.Object,
            metadata.Object,
            null!,
            Mock.Of<IMetadataCollectionStore>(store => store.GetAllCollections(It.IsAny<MetadataSource>()) == Array.Empty<ICollection>()),
            null!,
            null!,
            null!,
            null!,
            null!,
            null!,
            null!,
            null!,
            null!,
            null!,
            null!,
            null!,
            null!,
            scheduler,
            null!,
            NullLogger<MetadataPurgeService>.Instance
        );

        await MakeJob(14, manager, purge).Execute();

        Assert.Equal([unlinked.ToString()], queued);
    }

    #endregion

    #region Sources

    [Fact]
    public void PluginSourcesAndTmdbArePurgeable_AndACoreSourceOnlyWhileAProviderClaimsIt()
    {
        MetadataSource[] registered = [MetadataSource.Shoko, MetadataSource.User, MetadataSource.AniDB, MetadataSource.TMDB, TestSources.Plugin];

        Assert.Equal([MetadataSource.AniDB, MetadataSource.TMDB, TestSources.Plugin], PurgeUnusedMetadataJob.GetPurgeableSources(registered, [Info(MetadataSource.AniDB)]));
        Assert.Equal([MetadataSource.TMDB, TestSources.Plugin], PurgeUnusedMetadataJob.GetPurgeableSources(registered, []));
    }

    #endregion

    #region Days

    private static Func<string, string?> Variables(string? current, string? legacy)
        => name => name switch
        {
            PurgeUnusedMetadataJob.EnvironmentVariable => current,
            PurgeUnusedMetadataJob.LegacyEnvironmentVariable => legacy,
            _ => null,
        };

    [Theory]
    [InlineData(14, null, null, 14, false)]
    [InlineData(14, null, "0", 0, true)]
    [InlineData(14, "", "\"30\"", 30, true)]
    [InlineData(21, "21", "0", 21, false)]
    [InlineData(14, null, "never", 14, false)]
    public void TheLegacyVariable_IsHonouredOnlyWhileTheCurrentOneIsUnset_AndReadable(int setting, string? current, string? legacyValue, int expected, bool fromLegacy)
    {
        Assert.Equal(expected, PurgeUnusedMetadataJob.GetDays(setting, Variables(current, legacyValue), out var legacy));
        Assert.Equal(fromLegacy, legacy);
    }

    #endregion
}
