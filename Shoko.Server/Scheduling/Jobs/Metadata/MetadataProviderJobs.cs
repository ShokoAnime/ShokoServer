using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Shoko.Abstractions.Metadata.Providers;
using Shoko.QueueProcessor;
using Shoko.Server.Scheduling.Acquisition.Attributes;

namespace Shoko.Server.Scheduling.Jobs.Metadata;

/// <summary>
///   The core's job types for each metadata provider: one closed type per
///   provider and job, since the queue holds back and limits whole job types.
/// </summary>
public static class MetadataProviderJobs
{
    #region Types

    /// <summary>
    ///   The job types to register for a provider type found in a plugin or
    ///   in the core.
    /// </summary>
    /// <remarks>
    ///   A refresh job for a provider that refreshes series, films or
    ///   collections, a search job for one that auto-links, an image job for
    ///   one that supplies images, and an entity refresh job for one that
    ///   refreshes creators, characters, studios or networks. A job type whose stored name is too
    ///   long for the queue is left out; see <see cref="GetOverlongJobTypes"/>.
    /// </remarks>
    /// <param name="providerType">The provider's concrete type.</param>
    /// <returns>The closed job types, or none.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="providerType"/> is <c>null</c>.</exception>
    public static IReadOnlyList<Type> GetJobTypes(Type providerType)
        => [.. GetCandidateJobTypes(providerType).Where(QueueProcessorExtensions.FitsQueue)];

    /// <summary>
    ///   The job types a provider type would get but cannot, because their
    ///   stored names are longer than the queue keeps.
    /// </summary>
    /// <param name="providerType">The provider's concrete type.</param>
    /// <returns>The closed job types left out, or none.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="providerType"/> is <c>null</c>.</exception>
    public static IReadOnlyList<Type> GetOverlongJobTypes(Type providerType)
        => [.. GetCandidateJobTypes(providerType).Where(type => !QueueProcessorExtensions.FitsQueue(type))];

    /// <summary>
    ///   Every job type a provider type's shapes call for, whether or not the
    ///   queue can keep it.
    /// </summary>
    /// <param name="providerType">The provider's concrete type.</param>
    /// <returns>The closed job types, or none.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="providerType"/> is <c>null</c>.</exception>
    private static List<Type> GetCandidateJobTypes(Type providerType)
    {
        ArgumentNullException.ThrowIfNull(providerType);
        if (!providerType.IsClass || providerType.IsAbstract || providerType.ContainsGenericParameters ||
            !typeof(IMetadataProvider).IsAssignableFrom(providerType))
            return [];

        var jobTypes = new List<Type>();
        if (typeof(IMetadataSeriesProvider).IsAssignableFrom(providerType) || typeof(IMetadataMovieProvider).IsAssignableFrom(providerType) ||
            typeof(IMetadataCollectionProvider).IsAssignableFrom(providerType))
            jobTypes.Add(typeof(RefreshMetadataJob<>).MakeGenericType(providerType));
        if (typeof(IMetadataAutoLinkingProvider).IsAssignableFrom(providerType))
            jobTypes.Add(typeof(SearchMetadataJob<>).MakeGenericType(providerType));
        if (typeof(IMetadataImageProvider).IsAssignableFrom(providerType))
            jobTypes.Add(typeof(DownloadMetadataImagesJob<>).MakeGenericType(providerType));
        if (typeof(IMetadataEntityProvider).IsAssignableFrom(providerType))
            jobTypes.Add(typeof(RefreshMetadataEntityJob<>).MakeGenericType(providerType));

        return jobTypes;
    }

    /// <summary>
    ///   The provider type a job runs for.
    /// </summary>
    /// <param name="jobType">The job type.</param>
    /// <returns>
    ///   The provider type, or <c>null</c> when the job is not one
    ///   of a provider's.
    /// </returns>
    public static Type? GetProviderType(Type jobType)
        => jobType is { IsConstructedGenericType: true, GenericTypeArguments: [var providerType] } &&
            jobType.GetCustomAttribute<MetadataProviderJobAttribute>(inherit: true) is not null &&
            typeof(IMetadataProvider).IsAssignableFrom(providerType)
                ? providerType
                : null;

    /// <summary>
    ///   The refresh job type for a provider type.
    /// </summary>
    /// <param name="providerType">The provider's concrete type.</param>
    /// <returns>The closed job type, or <c>null</c> when it has none.</returns>
    public static Type? GetRefreshJobType(Type providerType)
        => GetJobTypes(providerType).FirstOrDefault(type => type.GetGenericTypeDefinition() == typeof(RefreshMetadataJob<>));

    /// <summary>
    ///   The search job type for a provider type.
    /// </summary>
    /// <param name="providerType">The provider's concrete type.</param>
    /// <returns>The closed job type, or <c>null</c> when it has none.</returns>
    public static Type? GetSearchJobType(Type providerType)
        => GetJobTypes(providerType).FirstOrDefault(type => type.GetGenericTypeDefinition() == typeof(SearchMetadataJob<>));

    /// <summary>
    ///   The image job type for a provider type.
    /// </summary>
    /// <param name="providerType">The provider's concrete type.</param>
    /// <returns>The closed job type, or <c>null</c> when it has none.</returns>
    public static Type? GetImagesJobType(Type providerType)
        => GetJobTypes(providerType).FirstOrDefault(type => type.GetGenericTypeDefinition() == typeof(DownloadMetadataImagesJob<>));

    /// <summary>
    ///   The entity refresh job type for a provider type.
    /// </summary>
    /// <param name="providerType">The provider's concrete type.</param>
    /// <returns>The closed job type, or <c>null</c> when it has none.</returns>
    public static Type? GetEntityRefreshJobType(Type providerType)
        => GetJobTypes(providerType).FirstOrDefault(type => type.GetGenericTypeDefinition() == typeof(RefreshMetadataEntityJob<>));

    #endregion
}
