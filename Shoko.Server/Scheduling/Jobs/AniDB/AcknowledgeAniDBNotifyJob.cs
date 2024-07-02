using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
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
public class AcknowledgeAniDBNotifyJob : BaseJob
{
    private readonly IRequestFactory _requestFactory;
    public int NotifyID { get; set; }
    public AniDBNotifyType NotifyType { get; set; }

    public override string TypeName => "Acknowledge AniDB Notify";

    public override string Title => "Acknowledging AniDB Notify";

    public override Task Process()
    {
        _logger.LogInformation("Processing {Job}: {Type} {ID}", nameof(AcknowledgeAniDBNotifyJob), NotifyType.ToString(), NotifyID);

        var requestAck = _requestFactory.Create<RequestAcknowledgeNotify>(
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
            var message = RepoFactory.AniDB_Message.GetByMessageId(NotifyID);
            if (message != null)
            {
                message.IsReadOnAniDB = true;
                RepoFactory.AniDB_Message.Save(message);
            }
        }
        return Task.CompletedTask;
    }

    public AcknowledgeAniDBNotifyJob(IRequestFactory requestFactory)
    {
        _requestFactory = requestFactory;
    }

    protected AcknowledgeAniDBNotifyJob()
    {
    }
}
