using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Quartz;
using Shoko.Plugin.Abstractions.Services;
using Shoko.Server.Models;
using Shoko.Server.Repositories;
using Shoko.Server.Scheduling.Acquisition.Attributes;
using Shoko.Server.Scheduling.Attributes;
using Shoko.Server.Scheduling.Concurrency;
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

        // Process and get the AniDB file entry.
        if (!ForceRecheck && _videoReleaseService.GetCurrentReleaseForVideo(_vlocal) is { } currentRelease)
            return;

        await _videoReleaseService.FindReleaseForVideo(_vlocal);
    }


    public ProcessFileJob(IVideoReleaseService videoReleaseService)
    {
        _videoReleaseService = videoReleaseService;
    }

    protected ProcessFileJob() { }
}
