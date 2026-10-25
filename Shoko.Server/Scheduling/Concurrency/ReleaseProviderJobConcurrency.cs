using System;
using System.Collections.Concurrent;
using System.Linq;
using Microsoft.Extensions.DependencyInjection;
using Shoko.Abstractions.Video.Services;
using Shoko.QueueProcessor.Abstractions;
using Shoko.Server.Scheduling.Jobs.Shoko;

namespace Shoko.Server.Scheduling.Concurrency;

/// <summary>
///   Limits each release provider's job type to what the provider declared in
///   <see cref="Abstractions.Video.Release.IReleaseInfoProvider.MaxConcurrentJobs"/>.
/// </summary>
/// <remarks>
///   The providers are registered after the container is built, so the
///   service is looked up when first needed, and a provider not registered
///   yet has no limit.
/// </remarks>
/// <param name="serviceProvider">Where the release service is found.</param>
public class ReleaseProviderJobConcurrency(IServiceProvider serviceProvider) : IJobConcurrencyProvider
{
    private readonly ConcurrentDictionary<Type, int?> _limits = new();

    private IVideoReleaseService? _releaseService;

    /// <inheritdoc />
    public int? GetConcurrencyLimit(Type jobType)
    {
        if (_limits.TryGetValue(jobType, out var limit))
            return limit;

        if (ReleaseProviderJobs.GetProviderType(jobType) is not { } providerType)
            return _limits[jobType] = null;

        // Remembered only once the providers are registered.
        _releaseService ??= serviceProvider.GetRequiredService<IVideoReleaseService>();
        var providers = _releaseService.GetAvailableProviders().ToList();
        if (providers.Count is 0)
            return null;

        return _limits[jobType] = providers.FirstOrDefault(info => info.Provider.GetType() == providerType)?.MaxConcurrentJobs;
    }
}
