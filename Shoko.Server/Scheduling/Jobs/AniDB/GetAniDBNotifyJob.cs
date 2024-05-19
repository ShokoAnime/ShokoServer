using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Quartz;
using Shoko.Server.Providers.AniDB.Interfaces;
using Shoko.Server.Providers.AniDB.UDP.User;
using Shoko.Server.Repositories;
using Shoko.Server.Scheduling.Acquisition.Attributes;
using Shoko.Server.Scheduling.Attributes;
using Shoko.Server.Scheduling.Concurrency;
using Shoko.Server.Server;
using Shoko.Server.Settings;
using Shoko.Server.Utilities;

namespace Shoko.Server.Scheduling.Jobs.AniDB;

[DatabaseRequired]
[AniDBUdpRateLimited]
[DisallowConcurrencyGroup(ConcurrencyGroups.AniDB_UDP)]
[JobKeyGroup(JobKeyGroup.AniDB)]
public class GetAniDBNotifyJob : BaseJob
{
    private readonly IRequestFactory _requestFactory;
    private readonly ISchedulerFactory _schedulerFactory;
    private readonly ISettingsProvider _settingsProvider;

    public override string TypeName => "Get AniDB Unread Notifications and Messages";

    public override string Title => "Getting AniDB Unread Notifications and Messages";

    public override async Task Process()
    {
        _logger.LogInformation("Processing {Job}", nameof(GetAniDBNotifyJob));

        var settings = _settingsProvider.GetSettings();
        var sched = RepoFactory.ScheduledUpdate.GetByUpdateType((int)ScheduledUpdateType.AniDBNotify);
        if (sched == null)
        {
            sched = new()
            {
                UpdateType = (int)ScheduledUpdateType.AniDBNotify,
                UpdateDetails = string.Empty
            };
        }
        else
        {
            var freqHours = Utils.GetScheduledHours(settings.AniDb.Notification_UpdateFrequency);

            // if we have run this in the last 12 hours and are not forcing it, then exit
            var tsLastRun = DateTime.Now - sched.LastUpdate;
            if (tsLastRun.TotalHours < freqHours) return;
        }

        sched.LastUpdate = DateTime.Now;
        RepoFactory.ScheduledUpdate.Save(sched);

        var requestCount = _requestFactory.Create<RequestGetNotifyCount>(r => r.Buddies = false); // we do not care about the number of online buddies
        var responseCount = requestCount.Send();
        if (responseCount?.Response == null) return;

        var scheduler = await _schedulerFactory.GetScheduler();

        var unreadCount = responseCount.Response.Files + responseCount.Response.Messages;
        if (unreadCount > 0)
        {
            _logger.LogInformation("There are {Count} unread notifications and messages", unreadCount);

            // request list of IDs
            var request = _requestFactory.Create<RequestGetNotifyList>();
            var response = request.Send();
            if (response?.Response == null) return;

            foreach (var notify in response.Response)
            {
                var type = RepoFactory.AniDB_NotifyQueue.GetByTypeID(notify.Type, notify.ID);
                if (type is not null) continue; // if we already have it in the queue

                // save to db queue
                type = new()
                {
                    Type = notify.Type,
                    ID = notify.ID,
                    Added = DateTime.Now
                };
                RepoFactory.AniDB_NotifyQueue.Save(type);
            }
        }

        // try to clear the queue
        var messages = RepoFactory.AniDB_NotifyQueue.GetByType(AniDBNotifyType.Message);
        if (messages.Count > 0)
        {
            foreach (var msg in messages)
                await scheduler.StartJob<GetAniDBMessageJob>(r => r.MessageID = msg.ID);
        }
    }

    public GetAniDBNotifyJob(IRequestFactory requestFactory, ISchedulerFactory schedulerFactory, ISettingsProvider settingsProvider)
    {
        _requestFactory = requestFactory;
        _schedulerFactory = schedulerFactory;
        _settingsProvider = settingsProvider;
    }

    protected GetAniDBNotifyJob()
    {
    }
}
