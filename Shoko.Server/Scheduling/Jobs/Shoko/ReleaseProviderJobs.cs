using System;
using System.Collections.Generic;
using System.Linq;
using Shoko.Abstractions.Video.Release;
using Shoko.QueueProcessor;
using Shoko.QueueProcessor.Abstractions;

namespace Shoko.Server.Scheduling.Jobs.Shoko;

/// <summary>
///   The job types for each release provider: a job class of its own, or one
///   closed <see cref="ProcessReleaseProviderJob{TProvider}"/> type per
///   provider without one, since the queue holds back and limits whole job types.
/// </summary>
public static class ReleaseProviderJobs
{
    #region Types

    /// <summary>
    ///   The generic job types to register for the release providers among the
    ///   types found in the plugins and in the core.
    /// </summary>
    /// <remarks>
    ///   A provider with a job class of its own among <paramref name="types"/>
    ///   gets none. A job type whose stored name is too long for the queue is
    ///   left out; see <see cref="GetOverlongJobTypes"/>.
    /// </remarks>
    /// <param name="types">Every type found, the providers and job classes among them.</param>
    /// <returns>The closed job types, or none.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="types"/> is <c>null</c>.</exception>
    public static IReadOnlyList<Type> GetJobTypes(IEnumerable<Type> types)
        => [.. GetCandidateJobTypes(types).Where(QueueProcessorExtensions.FitsQueue)];

    /// <summary>
    ///   The generic job types the release providers among the types found
    ///   would get but cannot, because their stored names are longer than the
    ///   queue keeps.
    /// </summary>
    /// <param name="types">Every type found, the providers and job classes among them.</param>
    /// <returns>The closed job types left out, or none.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="types"/> is <c>null</c>.</exception>
    public static IReadOnlyList<Type> GetOverlongJobTypes(IEnumerable<Type> types)
        => [.. GetCandidateJobTypes(types).Where(type => !QueueProcessorExtensions.FitsQueue(type))];

    /// <summary>
    ///   The job type to queue for each release provider among the registered
    ///   job types.
    /// </summary>
    /// <remarks>
    ///   A job class of the provider's own wins over the generic job.
    /// </remarks>
    /// <param name="jobTypes">The registered job types.</param>
    /// <returns>The job type for each provider type.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="jobTypes"/> is <c>null</c>.</exception>
    public static Dictionary<Type, Type> GetJobTypesByProvider(IEnumerable<Type> jobTypes)
    {
        ArgumentNullException.ThrowIfNull(jobTypes);
        return jobTypes
            .Select(jobType => (ProviderType: GetProviderType(jobType), JobType: jobType))
            .Where(pair => pair.ProviderType is not null)
            .GroupBy(pair => pair.ProviderType!)
            .ToDictionary(
                group => group.Key,
                group => group.Select(pair => pair.JobType).OrderBy(IsGenericJob).First()
            );
    }

    /// <summary>
    ///   The release provider a job type runs for, through the
    ///   <see cref="IVideoReleaseProviderJob{TProvider}"/> it implements.
    /// </summary>
    /// <param name="jobType">The job type.</param>
    /// <returns>The provider type, or <c>null</c> when the job is not a release provider's.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="jobType"/> is <c>null</c>.</exception>
    public static Type? GetProviderType(Type jobType)
    {
        ArgumentNullException.ThrowIfNull(jobType);
        return jobType.GetInterfaces()
            .FirstOrDefault(type => type.IsGenericType && type.GetGenericTypeDefinition() == typeof(IVideoReleaseProviderJob<>))
            ?.GenericTypeArguments[0];
    }

    private static List<Type> GetCandidateJobTypes(IEnumerable<Type> types)
    {
        ArgumentNullException.ThrowIfNull(types);
        var typeList = types.ToList();
        var providersWithJobs = typeList
            .Where(type => type is { IsClass: true, IsAbstract: false, ContainsGenericParameters: false } && !IsGenericJob(type))
            .Select(GetProviderType)
            .OfType<Type>()
            .ToHashSet();
        return typeList
            .Where(type => type is { IsClass: true, IsAbstract: false, ContainsGenericParameters: false } &&
                typeof(IReleaseInfoProvider).IsAssignableFrom(type) && !providersWithJobs.Contains(type))
            .Select(type => typeof(ProcessReleaseProviderJob<>).MakeGenericType(type))
            .ToList();
    }

    private static bool IsGenericJob(Type jobType)
        => jobType.IsConstructedGenericType && jobType.GetGenericTypeDefinition() == typeof(ProcessReleaseProviderJob<>);

    #endregion
}
