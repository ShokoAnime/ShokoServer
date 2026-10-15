using System;
using System.Collections.Generic;
using System.Linq;
using Shoko.Abstractions.Metadata.Airing;
using Shoko.QueueProcessor;

namespace Shoko.Server.Scheduling.Jobs.Airing;

/// <summary>
/// The core's job types for each airing schedule provider: one closed type per
/// provider and job, since the queue holds back and limits whole job types.
/// </summary>
public static class AiringScheduleProviderJobs
{
    #region Types

    /// <summary>
    /// The job types to register for a provider type found in a plugin or in
    /// the core.
    /// </summary>
    /// <remarks>
    /// A refresh job for every provider and a sweep job for one that sweeps. A
    /// job type whose stored name is too long for the queue is left out; see
    /// <see cref="GetOverlongJobTypes"/>.
    /// </remarks>
    /// <param name="providerType">The provider's concrete type.</param>
    /// <returns>The closed job types, or none.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="providerType"/> is <c>null</c>.</exception>
    public static IReadOnlyList<Type> GetJobTypes(Type providerType)
        => [.. GetCandidateJobTypes(providerType).Where(QueueProcessorExtensions.FitsQueue)];

    /// <summary>
    /// The job types a provider type would get but cannot, because their
    /// stored names are longer than the queue keeps.
    /// </summary>
    /// <param name="providerType">The provider's concrete type.</param>
    /// <returns>The closed job types left out, or none.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="providerType"/> is <c>null</c>.</exception>
    public static IReadOnlyList<Type> GetOverlongJobTypes(Type providerType)
        => [.. GetCandidateJobTypes(providerType).Where(type => !QueueProcessorExtensions.FitsQueue(type))];

    /// <summary>
    /// Every job type a provider type's shapes call for, whether or not the
    /// queue can keep it.
    /// </summary>
    /// <param name="providerType">The provider's concrete type.</param>
    /// <returns>The closed job types, or none.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="providerType"/> is <c>null</c>.</exception>
    private static List<Type> GetCandidateJobTypes(Type providerType)
    {
        ArgumentNullException.ThrowIfNull(providerType);
        if (!providerType.IsClass || providerType.IsAbstract || providerType.ContainsGenericParameters ||
            !typeof(IAiringScheduleProvider).IsAssignableFrom(providerType))
            return [];

        var jobTypes = new List<Type> { typeof(RefreshAiringScheduleJob<>).MakeGenericType(providerType) };
        if (typeof(ISweepingAiringScheduleProvider).IsAssignableFrom(providerType))
            jobTypes.Add(typeof(SweepAiringScheduleProviderJob<>).MakeGenericType(providerType));

        return jobTypes;
    }

    /// <summary>
    /// The airing schedule provider type a job runs for.
    /// </summary>
    /// <param name="jobType">The job type.</param>
    /// <returns>
    /// The provider type, or <c>null</c> when the job is not one of an airing
    /// schedule provider's.
    /// </returns>
    /// <exception cref="ArgumentNullException"><paramref name="jobType"/> is <c>null</c>.</exception>
    public static Type? GetProviderType(Type jobType)
        => ProviderJobs.GetProviderType(jobType) is { } providerType && typeof(IAiringScheduleProvider).IsAssignableFrom(providerType)
            ? providerType
            : null;

    /// <summary>
    /// The refresh job type for a provider type.
    /// </summary>
    /// <param name="providerType">The provider's concrete type.</param>
    /// <returns>The closed job type, or <c>null</c> when it has none.</returns>
    public static Type? GetRefreshJobType(Type providerType)
        => GetJobTypes(providerType).FirstOrDefault(type => type.GetGenericTypeDefinition() == typeof(RefreshAiringScheduleJob<>));

    /// <summary>
    /// The sweep job type for a provider type.
    /// </summary>
    /// <param name="providerType">The provider's concrete type.</param>
    /// <returns>The closed job type, or <c>null</c> when it has none.</returns>
    public static Type? GetSweepJobType(Type providerType)
        => GetJobTypes(providerType).FirstOrDefault(type => type.GetGenericTypeDefinition() == typeof(SweepAiringScheduleProviderJob<>));

    #endregion
}
