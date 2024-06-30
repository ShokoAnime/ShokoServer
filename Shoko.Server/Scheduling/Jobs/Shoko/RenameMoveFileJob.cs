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
public class RenameMoveFileJob : BaseJob
{
    private readonly VideoLocal_PlaceService _vlPlaceService;

    private SVR_VideoLocal _vlocal;
    private string _fileName;

    public int VideoLocalID { get; set; }

    public override string TypeName => "Rename/Move File";
    public override string Title => "Renaming/Moving File";

    public override void PostInit()
    {
        _vlocal = RepoFactory.VideoLocal.GetByID(VideoLocalID);
        if (_vlocal == null) throw new JobExecutionException($"VideoLocal not Found: {VideoLocalID}");
        _fileName = Utils.GetDistinctPath(_vlocal?.FirstValidPlace?.FullServerPath);
    }

    public override Dictionary<string, object> Details => new() { { "File Path", _fileName ?? VideoLocalID.ToString() } };


    public override async Task Process()
    {
        _logger.LogInformation("Processing {Job}: {FileName}", nameof(RenameMoveFileJob), _fileName ?? VideoLocalID.ToString());

        // Check if the video local (file) is available.
        if (_vlocal == null)
        {
            _vlocal = RepoFactory.VideoLocal.GetByID(VideoLocalID);
            if (_vlocal == null)
                return;
        }

        var places = _vlocal.Places;
        foreach (var place in places)
            await _vlPlaceService.RenameAndMoveAsRequired(place);
    }

    public RenameMoveFileJob(VideoLocal_PlaceService vlPlaceService)
    {
        _vlPlaceService = vlPlaceService;
    }

    protected RenameMoveFileJob() { }
}
