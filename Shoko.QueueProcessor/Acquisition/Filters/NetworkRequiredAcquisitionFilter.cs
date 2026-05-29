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
    {
        _connectivityService = connectivityService;
        _connectivityService.NetworkAvailabilityChanged += OnNetworkAvailabilityChanged;
        // Use OfType<NetworkRequiredAttribute>() rather than IsDefined so that subclasses
        // of NetworkRequiredAttribute (e.g. AniDBHttpRateLimitedAttribute) are also matched.
        _types = AppDomain.CurrentDomain.GetAssemblies()
            .SelectMany(a => a.GetTypes())
            .Where(a => typeof(IQueueJob).IsAssignableFrom(a) && !a.IsAbstract &&
                        a.GetCustomAttributes(inherit: true).OfType<NetworkRequiredAttribute>().Any())
            .ToArray();
    }

    ~NetworkRequiredAcquisitionFilter() => _connectivityService.NetworkAvailabilityChanged -= OnNetworkAvailabilityChanged;

    public Type? WatchedAttributeType => typeof(NetworkRequiredAttribute);

    private void OnNetworkAvailabilityChanged(object? sender, EventArgs e) => StateChanged?.Invoke(null, EventArgs.Empty);

    public IEnumerable<Type> GetTypesToExclude() =>
        _connectivityService.NetworkAvailability >= NetworkAvailability.PartialInternet ? [] : _types;

    public event EventHandler? StateChanged;
}
