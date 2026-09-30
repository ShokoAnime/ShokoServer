using System;
using System.Collections.Generic;
using System.Linq;
using Shoko.QueueProcessor;
using Shoko.QueueProcessor.Abstractions;
using Shoko.Server.Scheduling.Acquisition.Attributes;
using Shoko.Server.Scheduling.Jobs.Metadata;
using Shoko.Server.Services;

namespace Shoko.Server.Scheduling.Acquisition.Filters;

/// <summary>
/// Holds back a metadata provider's own jobs while it reports it is paused, through
/// <see cref="Abstractions.Metadata.Providers.IPausableMetadataProvider"/>. Every other provider's
/// jobs keep running.
/// </summary>
public class MetadataProviderPausedAcquisitionFilter : IAcquisitionFilter, IDisposable
{
    private readonly IMetadataProviderPauseState _pauseState;

    private readonly IReadOnlyDictionary<Type, Type[]> _jobTypesByProvider;

    private Type[] _excluded = [];

    /// <param name="providerManager">Tells the filter which providers are paused.</param>
    /// <param name="jobTypeRegistry">The registered job types, the provider jobs among them.</param>
    public MetadataProviderPausedAcquisitionFilter(MetadataProviderManager providerManager, QueueJobTypeRegistry jobTypeRegistry)
        : this((IMetadataProviderPauseState)providerManager, jobTypeRegistry.JobTypes) { }

    /// <param name="pauseState">Tells the filter which providers are paused.</param>
    /// <param name="jobTypes">The registered job types, the provider jobs among them.</param>
    internal MetadataProviderPausedAcquisitionFilter(IMetadataProviderPauseState pauseState, IEnumerable<Type> jobTypes)
    {
        _pauseState = pauseState;
        _jobTypesByProvider = jobTypes
            .Select(type => (JobType: type, ProviderType: MetadataProviderJobs.GetProviderType(type)))
            .Where(pair => pair.ProviderType is not null)
            .GroupBy(pair => pair.ProviderType!)
            .ToDictionary(group => group.Key, group => group.Select(pair => pair.JobType).Distinct().ToArray());
        _pauseState.PausedProvidersChanged += OnPausedProvidersChanged;
        Update();
    }

    public void Dispose()
    {
        _pauseState.PausedProvidersChanged -= OnPausedProvidersChanged;
        GC.SuppressFinalize(this);
    }

    public Type? WatchedAttributeType => typeof(MetadataProviderJobAttribute);

    public IEnumerable<Type> GetTypesToExclude() => _excluded;

    public event EventHandler? StateChanged;

    private void OnPausedProvidersChanged(object? sender, EventArgs e)
    {
        Update();
        StateChanged?.Invoke(this, EventArgs.Empty);
    }

    private void Update()
        => _excluded = [.. _pauseState.GetPausedProviderTypes()
            .SelectMany(providerType => _jobTypesByProvider.TryGetValue(providerType, out var jobTypes) ? jobTypes : [])];
}
