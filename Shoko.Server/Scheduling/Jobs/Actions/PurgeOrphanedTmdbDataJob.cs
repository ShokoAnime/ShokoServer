using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Shoko.Abstractions.Metadata.Tmdb.Services;
using Shoko.QueueProcessor.Acquisition.Attributes;
using Shoko.QueueProcessor.Builder;
using Shoko.QueueProcessor.Concurrency;
using Shoko.Server.Settings;

namespace Shoko.Server.Scheduling.Jobs.Actions;

[DatabaseRequired]
[DisallowConcurrentExecution]
[JobKeyGroup(JobKeyGroup.Actions)]
public class PurgeOrphanedTmdbDataJob(ISettingsProvider settingsProvider, ITmdbMetadataService tmdbMetadataService) : BaseJob
{
    public override string TypeName => "Purge Orphaned TMDB Data";

    public override string Title => "Purging Orphaned TMDB Data";

    public override async Task Execute()
    {
        var threshold = settingsProvider.GetSettings().TMDB.AutoPurgeUnlinkedAfterDays;
        if (threshold <= 0)
        {
            _logger.LogTrace("Auto-purge disabled (AutoPurgeUnlinkedAfterDays=0). Skipping.");
            return;
        }

        var cutoff = DateTime.Now.AddDays(-threshold);
        await tmdbMetadataService.PurgeAllUnusedShows(cutoff);
        await tmdbMetadataService.PurgeAllUnusedMovies(cutoff);
    }
}
