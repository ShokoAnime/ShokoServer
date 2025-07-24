using System;
using System.Collections.Generic;
using System.Linq;
using Quartz;
using Quartz.Util;
using Shoko.Plugin.Abstractions;
using Shoko.Plugin.Abstractions.Services;
using Shoko.Server.Providers.AniDB;
using Shoko.Server.Providers.AniDB.Interfaces;
using Shoko.Server.Scheduling.Acquisition.Attributes;
using Shoko.Server.Scheduling.Jobs.Shoko;

namespace Shoko.Server.Scheduling.Acquisition.Filters;

public class AniDBUdpRateLimitedAcquisitionFilter : IAcquisitionFilter
{
    private readonly Type[] _typesWithoutProcessJob;

    private readonly Type[] _typesWithProcessJob;

    private bool _processJobIncluded;

    private bool _ready;

    private readonly IUDPConnectionHandler _connectionHandler;

    private readonly IShokoEventHandler _shokoEventHandler;

    private readonly IVideoReleaseService _videoReleaseService;

    public AniDBUdpRateLimitedAcquisitionFilter(IUDPConnectionHandler connectionHandler, IShokoEventHandler shokoEventHandler, IVideoReleaseService videoReleaseService)
    {
        _connectionHandler = connectionHandler;
        _shokoEventHandler = shokoEventHandler;
        _videoReleaseService = videoReleaseService;
        _connectionHandler.AniDBStateUpdate += OnAniDBStateUpdate;
        _shokoEventHandler.Started += OnProvidersReady;
        _videoReleaseService.ProvidersUpdated += OnProvidersUpdated;
        _processJobIncluded = true;
        _ready = false;

        _typesWithProcessJob = AppDomain.CurrentDomain.GetAssemblies().SelectMany(a => a.GetTypes()).Where(a =>
            typeof(IJob).IsAssignableFrom(a) && !a.IsAbstract && ObjectUtils.IsAttributePresent(a, typeof(AniDBUdpRateLimitedAttribute))).ToArray();
        _typesWithoutProcessJob = _typesWithProcessJob.Where(a => !typeof(ProcessFileJob).IsAssignableFrom(a)).ToArray();
    }

    ~AniDBUdpRateLimitedAcquisitionFilter()
    {
        _connectionHandler.AniDBStateUpdate -= OnAniDBStateUpdate;
        _shokoEventHandler.Started -= OnProvidersReady;
        _videoReleaseService.ProvidersUpdated -= OnProvidersUpdated;
    }

    private void OnProvidersReady(object sender, EventArgs e)
    {
        _ready = true;
        _processJobIncluded = _videoReleaseService.GetAvailableProviders().Any(a => a.Provider.Name is "AniDB");
        StateChanged?.Invoke(null, EventArgs.Empty);
    }

    private void OnProvidersUpdated(object sender, EventArgs e)
    {
        if (!_ready) return;
        _processJobIncluded = _videoReleaseService.GetAvailableProviders().Any(a => a.Provider.Name is "AniDB");
        StateChanged?.Invoke(null, EventArgs.Empty);
    }

    private void OnAniDBStateUpdate(object sender, AniDBStateUpdate e)
    {
        StateChanged?.Invoke(null, EventArgs.Empty);
    }

    public IEnumerable<Type> GetTypesToExclude() => !_connectionHandler.IsAlive || _connectionHandler.IsBanned || _connectionHandler.IsInvalidSession
        ? _processJobIncluded ? _typesWithProcessJob : _typesWithoutProcessJob
        : [];

    public event EventHandler StateChanged;
}
