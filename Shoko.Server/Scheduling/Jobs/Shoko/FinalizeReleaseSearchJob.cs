using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Shoko.Abstractions.Video.Services;
using Shoko.QueueProcessor.Acquisition.Attributes;
using Shoko.QueueProcessor.Builder;
using Shoko.Server.Models.Release;
using Shoko.Server.Models.Shoko;
using Shoko.Server.Repositories.Cached;
using Shoko.Server.Services;

#pragma warning disable CS8618
#nullable enable
namespace Shoko.Server.Scheduling.Jobs.Shoko;

/// <summary>
/// Runs auto-management, fires <see cref="IVideoReleaseService.SearchCompleted"/>, and
/// schedules post-import actions (relocation) at the end of every provider job chain.
/// Always appended as the last entry in every chain built by <see cref="VideoReleaseService"/>.
/// </summary>
[DatabaseRequired]
[JobKeyGroup(JobKeyGroup.Import)]
public class FinalizeReleaseSearchJob : BaseJob
{
    private readonly VideoReleaseService _videoReleaseService;

    private readonly VideoLocalRepository _videoLocals;

    private readonly IVideoRelocationService _relocationService;

    private readonly StoredReleaseInfo_MatchAttemptRepository _matchAttempts;

    private readonly ReleaseAutoManagementService _releaseAutoManagement;

    private VideoLocal? _vlocal;

    private StoredReleaseInfo_MatchAttempt? _matchAttempt;

    private int? _attemptNumber;

    private string? _fileName;

    public int VideoLocalID { get; set; }

    public int MatchAttemptID { get; set; }

    public bool ShouldRelocate { get; set; }

    public override string TypeName => "Finalize Release Search";

    public override string Title => "Finalizing Release Search";

    public override Dictionary<string, object> Details
    {
        get
        {
            var result = new Dictionary<string, object>();
            if (_attemptNumber.HasValue)
                result["Attempt Number"] = _attemptNumber;
            if (string.IsNullOrEmpty(_fileName))
                result["Video"] = VideoLocalID;
            else
                result["File Path"] = _fileName;
            return result;
        }
    }

    public override void PostInit()
    {
        _vlocal = _videoLocals.GetByID(VideoLocalID);
        _matchAttempt = _matchAttempts.GetByID(MatchAttemptID);
        if (_vlocal is not null && _matchAttempt is not null)
        {
            var allAttempts = _matchAttempts.GetByEd2kAndFileSize(_vlocal.Hash, _vlocal.FileSize).ToList();
            var idx = allAttempts.FindIndex(m => m.StoredReleaseInfo_MatchAttemptID == MatchAttemptID);
            if (idx >= 0) _attemptNumber = idx + 1;
        }
        _fileName = VideoService.GetDistinctPath(_vlocal?.FirstValidPlace?.Path);
    }

    public override async Task Execute()
    {
        _logger.LogTrace("Finalizing release search for VideoLocalID={VideoLocalID}", VideoLocalID);

        _vlocal ??= _videoLocals.GetByID(VideoLocalID);
        if (_vlocal is null) return;
        if (_matchAttempts.GetByID(MatchAttemptID) is not { } matchAttempt) return;

        // Mark the chain as completed. AttemptEndedAt may already be set if a
        // non-deferred save occurred; set it here for the no-match case.
        var releaseFound = matchAttempt.IsSuccessful;
        if (!releaseFound)
            matchAttempt.AttemptEndedAt = DateTime.Now;
        matchAttempt.IsCompleted = true;
        _matchAttempts.Save(matchAttempt);

        // Run auto-management before any post-import actions so we know whether the
        // incoming file itself was identified as redundant and deleted.
        var incomingDeleted = await _releaseAutoManagement.CheckAndAutoManage(_vlocal);

        // Fire SearchCompleted now that auto-management has run. IsCancelled lets subscribers
        // (plugins, internal handlers) skip provider-specific post-import work.
        var completedArgs = _videoReleaseService.FireSearchCompleted(_vlocal, matchAttempt, isCancelled: incomingDeleted);

        if (incomingDeleted)
            return;

        // Call the winning provider's post-import hook (e.g. AniDB MyList sync).
        if (releaseFound && completedArgs.SelectedProvider is { } selectedProvider)
            await selectedProvider.Provider.OnSearchCompleted(completedArgs);

        // Trigger relocation if requested.
        if (ShouldRelocate)
            await _relocationService.ScheduleAutoRelocationForVideo(_vlocal);
    }

    public FinalizeReleaseSearchJob(
        IVideoReleaseService videoReleaseService,
        VideoLocalRepository videoLocals,
        IVideoRelocationService relocationService,
        StoredReleaseInfo_MatchAttemptRepository matchAttempts,
        ReleaseAutoManagementService releaseAutoManagement
    )
    {
        _videoReleaseService = (VideoReleaseService)videoReleaseService;
        _videoLocals = videoLocals;
        _relocationService = relocationService;
        _matchAttempts = matchAttempts;
        _releaseAutoManagement = releaseAutoManagement;
    }

    protected FinalizeReleaseSearchJob() { }
}
