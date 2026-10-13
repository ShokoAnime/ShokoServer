using System;
using System.Threading;
using System.Threading.Tasks;
using Shoko.Abstractions.Actions;
using Shoko.Abstractions.ScheduledActions;
using Shoko.Abstractions.Utilities;
using Shoko.Plugin.LegacyPlex.Jobs;
using Shoko.QueueProcessor.Abstractions;

namespace Shoko.Plugin.LegacyPlex.Actions;

/// <summary>
///   Sync watch states with Plex for all users with a Plex token.
/// </summary>
/// <remarks>
///   Only queues the syncs, which run on their own; the progress covers
///   the queuing.
/// </remarks>
/// <param name="scheduler">Queues the syncs.</param>
/// <param name="plexHelpers">Lists the users with a linked Plex account.</param>
public sealed class PlexSyncAllAction(IQueueScheduler scheduler, PlexHelperProvider plexHelpers) : IScheduledAction
{
    /// <inheritdoc/>
    public string Name => "Plex Sync All";

    /// <inheritdoc/>
    public string? Description => "Sync watch states with Plex for all users with a configured Plex token.";

    /// <inheritdoc/>
    public ActionCategory Category => ActionCategory.Sync;

    /// <inheritdoc/>
    public async Task Execute(IProgress<decimal> progress, CancellationToken token)
    {
        var users = plexHelpers.GetLinkedUsers();
        var items = new ItemProgress(progress, users.Count);
        items.Report(0);
        foreach (var user in users)
        {
            token.ThrowIfCancellationRequested();
            await scheduler.Enqueue<SyncPlexWatchedStatesJob>(c => c.UserID = user.JMMUserID, ct: token);
            items.Increment();
        }
    }
}
