using System;
using System.Linq;
using System.Threading.Tasks;
using Shoko.Abstractions.Utilities;
using Shoko.QueueProcessor.Acquisition.Attributes;
using Shoko.QueueProcessor.Builder;
using Shoko.QueueProcessor.Concurrency;
using Shoko.QueueProcessor.Workers;
using Shoko.Server.Repositories.Cached;

namespace Shoko.Server.Scheduling.Jobs.Actions;

[JobKeyMember("CleanupExpiredTokens")]
[JobKeyGroup(JobKeyGroup.System)]
[DisallowConcurrentExecution]
[DatabaseRequired]
public class CleanupExpiredTokensJob(AuthTokensRepository authTokensRepository, IJobCancellationAccessor cancellation, IJobProgressAccessor progress) : BaseJob
{
    public override string TypeName => "Cleanup Expired Tokens";

    public override string Title => "Cleaning up expired auth tokens";

    public override Task Execute()
    {
        var now = DateTime.Now;
        var tokens = authTokensRepository.GetAll();
        var expired = tokens.Where(t => t.ExpiresAt.HasValue && t.ExpiresAt.Value < now).ToList();
        var items = new ItemProgress(progress.Progress, expired.Count);
        items.Report(0);
        foreach (var token in expired)
        {
            cancellation.Token.ThrowIfCancellationRequested();
            authTokensRepository.Delete(token);
            items.Increment();
        }

        return Task.CompletedTask;
    }
}
