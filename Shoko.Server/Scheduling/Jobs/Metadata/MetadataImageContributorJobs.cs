using System;
using Shoko.Abstractions.Metadata.Providers;
using Shoko.QueueProcessor;

namespace Shoko.Server.Scheduling.Jobs.Metadata;

/// <summary>
///   The core's job type for each image contributor: one closed type per
///   contributor, so each runs in a pool of its own.
/// </summary>
public static class MetadataImageContributorJobs
{
    #region Types

    /// <summary>
    ///   The job type to register for a contributor type found in a plugin.
    /// </summary>
    /// <param name="contributorType">The contributor's concrete type.</param>
    /// <returns>
    ///   The closed job type, or <see langword="null"/> when the type is not a
    ///   concrete contributor or the job type's stored name is too long for
    ///   the queue; see <see cref="GetOverlongJobType"/>.
    /// </returns>
    /// <exception cref="ArgumentNullException"><paramref name="contributorType"/> is <see langword="null"/>.</exception>
    public static Type? GetJobType(Type contributorType)
        => GetCandidateJobType(contributorType) is { } jobType && QueueProcessorExtensions.FitsQueue(jobType) ? jobType : null;

    /// <summary>
    ///   The job type a contributor type would get but cannot, because its
    ///   stored name is longer than the queue keeps.
    /// </summary>
    /// <param name="contributorType">The contributor's concrete type.</param>
    /// <returns>The closed job type left out, or <see langword="null"/>.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="contributorType"/> is <see langword="null"/>.</exception>
    public static Type? GetOverlongJobType(Type contributorType)
        => GetCandidateJobType(contributorType) is { } jobType && !QueueProcessorExtensions.FitsQueue(jobType) ? jobType : null;

    /// <summary>
    ///   The contributor type a job runs for.
    /// </summary>
    /// <param name="jobType">The job type.</param>
    /// <returns>
    ///   The contributor type, or <see langword="null"/> when the job is not
    ///   one of a contributor's.
    /// </returns>
    public static Type? GetContributorType(Type jobType)
        => jobType is { IsConstructedGenericType: true, GenericTypeArguments: [var contributorType] } &&
            jobType.GetGenericTypeDefinition() == typeof(DownloadContributedImagesJob<>)
                ? contributorType
                : null;

    /// <summary>
    ///   The job type a contributor type calls for, whether or not the queue
    ///   can keep it.
    /// </summary>
    /// <param name="contributorType">The contributor's concrete type.</param>
    /// <returns>The closed job type, or <see langword="null"/>.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="contributorType"/> is <see langword="null"/>.</exception>
    private static Type? GetCandidateJobType(Type contributorType)
    {
        ArgumentNullException.ThrowIfNull(contributorType);
        return contributorType is { IsClass: true, IsAbstract: false, ContainsGenericParameters: false } &&
            typeof(IMetadataImageContributor).IsAssignableFrom(contributorType)
                ? typeof(DownloadContributedImagesJob<>).MakeGenericType(contributorType)
                : null;
    }

    #endregion
}
