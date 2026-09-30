using System;
using System.Collections.Concurrent;
using System.Linq;
using Microsoft.Extensions.DependencyInjection;
using Shoko.Abstractions.Metadata.Services;
using Shoko.QueueProcessor.Abstractions;
using Shoko.Server.Scheduling.Jobs.Metadata;

namespace Shoko.Server.Scheduling.Concurrency;

/// <summary>
///   Limits each provider's job types to what the provider declared in
///   <see cref="Abstractions.Metadata.Providers.MetadataProviderInfo.MaxConcurrentJobs"/>.
/// </summary>
/// <remarks>
///   The providers are registered after the container is built, so the
///   manager is looked up when first needed, and a provider not registered
///   yet has no limit.
/// </remarks>
/// <param name="serviceProvider">Where the provider manager is found.</param>
public class MetadataProviderJobConcurrency(IServiceProvider serviceProvider) : IJobConcurrencyProvider
{
    private readonly ConcurrentDictionary<Type, int?> _limits = new();

    private IMetadataProviderManager? _providerManager;

    /// <inheritdoc />
    public int? GetConcurrencyLimit(Type jobType)
    {
        if (_limits.TryGetValue(jobType, out var limit))
            return limit;

        if (MetadataProviderJobs.GetProviderType(jobType) is not { } providerType)
            return _limits[jobType] = null;

        // Remembered only once the providers are registered, since the limit
        // is read off the registration and never changes after it.
        _providerManager ??= serviceProvider.GetRequiredService<IMetadataProviderManager>();
        var providers = _providerManager.MetadataProviders;
        if (providers.Count is 0)
            return null;

        return _limits[jobType] = providers.FirstOrDefault(info => info.Provider.GetType() == providerType)?.MaxConcurrentJobs;
    }
}
