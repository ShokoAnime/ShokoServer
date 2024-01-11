using System;
using System.Linq;
using Quartz;
using Quartz.Util;
using Shoko.Server.Scheduling.Acquisition.Attributes;
using Shoko.Server.Server;

namespace Shoko.Server.Scheduling.Acquisition.Filters;

public class DatabaseRequiredAcquisitionFilter : IAcquisitionFilter
{
    private readonly Type[] _types;

    public DatabaseRequiredAcquisitionFilter()
    {
        _types = AppDomain.CurrentDomain.GetAssemblies().SelectMany(a => a.GetTypes()).Where(a =>
            typeof(IJob).IsAssignableFrom(a) && !a.IsAbstract && ObjectUtils.IsAttributePresent(a, typeof(DatabaseRequiredAttribute))).ToArray();
    }

    public Type[] GetTypesToExclude() => ServerState.Instance.ServerOnline && !ServerState.Instance.DatabaseBlocked.Blocked ? Array.Empty<Type>() : _types;
}
