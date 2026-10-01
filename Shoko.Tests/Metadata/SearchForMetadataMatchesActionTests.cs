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
/// The library-wide search covers every source whose enabled auto-linker
/// auto-links, core sources and plugin sources alike, once each.
/// </summary>
public class SearchForMetadataMatchesActionTests
{
    #region Helpers

    private static MetadataProviderInfo Provider(MetadataSource source, bool autoLinker, bool autoLink, bool enabled = true) => new()
    {
        ID = Guid.NewGuid(),
        Version = new(1, 0),
        Name = source.Name,
        Description = "A fake provider.",
        Provider = Mock.Of<IMetadataProvider>(),
        ConfigurationInfo = null,
        PluginInfo = PluginTestDoubles.InstalledPluginInfo(typeof(SearchForMetadataMatchesActionTests), Guid.NewGuid()),
        SupportsSeries = true,
        SupportsMovies = false,
        SupportsCollections = false,
        SupportsAutoLinking = true,
        SupportsLookup = false,
        Source = source,
        AvailableEntityTypes = new HashSet<MetadataEntityType> { MetadataEntityType.Series },
        EnabledEntityTypes = enabled ? new HashSet<MetadataEntityType> { MetadataEntityType.Series } : new HashSet<MetadataEntityType>(),
        IsAutoLinker = autoLinker,
        AutoLink = autoLink,
    };

    #endregion

    #region Tests

    [Fact]
    public async Task SearchesEachSourceThatAutoLinks_TmdbIncluded()
    {
        var providers = new Mock<IMetadataProviderManager>();
        providers.SetupGet(manager => manager.MetadataProviders).Returns([
            Provider(MetadataSource.TMDB, autoLinker: true, autoLink: true),
            Provider(TestSources.Plugin, autoLinker: true, autoLink: true),
            Provider(TestSources.Plugin, autoLinker: true, autoLink: true),
            Provider(TestSources.AniList, autoLinker: true, autoLink: false),
            Provider(TestSources.LocalPlugin, autoLinker: false, autoLink: false),
            Provider(TestSources.LocalPlugin, autoLinker: true, autoLink: true, enabled: false),
        ]);
        var searched = new List<MetadataSource>();
        var refresh = new Mock<IMetadataRefreshService>();
        refresh.Setup(service => service.AutoSearchAll(It.IsAny<MetadataSource>(), false, It.IsAny<CancellationToken>()))
            .Callback((MetadataSource source, bool _, CancellationToken _) => searched.Add(source))
            .ReturnsAsync(0);

        await new SearchForMetadataMatchesAction(providers.Object, refresh.Object)
            .Execute(new Progress<decimal>(), TestContext.Current.CancellationToken);

        Assert.Equal([MetadataSource.TMDB, TestSources.Plugin], searched);
    }

    #endregion
}
