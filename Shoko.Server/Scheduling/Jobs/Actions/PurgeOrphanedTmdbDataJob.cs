using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Shoko.Abstractions.Metadata.Tmdb.Services;
using Shoko.QueueProcessor.Acquisition.Attributes;
using Shoko.QueueProcessor.Builder;
using Shoko.QueueProcessor.Concurrency;
using Shoko.Server.Settings;

#pragma warning disable CS8618
#nullable enable
namespace Shoko.Server.Scheduling.Jobs.Actions;

[DatabaseRequired]
[DisallowConcurrentExecution]
[JobKeyGroup(JobKeyGroup.Actions)]
public class PurgeOrphanedTmdbDataJob : BaseJob
{
    private readonly ISettingsProvider _settingsProvider;
    private readonly ITmdbMetadataService _tmdbMetadataService;

    public override string TypeName => "Purge Orphaned TMDB Data";

    public override string Title => "Purging Orphaned TMDB Data";

    public override async Task Execute()
    {
        var threshold = _settingsProvider.GetSettings().TMDB.AutoPurgeUnlinkedAfterDays;
        if (threshold <= 0)
        {
            _logger.LogTrace("Auto-purge disabled (AutoPurgeUnlinkedAfterDays=0). Skipping.");
            return;
        }

        var cutoff = DateTime.Now.AddDays(-threshold);
        await _tmdbMetadataService.PurgeAllUnusedShows(cutoff);
        await _tmdbMetadataService.PurgeAllUnusedMovies(cutoff);
    }

    public PurgeOrphanedTmdbDataJob(ISettingsProvider settingsProvider, ITmdbMetadataService tmdbMetadataService)
    {
        _settingsProvider = settingsProvider;
        _tmdbMetadataService = tmdbMetadataService;
    }

    protected PurgeOrphanedTmdbDataJob() { }
}
