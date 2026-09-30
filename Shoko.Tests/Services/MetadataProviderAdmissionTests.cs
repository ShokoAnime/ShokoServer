using System;
using System.Collections.Generic;
using Moq;
using Shoko.Abstractions.Metadata;
using Shoko.Abstractions.Metadata.Providers;
using Shoko.Abstractions.Plugin.Models;
using Shoko.Server.Plugin;
using Shoko.Server.Services;
using Shoko.Tests.Infrastructure;
using Xunit;

namespace Shoko.Tests.Services;

/// <summary>
/// Covers which metadata providers <see cref="MetadataProviderManager"/> takes at start-up and
/// which it drops, through <see cref="MetadataProviderManager.RejectReason"/>.
/// </summary>
public class MetadataProviderAdmissionTests
{
    private static readonly IReadOnlySet<MetadataSource> _reserved = MetadataProviderManager.CoreReservedSources;

    private static readonly LocalPluginInfo _core = PluginTestDoubles.CorePluginInfo(typeof(CorePlugin), Guid.Parse("44444444-4444-4444-4444-444444444444"));

    private static readonly LocalPluginInfo _plugin = PluginTestDoubles.InstalledPluginInfo(typeof(PluginTestDoubles.TestPlugin), Guid.Parse("55555555-5555-5555-5555-555555555555"));

    #region Helpers

    private static IMetadataProvider Provider<TProvider>(MetadataSource? source) where TProvider : class, IMetadataProvider
    {
        var provider = new Mock<TProvider>();
        provider.SetupGet(p => p.Name).Returns("Test Provider");
        provider.SetupGet(p => p.Source).Returns(source!);
        return provider.Object;
    }

    #endregion

    #region Dropped

    public static TheoryData<IMetadataProvider> ProvidersAnsweringNothing
        => new(Provider<IMetadataProvider>(TestSources.Plugin), Provider<IMetadataCollectionProvider>(TestSources.Plugin));

    [Theory]
    [MemberData(nameof(ProvidersAnsweringNothing))]
    public void AProviderAnsweringNothingIsDropped(IMetadataProvider provider)
        => Assert.NotNull(MetadataProviderManager.RejectReason(provider, _plugin, _reserved));

    [Fact]
    public void AProviderOutsideALoadedPluginIsDropped()
        => Assert.NotNull(MetadataProviderManager.RejectReason(Provider<IMetadataSeriesProvider>(TestSources.Plugin), null, _reserved));

    [Fact]
    public void AProviderWithoutASourceIsDropped()
        => Assert.NotNull(MetadataProviderManager.RejectReason(Provider<IMetadataSeriesProvider>(null), _plugin, _reserved));

    [Fact]
    public void APluginProviderClaimingAReservedSourceIsDropped()
    {
        Assert.NotEmpty(_reserved);
        Assert.All(_reserved, source => Assert.NotNull(MetadataProviderManager.RejectReason(Provider<IMetadataSeriesProvider>(MetadataSource.Parse(source.Value)), _plugin, _reserved)));
    }

    #endregion

    #region Accepted

    public static TheoryData<IMetadataProvider> PluginProviders => new(
        Provider<IMetadataSeriesProvider>(TestSources.Plugin),
        Provider<IMetadataMovieProvider>(TestSources.Plugin),
        Provider<IMetadataAutoLinkingProvider>(TestSources.AniList)
    );

    [Theory]
    [MemberData(nameof(PluginProviders))]
    public void APluginProviderOnItsOwnSourceIsTaken(IMetadataProvider provider)
        => Assert.Null(MetadataProviderManager.RejectReason(provider, _plugin, _reserved));

    [Fact]
    public void ACoreProviderOnTmdbIsTaken()
        => Assert.Null(MetadataProviderManager.RejectReason(Provider<IMetadataSeriesProvider>(MetadataSource.TMDB), _core, _reserved));

    #endregion
}
