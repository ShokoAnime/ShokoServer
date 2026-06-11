using System;
using System.Linq;
using System.Threading.Tasks;
using Shoko.QueueProcessor.Acquisition.Attributes;
using Shoko.QueueProcessor.Builder;
using Shoko.QueueProcessor.Concurrency;
using Shoko.Server.Repositories;

namespace Shoko.Server.Scheduling.Jobs.Actions;

[JobKeyMember("CleanupExpiredTokens")]
[JobKeyGroup(JobKeyGroup.System)]
[DisallowConcurrentExecution]
[DatabaseRequired]
public class CleanupExpiredTokensJob : BaseJob
{
    public override string TypeName => "Cleanup Expired Tokens";
    public override string Title => "Cleaning up expired auth tokens";

    protected CleanupExpiredTokensJob() { }

    public override Task Execute()
    {
        var now = DateTime.Now;
        var tokens = RepoFactory.AuthTokens.GetAll();
        var expired = tokens.Where(t => t.ExpiresAt.HasValue && t.ExpiresAt.Value < now).ToList();
        foreach (var token in expired)
            RepoFactory.AuthTokens.Delete(token);

        return Task.CompletedTask;
    }
}
