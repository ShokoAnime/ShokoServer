using System;
using System.Collections.Generic;
using System.Linq;
using Shoko.Abstractions.Connectivity.Enums;
using Shoko.Abstractions.Connectivity.Services;
using Shoko.QueueProcessor.Abstractions;
using Shoko.QueueProcessor.Acquisition.Attributes;

namespace Shoko.QueueProcessor.Acquisition.Filters;

public class NetworkRequiredAcquisitionFilter : IAcquisitionFilter
{
    private readonly Type[] _types;
    private readonly IConnectivityService _connectivityService;

    public NetworkRequiredAcquisitionFilter(IConnectivityService connectivityService)
        : this(connectivityService, null) { }

    /// <param name="connectivityService">Tells the filter when the network comes and goes.</param>
    /// <param name="jobTypeRegistry">
    /// The registered job types, which add the closed generic job types no assembly scan finds.
    /// </param>
    public NetworkRequiredAcquisitionFilter(IConnectivityService connectivityService, QueueJobTypeRegistry? jobTypeRegistry)
    {
        _connectivityService = connectivityService;
        _connectivityService.NetworkAvailabilityChanged += OnNetworkAvailabilityChanged;
        // Use OfType<NetworkRequiredAttribute>() rather than IsDefined so that subclasses
        // of NetworkRequiredAttribute (e.g. AniDBHttpRateLimitedAttribute) are also matched.
        // Skipping runtime-emitted assemblies: `GetTypes()` throws on one still being written to,
        // and no job type is ever emitted at runtime.
        _types = AppDomain.CurrentDomain.GetAssemblies()
            .Where(a => !a.IsDynamic)
            .SelectMany(a => a.GetTypes())
            .Concat(jobTypeRegistry?.JobTypes.Where(type => type.IsConstructedGenericType) ?? [])
            .Where(a => typeof(IQueueJob).IsAssignableFrom(a) && !a.IsAbstract && !a.ContainsGenericParameters &&
                        a.GetCustomAttributes(inherit: true).OfType<NetworkRequiredAttribute>().Any())
            .Distinct()
            .ToArray();
    }

    ~NetworkRequiredAcquisitionFilter() => _connectivityService.NetworkAvailabilityChanged -= OnNetworkAvailabilityChanged;

    public Type? WatchedAttributeType => typeof(NetworkRequiredAttribute);

    private void OnNetworkAvailabilityChanged(object? sender, EventArgs e) => StateChanged?.Invoke(null, EventArgs.Empty);

    public IEnumerable<Type> GetTypesToExclude() =>
        _connectivityService.NetworkAvailability >= NetworkAvailability.PartialInternet ? [] : _types;

    public event EventHandler? StateChanged;
}
