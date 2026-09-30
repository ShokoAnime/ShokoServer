using System;

namespace Shoko.QueueProcessor.Workers;

/// <summary>
/// Default <see cref="IJobProgressAccessor"/>. Registered as a scoped service and stamped by
/// <see cref="Worker"/> before every <see cref="Abstractions.IQueueJob.Process"/> call, the same
/// way <see cref="JobCancellationAccessor"/> is.
/// </summary>
/// <remarks>
/// Chain jobs share one scope, so the reporter is re-stamped per job: each execution gets a
/// reporter of its own, and one captured by an earlier job in the chain keeps reporting to that
/// job, which has ended, so its reports are dropped.
/// </remarks>
public class JobProgressAccessor : IJobProgressAccessor
{
    private IProgress<decimal> _progress = NullProgress.Instance;

    /// <summary>
    /// The progress reporter for the currently-executing job, taking a percentage from 0 to 100.
    /// Reports go nowhere outside a worker.
    /// </summary>
    public IProgress<decimal> Progress => _progress;

    // Called by Worker before each job executes so the job reports to its own execution
    internal void SetCurrentReporter(IProgress<decimal>? progress) => _progress = progress ?? NullProgress.Instance;

    private sealed class NullProgress : IProgress<decimal>
    {
        public static readonly NullProgress Instance = new();

        public void Report(decimal value) { }
    }
}
