using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Moq;
using Shoko.Abstractions.Metadata;
using Shoko.Abstractions.Metadata.Enums;
using Shoko.Abstractions.Metadata.Providers;
using Shoko.Abstractions.Metadata.Services;
using Shoko.Server.Actions;
using Shoko.Tests.Infrastructure;
using Xunit;

namespace Shoko.Tests.Metadata;

/// <summary>
/// The purges across every metadata source cover TMDB and the plugin sources
/// alike, one named source narrows them to it, a source that is not known or
/// keeps nothing is refused, and the TMDB-only purges stay on TMDB.
/// </summary>
public class MetadataPurgeActionsTests
{
    #region Helpers

    private static MetadataProviderInfo Provider(MetadataSource source) => new()
    {
        ID = Guid.NewGuid(),
        Version = new(1, 0),
        Name = source.Name,
        Description = "A fake provider.",
        Provider = Mock.Of<IMetadataProvider>(),
        ConfigurationInfo = null,
        PluginInfo = PluginTestDoubles.InstalledPluginInfo(typeof(MetadataPurgeActionsTests), Guid.NewGuid()),
        SupportsSeries = true,
        SupportsMovies = false,
        SupportsCollections = false,
        SupportsAutoLinking = false,
        SupportsLookup = false,
        Source = source,
        AvailableEntityTypes = new HashSet<MetadataEntityType> { MetadataEntityType.Series },
        EnabledEntityTypes = new HashSet<MetadataEntityType> { MetadataEntityType.Series },
        IsAutoLinker = false,
        AutoLink = false,
    };

    private static IMetadataProviderManager Providers()
    {
        var providers = new Mock<IMetadataProviderManager>();
        providers.SetupGet(manager => manager.MetadataProviders).Returns([Provider(MetadataSource.TMDB), Provider(TestSources.Plugin)]);
        return providers.Object;
    }

    private static Mock<IMetadataPurgeService> PurgeService(List<(MetadataSource Source, MetadataEntityType? Kind)> purged)
    {
        var purge = new Mock<IMetadataPurgeService>();
        purge.Setup(service => service.PurgeUnused(
            It.IsAny<MetadataSource>(),
            It.IsAny<DateTime?>(),
            It.IsAny<MetadataEntityType?>(),
            It.IsAny<IProgress<decimal>?>(),
            It.IsAny<CancellationToken>()
        ))
            .Callback((MetadataSource source, DateTime? _, MetadataEntityType? kind, IProgress<decimal>? _, CancellationToken _) => purged.Add((source, kind)))
            .ReturnsAsync(0);
        return purge;
    }

    #endregion

    #region Tests

    [Fact]
    public async Task TheScheduledPurge_CoversTmdbAndThePluginSources()
    {
        var purged = new List<(MetadataSource Source, MetadataEntityType? Kind)>();

        await new PurgeAllUnusedMetadataAction(Providers(), PurgeService(purged).Object)
            .Execute(new Progress<decimal>(), TestContext.Current.CancellationToken);

        Assert.Contains((MetadataSource.TMDB, null), purged);
        Assert.Contains((TestSources.Plugin, null), purged);
        Assert.DoesNotContain(purged, pair => pair.Source == MetadataSource.AniDB);
    }

    [Fact]
    public async Task TheExecutablePurge_CoversEverySourceWithoutOne_AndOnlyTheOneNamed()
    {
        var purged = new List<(MetadataSource Source, MetadataEntityType? Kind)>();
        var purge = PurgeService(purged).Object;

        await new PurgeUnusedMetadataAction(Providers(), purge).Execute(TestContext.Current.CancellationToken);
        Assert.Contains((MetadataSource.TMDB, null), purged);
        Assert.Contains((TestSources.Plugin, null), purged);

        purged.Clear();
        await new PurgeUnusedMetadataAction(Providers(), purge) { Source = MetadataSource.TMDB }.Execute(TestContext.Current.CancellationToken);
        Assert.Equal([(MetadataSource.TMDB, null)], purged);
    }

    [Fact]
    public async Task TheExecutablePurge_RefusesAnUnknownSource_AndOneKeepingNothing()
    {
        var purge = PurgeService([]).Object;

        Assert.Null(await new PurgeUnusedMetadataAction(Providers(), purge) { Source = TestSources.Plugin }.Validate(TestContext.Current.CancellationToken));
        Assert.NotNull(await new PurgeUnusedMetadataAction(Providers(), purge) { Source = MetadataSource.Parse("not-a-source", null) }
            .Validate(TestContext.Current.CancellationToken));
        Assert.NotNull(await new PurgeUnusedMetadataAction(Providers(), purge) { Source = MetadataSource.AniDB }.Validate(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task TheTmdbOnlyPurge_StaysOnTmdbShows()
    {
        var purged = new List<(MetadataSource Source, MetadataEntityType? Kind)>();

        await new PurgeAllUnusedTmdbShowsAction(PurgeService(purged).Object).Execute(new Progress<decimal>(), TestContext.Current.CancellationToken);

        Assert.Equal([(MetadataSource.TMDB, MetadataEntityType.Series)], purged);
    }

    #endregion
}
