using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Shoko.Abstractions.Video.Services;
using Shoko.QueueProcessor.Acquisition.Attributes;
using Shoko.QueueProcessor.Builder;
using Shoko.QueueProcessor.Concurrency;
using Shoko.Server.Services;

namespace Shoko.Server.Scheduling.Jobs.Actions;

[DatabaseRequired]
[JobKeyMember("Import")]
[JobKeyGroup(JobKeyGroup.Legacy)]
[DisallowConcurrentExecution]
public class ImportJob : BaseJob
{
    private readonly IVideoService _videoService;

    private readonly ActionService _service;
    public override string TypeName => "Run Import";
    public override string Title => "Running Import";

    public override async Task Execute()
    {
        await _service.RunImport_IntegrityCheck();

        // managed folder
        await _videoService.ScheduleScanForManagedFolders();

        // TMDB association checks
        await _service.RunImport_ScanTMDB();

        // TMDB Purge people — partial failures are non-fatal; log and continue.
        try
        {
            await _service.RunImport_PurgeUnlinkedTmdbPeople();
        }
        catch (AggregateException ex)
        {
            _logger.LogWarning(ex, "TMDB: Failed to purge one or more people during import");
        }

        // TMDB Purge networks
        await _service.RunImport_PurgeUnlinkedTmdbShowNetworks();

        // Check for missing images
        await _service.RunImport_GetImages();

        // Check for previously ignored files
        _service.CheckForPreviouslyIgnored();

        await _service.ScheduleMissingAnidbAnimeForFiles();
    }

    public ImportJob(IVideoService videoService, ActionService service)
    {
        _videoService = videoService;
        _service = service;
    }

    protected ImportJob() { }
}
