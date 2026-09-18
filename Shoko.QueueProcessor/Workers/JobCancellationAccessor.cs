using System.Threading;

namespace Shoko.QueueProcessor.Workers;

/// <summary>
/// Default <see cref="IJobCancellationAccessor"/>. Registered as a scoped service and populated
/// by <see cref="Worker"/> before every <see cref="Abstractions.IQueueJob.Process"/> call, in the
/// same way <see cref="Chain.JobChainContextAccessor.SetCurrentJob"/> is — no AsyncLocal.
/// </summary>
/// <remarks>
/// A standalone job gets its own scope, so the instance and the token are one-to-one. Chain jobs
/// share a single scope for the whole chain, so the instance outlives any one job and the token
/// has to be re-stamped per job; that is why <see cref="SetCurrentToken"/> is called on the
/// per-job path and not at chain-scope construction.
/// </remarks>
public class JobCancellationAccessor : IJobCancellationAccessor
{
    private CancellationToken _token = CancellationToken.None;

    /// <inheritdoc/>
    public CancellationToken Token => _token;

    // Called by Worker before each job executes so the job observes its own worker's token
    internal void SetCurrentToken(CancellationToken token) => _token = token;
}
