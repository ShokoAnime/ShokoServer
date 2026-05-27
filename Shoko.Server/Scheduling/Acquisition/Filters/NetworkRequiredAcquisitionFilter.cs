#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using Shoko.Abstractions.Connectivity.Enums;
using Shoko.Abstractions.Connectivity.Services;
using Shoko.QueueProcessor.Abstractions;
using Shoko.Server.Scheduling.Acquisition.Attributes;

namespace Shoko.Server.Scheduling.Acquisition.Filters;

public class NetworkRequiredAcquisitionFilter : IAcquisitionFilter
{
    private readonly Type[] _types;
    private readonly IConnectivityService _connectivityService;

    public NetworkRequiredAcquisitionFilter(IConnectivityService connectivityService)
    {
        _connectivityService = connectivityService;
        _connectivityService.NetworkAvailabilityChanged += OnNetworkAvailabilityChanged;
        _types = AppDomain.CurrentDomain.GetAssemblies()
            .SelectMany(a => a.GetTypes())
            .Where(a => typeof(IQueueJob).IsAssignableFrom(a) && !a.IsAbstract &&
                        a.IsDefined(typeof(NetworkRequiredAttribute), true))
            .ToArray();
    }

    ~NetworkRequiredAcquisitionFilter() => _connectivityService.NetworkAvailabilityChanged -= OnNetworkAvailabilityChanged;

    public Type? WatchedAttributeType => null; // global filter — applies to Default pool

    private void OnNetworkAvailabilityChanged(object? sender, EventArgs e) => StateChanged?.Invoke(null, EventArgs.Empty);

    public IEnumerable<Type> GetTypesToExclude() =>
        _connectivityService.NetworkAvailability >= NetworkAvailability.PartialInternet ? [] : _types;

    public event EventHandler? StateChanged;
}
