#nullable enable
using System;
using System.Diagnostics;
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
    private readonly IServiceProvider _serviceProvider;
    private readonly QueueOrchestrator _orchestrator;
    private readonly QueueMetrics _metrics;
    private readonly QueueStateEventHandler _events;
    private readonly ChannelReader<bool> _wakeReader;
    private readonly ILogger<Worker> _logger;
    private readonly int _maxIdlePollMs;

    public Worker(
        WorkerPool pool,
        IServiceProvider serviceProvider,
        QueueOrchestrator orchestrator,
        QueueMetrics metrics,
        QueueStateEventHandler events,
        ChannelReader<bool> wakeReader,
        int maxIdlePollMs = 5000)
    {
        _pool = pool;
        _serviceProvider = serviceProvider;
        _orchestrator = orchestrator;
        _metrics = metrics;
        _events = events;
        _wakeReader = wakeReader;
        _logger = serviceProvider.GetRequiredService<ILogger<Worker>>();
        _maxIdlePollMs = maxIdlePollMs;
    }

    public void Start(CancellationToken ct) =>
        Task.Factory.StartNew(() => RunAsync(ct), ct, TaskCreationOptions.LongRunning, TaskScheduler.Default);

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
                // Resolve the job instance — type lives in Shoko.Server, no compile-time dep
                var jobType = Type.GetType(job.JobType);
                if (jobType == null)
                {
                    _logger.LogError("Worker cannot resolve type '{JobType}' — skipping job {Id}", job.JobType, job.Id);
                    _orchestrator.OnComplete(job.Id);
                    continue;
                }

                using var scope = _serviceProvider.CreateScope();
                var instance = (IQueueJob)scope.ServiceProvider.GetRequiredService(jobType);
                JobDataSerializer.Apply(instance, job.JobDataJson);
                instance.PostInit();

                var executingEntries = _orchestrator.GetExecuting();
                var thisEntry = System.Linq.Enumerable.FirstOrDefault(executingEntries, e => e.Id == job.Id);
                _events.OnJobExecuting(
                    thisEntry,
                    BuildExecutingItems(executingEntries), BuildWaitingItems(), _orchestrator.WaitingCount,
                    0, _pool.MaxWorkers);

                await instance.Process().ConfigureAwait(false);

                sw.Stop();
                _orchestrator.OnComplete(job.Id);
                _metrics.RecordCompletion(jobType.Name, _pool.Name, sw.Elapsed);

                _events.OnJobCompleted(
                    job.Id,
                    BuildExecutingItems(_orchestrator.GetExecuting()), BuildWaitingItems(),
                    _orchestrator.WaitingCount, 0, _pool.MaxWorkers, _orchestrator.GetMetrics());
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

    private static System.Collections.Generic.IReadOnlyList<Abstractions.QueueItem> BuildExecutingItems(
        System.Collections.Generic.IReadOnlyList<ExecutingEntry> entries) =>
        System.Linq.Enumerable.ToList(System.Linq.Enumerable.Select(entries, e => new Abstractions.QueueItem
        {
            Key = e.Id.ToString(),
            JobType = e.JobType.Name,
            Running = true,
            StartTime = e.StartedAt,
            PoolName = e.PoolName,
            RetryCount = e.RetryCount
        }));

    private static System.Collections.Generic.IReadOnlyList<Abstractions.QueueItem> BuildWaitingItems() => [];
}
