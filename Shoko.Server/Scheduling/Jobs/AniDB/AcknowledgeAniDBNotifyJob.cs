using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Shoko.QueueProcessor.Acquisition.Attributes;
using Shoko.QueueProcessor.Builder;
using Shoko.QueueProcessor.Concurrency;
using Shoko.Server.Providers.AniDB.Interfaces;
using Shoko.Server.Providers.AniDB.UDP.User;
using Shoko.Server.Repositories.Direct;
using Shoko.Server.Scheduling.Acquisition.Attributes;
using Shoko.Server.Scheduling.Concurrency;
using Shoko.Server.Server;

namespace Shoko.Server.Scheduling.Jobs.AniDB;

[DatabaseRequired]
[AniDBUdpRateLimited]
[DisallowConcurrencyGroup(ConcurrencyGroups.AniDB_UDP)]
[JobKeyGroup(JobKeyGroup.AniDB)]
public class AcknowledgeAniDBNotifyJob(IRequestFactory requestFactory, AniDB_MessageRepository anidbMessages) : BaseJob
{
    public int NotifyID { get; set; }

    public AniDBNotifyType NotifyType { get; set; }

    public override string TypeName => "Acknowledge AniDB Notify";

    public override string Title => "Acknowledging AniDB Notify";

    public override Task Execute()
    {
        _logger.LogInformation("Processing {Job}: {Type} {ID}", nameof(AcknowledgeAniDBNotifyJob), NotifyType.ToString(), NotifyID);

        var requestAck = requestFactory.Create<RequestAcknowledgeNotify>(
            r =>
            {
                r.Type = NotifyType;
                r.ID = NotifyID;
            }
        );
        var responseAck = requestAck.Send();

        // successful, set the read flag
        if (NotifyType == AniDBNotifyType.Message)
        {
            var message = anidbMessages.GetByMessageId(NotifyID);
            if (message != null)
            {
                message.IsReadOnAniDB = true;
                anidbMessages.Save(message);
            }
        }
        return Task.CompletedTask;
    }
}
