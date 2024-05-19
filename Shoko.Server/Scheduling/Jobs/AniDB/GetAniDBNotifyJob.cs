using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Shoko.Models.Server;
using Shoko.Server.Providers.AniDB.Interfaces;
using Shoko.Server.Providers.AniDB.UDP.User;
using Shoko.Server.Repositories;
using Shoko.Server.Scheduling.Acquisition.Attributes;
using Shoko.Server.Scheduling.Attributes;
using Shoko.Server.Scheduling.Concurrency;
using Shoko.Server.Server;

namespace Shoko.Server.Scheduling.Jobs.AniDB;

[DatabaseRequired]
[AniDBUdpRateLimited]
[DisallowConcurrencyGroup(ConcurrencyGroups.AniDB_UDP)]
[JobKeyGroup(JobKeyGroup.AniDB)]
public class GetAniDBNotifyJob : BaseJob
{
    private readonly IRequestFactory _requestFactory;

    public override string TypeName => "Get AniDB Unread Notifications and Messages";

    public override string Title => "Getting AniDB Unread Notifications and Messages";

    public override Task Process()
    {
        _logger.LogInformation("Processing {Job}", nameof(GetAniDBNotifyJob));

        var sched = RepoFactory.ScheduledUpdate.GetByUpdateType((int)ScheduledUpdateType.AniDBNotify);
        if (sched == null)
        {
            sched = new ScheduledUpdate
            {
                UpdateType = (int)ScheduledUpdateType.AniDBNotify,
                UpdateDetails = string.Empty
            };
        }
        else
        {
            // command MUST NOT be issued more than once every 20 minutes. We'll do 30 to be safe
            var tsLastRun = DateTime.Now - sched.LastUpdate;
            if (tsLastRun.TotalMinutes < 30)
            {
                return Task.CompletedTask;
            }
        }

        sched.LastUpdate = DateTime.Now;

        var requestCount = _requestFactory.Create<RequestGetNotifyCount>(r => r.Buddies = false); // we do not care about the number of online buddies
        var responseCount = requestCount.Send();
        if (responseCount?.Response == null) return Task.CompletedTask;

        var count = responseCount.Response.Files + responseCount.Response.Messages;
        if (count > 0)
        {
            _logger.LogInformation("There are {Count} unread notifications and messages", count);

            // request list of IDs
            var requestNotifyList = _requestFactory.Create<RequestGetNotifyList>();
            var responseNotifyList = requestNotifyList.Send();
            if (responseNotifyList.Response == null) return Task.CompletedTask;

            foreach (var notify in responseNotifyList.Response)
            {
                if (notify.Message)
                {
                    var message = RepoFactory.AniDB_Message.GetByMessageId(notify.ID);
                    if (message != null) continue; // if the message was already fetched

                    _logger.LogDebug("Fetching message {ID}", notify.ID);
                    var requestMessage = _requestFactory.Create<RequestGetMessageContent>(r => r.ID = notify.ID);
                    var responseMessage = requestMessage.Send();

                    if (responseMessage?.Response == null) continue;

                    // save to db
                    message ??= new AniDB_Message();
                    message.MessageID = notify.ID;
                    message.FromUserId = responseMessage.Response.SenderID;
                    message.FromUserName = responseMessage.Response.SenderName;
                    message.Date = responseMessage.Response.SentTime;
                    message.Type = responseMessage.Response.Type;
                    message.Title = responseMessage.Response.Title;
                    message.Body = responseMessage.Response.Body;
                    RepoFactory.AniDB_Message.Save(message);
                }
                else
                {
                    var notification = RepoFactory.AniDB_Notification.GetByNotificationId(notify.ID);
                    if (notification != null) continue; // if the message was already fetched

                    _logger.LogDebug("Fetching notification {ID}", notify.ID);
                    var requestNotification = _requestFactory.Create<RequestGetNotificationContent>(r => r.ID = notify.ID);
                    var responseNotification = requestNotification.Send();

                    if (responseNotification?.Response == null) continue;

                    // save to db
                    notification ??= new AniDB_Notification();
                    notification.NotificationID = notify.ID;
                    notification.RelatedTypeID = responseNotification.Response.RelatedTypeID;
                    notification.NotificationType = responseNotification.Response.Type;
                    notification.CountPending = responseNotification.Response.PendingEvents;
                    notification.Date = responseNotification.Response.SentTime;
                    notification.RelatedTypeName = responseNotification.Response.RelatedTypeName;
                    notification.FileIds = string.Join(",", responseNotification.Response.FileIDs);
                    RepoFactory.AniDB_Notification.Save(notification);
                }
            }
        }
        RepoFactory.ScheduledUpdate.Save(sched);
        return Task.CompletedTask;
    }

    public GetAniDBNotifyJob(IRequestFactory requestFactory)
    {
        _requestFactory = requestFactory;
    }

    protected GetAniDBNotifyJob()
    {
    }
}
