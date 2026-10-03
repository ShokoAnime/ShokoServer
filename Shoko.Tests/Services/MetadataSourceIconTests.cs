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
/// Covers which provider's icon a source shows, and that the core ships the
/// icon it names for AniDB.
/// </summary>
public class MetadataSourceIconTests
{
    #region Helpers

    private static MetadataProviderInfo Info(IMetadataProvider provider, MetadataSource source, string? iconPath)
        => new()
        {
            ID = Guid.NewGuid(),
            Version = new(1, 0),
            Name = "Provider",
            Description = string.Empty,
            Provider = provider,
            ConfigurationInfo = null,
            PluginInfo = null!,
            SupportsSeries = provider is IMetadataSeriesProvider,
            SupportsMovies = provider is IMetadataMovieProvider,
            SupportsCollections = false,
            SupportsAutoLinking = false,
            Source = source,
            AvailableEntityTypes = new HashSet<MetadataEntityType>(),
            EnabledEntityTypes = new HashSet<MetadataEntityType>(),
            Icon = iconPath is null ? null : new PackageImageInfo { FilePath = iconPath, MimeType = "image/svg+xml", Width = 16, Height = 16 },
        };

    #endregion

    [Fact]
    public void TheSeriesProvidersIconWinsOverTheMovieProviders()
    {
        var source = TestSources.Plugin;
        List<MetadataProviderInfo> providers =
        [
            Info(Mock.Of<IMetadataMovieProvider>(), source, "movie.svg"),
            Info(Mock.Of<IMetadataSeriesProvider>(), source, "series.svg"),
        ];

        Assert.Equal("series.svg", MetadataProviderManager.ChooseSourceIcon(providers, source)?.FilePath);

        providers.RemoveAt(1);
        Assert.Equal("movie.svg", MetadataProviderManager.ChooseSourceIcon(providers, source)?.FilePath);
        Assert.Null(MetadataProviderManager.ChooseSourceIcon(providers, TestSources.AniList));
    }

    [Fact]
    public void TheCoreShipsTheIconItNames()
        => Assert.Contains(MetadataProviderManager.AnidbIconResourceName, typeof(CorePlugin).Assembly.GetManifestResourceNames());
}
