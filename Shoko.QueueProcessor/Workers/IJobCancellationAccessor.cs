using System.Threading;

namespace Shoko.QueueProcessor.Workers;

/// <summary>
/// Scoped service that gives the currently-executing job access to the cancellation token
/// its worker is cancelled by, without changing the parameterless
/// <see cref="Abstractions.IQueueJob.Process"/> signature. Inject it into a job and pass
/// <see cref="Token"/> to whatever the job awaits.
/// </summary>
/// <remarks>
/// The token is the worker pool's shutdown token: it is cancelled when the pool that owns the
/// running worker is stopped — server shutdown, or an explicit queue stop — and at no other
/// time. There is no per-job cancellation; cancelling a single queued job is not supported.
/// Outside a worker (for example under <see cref="Abstractions.IJobFactory.Execute{T}"/>, which
/// resolves the job in a fresh scope) nothing sets the token and <see cref="Token"/> is
/// <see cref="CancellationToken.None"/> — valid, never cancelled, and
/// <see cref="CancellationToken.CanBeCanceled"/> is <see langword="false"/>.
/// </remarks>
public interface IJobCancellationAccessor
{
    /// <summary>
    /// The cancellation token for the currently-executing job, or
    /// <see cref="CancellationToken.None"/> when the job is not running under a worker.
    /// </summary>
    CancellationToken Token { get; }
}
