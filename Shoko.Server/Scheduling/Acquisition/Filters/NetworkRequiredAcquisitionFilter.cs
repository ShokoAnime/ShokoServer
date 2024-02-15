using System;
using System.Collections.Generic;
using System.Linq;
using Quartz;
using Quartz.Util;
using Shoko.Server.Scheduling.Acquisition.Attributes;
using Shoko.Server.Services.Connectivity;

namespace Shoko.Server.Scheduling.Acquisition.Filters;

public class NetworkRequiredAcquisitionFilter : IAcquisitionFilter
{
    private readonly Type[] _types;
    private readonly IConnectivityMonitor[] _connectivityMonitors;

    public NetworkRequiredAcquisitionFilter(IEnumerable<IConnectivityMonitor> connectivityMonitors)
    {
        _connectivityMonitors = connectivityMonitors.ToArray();
        foreach (var monitor in _connectivityMonitors)
        {
            monitor.StateChanged += MonitorOnStateChanged;
        }
        _types = AppDomain.CurrentDomain.GetAssemblies().SelectMany(a => a.GetTypes()).Where(a =>
            typeof(IJob).IsAssignableFrom(a) && !a.IsAbstract && ObjectUtils.IsAttributePresent(a, typeof(NetworkRequiredAttribute))).ToArray();
    }

    ~NetworkRequiredAcquisitionFilter()
    {
        foreach (var monitor in _connectivityMonitors)
        {
            monitor.StateChanged -= MonitorOnStateChanged;
        }
    }

    private void MonitorOnStateChanged(object sender, EventArgs e)
    {
        StateChanged?.Invoke(null, EventArgs.Empty);
    }

    public IEnumerable<Type> GetTypesToExclude() => _connectivityMonitors.Any(a => a.HasConnected) ? Array.Empty<Type>() : _types;
    public event EventHandler StateChanged;
}
