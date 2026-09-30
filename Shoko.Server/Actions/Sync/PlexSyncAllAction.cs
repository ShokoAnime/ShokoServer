using System;
using System.Threading;
using System.Threading.Tasks;
using Shoko.Abstractions.Actions;
using Shoko.Abstractions.ScheduledActions;
using Shoko.QueueProcessor.Abstractions;
using Shoko.Server.Repositories.Cached;
using Shoko.Server.Scheduling.Jobs.Plex;

namespace Shoko.Server.Actions;

/// <summary>
///   Sync watch states with Plex for all users with a Plex token.
/// </summary>
public sealed class PlexSyncAllAction(IQueueScheduler scheduler, JMMUserRepository jmmUsers) : IScheduledAction
{
    public string Name => "Plex Sync All";

    public string? Description => "Sync watch states with Plex for all users with a configured Plex token.";

    public ActionCategory Category => ActionCategory.Sync;

    public async Task Execute(IProgress<decimal> progress, CancellationToken token)
    {
        foreach (var user in jmmUsers.GetAll())
        {
            if (string.IsNullOrEmpty(user.PlexToken)) continue;
            await scheduler.Enqueue<SyncPlexWatchedStatesJob>(c => c.User = user, ct: token);
        }
    }
}
