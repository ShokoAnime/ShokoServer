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
public class RenameMoveFileLocationJob : BaseJob
{
    private readonly VideoLocal_PlaceService _vlPlaceService;

    private VideoLocal_Place? _location;

    private string? _fileName;

    public int ManagedFolderID { get; set; }

    public string RelativePath { get; set; }

    public override string TypeName => "Rename/Move Video Location";

    public override string Title => "Renaming/Moving Video Location";

    public override void PostInit()
    {
        _location = RepoFactory.VideoLocalPlace.GetByRelativePathAndManagedFolderID(RelativePath, ManagedFolderID);
        _fileName = _location?.Path;
        if (_location == null || string.IsNullOrEmpty(_fileName)) throw new JobExecutionException($"VideoLocalPlace not Found: {RelativePath} (ManagedFolder={ManagedFolderID})");
    }

    public override Dictionary<string, object> Details => new() { { "File Path", _fileName ?? RelativePath } };


    public override async Task Process()
    {
        _logger.LogInformation("Processing {Job}: {FileName}", nameof(RenameMoveFileLocationJob), _fileName);

        // Check if the video local (file) is available.
        if (_location == null)
        {
            _location = RepoFactory.VideoLocalPlace.GetByRelativePathAndManagedFolderID(RelativePath, ManagedFolderID);
            if (_location == null)
                return;
        }

        var locationPath = _fileName;
        var folder = _location.ManagedFolder;
        if (string.IsNullOrEmpty(locationPath) || folder is null)
        {
            _logger.LogTrace("Invalid path or managed folder, skipping {FileName}. (Video={VideoID},Location={LocationID})", locationPath, _location.VideoID, _location.ID);
            return;
        }

        if (!folder.IsDropDestination && !folder.IsDropSource)
        {
            _logger.LogTrace("Not in a drop destination or source, skipping {FileName}. (Video={VideoID},Location={LocationID})", locationPath, _location.VideoID, _location.ID);
            return;
        }

        var result = await _vlPlaceService.AutoRelocateFile(_location);
        if (!result.Success)
            _logger.LogTrace(result.Exception, "Unable to relocate video file; {ErrorMessage} (Video={VideoID},Location={LocationID})", result.ErrorMessage, _location.VideoID, _location.ID);
    }

    public RenameMoveFileLocationJob(VideoLocal_PlaceService vlPlaceService)
    {
        _vlPlaceService = vlPlaceService;
    }

    protected RenameMoveFileLocationJob() { }
}
