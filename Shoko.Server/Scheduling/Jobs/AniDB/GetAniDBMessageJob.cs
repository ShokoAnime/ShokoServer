using System.Collections.Generic;
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

namespace Shoko.Server.Scheduling.Jobs.AniDB;

[DatabaseRequired]
[AniDBUdpRateLimited]
[DisallowConcurrencyGroup(ConcurrencyGroups.AniDB_UDP)]
[JobKeyGroup(JobKeyGroup.AniDB)]
public class GetAniDBMessageJob : BaseJob
{
    private readonly IRequestFactory _requestFactory;
    private readonly ISchedulerFactory _schedulerFactory;
    private readonly ISettingsProvider _settingsProvider;
    public int MessageID { get; set; }
    public bool ForceRefresh { get; set; }

    public override string TypeName => "Get AniDB Message Content";

    public override string Title => "Getting AniDB Message Content";

    public override Dictionary<string, object> Details => new()
    {
        {
            "MessageID", MessageID
        }
    };

    public override Task Process()
    {
        _logger.LogInformation("Processing {Job}: {MessageID}", nameof(GetAniDBMessageJob), MessageID);

        var message = RepoFactory.AniDB_Message.GetByMessageId(MessageID);
        if (!ForceRefresh && message != null) return Task.CompletedTask;

        var request = _requestFactory.Create<RequestGetMessageContent>(r => r.ID = MessageID);
        var response = request.Send();

        if (response?.Response == null) return Task.CompletedTask;

        message = new()
        {
            MessageID = MessageID,
            FromUserId = response.Response.SenderID,
            FromUserName = response.Response.SenderName,
            Date = response.Response.SentTime,
            Type = response.Response.Type,
            Title = response.Response.Title,
            Body = response.Response.Body,
            Flags = AniDBMessageFlags.None
        };

        // add flag if its a file moved system message
        if (message.Type == AniDBMessageType.System && message.Title.StartsWith("file moved:"))
        {
            message.IsFileMoved = true;
        }

        // acknowledge if enabled
        var settings = _settingsProvider.GetSettings();
        if (settings.AniDb.Notification_Acknowledge)
        {
            var requestAck = _requestFactory.Create<RequestAcknowledgeNotify>(
                r =>
                {
                    r.Type = AniDBNotifyType.Message;
                    r.ID = MessageID;
                }
            );
            var responseAck = requestAck.Send();
            // set flag
            message.IsReadOnAniDB = true;
        }

        // save to db and remove from queue
        RepoFactory.AniDB_Message.Save(message);
        RepoFactory.AniDB_NotifyQueue.DeleteForTypeID(AniDBNotifyType.Message, MessageID);
        return Task.CompletedTask;
    }

    public GetAniDBMessageJob(IRequestFactory requestFactory, ISchedulerFactory schedulerFactory, ISettingsProvider settingsProvider)
    {
        _requestFactory = requestFactory;
        _schedulerFactory = schedulerFactory;
        _settingsProvider = settingsProvider;
    }

    protected GetAniDBMessageJob()
    {
    }
}
