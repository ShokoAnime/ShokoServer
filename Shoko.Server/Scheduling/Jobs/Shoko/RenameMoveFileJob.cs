using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Quartz;
using Shoko.Abstractions.Services;
using Shoko.Server.Models.Shoko;
using Shoko.Server.Repositories;
using Shoko.Server.Scheduling.Acquisition.Attributes;
using Shoko.Server.Scheduling.Attributes;
using Shoko.Server.Utilities;

#pragma warning disable CS8618
#nullable enable
namespace Shoko.Server.Scheduling.Jobs.Shoko;

[DatabaseRequired]
[JobKeyGroup(JobKeyGroup.Import)]
public class RenameMoveFileJob : BaseJob
{
    private readonly IRelocationService _relocationService;

    private VideoLocal? _vlocal;

    private string? _fileName;

    public int VideoLocalID { get; set; }

    public override string TypeName => "Rename/Move Video";

    public override string Title => "Renaming/Moving Video";

    public override void PostInit()
    {
        _vlocal = RepoFactory.VideoLocal.GetByID(VideoLocalID);
        if (_vlocal == null) throw new JobExecutionException($"VideoLocal not Found: {VideoLocalID}");
        _fileName = Utils.GetDistinctPath(_vlocal?.FirstValidPlace?.Path);
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
            var locationPath = location.Path;
            var folder = location.ManagedFolder;
            if (string.IsNullOrEmpty(locationPath) || folder is null)
            {
                _logger.LogTrace("Invalid path or managed folder, skipping {FileName}. (Video={VideoID},Location={LocationID})", locationPath, _vlocal.VideoLocalID, location.ID);
                continue;
            }

            if (!folder.IsDropDestination && !folder.IsDropSource)
            {
                _logger.LogTrace("Not in a drop destination or source, skipping {FileName}. (Video={VideoID},Location={LocationID})", locationPath, _vlocal.VideoLocalID, location.ID);
                continue;
            }

            var result = await _relocationService.AutoRelocateFile(location);
            if (!result.Success)
                _logger.LogTrace(result.Error.Exception, "Unable to move/rename file; {ErrorMessage} (Video={VideoID},Location={LocationID})", result.Error.Message, _vlocal.VideoLocalID, location.ID);
        }
    }

    public RenameMoveFileJob(IRelocationService relocationService)
    {
        _relocationService = relocationService;
    }

    protected RenameMoveFileJob() { }
}
