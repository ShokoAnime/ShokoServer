using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Shoko.Abstractions.Video.Services;
using Shoko.QueueProcessor.Acquisition.Attributes;
using Shoko.QueueProcessor.Builder;
using Shoko.QueueProcessor.Concurrency;
using Shoko.Server.Repositories.Cached;
using Shoko.Server.Repositories.Direct;
using Shoko.Server.Scheduling.Acquisition.Attributes;
using Shoko.Server.Scheduling.Concurrency;
using Shoko.Server.Server;
using Shoko.Server.Services;
using Shoko.Server.Settings;

namespace Shoko.Server.Scheduling.Jobs.AniDB;

[DatabaseRequired]
[AniDBUdpRateLimited]
[DisallowConcurrencyGroup(ConcurrencyGroups.AniDB_UDP)]
[JobKeyMember("CheckAniDBFileUpdates")]
[JobKeyGroup(JobKeyGroup.AniDB)]
[DisallowConcurrentExecution]
public class CheckAniDBFileUpdatesJob(ISettingsProvider settingsProvider, IVideoReleaseService videoReleaseService, VideoLocalRepository videoLocals, StoredReleaseInfo_MatchAttemptRepository storedReleaseInfoMatchAttempts, ScheduledUpdateRepository scheduledUpdates, ActionService actionService) : BaseJob
{
    public override string TypeName => "Check AniDB File Updates";

    public override string Title => "Checking AniDB File Updates";

    public override async Task Execute()
    {
        _logger.LogInformation("Processing {Job}", nameof(CheckAniDBFileUpdatesJob));

        // How often this runs is up to the triggers of the action that queues it.
        var settings = settingsProvider.GetSettings();
        var schedule = scheduledUpdates.GetByUpdateType((int)ScheduledUpdateType.AniDBFileUpdates);

        if (videoReleaseService.AutoMatchEnabled)
        {
            var filesWithoutEpisode = videoLocals.GetVideosWithoutEpisode();
            foreach (var vl in filesWithoutEpisode)
            {
                if (settings.Import.MaxAutoScanAttemptsPerFile != 0)
                {
                    var matchAttempts = storedReleaseInfoMatchAttempts.GetByEd2kAndFileSize(vl.Hash, vl.FileSize).Count;
                    if (matchAttempts > settings.Import.MaxAutoScanAttemptsPerFile)
                        continue;
                }

                await videoReleaseService.ScheduleFindReleaseForVideo(vl);
            }
        }

        await actionService.ScheduleMissingAnidbAnimeForFiles();

        schedule ??= new()
        {
            UpdateType = (int)ScheduledUpdateType.AniDBFileUpdates,
            UpdateDetails = string.Empty,
        };
        schedule.LastUpdate = DateTime.Now;
        scheduledUpdates.Save(schedule);
    }
}
