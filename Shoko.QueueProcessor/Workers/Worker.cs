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
using Shoko.QueueProcessor.Events;
using Shoko.QueueProcessor.Orchestration;

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
    private readonly QueueMetrics _metrics;
    private readonly QueueStateEventHandler _events;
    private readonly ChannelReader<bool> _wakeReader;
    private readonly ILogger<Worker> _logger;
    private readonly int _maxIdlePollMs;

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
        QueueMetrics metrics,
        QueueStateEventHandler events,
        ChannelReader<bool> wakeReader,
        int maxIdlePollMs = 5000)
    {
        _pool = pool;
        _index = index;
        _serviceProvider = serviceProvider;
        _orchestrator = orchestrator;
        _metrics = metrics;
        _events = events;
        _wakeReader = wakeReader;
        _logger = serviceProvider.GetRequiredService<ILogger<Worker>>();
        _maxIdlePollMs = maxIdlePollMs;
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

            var job = _pool.TryAcquire();
            if (job == null) continue;

            _pool.IncrementActive();
            var sw = Stopwatch.StartNew();
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

                using var scope = _serviceProvider.CreateScope();
                var instance = (IQueueJob)scope.ServiceProvider.GetRequiredService(jobType);
                JobDataSerializer.Apply(instance, job.JobDataJson);
                instance.Setup(scope.ServiceProvider);
                instance.PostInit();

                // Store TypeName/Title/Details from the resolved instance so API snapshots show them
                _orchestrator.UpdateExecutingItem(job.Id, instance.TypeName, instance.Title, instance.Details);

                var executingEntries = _orchestrator.GetExecuting();
                var thisEntry = executingEntries.FirstOrDefault(e => e.Id == job.Id);
                _events.OnJobExecuting(
                    thisEntry,
                    BuildExecutingItems(executingEntries), _orchestrator.WaitingCount,
                    _orchestrator.BlockedWaitingCount, _orchestrator.MaxConcurrentJobs);

                await instance.Process().ConfigureAwait(false);

                sw.Stop();
                _orchestrator.OnComplete(job.Id);
                _metrics.RecordCompletion(jobType.Name, _pool.Name, sw.Elapsed);

                _events.OnJobCompleted(
                    job.Id,
                    BuildExecutingItems(_orchestrator.GetExecuting()),
                    _orchestrator.WaitingCount, _orchestrator.BlockedWaitingCount,
                    _orchestrator.MaxConcurrentJobs, _orchestrator.GetMetrics());
            }
            catch (RequeueJobException requeueEx)
            {
                sw.Stop();
                _logger.LogDebug(requeueEx, "Job {Id} ({JobType}) requested re-queue (no retry increment)", job.Id, job.JobType);
                await _orchestrator.OnFailureAsync(job.Id, requeueEx, incrementRetry: false, ct: ct).ConfigureAwait(false);
            }
            catch (Exception ex) when (!ct.IsCancellationRequested)
            {
                sw.Stop();
                _logger.LogError(ex, "Job {Id} ({JobType}) threw an exception", job.Id, job.JobType);
                await _orchestrator.OnFailureAsync(job.Id, ex, incrementRetry: true, ct: ct).ConfigureAwait(false);
            }
            finally
            {
                _pool.DecrementActive();
            }
        }
    }

    private static IReadOnlyList<QueueItem> BuildExecutingItems(
        IReadOnlyList<ExecutingEntry> entries) =>
        entries.Select(e => new QueueItem
        {
            Key = e.JobKey,
            JobType = e.JobType.Name,
            TypeName = string.IsNullOrEmpty(e.TypeName) ? e.JobType.Name : e.TypeName,
            Title = e.Title,
            Details = e.Details,
            Running = true,
            StartTime = e.StartedAt,
            PoolName = e.PoolName,
            RetryCount = e.RetryCount
        }).ToList();
}
