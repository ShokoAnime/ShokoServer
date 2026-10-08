using System;
using System.Collections.Generic;
using System.Linq;
using Shoko.Abstractions.Connectivity.Services;
using Shoko.Abstractions.Connectivity.Suspensions;
using Shoko.Abstractions.Connectivity.Suspensions.Attributes;
using Shoko.QueueProcessor;
using Shoko.QueueProcessor.Abstractions;
using Shoko.Server.Scheduling.Jobs;

namespace Shoko.Server.Scheduling.Acquisition.Filters;

/// <summary>
///   Holds back the jobs of every provider a suspended
///   <see cref="ISuspensionProvider"/> names in its
///   <see cref="SuspensionProviderInfo.HeldProviderTypes"/>: a metadata
///   provider's jobs and a release provider's own job.
/// </summary>
/// <remarks>
///   The held jobs carry <see cref="ProviderJobAttribute"/>, which attaches the
///   filter to their pools.
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
            .Select(type => (JobType: type, ProviderType: ProviderJobs.GetProviderType(type)))
            .Where(pair => pair.ProviderType is not null)
            .GroupBy(pair => pair.ProviderType!)
            .ToDictionary(group => group.Key, group => group.Select(pair => pair.JobType).Distinct().ToArray());
        _suspensionService.SuspensionChanged += OnSuspensionChanged;
        Update();
    }

    /// <inheritdoc />
    public void Dispose()
        => _suspensionService.SuspensionChanged -= OnSuspensionChanged;

    /// <inheritdoc />
    public Type? WatchedAttributeType => typeof(ProviderJobAttribute);

    /// <inheritdoc />
    public IEnumerable<Type> GetTypesToExclude() => _excluded;

    /// <inheritdoc />
    public event EventHandler? StateChanged;

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
