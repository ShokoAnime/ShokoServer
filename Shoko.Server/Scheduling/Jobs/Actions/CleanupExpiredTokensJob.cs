using System.Threading.Tasks;
using Shoko.Abstractions.User.Services;
using Shoko.QueueProcessor.Acquisition.Attributes;
using Shoko.QueueProcessor.Builder;
using Shoko.QueueProcessor.Concurrency;
using Shoko.QueueProcessor.Workers;
using Shoko.Server.Services;

namespace Shoko.Server.Scheduling.Jobs.Actions;

[JobKeyMember("CleanupExpiredTokens")]
[JobKeyGroup(JobKeyGroup.System)]
[DisallowConcurrentExecution]
[DatabaseRequired]
public class CleanupExpiredTokensJob(IUserService userService, IJobCancellationAccessor cancellation) : BaseJob
{
    public override string TypeName => "Cleanup Expired Tokens";

    public override string Title => "Cleaning up expired auth tokens";

    public override Task Execute()
    {
        // Through the user service, so every token taken away is announced.
        ((UserService)userService).InvalidateExpiredApiTokens(cancellation.Token);
        return Task.CompletedTask;
    }
}
