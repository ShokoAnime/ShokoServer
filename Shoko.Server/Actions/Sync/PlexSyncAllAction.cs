using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Shoko.Abstractions.Actions;
using Shoko.Abstractions.ScheduledActions;
using Shoko.Abstractions.Utilities;
using Shoko.QueueProcessor.Abstractions;
using Shoko.Server.Repositories.Cached;
using Shoko.Server.Scheduling.Jobs.Plex;

namespace Shoko.Server.Actions;

/// <summary>
///   Sync watch states with Plex for all users with a Plex token.
/// </summary>
/// <remarks>
///   Only queues the syncs, which run on their own; the progress covers
///   the queuing.
/// </remarks>
public sealed class PlexSyncAllAction(IQueueScheduler scheduler, JMMUserRepository jmmUsers) : IScheduledAction
{
    public string Name => "Plex Sync All";

    public string? Description => "Sync watch states with Plex for all users with a configured Plex token.";

    public ActionCategory Category => ActionCategory.Sync;

    public async Task Execute(IProgress<decimal> progress, CancellationToken token)
    {
        var users = jmmUsers.GetAll().Where(user => !string.IsNullOrEmpty(user.PlexToken)).ToList();
        var items = new ItemProgress(progress, users.Count);
        items.Report(0);
        foreach (var user in users)
        {
            token.ThrowIfCancellationRequested();
            await scheduler.Enqueue<SyncPlexWatchedStatesJob>(c => c.User = user, ct: token);
            items.Increment();
        }
    }
}
