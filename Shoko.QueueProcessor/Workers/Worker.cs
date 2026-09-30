using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Shoko.QueueProcessor.Abstractions;
using Shoko.QueueProcessor.Analytics;
using Shoko.QueueProcessor.Builder;
using Shoko.QueueProcessor.Chain;
using Shoko.QueueProcessor.Events;
using Shoko.QueueProcessor.Orchestration;
using Shoko.QueueProcessor.Storage;

namespace Shoko.QueueProcessor.Workers;

/// <summary>
/// Single long-running worker task. Awaits a wake signal from its pool, calls
/// <see cref="WorkerPool.TryAcquire"/>, executes the job, and notifies the orchestrator.
/// </summary>
internal sealed class Worker
{
    private readonly WorkerPool _pool;
    private readonly int _index;
    private readonly IServiceProvider _serviceProvider;
    private readonly QueueOrchestrator _orchestrator;
    private readonly IChainScopeRegistry _chainScopeRegistry;
    private readonly QueueMetrics _metrics;
    private readonly QueueStateEventHandler _events;
    private readonly ChannelReader<bool> _wakeReader;
    private readonly ILogger<Worker> _logger;
    private readonly int _maxIdlePollMs;
    private readonly IJobActorAccessor? _actorAccessor;

    // Completes when RunAsync exits — used by WorkerPool.WhenStoppedAsync so the
    // WorkerPoolManager can wait for in-flight Process() calls to finish before flushing
    // the PersistenceBuffer on shutdown. Without this, an in-flight job that completes
    // after FlushNowAsync buffers a DELETE that never gets written.
    private readonly TaskCompletionSource _completed = new(TaskCreationOptions.RunContinuationsAsynchronously);
    public Task Completion => _completed.Task;

    public Worker(
        WorkerPool pool,
        int index,
        IServiceProvider serviceProvider,
        QueueOrchestrator orchestrator,
        IChainScopeRegistry chainScopeRegistry,
        QueueMetrics metrics,
        QueueStateEventHandler events,
        ChannelReader<bool> wakeReader,
        IJobActorAccessor? actorAccessor,
        int maxIdlePollMs = 5000)
    {
        _pool = pool;
        _index = index;
        _serviceProvider = serviceProvider;
        _orchestrator = orchestrator;
        _chainScopeRegistry = chainScopeRegistry;
        _metrics = metrics;
        _events = events;
        _wakeReader = wakeReader;
        _logger = serviceProvider.GetRequiredService<ILogger<Worker>>();
        _maxIdlePollMs = maxIdlePollMs;
        _actorAccessor = actorAccessor;
    }

    public void Start(CancellationToken ct)
    {
        // Use a real OS thread rather than Task.Factory.StartNew with LongRunning.
        // StartNew with an async lambda only uses the LongRunning thread for the synchronous
        // prefix of RunAsync (up to the first await), then that thread exits and all async
        // continuations — including job execution — run on ThreadPool threads. With many
        // workers blocked on synchronous NHibernate/SQLite calls this saturates the ThreadPool
        // and starves Kestrel request processing.
        // A dedicated Thread holds the OS thread alive across the entire worker lifetime,
        // keeping job execution off the ThreadPool.
        var thread = new Thread(() =>
        {
            try { RunAsync(ct).GetAwaiter().GetResult(); }
            finally { _completed.TrySetResult(); }
        })
        {
            IsBackground = true,
            Name = $"Queue.{_pool.Name}.{_index}"
        };

        // The worker outlives whatever started it, which may be a request resizing the pools, so
        // it starts from an empty context rather than inheriting that flow's ambient state.
        using (DetachedFlow.Suppress())
            thread.Start();
    }

    private async Task RunAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            _pool.IncrementIdle();
            try
            {
                // Wait for a wake signal or poll timeout (for ScheduledAt expiry checks)
                await _wakeReader.WaitToReadAsync(ct).AsTask()
                    .WaitAsync(TimeSpan.FromMilliseconds(_maxIdlePollMs), ct)
                    .ConfigureAwait(false);
                _wakeReader.TryRead(out _); // drain
            }
            catch (TimeoutException) { /* poll tick — check ScheduledAt-ready jobs */ }
            catch (OperationCanceledException) { _pool.DecrementIdle(); return; }

            _pool.DecrementIdle();

            if (_pool.ShouldAttemptAcquisition?.Invoke() == false) continue;

            var job = _pool.TryAcquire();
            if (job == null) continue;

