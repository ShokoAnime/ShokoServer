using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Shoko.QueueProcessor.Abstractions;
using Shoko.QueueProcessor.Acquisition.Attributes;
using Shoko.QueueProcessor.Builder;
using Shoko.QueueProcessor.Concurrency;
using Shoko.QueueProcessor.Scheduling;
using Shoko.Server.Repositories.Direct;
using Shoko.Server.Scheduling.Acquisition.Attributes;
using Shoko.Server.Scheduling.Jobs.Shoko;
using Shoko.Server.Server;
using Shoko.Server.Settings;

namespace Shoko.Server.Scheduling.Jobs.AniDB;

[DatabaseRequired]
[AniDBUdpRateLimited]
[JobKeyMember("CheckAniDBNotifications")]
[JobKeyGroup(JobKeyGroup.AniDB)]
[DisallowConcurrentExecution]
public class CheckAniDBNotificationsJob(IQueueScheduler scheduler, ISettingsProvider settingsProvider, ScheduledUpdateRepository scheduledUpdates, AniDB_MessageRepository anidbMessages) : BaseJob
{
    /// <summary>
    /// When true, skips the user-configured frequency check. The 30-minute AniDB minimum is always enforced.
    /// </summary>
    public bool ForceRefresh { get; set; }

    public override string TypeName => "Check AniDB Notifications";

    public override string Title => "Checking AniDB Notifications";

    public override async Task Execute()
    {
        _logger.LogInformation("Processing {Job}", nameof(CheckAniDBNotificationsJob));

        var settings = settingsProvider.GetSettings();
        if (!ForceRefresh && settings.AniDb.Notification_UpdateFrequency == ScheduledUpdateFrequency.Never)
            return;

        var schedule = scheduledUpdates.GetByUpdateType((int)ScheduledUpdateType.AniDBNotify);
        if (schedule is null)
        {
            schedule = new()
            {
                UpdateType = (int)ScheduledUpdateType.AniDBNotify,
                UpdateDetails = string.Empty,
            };
        }
        else
        {
            var tsLastRun = DateTime.Now - schedule.LastUpdate;

            // The NOTIFY command must not be issued more than once every 20 minutes per the AniDB UDP API docs.
            // We use 30 minutes as a safe margin.
            if (tsLastRun.TotalMinutes < 30) return;

            if (!ForceRefresh && tsLastRun.TotalHours < settings.AniDb.Notification_UpdateFrequency.Hours) return;
        }

        schedule.LastUpdate = DateTime.Now;
        scheduledUpdates.Save(schedule);

        await scheduler.StartJob<GetAniDBNotifyJob>();

        if (settings.AniDb.Notification_HandleMovedFiles)
        {
            var messages = anidbMessages.GetUnhandledFileMoveMessages();
            foreach (var msg in messages)
                await scheduler.StartJob<ProcessFileMovedMessageJob>(c => c.MessageID = msg.MessageID);
        }
    }


}
