using System;
using System.Collections.Generic;
using Shoko.Abstractions.Metadata;
using Shoko.Abstractions.Metadata.Providers;
using Shoko.Server.API.v3.Controllers;
using Shoko.Tests.Infrastructure;
using Xunit;

namespace Shoko.Tests.API;

/// <summary>
/// Covers which sources <c>/api/v3/Dashboard/MissingLinks</c> counts. The
/// counting itself runs in the database and is covered by the integration
/// tests.
/// </summary>
public class DashboardMissingLinksTests
{
    #region Helpers

    private static MetadataProviderInfo Info(MetadataSource source, bool enabled = true, bool autoLinker = true)
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
            SupportsCollections = false,
            SupportsAutoLinking = true,
            Source = source,
            AvailableEntityTypes = new HashSet<MetadataEntityType> { MetadataEntityType.Series },
            EnabledEntityTypes = enabled ? new HashSet<MetadataEntityType> { MetadataEntityType.Series } : new HashSet<MetadataEntityType>(),
            IsAutoLinker = autoLinker,
        };

    #endregion

    #region Sources

    [Fact]
    public void OnlySourcesWithAnEnabledAutoLinkerAreCounted()
    {
        var sources = DashboardController.MissingLinkSources(
        [
            Info(TestSources.Plugin),
            Info(MetadataSource.TMDB),
            // A second provider for the same source is counted once.
            Info(MetadataSource.TMDB, autoLinker: false),
            // Off, or not the one working out what an anime is.
            Info(TestSources.AniList, enabled: false),
            Info(MetadataSource.AniDB, autoLinker: false),
        ]);

        Assert.Equal([TestSources.Plugin, MetadataSource.TMDB], sources);
    }

    #endregion
}
