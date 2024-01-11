using System;
using System.Linq;
using Quartz;
using Quartz.Util;
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
        _types = AppDomain.CurrentDomain.GetAssemblies().SelectMany(a => a.GetTypes()).Where(a =>
            typeof(IJob).IsAssignableFrom(a) && !a.IsAbstract && ObjectUtils.IsAttributePresent(a, typeof(AniDBUdpRateLimitedAttribute))).ToArray();
    }

    public Type[] GetTypesToExclude() => _connectionHandler.IsBanned || _connectionHandler.IsInvalidSession ? _types : Array.Empty<Type>();
}
