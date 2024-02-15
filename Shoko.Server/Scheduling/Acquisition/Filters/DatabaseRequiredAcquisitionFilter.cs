using System;
using System.Collections.Generic;
using System.Linq;
using Quartz;
using Quartz.Util;
using Shoko.Server.Scheduling.Acquisition.Attributes;
using Shoko.Server.Server;

namespace Shoko.Server.Scheduling.Acquisition.Filters;

public class DatabaseRequiredAcquisitionFilter : IAcquisitionFilter
{
    private readonly ShokoServer _server;
    private readonly Type[] _types;

    public DatabaseRequiredAcquisitionFilter(ShokoServer server)
    {
        _server = server;
        _server.DBSetupCompleted += ServerOnDBSetupCompleted;
        _types = AppDomain.CurrentDomain.GetAssemblies().SelectMany(a => a.GetTypes()).Where(a =>
            typeof(IJob).IsAssignableFrom(a) && !a.IsAbstract && ObjectUtils.IsAttributePresent(a, typeof(DatabaseRequiredAttribute))).ToArray();
    }

    ~DatabaseRequiredAcquisitionFilter()
    {
        _server.DBSetupCompleted -= ServerOnDBSetupCompleted;
    }

    private void ServerOnDBSetupCompleted(object sender, EventArgs e)
    {
        StateChanged?.Invoke(null, EventArgs.Empty);
    }

    public IEnumerable<Type> GetTypesToExclude() => ServerState.Instance.ServerOnline && !ServerState.Instance.DatabaseBlocked.Blocked ? Array.Empty<Type>() : _types;
    public event EventHandler StateChanged;
}
