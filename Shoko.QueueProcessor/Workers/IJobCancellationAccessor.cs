using System.Threading;

namespace Shoko.QueueProcessor.Workers;

/// <summary>
/// Scoped service that gives the currently-executing job access to its cancellation token,
/// without changing the parameterless
/// <see cref="Abstractions.IQueueJob.Process"/> signature. Inject it into a job and pass
/// <see cref="Token"/> to whatever the job awaits.
/// </summary>
/// <remarks>
/// Injecting this accessor is what makes a job cancellable; a running job that does not take it
/// cannot be cancelled. The token fires on a user cancel and when the pool stops. Stop by throwing
/// <see cref="System.OperationCanceledException"/>: after a user cancel the job ends as cancelled
/// and is not retried, after a shutdown it is queued again unchanged. Outside a worker (such as
/// <see cref="Abstractions.IJobFactory.Execute{T}"/>) the token is <see cref="CancellationToken.None"/>.
/// </remarks>
public interface IJobCancellationAccessor
{
    /// <summary>
    /// The cancellation token of the currently-executing job, or
    /// <see cref="CancellationToken.None"/> when the job is not running under a worker.
    /// </summary>
    CancellationToken Token { get; }
}
