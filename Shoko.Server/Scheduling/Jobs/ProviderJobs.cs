using System;
using System.Reflection;
using Shoko.Abstractions.Connectivity.Suspensions.Attributes;
using Shoko.Abstractions.Metadata.Providers;
using Shoko.Abstractions.Video.Release;
using Shoko.Server.Scheduling.Jobs.Shoko;

namespace Shoko.Server.Scheduling.Jobs;

/// <summary>
///   Tells which provider a job runs for, whether a metadata provider or a
///   release provider.
/// </summary>
public static class ProviderJobs
{
    #region Providers

    /// <summary>
    ///   The provider a job type runs for.
    /// </summary>
    /// <remarks>
    ///   The job type or one of its base types is a closed generic type marked
    ///   <see cref="ProviderJobAttribute"/> whose single type argument is a
    ///   metadata or release provider, or the job type is a release provider's
    ///   job class of its own.
    /// </remarks>
    /// <param name="jobType">The job type.</param>
    /// <returns>The provider type, or <c>null</c> when the job is not one of a provider's.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="jobType"/> is <c>null</c>.</exception>
    public static Type? GetProviderType(Type jobType)
    {
        ArgumentNullException.ThrowIfNull(jobType);
        for (var type = jobType; type is not null; type = type.BaseType)
        {
            if (type is { IsConstructedGenericType: true, GenericTypeArguments: [var providerType] } &&
                type.GetGenericTypeDefinition().GetCustomAttribute<ProviderJobAttribute>(inherit: true) is not null &&
                (typeof(IMetadataProvider).IsAssignableFrom(providerType) || typeof(IReleaseInfoProvider).IsAssignableFrom(providerType)))
                return providerType;
        }

        return ReleaseProviderJobs.GetProviderType(jobType);
    }

    #endregion
}
