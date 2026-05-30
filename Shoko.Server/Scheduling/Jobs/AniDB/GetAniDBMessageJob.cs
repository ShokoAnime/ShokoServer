using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Shoko.QueueProcessor.Abstractions;
using Shoko.QueueProcessor.Acquisition.Attributes;
using Shoko.QueueProcessor.Builder;
using Shoko.QueueProcessor.Concurrency;
using Shoko.QueueProcessor.Scheduling;
using Shoko.Server.Providers.AniDB.Interfaces;
using Shoko.Server.Providers.AniDB.UDP.User;
using Shoko.Server.Repositories.Direct;
using Shoko.Server.Scheduling.Acquisition.Attributes;
using Shoko.Server.Scheduling.Concurrency;
using Shoko.Server.Scheduling.Jobs.Shoko;
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
    private readonly IQueueScheduler _scheduler;
    private readonly ISettingsProvider _settingsProvider;
    public int MessageID { get; set; }

    public override string TypeName => "Get AniDB Message Content";

    public override string Title => "Getting AniDB Message Content";

    public override async Task Execute()
    {
        _logger.LogInformation("Processing {Job}: {MessageID}", nameof(GetAniDBMessageJob), MessageID);

        var message = _anidbMessages.GetByMessageId(MessageID);
        if (message is not null) return; // message content has already been fetched

        var request = _requestFactory.Create<RequestGetMessageContent>(r => r.ID = MessageID);
        var response = request.Send();
        if (response?.Response == null) return;

        message = new()
        {
            MessageID = MessageID,
            FromUserId = response.Response.SenderID,
            FromUserName = response.Response.SenderName,
            SentAt = response.Response.SentTime,
            FetchedAt = DateTime.Now,
            Type = response.Response.Type,
            Title = response.Response.Title,
            Body = response.Response.Body,
            Flags = AniDBMessageFlags.None
        };

        // set flag if its a file moved system message
        if (message.Type == AniDBMessageType.System && message.Title.ToLower().StartsWith("file moved:"))
        {
            message.IsFileMoved = true;
        }

        // save to db and remove from queue
        _anidbMessages.Save(message);
        _anidbNotifyQueues.DeleteForTypeID(AniDBNotifyType.Message, MessageID);

        var settings = _settingsProvider.GetSettings();
        await _scheduler.StartJob<AcknowledgeAniDBNotifyJob>(
            r =>
            {
                r.NotifyType = AniDBNotifyType.Message;
                r.NotifyID = MessageID;
            }
        );

        if (message.IsFileMoved && settings.AniDb.Notification_HandleMovedFiles)
        {
            await _scheduler.StartJob<ProcessFileMovedMessageJob>(c => c.MessageID = message.MessageID);
        }
    }

    private readonly AniDB_MessageRepository _anidbMessages;
    private readonly AniDB_NotifyQueueRepository _anidbNotifyQueues;
    public GetAniDBMessageJob(IRequestFactory requestFactory, IQueueScheduler schedulerFactory, ISettingsProvider settingsProvider,
        AniDB_MessageRepository anidbMessages,
        AniDB_NotifyQueueRepository anidbNotifyQueues
    )
    {
        _requestFactory = requestFactory;
        _scheduler = schedulerFactory;
        _settingsProvider = settingsProvider;
        _anidbMessages = anidbMessages;
        _anidbNotifyQueues = anidbNotifyQueues;

    }

    protected GetAniDBMessageJob()
    {
    }
}
