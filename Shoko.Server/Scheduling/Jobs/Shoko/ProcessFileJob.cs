using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Shoko.Abstractions.Video.Services;
using Shoko.QueueProcessor.Acquisition.Attributes;
using Shoko.QueueProcessor.Builder;
using Shoko.Server.Models.Shoko;
using Shoko.Server.Repositories.Cached;
using Shoko.Server.Services;

#pragma warning disable CS8618
#nullable enable
namespace Shoko.Server.Scheduling.Jobs.Shoko;

[DatabaseRequired]
[JobKeyGroup(JobKeyGroup.Import)]
public class ProcessFileJob : BaseJob
{
    private readonly IVideoReleaseService _videoReleaseService;

    private readonly IVideoRelocationService _relocationService;

    private VideoLocal _vlocal;

    private string? _fileName;

    public int VideoLocalID { get; set; }

    public bool ForceRecheck { get; set; }

    public bool SkipMyList { get; set; }

    public bool ShouldRelocate { get; set; }

    public override string TypeName => "Get Release Information for Video";

    public override string Title => "Getting Release Information for Video";

    public override Dictionary<string, object> Details
    {
        get
        {
            var result = new Dictionary<string, object> { };
            if (string.IsNullOrEmpty(_fileName))
                result["Video"] = VideoLocalID;
            else
                result["File Path"] = _fileName;
            if (ForceRecheck) result["Force"] = true;
            if (!SkipMyList) result["Add to MyList"] = true;
            return result;
        }
    }

    public override void PostInit()
    {
        _vlocal = _videoLocals.GetByID(VideoLocalID);
        if (_vlocal == null) throw new Exception($"VideoLocal not Found: {VideoLocalID}");
        _fileName = VideoService.GetDistinctPath(_vlocal?.FirstValidPlace?.Path);
    }

    public override async Task Execute()
    {
        _logger.LogInformation("Processing {Job}: {FileName}", nameof(ProcessFileJob), _fileName ?? VideoLocalID.ToString());

        // Check if the video local (file) is available.
        if (_vlocal == null)
        {
            _vlocal = _videoLocals.GetByID(VideoLocalID);
            if (_vlocal == null)
                return;
        }

        // Dispatch provider jobs as a chain; each provider runs with its own rate limiting.
        if (ForceRecheck || _videoReleaseService.GetCurrentReleaseForVideo(_vlocal) is null)
            await _videoReleaseService.DispatchProviderJobsForVideo(_vlocal, addToMylist: !SkipMyList);

        if (ShouldRelocate)
            await _relocationService.ScheduleAutoRelocationForVideo(_vlocal);
    }


    private readonly VideoLocalRepository _videoLocals;
    public ProcessFileJob(IVideoReleaseService videoReleaseService, IVideoRelocationService relocationService,
        VideoLocalRepository videoLocals
    )
    {
        _videoReleaseService = videoReleaseService;
        _relocationService = relocationService;
        _videoLocals = videoLocals;

    }

    protected ProcessFileJob() { }
}
