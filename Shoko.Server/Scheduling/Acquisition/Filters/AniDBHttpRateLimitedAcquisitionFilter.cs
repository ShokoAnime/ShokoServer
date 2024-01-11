using System;
using System.Linq;
using Quartz;
using Quartz.Util;
using Shoko.Server.Providers.AniDB.Interfaces;
using Shoko.Server.Scheduling.Acquisition.Attributes;

namespace Shoko.Server.Scheduling.Acquisition.Filters;

public class AniDBHttpRateLimitedAcquisitionFilter : IAcquisitionFilter
{
    private readonly Type[] _types;
    private readonly IHttpConnectionHandler _connectionHandler;

    public AniDBHttpRateLimitedAcquisitionFilter(IHttpConnectionHandler connectionHandler)
    {
        _connectionHandler = connectionHandler;
        _types = AppDomain.CurrentDomain.GetAssemblies().SelectMany(a => a.GetTypes()).Where(a =>
            typeof(IJob).IsAssignableFrom(a) && !a.IsAbstract && ObjectUtils.IsAttributePresent(a, typeof(AniDBHttpRateLimitedAttribute))).ToArray();
    }

    public Type[] GetTypesToExclude() => _connectionHandler.IsBanned ? _types : Array.Empty<Type>();
}
