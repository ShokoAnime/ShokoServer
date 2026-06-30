using System;
using System.Collections.Generic;
using System.Linq;
using Shoko.Abstractions.Core.Services;
using Shoko.QueueProcessor.Abstractions;
using Shoko.Server.Providers.AniDB;
using Shoko.Server.Providers.AniDB.Interfaces;
using Shoko.Server.Scheduling.Acquisition.Attributes;

namespace Shoko.Server.Scheduling.Acquisition.Filters;

public class AniDBUdpRateLimitedAcquisitionFilter : IAcquisitionFilter
{
    private readonly Type[] _types;
    private readonly IUDPConnectionHandler _connectionHandler;
    private readonly ISystemService _systemService;

    public AniDBUdpRateLimitedAcquisitionFilter(IUDPConnectionHandler connectionHandler, ISystemService systemService)
    {
        _connectionHandler = connectionHandler;
        _systemService = systemService;
        _connectionHandler.AniDBStateUpdate += OnAniDBStateUpdate;
        _systemService.AboutToStart += OnProvidersReady;
        _types = AppDomain.CurrentDomain.GetAssemblies()
            .SelectMany(a => a.GetTypes())
            .Where(a => typeof(IQueueJob).IsAssignableFrom(a) && !a.IsAbstract &&
                        a.GetCustomAttributes(inherit: true).OfType<AniDBUdpRateLimitedAttribute>().Any())
            .ToArray();
    }

    ~AniDBUdpRateLimitedAcquisitionFilter()
    {
        _connectionHandler.AniDBStateUpdate -= OnAniDBStateUpdate;
        _systemService.AboutToStart -= OnProvidersReady;
    }

    public Type? WatchedAttributeType => typeof(AniDBUdpRateLimitedAttribute);

    private void OnProvidersReady(object? sender, EventArgs e)
    {
        StateChanged?.Invoke(null, EventArgs.Empty);
    }

    private void OnAniDBStateUpdate(object? sender, AniDBStateUpdate e) => StateChanged?.Invoke(null, EventArgs.Empty);

    // Network availability is handled by NetworkRequiredAcquisitionFilter, which picks up
    // AniDB jobs automatically because AniDBUdpRateLimitedAttribute : NetworkRequiredAttribute.
    // This filter only gates on the AniDB-specific ban / session / connection state.
    public IEnumerable<Type> GetTypesToExclude() =>
        !_connectionHandler.IsAlive || _connectionHandler.IsBanned || _connectionHandler.IsInvalidSession || _connectionHandler.IsLoginFailed
            ? _types
            : [];

    public event EventHandler? StateChanged;
}
