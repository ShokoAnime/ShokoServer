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

#pragma warning disable CS8618
#nullable enable
namespace Shoko.Server.Scheduling.Jobs.Shoko;

[DatabaseRequired]
[JobKeyGroup(JobKeyGroup.Import)]
public class RenameMoveFileJob : BaseJob
{
    private readonly VideoLocal_PlaceService _vlPlaceService;

    private SVR_VideoLocal? _vlocal;
    private string? _fileName;

    public int VideoLocalID { get; set; }

    public override string TypeName => "Rename/Move Video";

    public override string Title => "Renaming/Moving Video";

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

        foreach (var location in _vlocal.Places)
        {
            var locationPath = location.FullServerPath;
            var importFolder = location.ImportFolder;
            if (string.IsNullOrEmpty(locationPath) || importFolder == null)
            {
                _logger.LogTrace("Invalid path or import folder, skipping {FileName}. (Video={VideoID},Location={LocationID})", locationPath, _vlocal.VideoLocalID, location.VideoLocal_Place_ID);
                continue;
            }

            if (importFolder.IsDropDestination != 1 && importFolder.IsDropSource != 1)
            {
                _logger.LogTrace("Not in a drop destination or source, skipping {FileName}. (Video={VideoID},Location={LocationID})", locationPath, _vlocal.VideoLocalID, location.VideoLocal_Place_ID);
                continue;
            }

            var result = await _vlPlaceService.AutoRelocateFile(location);
            if (!result.Success)
                _logger.LogTrace(result.Exception, "Unable to move/rename file; {ErrorMessage} (Video={VideoID},Location={LocationID})", result.ErrorMessage, _vlocal.VideoLocalID, location.VideoLocal_Place_ID);
        }
    }

    public RenameMoveFileJob(VideoLocal_PlaceService vlPlaceService)
    {
        _vlPlaceService = vlPlaceService;
    }

    protected RenameMoveFileJob() { }
}