            _pool.IncrementActive();
            var sw = Stopwatch.StartNew();
            IServiceScope? scope = null;
            var isChainJob = job.ChainId.HasValue;
            // The job's own token, linked to the pool's: a user cancel fires it for this job alone.
            CancellationTokenSource? jobCancellation = null;
            JobExecutionState? execution = null;
            var thisEntry = default(ExecutingEntry);
            try
            {
                // Resolve the job instance using the pool's pre-built type cache
                var jobType = _pool.ResolveJobType(job.JobType);
                if (jobType == null)
                {
                    _logger.LogError("Worker cannot resolve type '{JobType}' — skipping job {Id}", job.JobType, job.Id);
                    _orchestrator.OnComplete(job.Id);
                    continue;
                }

                // Chain jobs share a DI scope for their full lifetime; standalone jobs get a per-execution scope
                scope = isChainJob
                    ? _chainScopeRegistry.GetOrCreateChainScope(job.ChainId!.Value)
                    : _serviceProvider.CreateScope();

                // Stamped per job, not per scope: a chain scope is shared by jobs that may run on
                // workers from different pools, each with its own token.
                jobCancellation = CancellationTokenSource.CreateLinkedTokenSource(ct);
                execution = await _orchestrator.BeginExecutionAsync(job.Id, jobCancellation).ConfigureAwait(false);
                scope.ServiceProvider.GetRequiredService<JobCancellationAccessor>().SetCurrentToken(jobCancellation.Token);
                scope.ServiceProvider.GetRequiredService<JobProgressAccessor>().SetCurrentReporter(execution);

                // Hydrate chain context accessor on first job in this chain (or after crash-recovery scope rebuild),
                // then update which job is currently executing so SetResult tags results by job ID.
                if (isChainJob)
                {
                    var accessor = scope.ServiceProvider.GetRequiredService<JobChainContextAccessor>();
                    if (accessor.GetCurrentContext() == null)
                    {
                        var repo = scope.ServiceProvider.GetRequiredService<IJobChainContextRepository>();
                        accessor.Initialize(await repo.GetOrCreateAsync(job.ChainId!.Value, ct).ConfigureAwait(false));
                    }
                    accessor.SetCurrentJob(job.Id, job.JobKey);
                }

                var instance = (IQueueJob)scope.ServiceProvider.GetRequiredService(jobType);
                JobDataSerializer.Apply(instance, job.JobDataJson);
                instance.Setup(scope.ServiceProvider);
                instance.PostInit();

                // Store TypeName/Title/Details from the resolved instance so API snapshots show them
                _orchestrator.UpdateExecutingItem(job.Id, instance.TypeName, instance.Title, instance.Details);

                var executingEntries = _orchestrator.GetExecuting();
                thisEntry = executingEntries.FirstOrDefault(e => e.Id == job.Id);
                _events.OnJobExecuting(
                    thisEntry,
                    BuildExecutingItems(executingEntries), _orchestrator.WaitingCount,
                    _orchestrator.BlockedWaitingCount, _orchestrator.MaxConcurrentJobs);

                SubExecutionTracker.CurrentJobId.Value = job.Id;

                // Run the job for whoever queued it, and for no one when it was queued without an
                // actor. Jobs it queues capture the same actor, so they inherit it.
                using (_actorAccessor?.Restore(job.Actor))
                    await instance.Process().ConfigureAwait(false);

                sw.Stop();

                // Record success outcome in chain context and persist
                if (isChainJob)
                {
                    var ctx = scope.ServiceProvider.GetRequiredService<JobChainContextAccessor>().GetCurrentContext()!;
                    ctx.AddOutcome(new JobOutcome { JobId = job.Id, JobType = job.JobType, JobKey = job.JobKey, Status = JobOutcomeStatus.Succeeded, CompletedAt = DateTimeOffset.UtcNow });
                    var repo = scope.ServiceProvider.GetRequiredService<IJobChainContextRepository>();
                    try
                    {
                        await repo.SaveAsync(ctx, CancellationToken.None).ConfigureAwait(false);
                    }
                    catch (Exception saveEx)
                    {
                        // Don't let chain-context persistence block OnComplete — after-parent
                        // children would be stuck forever if OnComplete is never called.
                        _logger.LogError(saveEx, "Failed to save chain context for job {Id} ({JobType}); proceeding with OnComplete", job.Id, job.JobType);
                    }
                }

                _orchestrator.OnComplete(job.Id);
                _metrics.RecordCompletion(JobTypeNames.Key(jobType), _pool.Name, sw.Elapsed);

                // Reuse the entry captured pre-Process: it carries TypeName/Title/Details set by
                // UpdateExecutingItem, and by this point the orchestrator has already removed it
                // from _executingSet via OnComplete, so re-querying would return nothing.
                _events.OnJobCompleted(
                    thisEntry,
                    BuildExecutingItems(_orchestrator.GetExecuting()),
                    _orchestrator.WaitingCount, _orchestrator.BlockedWaitingCount,
                    _orchestrator.MaxConcurrentJobs);
            }
            catch (RequeueJobException requeueEx) when (execution?.CancellationRequested is not true)
            {
                sw.Stop();
                _logger.LogDebug(requeueEx, "Job {Id} ({JobType}) requested re-queue (no retry increment)", job.Id, job.JobType);
                // No chain context update — transient; job will retry with same chain state
                await _orchestrator.OnFailureAsync(job.Id, requeueEx, incrementRetry: false, ct: ct).ConfigureAwait(false);
            }
            catch (Exception ex) when (execution?.CancellationRequested is true && ex is not ChainAbortException)
            {
                // A cancelled job is never retried. A re-queue request counts as stopping; any other
                // error (a provider wrapping the cancelled call) ends it as failed.
                sw.Stop();
                var failed = ex is not RequeueJobException && !IsCancellation(ex);
                if (failed)
                    _logger.LogWarning(ex, "Job {Id} ({JobType}) failed after its cancellation was requested; it is not retried", job.Id, job.JobType);
                else
                    _logger.LogInformation("Job {Id} ({JobType}) stopped after its cancellation was requested", job.Id, job.JobType);
                if (isChainJob && scope != null)
                    await RecordChainOutcomeAsync(scope, job, failed ? JobOutcomeStatus.Failed : JobOutcomeStatus.Cancelled, ex).ConfigureAwait(false);

                await _orchestrator.OnCancelledAsync(job.Id, ex, failed).ConfigureAwait(false);
                // Only a job that got as far as its executing event has an entry to report.
                if (thisEntry.Id != Guid.Empty)
                    _events.OnJobCompleted(
                        thisEntry with { CancellationRequested = true, Progress = execution.Progress },
                        BuildExecutingItems(_orchestrator.GetExecuting()),
                        _orchestrator.WaitingCount, _orchestrator.BlockedWaitingCount,
                        _orchestrator.MaxConcurrentJobs);
            }
            catch (Exception ex)
            {
                sw.Stop();
                var isCancellation = ct.IsCancellationRequested;

                if (!isCancellation)
                    _logger.LogError(ex, "Job {Id} ({JobType}) threw an exception", job.Id, job.JobType);
                else
                    _logger.LogDebug(ex, "Job {Id} ({JobType}) was cancelled", job.Id, job.JobType);

                // Record failure/abort outcome before handing off; skip for cancellation (transient)
                if (isChainJob && scope != null && !isCancellation)
                    await RecordChainOutcomeAsync(scope, job, ex is ChainAbortException ? JobOutcomeStatus.Aborted : JobOutcomeStatus.Failed, ex).ConfigureAwait(false);

                // Use CancellationToken.None so cleanup always completes even during shutdown.
                // Cancelled jobs use incrementRetry:false — don't penalise retry count or discard children.
                await _orchestrator.OnFailureAsync(job.Id, ex, incrementRetry: !isCancellation, ct: CancellationToken.None).ConfigureAwait(false);
            }
            finally
            {
                // The orchestrator has let go of the job by now, so nothing can cancel the source any more.
                jobCancellation?.Dispose();
                SubExecutionTracker.ClearStack(job.Id);
                // Only dispose per-job scopes; chain scopes live until CompleteChainScope is called
                if (!isChainJob) scope?.Dispose();
                _pool.DecrementActive();
            }
        }
    }

    /// <summary>
    /// Records a chain job's outcome in the chain context and saves it. Failures are logged,
    /// never thrown.
    /// </summary>
    /// <param name="scope">The chain's scope.</param>
    /// <param name="job">The job.</param>
    /// <param name="status">The job's outcome.</param>
    /// <param name="ex">The exception the job ended with.</param>
    /// <returns>A task that completes once the context is saved.</returns>
    private async Task RecordChainOutcomeAsync(IServiceScope scope, QueuedJob job, JobOutcomeStatus status, Exception ex)
    {
        try
        {
            var accessor = scope.ServiceProvider.GetRequiredService<JobChainContextAccessor>();
            var ctx = accessor.GetCurrentContext();
            if (ctx == null)
                return;

            ctx.AddOutcome(new JobOutcome
            {
                JobId = job.Id,
                JobType = job.JobType,
                JobKey = job.JobKey,
                Status = status,
                ExceptionMessage = ex.Message,
                StackTrace = ex.StackTrace,
                CompletedAt = DateTimeOffset.UtcNow,
            });
            var repo = scope.ServiceProvider.GetRequiredService<IJobChainContextRepository>();
            await repo.SaveAsync(ctx, CancellationToken.None).ConfigureAwait(false);
        }
        catch (Exception saveEx)
        {
            _logger.LogError(saveEx, "Failed to save chain context outcome for job {Id}", job.Id);
        }
    }

    /// <summary>
    /// Whether <paramref name="ex"/> is, or wraps, an <see cref="OperationCanceledException"/>,
    /// which is how a job says it stopped because its token was cancelled.
    /// </summary>
    /// <param name="ex">The exception the job ended with.</param>
    /// <returns><see langword="true"/> when the job stopped for cancellation.</returns>
    internal static bool IsCancellation(Exception ex)
    {
        for (Exception? current = ex; current != null; current = current.InnerException)
        {
            if (current is OperationCanceledException)
                return true;
            if (current is AggregateException aggregate)
                return aggregate.InnerExceptions.Count > 0 && aggregate.InnerExceptions.All(IsCancellation);
        }
        return false;
    }

    private static IReadOnlyList<QueueItem> BuildExecutingItems(
        IReadOnlyList<ExecutingEntry> entries) =>
        entries.Select(e => QueueItem.FromExecuting(e)).ToList();
}
