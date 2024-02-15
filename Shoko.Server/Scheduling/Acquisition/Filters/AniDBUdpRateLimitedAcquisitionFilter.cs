using System;
using System.Collections.Generic;
using System.Linq;
using Quartz;
using Quartz.Util;
using Shoko.Server.Providers.AniDB;
using Shoko.Server.Providers.AniDB.Interfaces;
using Shoko.Server.Scheduling.Acquisition.Attributes;

namespace Shoko.Server.Scheduling.Acquisition.Filters;

public class AniDBUdpRateLimitedAcquisitionFilter : IAcquisitionFilter
{
    private readonly Type[] _types;
    private readonly IUDPConnectionHandler _connectionHandler;

    public AniDBUdpRateLimitedAcquisitionFilter(IUDPConnectionHandler connectionHandler)
    {
        _connectionHandler = connectionHandler;
        _connectionHandler.AniDBStateUpdate += OnAniDBStateUpdate;
        _types = AppDomain.CurrentDomain.GetAssemblies().SelectMany(a => a.GetTypes()).Where(a =>
            typeof(IJob).IsAssignableFrom(a) && !a.IsAbstract && ObjectUtils.IsAttributePresent(a, typeof(AniDBUdpRateLimitedAttribute))).ToArray();
    }

    ~AniDBUdpRateLimitedAcquisitionFilter()
    {
        _connectionHandler.AniDBStateUpdate -= OnAniDBStateUpdate;
    }

    private void OnAniDBStateUpdate(object sender, AniDBStateUpdate e)
    {
        StateChanged?.Invoke(null, EventArgs.Empty);
    }

    public IEnumerable<Type> GetTypesToExclude() => _connectionHandler.IsBanned || _connectionHandler.IsInvalidSession ? _types : Array.Empty<Type>();
    public event EventHandler StateChanged;
}
