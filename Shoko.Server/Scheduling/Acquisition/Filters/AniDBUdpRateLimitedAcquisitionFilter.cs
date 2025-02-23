using System;
using System.Collections.Generic;
using System.Linq;
using Quartz;
using Quartz.Util;
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

    private readonly IUDPConnectionHandler _connectionHandler;

    private readonly IVideoReleaseService _videoReleaseService;

    public AniDBUdpRateLimitedAcquisitionFilter(IUDPConnectionHandler connectionHandler, IVideoReleaseService videoReleaseService)
    {
        _connectionHandler = connectionHandler;
        _connectionHandler.AniDBStateUpdate += OnAniDBStateUpdate;
        _videoReleaseService = videoReleaseService;
        _videoReleaseService.ProvidersUpdated += OnProvidersUpdated;
        _processJobIncluded = _videoReleaseService.GetAvailableProviders().Any(a => a.Provider.Name is "AniDB");

        _typesWithProcessJob = AppDomain.CurrentDomain.GetAssemblies().SelectMany(a => a.GetTypes()).Where(a =>
            typeof(IJob).IsAssignableFrom(a) && !a.IsAbstract && ObjectUtils.IsAttributePresent(a, typeof(AniDBUdpRateLimitedAttribute))).ToArray();
        _typesWithoutProcessJob = _typesWithProcessJob.Where(a => !typeof(ProcessFileJob).IsAssignableFrom(a)).ToArray();
    }

    ~AniDBUdpRateLimitedAcquisitionFilter()
    {
        _connectionHandler.AniDBStateUpdate -= OnAniDBStateUpdate;
        _videoReleaseService.ProvidersUpdated -= OnProvidersUpdated;
    }

    private void OnProvidersUpdated(object sender, EventArgs e)
    {
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
