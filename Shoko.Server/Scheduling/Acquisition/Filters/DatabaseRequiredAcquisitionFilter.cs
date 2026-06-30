using System;
using System.Collections.Generic;
using System.Linq;
using Shoko.Abstractions.Core.Services;
using Shoko.QueueProcessor.Abstractions;
using Shoko.QueueProcessor.Acquisition.Attributes;

namespace Shoko.Server.Scheduling.Acquisition.Filters;

public class DatabaseRequiredAcquisitionFilter : IAcquisitionFilter
{
    private readonly ISystemService _systemService;
    private readonly Type[] _types;

    public DatabaseRequiredAcquisitionFilter(ISystemService systemService)
    {
        _systemService = systemService;
        _systemService.DatabaseBlockedChanged += ServerOnDBSetupCompleted;
        _types = AppDomain.CurrentDomain.GetAssemblies()
            .SelectMany(a => a.GetTypes())
            .Where(a => typeof(IQueueJob).IsAssignableFrom(a) && !a.IsAbstract &&
                        a.IsDefined(typeof(DatabaseRequiredAttribute), true))
            .ToArray();
    }

    ~DatabaseRequiredAcquisitionFilter() => _systemService.DatabaseBlockedChanged -= ServerOnDBSetupCompleted;

    public Type? WatchedAttributeType => typeof(DatabaseRequiredAttribute);

    private void ServerOnDBSetupCompleted(object? sender, EventArgs e) => StateChanged?.Invoke(null, EventArgs.Empty);

    public IEnumerable<Type> GetTypesToExclude() => _systemService.IsDatabaseBlocked ? _types : [];

    public event EventHandler? StateChanged;
}
