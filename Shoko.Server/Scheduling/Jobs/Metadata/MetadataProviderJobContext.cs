using System;
using Microsoft.Extensions.Logging;
using Shoko.Abstractions.Metadata.Providers;
using Shoko.Abstractions.Metadata.Services;

namespace Shoko.Server.Scheduling.Jobs.Metadata;

/// <summary>
///   Finds the registered provider a provider job runs for.
/// </summary>
internal static class MetadataProviderJobContext
{
    /// <summary>
    ///   The registered provider of a type, with its info.
    /// </summary>
    /// <typeparam name="TProvider">The provider's type.</typeparam>
    /// <param name="providerManager">Where the providers are registered.</param>
    /// <param name="logger">Where a missing provider is reported.</param>
    /// <returns>
    ///   The info and the provider, or <see langword="null"/> when no provider
    ///   of the type was registered, such as when its plugin refused a
    ///   reserved source.
    /// </returns>
    public static (MetadataProviderInfo Info, TProvider Provider)? Resolve<TProvider>(IMetadataProviderManager providerManager, ILogger logger)
        where TProvider : class, IMetadataProvider
    {
        try
        {
            var info = providerManager.GetProviderInfo(typeof(TProvider));
            if (info.Provider is TProvider provider)
                return (info, provider);
        }
        catch (ArgumentException)
        {
        }

        logger.LogWarning("Skipping a job for {Provider}, which is not a registered metadata provider.", typeof(TProvider).Name);
        return null;
    }
}
