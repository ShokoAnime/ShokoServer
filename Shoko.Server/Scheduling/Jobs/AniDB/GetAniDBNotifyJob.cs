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
using Shoko.Server.Services;

namespace Shoko.Server.Scheduling.Jobs.AniDB;

[DatabaseRequired]
[AniDBUdpRateLimited]
[DisallowConcurrencyGroup(ConcurrencyGroups.AniDB_UDP)]
[JobKeyGroup(JobKeyGroup.AniDB)]
public class GetAniDBNotifyJob : BaseJob
{
    private readonly IRequestFactory _requestFactory;
    private readonly ISchedulerFactory _schedulerFactory;

    public override string TypeName => "Fetch Unread AniDB Messages List";

    public override string Title => "Fetching Unread AniDB Messages List";

    public override async Task Process()
    {
        _logger.LogInformation("Processing {Job}", nameof(GetAniDBNotifyJob));

        var requestCount = _requestFactory.Create<RequestGetNotifyCount>(r => r.Buddies = false); // we do not care about the number of online buddies
        var responseCount = requestCount.Send();
        if (responseCount?.Response == null) return;

        var unreadCount = responseCount.Response.Files + responseCount.Response.Messages;
        if (unreadCount > 0)
        {
            _logger.LogInformation("There are {Count} unread notifications and messages", unreadCount);

            // request an ID list of all unread messages and notifications
            var request = _requestFactory.Create<RequestGetNotifyList>();
            var response = request.Send();
            if (response?.Response == null) return;

            foreach (var notify in response.Response)
            {
                var type = RepoFactory.AniDB_NotifyQueue.GetByTypeID(notify.Type, notify.ID);
                if (type is not null) continue; // if we already have it in the queue

                if (notify.Type == AniDBNotifyType.Message)
                {
                    var msg = RepoFactory.AniDB_Message.GetByMessageId(notify.ID);
                    if (msg is not null) continue; // if the message content was already fetched
                }

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

        // fetch the content of all messages currently in the queue
        var messages = RepoFactory.AniDB_NotifyQueue.GetByType(AniDBNotifyType.Message);
        if (messages.Count > 0)
        {
            var scheduler = await _schedulerFactory.GetScheduler();
            foreach (var msg in messages)
                await scheduler.StartJob<GetAniDBMessageJob>(r => r.MessageID = msg.ID);
        }
    }

    public GetAniDBNotifyJob(IRequestFactory requestFactory, ISchedulerFactory schedulerFactory)
    {
        _requestFactory = requestFactory;
        _schedulerFactory = schedulerFactory;
    }

    protected GetAniDBNotifyJob()
    {
    }
}
