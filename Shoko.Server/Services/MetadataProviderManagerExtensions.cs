using System.Collections.Generic;
using System.Linq;
using Shoko.Abstractions.Metadata.Providers;
using Shoko.Abstractions.Metadata.Services;

namespace Shoko.Server.Services;

/// <summary>
///   Picks providers out of the provider manager.
/// </summary>
internal static class MetadataProviderManagerExtensions
{
    #region Auto-linkers

    /// <summary>
    ///   The enabled auto-linker of every source, core and plugin sources
    ///   alike, one per source, in provider order. Unconfigured ones are
    ///   included; the searches skip them.
    /// </summary>
    /// <param name="providerManager">Lists the providers.</param>
    /// <param name="onRequest">
    ///   Whether a person asked for the search, so an auto-linker that only
    ///   links when asked counts too. Left off, only the ones that auto-link.
    /// </param>
    /// <returns>The auto-linkers.</returns>
    public static IReadOnlyList<MetadataProviderInfo> GetAutoLinkers(this IMetadataProviderManager providerManager, bool onRequest = false)
        => providerManager.MetadataProviders
            .Where(info => info.Enabled && info.IsAutoLinker && (onRequest || info.AutoLink))
            .DistinctBy(info => info.Source)
            .ToList();

    #endregion
}
