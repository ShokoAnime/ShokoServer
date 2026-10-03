using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Moq;
using Shoko.Abstractions.Metadata;
using Shoko.Abstractions.Metadata.Providers;
using Shoko.Tests.Infrastructure;
using Xunit;

namespace Shoko.Tests.Services;

/// <summary>
/// Covers how <see cref="Shoko.Server.Services.MetadataService"/> finds an
/// entry's page on its source's site: the core's own answers for AniDB, a
/// plugin's resolver before the source's provider, nothing for a pair
/// nobody owns, and nothing for an owner that throws.
/// </summary>
[Collection(nameof(RepoFactoryCollection))]
public class MetadataSiteUrlTests
{
    #region Helpers

    private static readonly MetadataGuid PluginSeries = new(TestSources.Plugin, MetadataEntityType.Series, "s1");

    /// <summary>
    /// A series provider giving one page, or throwing when asked to.
    /// </summary>
    private sealed class PageProvider(string? url, bool throws = false) : IMetadataSeriesProvider
    {
        public string Name => "Pages";

        public MetadataSource Source => TestSources.Plugin;

        public Task RefreshSeries(MetadataGuid seriesID, MetadataRefreshOptions options, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public string? GetSiteUrl(IMetadata entry)
            => throws ? throw new InvalidOperationException("Broken.") : url;
    }

    private static MetadataProviderInfo Info(IMetadataProvider provider)
        => new()
        {
            ID = Guid.NewGuid(),
            Version = new(1, 0),
            Name = provider.Name,
            Description = string.Empty,
            Provider = provider,
            ConfigurationInfo = null,
            PluginInfo = null!,
            SupportsSeries = true,
            SupportsMovies = false,
            SupportsCollections = false,
            SupportsAutoLinking = false,
            Source = provider.Source,
            AvailableEntityTypes = new HashSet<MetadataEntityType> { MetadataEntityType.Series },
            // Not enabled: a page does not depend on it.
            EnabledEntityTypes = new HashSet<MetadataEntityType>(),
        };

    private static Mock<IMetadataResolver> Resolver(string? url)
    {
        var resolver = new Mock<IMetadataResolver>();
        resolver.SetupGet(r => r.Name).Returns("Resolver");
        resolver.SetupGet(r => r.Scope).Returns(MetadataEntityScope.Single(TestSources.Plugin, MetadataEntityType.Series));
        resolver.Setup(r => r.GetSiteUrl(It.IsAny<IMetadata>())).Returns(url);
        return resolver;
    }

    private static IMetadata Entry(MetadataGuid id)
        => Mock.Of<IMetadata>(entry => entry.ID == id);

    #endregion

    #region Routing

    [Fact]
    public void AResolverAnswersBeforeTheProvider()
    {
        var tables = new MetadataLookupTables();
        tables.Providers.Add(Info(new PageProvider("https://example.com/provider")));
        tables.Service.AddParts([], [Resolver("https://example.com/resolver").Object]);

        Assert.Equal("https://example.com/resolver", tables.Service.GetSiteUrl(Entry(PluginSeries)));
    }

    [Fact]
    public void AResolverWithoutAPageLeavesItToTheProvider()
    {
        var tables = new MetadataLookupTables();
        tables.Providers.Add(Info(new PageProvider("https://example.com/provider")));
        tables.Service.AddParts([], [Resolver(null).Object]);

        Assert.Equal("https://example.com/provider", tables.Service.GetSiteUrl(Entry(PluginSeries)));
    }

    [Fact]
    public void APairNobodyOwnsHasNoPage()
    {
        var tables = new MetadataLookupTables();
        tables.Providers.Add(Info(new PageProvider("https://example.com/provider")));

        // A series provider owns no movies, and Shoko's own entries have no page.
        Assert.Null(tables.Service.GetSiteUrl(Entry(new(TestSources.Plugin, MetadataEntityType.Movie, "m1"))));
        Assert.Null(tables.Service.GetSiteUrl(MetadataGuid.Parse("shoko://series/3")));
    }

    /// <summary>
    /// An entity provider giving one page for people, or nothing.
    /// </summary>
    private sealed class PeoplePages(string? url) : IMetadataEntityProvider
    {
        public string Name => "People";

        public MetadataSource Source => TestSources.Plugin;

        public MetadataEntityScope EntityScope => MetadataEntityScope.Single(TestSources.Plugin, MetadataEntityType.Creator);

        public Task<bool> RefreshEntity(MetadataGuid entityID, CancellationToken cancellationToken = default)
            => Task.FromResult(false);

        public string? GetSiteUrl(IMetadata entry)
            => url;
    }

    [Fact]
    public void APersonAsksTheEntityProviderThenTheSeriesProvider()
    {
        var creator = new MetadataGuid(TestSources.Plugin, MetadataEntityType.Creator, "c1");
        var answering = new MetadataLookupTables();
        answering.Providers.Add(Info(new PageProvider("https://example.com/series")));
        answering.Providers.Add(Info(new PeoplePages("https://example.com/person")));
        var silent = new MetadataLookupTables();
        silent.Providers.Add(Info(new PageProvider("https://example.com/series")));
        silent.Providers.Add(Info(new PeoplePages(null)));

        Assert.Equal("https://example.com/person", answering.Service.GetSiteUrl(Entry(creator)));
        Assert.Equal("https://example.com/series", silent.Service.GetSiteUrl(Entry(creator)));
    }

    [Fact]
    public void AThrowingOwnerHasNoPage()
    {
        var tables = new MetadataLookupTables();
        tables.Providers.Add(Info(new PageProvider("https://example.com/provider", throws: true)));

        Assert.Null(tables.Service.GetSiteUrl(Entry(PluginSeries)));
    }

    [Fact]
    public void AnEntryNothingHoldsIsAskedAboutByItsID()
    {
        var tables = new MetadataLookupTables();
        var resolver = Resolver("https://example.com/resolver");
        tables.Service.AddParts([], [resolver.Object]);

        Assert.NotNull(tables.Service.GetSiteUrl(PluginSeries));
        resolver.Verify(r => r.GetSiteUrl(It.Is<IMetadata>(entry => entry.ID == PluginSeries)), Times.Once);
    }

    #endregion

    #region Core Sources

    [Theory]
    [InlineData("anidb://series/30", "https://anidb.net/anime/30")]
    [InlineData("anidb://episode/300", "https://anidb.net/episode/300")]
    [InlineData("anidb://creator/40", "https://anidb.net/creator/40")]
    [InlineData("anidb://studio/40", "https://anidb.net/creator/40")]
    [InlineData("anidb://character/41", "https://anidb.net/character/41")]
    [InlineData("anidb://tag/42", null)]
    public void TheCoreAnswersAniDB(string id, string? url)
    {
        var tables = new MetadataLookupTables();

        Assert.Equal(url, tables.Service.GetSiteUrl(MetadataGuid.Parse(id)));
    }

    #endregion
}
