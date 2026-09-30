using System;
using System.Collections.Concurrent;
using System.Linq;
using Microsoft.Extensions.DependencyInjection;
using Shoko.Abstractions.Metadata.Services;
using Shoko.QueueProcessor.Abstractions;
using Shoko.Server.Scheduling.Jobs.Metadata;
using Shoko.Server.Services;

namespace Shoko.Server.Scheduling.Concurrency;

/// <summary>
///   Gives each image contributor's job type a pool of its own, of the size
///   the contributor declared in
///   <see cref="Abstractions.Metadata.Providers.MetadataImageContributorInfo.MaxConcurrentJobs"/>.
/// </summary>
/// <remarks>
///   The contributors are registered after the container is built, so the
///   manager is looked up when first needed, and a contributor not
///   registered yet gets the default limit.
/// </remarks>
/// <param name="serviceProvider">Where the contributor manager is found.</param>
public class MetadataImageContributorJobConcurrency(IServiceProvider serviceProvider) : IJobConcurrencyProvider
{
    private readonly ConcurrentDictionary<Type, int?> _limits = new();

    private IMetadataImageContributorManager? _contributorManager;

    /// <inheritdoc />
    public int? GetConcurrencyLimit(Type jobType)
    {
        if (_limits.TryGetValue(jobType, out var limit))
            return limit;

        if (MetadataImageContributorJobs.GetContributorType(jobType) is not { } contributorType)
            return _limits[jobType] = null;

        // Remembered only once the contributors are registered, since the
        // limit is read off the registration and never changes after it.
        _contributorManager ??= serviceProvider.GetRequiredService<IMetadataImageContributorManager>();
        var contributors = _contributorManager.ImageContributors;
        if (contributors.Count is 0)
            return MetadataImageContributorManager.DefaultMaxConcurrentJobs;

        return _limits[jobType] = contributors.FirstOrDefault(info => info.Contributor.GetType() == contributorType)?.MaxConcurrentJobs
            ?? MetadataImageContributorManager.DefaultMaxConcurrentJobs;
    }
}
