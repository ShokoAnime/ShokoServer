using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Shoko.Abstractions.Metadata.Anilist.Services;
using Shoko.QueueProcessor.Acquisition.Attributes;
using Shoko.QueueProcessor.Builder;
using Shoko.QueueProcessor.Concurrency;
using Shoko.Server.Settings;

namespace Shoko.Server.Scheduling.Jobs.Actions;

[DatabaseRequired]
[DisallowConcurrentExecution]
[JobKeyGroup(JobKeyGroup.Actions)]
public class PurgeOrphanedAnilistDataJob(ISettingsProvider settingsProvider, IAnilistMetadataService anilistMetadataService) : BaseJob
{
    public override string TypeName => "Purge Orphaned AniList Data";

    public override string Title => "Purging Orphaned AniList Data";

    public override async Task Execute()
    {
        var threshold = settingsProvider.GetSettings().Anilist.AutoPurgeUnlinkedAfterDays;
        if (threshold <= 0)
        {
            _logger.LogTrace("Auto-purge disabled (AutoPurgeUnlinkedAfterDays=0). Skipping.");
            return;
        }

        var cutoff = DateTime.Now.AddDays(-threshold);
        await anilistMetadataService.PurgeAllUnusedAnime(cutoff);
    }
}
