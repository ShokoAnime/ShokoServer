using System.Threading;
using System.Threading.Tasks;

#nullable enable
namespace Shoko.Server.Actions;

/// <summary>
/// A scheduled action whose work is one queue job, which a run queues
/// directly, so the job keeps its own acquisition filters, concurrency limits
/// and dedup key, and the scheduled action's state is that job's.
/// </summary>
public interface IQueueJobScheduledAction
{
    /// <summary>
    /// The key of the job a run queues.
    /// </summary>
    string JobKey { get; }

    /// <summary>
    /// Queues the job, unless one with the same key is already waiting or
    /// running.
    /// </summary>
    /// <param name="token">Cancels the queuing.</param>
    /// <returns>A task that completes once the job is queued.</returns>
    Task EnqueueJob(CancellationToken token);
}
