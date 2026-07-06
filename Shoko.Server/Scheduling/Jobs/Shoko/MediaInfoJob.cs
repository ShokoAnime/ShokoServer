using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Shoko.Abstractions.Video.Services;
using Shoko.QueueProcessor.Acquisition.Attributes;
using Shoko.QueueProcessor.Builder;
using Shoko.QueueProcessor.Concurrency;
using Shoko.Server.Models.Shoko;
using Shoko.Server.Repositories.Cached;
using Shoko.Server.Services;

namespace Shoko.Server.Scheduling.Jobs.Shoko;

[DatabaseRequired]
[LimitConcurrency(2)]
[JobKeyGroup(JobKeyGroup.Import)]
public class MediaInfoJob(IVideoService videoService, VideoLocalRepository videoLocals) : BaseJob
{
    private VideoLocal? _vlocal;

    private string? _fileName;

    public int VideoLocalID { get; set; }

    public override string TypeName => "Read MediaInfo for File";
    public override string Title => "Reading MediaInfo for File";

    public override void PostInit()
    {
        _vlocal = videoLocals.GetByID(VideoLocalID) ??
            throw new Exception($"VideoLocal not Found: {VideoLocalID}");
        _fileName = VideoService.GetDistinctPath(_vlocal.FirstValidPlace?.Path);
    }

    public override Dictionary<string, object> Details => new() { { "File Path", _fileName ?? VideoLocalID.ToString() } };

    public override Task Execute()
    {
        _logger.LogInformation("Processing {Job}: {FileName}", nameof(MediaInfoJob), _fileName);
        if (_vlocal?.FirstResolvedPlace is not { } place)
        {
            _logger.LogWarning("Could not find file for Video: {VideoLocalID}", VideoLocalID);
            return Task.CompletedTask;
        }

        if (((VideoService)videoService).RefreshMediaInfo(place, _vlocal))
            videoLocals.Save(_vlocal, true);

        return Task.CompletedTask;
    }

}
