using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Shoko.Commons.Queue;
using Shoko.Models.Queue;
using Shoko.Server.Models;
using Shoko.Server.Repositories;
using Shoko.Server.Scheduling.Acquisition.Attributes;
using Shoko.Server.Scheduling.Attributes;
using Shoko.Server.Services;

namespace Shoko.Server.Scheduling.Jobs.Shoko;

[DatabaseRequired]
[JobKeyGroup(JobKeyGroup.Import)]
public class MediaInfoJob : BaseJob
{
    private readonly VideoLocal_PlaceService _vlPlaceService;

    private SVR_VideoLocal _vlocal;
    private string _fileName;

    public int VideoLocalID { get; set; }

    public override string Name => "Read MediaInfo";

    public override void PostInit()
    {
        _vlocal = RepoFactory.VideoLocal.GetByID(VideoLocalID);
        _fileName = _vlocal?.GetBestVideoLocalPlace()?.FileName ?? VideoLocalID.ToString();
    }

    public override QueueStateStruct Description => new()
    {
        message = "Reading media info for file: {0}",
        queueState = QueueStateEnum.ReadingMedia,
        extraParams = new[] { _fileName }
    };

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
