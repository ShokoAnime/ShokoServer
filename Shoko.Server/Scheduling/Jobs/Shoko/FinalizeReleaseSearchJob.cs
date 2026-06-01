using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Shoko.Abstractions.Extensions;
using Shoko.Abstractions.Video.Services;
using Shoko.QueueProcessor.Acquisition.Attributes;
using Shoko.QueueProcessor.Builder;
using Shoko.Server.Models.Shoko;
using Shoko.Server.Repositories.Cached;
using Shoko.Server.Services;

#pragma warning disable CS8618
#nullable enable
namespace Shoko.Server.Scheduling.Jobs.Shoko;

/// <summary>
/// Fires the <see cref="IVideoReleaseService.SearchCompleted"/> event at the end of a provider
/// job chain. Always appended as the last entry in every chain built by
/// <see cref="VideoReleaseService.DispatchProviderJobsForVideo"/>.
/// </summary>
[DatabaseRequired]
[JobKeyGroup(JobKeyGroup.Import)]
public class FinalizeReleaseSearchJob : BaseJob
{
    private readonly IVideoReleaseService _videoReleaseService;
    private readonly VideoLocalRepository _videoLocals;

    private VideoLocal? _vlocal;

    public int VideoLocalID { get; set; }

    public bool AddToMyList { get; set; }

    public bool IsAutomatic { get; set; }

    /// <summary>Included in the job key so that multiple finalization jobs for the same video (from separate chains) are not deduped.</summary>
    [JobKeyMember]
    public DateTimeOffset SearchStartedAt { get; set; }

    public Guid[] AttemptedProviderIDs { get; set; } = [];

    public override string TypeName => "Finalize Release Search";

    public override string Title => "Finalizing Release Search";

    public override Dictionary<string, object> Details
    {
        get
        {
            var result = new Dictionary<string, object>();
            if (_vlocal?.FirstValidPlace?.Path is { } path)
                result["File Path"] = VideoService.GetDistinctPath(path);
            else
                result["Video"] = VideoLocalID;
            return result;
        }
    }

    public override void PostInit()
    {
        _vlocal = _videoLocals.GetByID(VideoLocalID);
    }

    public override Task Execute()
    {
        _logger.LogTrace("Finalizing release search for VideoLocalID={VideoLocalID}", VideoLocalID);

        var video = _vlocal ?? (_vlocal = _videoLocals.GetByID(VideoLocalID));
        if (video is null) return Task.CompletedTask;

        var currentRelease = _videoReleaseService.GetCurrentReleaseForVideo(video);
        var attemptedProviders = AttemptedProviderIDs
            .Select(id => _videoReleaseService.GetProviderInfo(id))
            .WhereNotNull()
            .ToList();

        var selectedProvider = currentRelease is not null
            ? attemptedProviders.FirstOrDefault(p =>
                currentRelease.ProviderName.Split('+', StringSplitOptions.RemoveEmptyEntries)
                    .Contains(p.Name, StringComparer.OrdinalIgnoreCase))
            : null;

        _videoReleaseService.FireSearchCompleted(video, currentRelease, AddToMyList, IsAutomatic,
            SearchStartedAt.DateTime, attemptedProviders, selectedProvider);

        return Task.CompletedTask;
    }

    public FinalizeReleaseSearchJob(IVideoReleaseService videoReleaseService, VideoLocalRepository videoLocals)
    {
        _videoReleaseService = videoReleaseService;
        _videoLocals = videoLocals;
    }

    protected FinalizeReleaseSearchJob() { }
}
