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
public class ImportJob(IVideoService videoService, ActionService service) : BaseJob
{
    public override string TypeName => "Run Import";
    public override string Title => "Running Import";

    public override async Task Execute()
    {
        await service.RunImport_IntegrityCheck();

        // managed folder
        await videoService.ScheduleScanForManagedFolders();

        // TMDB association checks
        await service.RunImport_ScanTMDB();

        // TMDB Purge people — partial failures are non-fatal; log and continue.
        try
        {
            await service.RunImport_PurgeUnlinkedTmdbPeople();
        }
        catch (AggregateException ex)
        {
            _logger.LogWarning(ex, "TMDB: Failed to purge one or more people during import");
        }

        // TMDB Purge networks
        await service.RunImport_PurgeUnlinkedTmdbShowNetworks();

        // Check for missing images
        await service.RunImport_GetImages();

        // Check for previously ignored files
        service.CheckForPreviouslyIgnored();

        await service.ScheduleMissingAnidbAnimeForFiles();
    }
}
