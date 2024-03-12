using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Quartz;
using Shoko.Server.Models;
using Shoko.Server.Repositories;
using Shoko.Server.Scheduling.Acquisition.Attributes;
using Shoko.Server.Scheduling.Attributes;
using Shoko.Server.Services;
using Shoko.Server.Utilities;

namespace Shoko.Server.Scheduling.Jobs.Shoko;

[DatabaseRequired]
[JobKeyGroup(JobKeyGroup.Import)]
public class MediaInfoJob : BaseJob
{
    private readonly VideoLocal_PlaceService _vlPlaceService;

    private SVR_VideoLocal _vlocal;
    private string _fileName;

    public int VideoLocalID { get; set; }

    public override string TypeName => "Read MediaInfo";

    public override void PostInit()
    {
        _vlocal = RepoFactory.VideoLocal.GetByID(VideoLocalID);
        if (_vlocal == null) throw new JobExecutionException($"VideoLocal not Found: {VideoLocalID}");
        _fileName = Utils.GetDistinctPath(_vlocal.GetBestVideoLocalPlace()?.FullServerPath);
    }

    public override string Title => "Reading MediaInfo for File";
    public override Dictionary<string, object> Details => new() { { "File Path", _fileName } };

    public override Task Process()
    {
        _logger.LogInformation("Processing {Job}: {FileName}", nameof(MediaInfoJob), _fileName);

        var place = _vlocal?.GetBestVideoLocalPlace(true);
        if (place == null)
        {
            _logger.LogWarning("Could not find file for Video: {VideoLocalID}", VideoLocalID);
            return Task.CompletedTask;
        }

        if (_vlPlaceService.RefreshMediaInfo(place))
        {
            RepoFactory.VideoLocal.Save(place.VideoLocal, true);
        }

        return Task.CompletedTask;
    }

    public MediaInfoJob(VideoLocal_PlaceService vlPlaceService)
    {
        _vlPlaceService = vlPlaceService;
    }

    protected MediaInfoJob() { }
}
