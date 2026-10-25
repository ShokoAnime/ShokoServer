using System;
using System.Collections.Concurrent;
using System.Linq;
using Microsoft.Extensions.DependencyInjection;
using Shoko.Abstractions.Metadata.Services;
using Shoko.QueueProcessor.Abstractions;
using Shoko.Server.Scheduling.Jobs.Airing;

namespace Shoko.Server.Scheduling.Concurrency;

/// <summary>
///   Limits each airing schedule provider's refresh job type to what the
///   provider declared in
///   <see cref="Abstractions.Metadata.Airing.IAiringScheduleProvider.MaxConcurrentJobs"/>.
/// </summary>
/// <remarks>
///   The providers are registered after the container is built, so the
///   service is looked up when first needed, and a provider not registered
///   yet has no limit.
/// </remarks>
/// <param name="serviceProvider">Where the airing schedule service is found.</param>
public class AiringScheduleProviderJobConcurrency(IServiceProvider serviceProvider) : IJobConcurrencyProvider
{
    private readonly ConcurrentDictionary<Type, int?> _limits = new();

    private IAiringScheduleService? _airingScheduleService;

    /// <inheritdoc />
    public int? GetConcurrencyLimit(Type jobType)
    {
        if (_limits.TryGetValue(jobType, out var limit))
            return limit;

        if (jobType is not { IsConstructedGenericType: true, GenericTypeArguments: [var providerType] } ||
            jobType.GetGenericTypeDefinition() != typeof(RefreshAiringScheduleJob<>))
            return _limits[jobType] = null;

        // Remembered only once the providers are registered.
        _airingScheduleService ??= serviceProvider.GetRequiredService<IAiringScheduleService>();
        var providers = _airingScheduleService.GetAvailableProviders().ToList();
        if (providers.Count is 0)
            return null;

        return _limits[jobType] = providers.FirstOrDefault(info => info.Provider.GetType() == providerType)?.MaxConcurrentJobs;
    }
}
