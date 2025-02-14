using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Quartz;
using Shoko.Plugin.Abstractions.Release;
using Shoko.Plugin.Abstractions.Services;
using Shoko.Server.Extensions;
using Shoko.Server.Models;
using Shoko.Server.Providers.AniDB.Interfaces;
using Shoko.Server.Repositories;
using Shoko.Server.Scheduling.Acquisition.Attributes;
using Shoko.Server.Scheduling.Attributes;
using Shoko.Server.Scheduling.Concurrency;
using Shoko.Server.Scheduling.Jobs.AniDB;
using Shoko.Server.Settings;
using Shoko.Server.Utilities;

#pragma warning disable CS8618
#nullable enable
namespace Shoko.Server.Scheduling.Jobs.Shoko;

[DatabaseRequired]
[AniDBUdpRateLimited]
[DisallowConcurrencyGroup(ConcurrencyGroups.AniDB_UDP)]
[JobKeyGroup(JobKeyGroup.Import)]
public class ProcessFileJob : BaseJob
{
    private readonly ISchedulerFactory _schedulerFactory;
    private readonly IServerSettings _settings;
    private readonly IUDPConnectionHandler _udpConnectionHandler;
    private readonly IVideoReleaseService _videoReleaseService;

    private SVR_VideoLocal _vlocal;
    private string _fileName;

    public int VideoLocalID { get; set; }

    public bool ForceRecheck { get; set; }

    public bool SkipMyList { get; set; }

    public override string TypeName => "Get Cross-References for File";

    public override string Title => "Getting Cross-References for File";

    public override Dictionary<string, object> Details => new()
    {
        { "File Path", _fileName ?? VideoLocalID.ToString() }
    };

    public override void PostInit()
    {
        _vlocal = RepoFactory.VideoLocal.GetByID(VideoLocalID);
        if (_vlocal == null) throw new JobExecutionException($"VideoLocal not Found: {VideoLocalID}");
        _fileName = Utils.GetDistinctPath(_vlocal?.FirstValidPlace?.FullServerPath);
    }

    public override async Task Process()
    {
        _logger.LogInformation("Processing {Job}: {FileName}", nameof(ProcessFileJob), _fileName ?? VideoLocalID.ToString());

        // Check if the video local (file) is available.
        if (_vlocal == null)
        {
            _vlocal = RepoFactory.VideoLocal.GetByID(VideoLocalID);
            if (_vlocal == null)
                return;
        }

        // Store a hash-set of the old cross-references for comparison later.
        var oldXRefs = _vlocal.EpisodeCrossReferences
            .Select(xref => xref.ToString())
            .Join(',');

        // Process and get the AniDB file entry.
        var releaseInfo = await GetReleaseInfo().ConfigureAwait(false);

        // Check if an AniDB file is now available and if the cross-references changed.
        var newXRefs = _vlocal.EpisodeCrossReferences
            .Select(xref => xref.ToString())
            .Join(',');
        var xRefsMatch = newXRefs == oldXRefs;
        // Fire the file matched event on first import and any later scans where the xrefs changed
        if (releaseInfo is not null && !string.IsNullOrEmpty(newXRefs) && (!_vlocal.DateTimeImported.HasValue || !xRefsMatch))
        {
            // Set/update the import date
            _vlocal.DateTimeImported = DateTime.Now;
            RepoFactory.VideoLocal.Save(_vlocal);

            // Dispatch the on file matched event.
            ShokoEventHandler.Instance.OnFileMatched(_vlocal.FirstValidPlace!, _vlocal);
        }
        // Fire the file not matched event if we didn't update the cross-references.
        else
        {
            var autoMatchAttempts = RepoFactory.AniDB_FileUpdate.GetByFileSizeAndHash(_vlocal.FileSize, _vlocal.Hash).Count;
            var hasXRefs = !string.IsNullOrEmpty(newXRefs) && xRefsMatch;
            var isUDPBanned = _udpConnectionHandler.IsBanned;
            ShokoEventHandler.Instance.OnFileNotMatched(_vlocal.FirstValidPlace!, _vlocal, autoMatchAttempts, hasXRefs, isUDPBanned);
        }

        // Rename and/or move the physical file(s) if needed.
        if (_settings.Plugins.Renamer.RelocateOnImport)
        {
            var scheduler = await _schedulerFactory.GetScheduler().ConfigureAwait(false);
            await scheduler.StartJob<RenameMoveFileJob>(job => job.VideoLocalID = _vlocal.VideoLocalID).ConfigureAwait(false);
        }
    }

    private async Task<IReleaseInfo?> GetReleaseInfo()
    {
        if (!ForceRecheck && _videoReleaseService.GetCurrentReleaseForVideo(_vlocal) is { } currentRelease)
            return currentRelease;

        return await _videoReleaseService.FindReleaseForVideo(_vlocal);
    }


    public ProcessFileJob(
        ISettingsProvider settingsProvider,
        ISchedulerFactory schedulerFactory,
        IUDPConnectionHandler udpConnectionHandler,
        IVideoReleaseService videoReleaseService
    )
    {
        _schedulerFactory = schedulerFactory;
        _settings = settingsProvider.GetSettings();
        _udpConnectionHandler = udpConnectionHandler;
        _videoReleaseService = videoReleaseService;
    }

    protected ProcessFileJob() { }
}
