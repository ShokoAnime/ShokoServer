#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using Shoko.QueueProcessor.Abstractions;
using Shoko.Server.Providers.AniDB;
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
        _connectionHandler.AniDBStateUpdate += OnAniDBStateUpdate;
        _types = AppDomain.CurrentDomain.GetAssemblies()
            .SelectMany(a => a.GetTypes())
            .Where(a => typeof(IQueueJob).IsAssignableFrom(a) && !a.IsAbstract &&
                        a.IsDefined(typeof(AniDBHttpRateLimitedAttribute), true))
            .ToArray();
    }

    ~AniDBHttpRateLimitedAcquisitionFilter() => _connectionHandler.AniDBStateUpdate -= OnAniDBStateUpdate;

    public Type? WatchedAttributeType => typeof(AniDBHttpRateLimitedAttribute);

    private void OnAniDBStateUpdate(object? sender, AniDBStateUpdate e) => StateChanged?.Invoke(null, EventArgs.Empty);

    public IEnumerable<Type> GetTypesToExclude() =>
        !_connectionHandler.IsAlive || _connectionHandler.IsBanned ? _types : [];

    public event EventHandler? StateChanged;
}
