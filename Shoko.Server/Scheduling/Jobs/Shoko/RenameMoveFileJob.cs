using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using QuartzJobFactory.Attributes;
using Shoko.Commons.Queue;
using Shoko.Models.Queue;
using Shoko.Server.Models;
using Shoko.Server.Repositories;
using Shoko.Server.Scheduling.Acquisition.Attributes;
using Shoko.Server.Services;

namespace Shoko.Server.Scheduling.Jobs.Shoko;

[DatabaseRequired]
[JobKeyGroup(JobKeyGroup.Import)]
public class RenameMoveFileJob : BaseJob
{
    private readonly VideoLocal_PlaceService _vlPlaceService;

    private SVR_VideoLocal _vlocal;
    private string _fileName;

    public int VideoLocalID { get; set; }

    public override string Name => "Rename/Move File";

    public override void PostInit()
    {
        _vlocal = RepoFactory.VideoLocal.GetByID(VideoLocalID);
        _fileName = _vlocal?.GetBestVideoLocalPlace()?.FileName;
    }

    public override QueueStateStruct Description
    {
        get
        {
            if (_vlocal != null)
            {
                return new QueueStateStruct
                {
                    message = "Renaming and/or Moving File: {0}",
                    queueState = QueueStateEnum.CheckingFile,
                    extraParams = new[]
                    {
                        _fileName
                    }
                };
            }

            return new QueueStateStruct
            {
                message = "Renaming and/or Moving File: {0}",
                queueState = QueueStateEnum.CheckingFile,
                extraParams = new[]
                {
                    _fileName
                }
            };
        }
    }

    public override Task Process()
    {
        _logger.LogInformation("Processing {Job}: {FileName}", nameof(RenameMoveFileJob), _fileName ?? VideoLocalID.ToString());

        // Check if the video local (file) is available.
        if (_vlocal == null)
        {
            _vlocal = RepoFactory.VideoLocal.GetByID(VideoLocalID);
            if (_vlocal == null)
                return Task.CompletedTask;
        }

        var places = _vlocal.Places;
        foreach (var place in places)
        {
            _vlPlaceService.RenameAndMoveAsRequired(place);
        }

        return Task.CompletedTask;
    }

    public RenameMoveFileJob(VideoLocal_PlaceService vlPlaceService)
    {
        _vlPlaceService = vlPlaceService;
    }

    protected RenameMoveFileJob() { }
}
