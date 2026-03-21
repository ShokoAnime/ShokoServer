using System;
using System.Collections.Generic;
using System.Linq;
using Quartz;
using Quartz.Util;
using Shoko.Abstractions.Connectivity.Enums;
using Shoko.Abstractions.Connectivity.Services;
using Shoko.Server.Scheduling.Acquisition.Attributes;

#nullable enable
namespace Shoko.Server.Scheduling.Acquisition.Filters;

public class NetworkRequiredAcquisitionFilter : IAcquisitionFilter
{
    private readonly Type[] _types;
    private readonly IConnectivityService _connectivityService;

    public NetworkRequiredAcquisitionFilter(IConnectivityService connectivityService)
    {
        _connectivityService = connectivityService;
        _connectivityService.NetworkAvailabilityChanged += OnNetworkAvailabilityChanged;
        _types = AppDomain.CurrentDomain.GetAssemblies().SelectMany(a => a.GetTypes()).Where(a =>
            typeof(IJob).IsAssignableFrom(a) && !a.IsAbstract && ObjectUtils.IsAttributePresent(a, typeof(NetworkRequiredAttribute))).ToArray();
    }

    ~NetworkRequiredAcquisitionFilter()
    {
        _connectivityService.NetworkAvailabilityChanged -= OnNetworkAvailabilityChanged;
    }

    private void OnNetworkAvailabilityChanged(object? sender, EventArgs e)
    {
        StateChanged?.Invoke(null, EventArgs.Empty);
    }

    public IEnumerable<Type> GetTypesToExclude() =>
        _connectivityService.NetworkAvailability >= NetworkAvailability.PartialInternet
            ? []
            : _types;

    public event EventHandler? StateChanged;
}
