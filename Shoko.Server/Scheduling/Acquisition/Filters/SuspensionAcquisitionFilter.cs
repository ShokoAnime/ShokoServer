using System;
using System.Collections.Generic;
using System.Linq;
using Shoko.Abstractions.Connectivity.Services;
using Shoko.Abstractions.Connectivity.Suspensions;
using Shoko.QueueProcessor;
using Shoko.QueueProcessor.Abstractions;
using Shoko.Server.Scheduling.Jobs.Metadata;

namespace Shoko.Server.Scheduling.Acquisition.Filters;

/// <summary>
///   Holds back the jobs of every provider a suspended
///   <see cref="ISuspensionProvider"/> names in its
///   <see cref="SuspensionProviderInfo.HeldProviderTypes"/>: a metadata
///   provider's jobs and a release provider's own job.
/// </summary>
/// <remarks>
///   The held jobs may carry any attribute, so the filter watches every
///   attribute and attaches to each pool whose jobs carry one.
/// </remarks>
public sealed class SuspensionAcquisitionFilter : IAcquisitionFilter, IDisposable
{
    private readonly ISuspensionService _suspensionService;

    private readonly IReadOnlyDictionary<Type, Type[]> _jobTypesByProvider;

    private Type[] _excluded = [];

    /// <summary>
    ///   Creates the filter over the registered job types.
    /// </summary>
    /// <param name="suspensionService">Tells which providers are suspended.</param>
    /// <param name="jobTypeRegistry">The registered job types, the provider jobs among them.</param>
    public SuspensionAcquisitionFilter(ISuspensionService suspensionService, QueueJobTypeRegistry jobTypeRegistry)
        : this(suspensionService, jobTypeRegistry.JobTypes) { }

    /// <summary>
    ///   Creates the filter over the given job types.
    /// </summary>
    /// <param name="suspensionService">Tells which providers are suspended.</param>
    /// <param name="jobTypes">The job types, the provider jobs among them.</param>
    internal SuspensionAcquisitionFilter(ISuspensionService suspensionService, IEnumerable<Type> jobTypes)
    {
        _suspensionService = suspensionService;
        _jobTypesByProvider = jobTypes
            .SelectMany(type => GetProviderTypes(type).Select(providerType => (JobType: type, ProviderType: providerType)))
            .GroupBy(pair => pair.ProviderType)
            .ToDictionary(group => group.Key, group => group.Select(pair => pair.JobType).Distinct().ToArray());
        _suspensionService.SuspensionChanged += OnSuspensionChanged;
        Update();
    }

    /// <inheritdoc />
    public void Dispose()
        => _suspensionService.SuspensionChanged -= OnSuspensionChanged;

    /// <inheritdoc />
    public Type? WatchedAttributeType => typeof(Attribute);

    /// <inheritdoc />
    public IEnumerable<Type> GetTypesToExclude() => _excluded;

    /// <inheritdoc />
    public event EventHandler? StateChanged;

    /// <summary>
    ///   The providers a job type runs for: the metadata provider of a
    ///   metadata job, and the release provider of a release provider job.
    /// </summary>
    /// <param name="jobType">The job type.</param>
    /// <returns>The provider types, which is empty for any other job.</returns>
    internal static IEnumerable<Type> GetProviderTypes(Type jobType)
    {
        if (MetadataProviderJobs.GetProviderType(jobType) is { } metadataProviderType)
            yield return metadataProviderType;

        foreach (var interfaceType in jobType.GetInterfaces())
        {
            if (interfaceType.IsGenericType && interfaceType.GetGenericTypeDefinition() == typeof(IVideoReleaseProviderJob<>))
                yield return interfaceType.GetGenericArguments()[0];
        }
    }

    private void OnSuspensionChanged(object? sender, SuspensionChangedEventArgs e)
    {
        if (e.Previous.IsSuspended == e.Current.IsSuspended)
            return;

        Update();
        StateChanged?.Invoke(this, EventArgs.Empty);
    }

    private void Update()
        => _excluded = [.. _suspensionService.GetAll()
            .Where(status => status.IsSuspended)
            .SelectMany(status => status.Provider.HeldProviderTypes)
            .SelectMany(providerType => _jobTypesByProvider.TryGetValue(providerType, out var jobTypes) ? jobTypes : [])
            .Distinct()];
}
