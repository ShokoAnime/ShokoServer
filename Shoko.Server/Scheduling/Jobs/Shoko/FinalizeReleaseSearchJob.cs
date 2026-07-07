using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Shoko.Abstractions.Extensions;
using Shoko.Abstractions.Video.Services;
using Shoko.QueueProcessor.Acquisition.Attributes;
using Shoko.QueueProcessor.Builder;
using Shoko.Server.Models.Release;
using Shoko.Server.Models.Shoko;
using Shoko.Server.Repositories.Cached;
using Shoko.Server.Services;

namespace Shoko.Server.Scheduling.Jobs.Shoko;

/// <summary>
/// Runs auto-management, fires <see cref="IVideoReleaseService.SearchCompleted"/>, and
/// schedules post-import actions (relocation) at the end of every provider job chain.
/// Always appended as the last entry in every chain built by <see cref="VideoReleaseService"/>.
/// </summary>
[DatabaseRequired]
[JobKeyGroup(JobKeyGroup.Import)]
public class FinalizeReleaseSearchJob(IVideoReleaseService videoReleaseService, VideoLocalRepository videoLocals, IVideoRelocationService relocationService, StoredReleaseInfo_MatchAttemptRepository matchAttempts) : BaseJob
{
    private readonly VideoReleaseService _videoReleaseService = (VideoReleaseService)videoReleaseService;

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
        _vlocal = videoLocals.GetByID(VideoLocalID);
        _matchAttempt = matchAttempts.GetByID(MatchAttemptID);
        if (_vlocal is not null && _matchAttempt is not null)
            _attemptNumber = matchAttempts.GetByEd2kAndFileSize(_vlocal.Hash, _vlocal.FileSize)
                .FindIndex(m => m.StoredReleaseInfo_MatchAttemptID == MatchAttemptID) + 1;
        _fileName = VideoService.GetDistinctPath(_vlocal?.FirstValidPlace?.Path);
    }

    public override async Task Execute()
    {
        _logger.LogTrace("Finalizing release search for VideoLocalID={VideoLocalID}", VideoLocalID);

        _vlocal ??= videoLocals.GetByID(VideoLocalID);
        if (_vlocal is null) return;
        if (matchAttempts.GetByID(MatchAttemptID) is not { } matchAttempt) return;

        // Mark the chain as completed. AttemptEndedAt may already be set if a
        // non-deferred save occurred; set it here for the no-match case.
        var releaseFound = matchAttempt.IsSuccessful;
        if (!releaseFound)
            matchAttempt.AttemptEndedAt = DateTime.Now;
        matchAttempt.IsCompleted = true;
        matchAttempts.Save(matchAttempt);

        // Fire SearchCompleted now that auto-management has run. IsCancelled lets subscribers
        // (plugins, internal handlers) skip provider-specific post-import work.
        var args = await _videoReleaseService.FireSearchCompleted(_vlocal, matchAttempt);
        if (args.IsCancelled)
            return;

        // Trigger relocation if requested.
        if (ShouldRelocate)
            await relocationService.ScheduleAutoRelocationForVideo(_vlocal);
    }

}
