using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Shoko.QueueProcessor.Storage;

using Timer = System.Timers.Timer;

namespace Shoko.QueueProcessor.Orchestration;

/// <summary>
/// Coalesces enqueue (INSERT) and completion (DELETE) DB operations into batched flushes.
/// <para>
/// Key property: if a job is enqueued and completed before the flush fires, it is cancelled out
/// and <strong>never touches the database at all</strong>.
/// </para>
/// <para>
/// Crash semantics:
/// <list type="bullet">
///   <item>Jobs only in <c>_pendingInserts</c> at crash → never persisted → lost (acceptable; fast jobs that completed normally)</item>
///   <item>Jobs only in <c>_pendingDeletes</c> at crash → still in DB → reload as waiting on restart → re-execute (idempotent)</item>
/// </list>
/// </para>
/// </summary>
public sealed class PersistenceBuffer : IAsyncDisposable
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<PersistenceBuffer> _logger;
    private readonly int _flushIntervalMs;
    private readonly int _maxFlushBatch;

    // Pending inserts: keyed by Id for O(1) cancel-on-complete
    private readonly Dictionary<Guid, QueuedJob> _pendingInserts = new();
    // Pending deletes: jobs that were in DB (survived a previous flush or existed from a prior run)
    private readonly HashSet<Guid> _pendingDeletes = new();
    private readonly object _bufferLock = new();

    private readonly SemaphoreSlim _flushGate = new(1, 1);
    private Timer? _timer;

    public PersistenceBuffer(
        IServiceScopeFactory scopeFactory,
        ILogger<PersistenceBuffer> logger,
        int flushIntervalMs = 3000,
        int maxFlushBatch = 500)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
        _flushIntervalMs = flushIntervalMs;
        _maxFlushBatch = maxFlushBatch;
    }

    /// <summary>Buffer a new job for insertion. Arms the flush timer if not already running.</summary>
    public void OnEnqueue(QueuedJob job)
    {
        bool shouldFlush;
        lock (_bufferLock)
        {
            _pendingInserts[job.Id] = job;
            shouldFlush = _pendingInserts.Count + _pendingDeletes.Count >= _maxFlushBatch;
            if (!shouldFlush) ArmTimerLocked();
        }
        if (shouldFlush) _ = FlushNowAsync(CancellationToken.None);
    }

    /// <summary>
    /// Buffers a batch of jobs for insertion under a single lock acquisition.
    /// Used by <see cref="QueueOrchestrator.EnqueueRangeAsync"/> for bulk enqueue.
    /// </summary>
    public void OnEnqueueBatch(IEnumerable<QueuedJob> jobs)
    {
        bool shouldFlush;
        lock (_bufferLock)
        {
            foreach (var job in jobs)
                _pendingInserts[job.Id] = job;
            shouldFlush = _pendingInserts.Count + _pendingDeletes.Count >= _maxFlushBatch;
            if (!shouldFlush) ArmTimerLocked();
        }
        if (shouldFlush) _ = FlushNowAsync(CancellationToken.None);
    }

    /// <summary>
    /// Marks a completed job for deletion. If the job was still in the insert buffer
    /// (completed before the flush fired), it is cancelled out and never written to the DB.
    /// </summary>
    public void OnComplete(Guid id)
    {
        bool shouldFlush;
        lock (_bufferLock)
        {
            if (_pendingInserts.Remove(id))
            {
                // Fast-job case: insert + delete cancel out — zero DB writes
                return;
            }

            _pendingDeletes.Add(id);
            shouldFlush = _pendingDeletes.Count >= _maxFlushBatch;
            if (!shouldFlush) ArmTimerLocked();
        }
        if (shouldFlush) _ = FlushNowAsync(CancellationToken.None);
    }

    /// <summary>
    /// Checks whether <paramref name="id"/> is still in the pending-insert buffer
    /// (i.e., has not yet been persisted to the DB).
    /// </summary>
    public bool IsPendingInsert(Guid id)
    {
        lock (_bufferLock) return _pendingInserts.ContainsKey(id);
    }

    /// <summary>
    /// Forces an immediate flush of all pending inserts and deletes.
    /// Called by <see cref="IAsyncDisposable.DisposeAsync"/> on graceful shutdown.
    /// </summary>
    public async Task FlushNowAsync(CancellationToken ct = default)
    {
        QueuedJob[] inserts;
        Guid[] deletes;

        lock (_bufferLock)
        {
            _timer?.Stop();
            inserts = [.._pendingInserts.Values];
            deletes = [.._pendingDeletes];
            _pendingInserts.Clear();
            _pendingDeletes.Clear();
        }

        if (inserts.Length == 0 && deletes.Length == 0) return;

        await _flushGate.WaitAsync(ct);
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var repo = scope.ServiceProvider.GetRequiredService<IJobRepository>();
            if (inserts.Length > 0)
            {
                _logger.LogDebug("PersistenceBuffer: flushing {InsertCount} inserts", inserts.Length);
                await repo.InsertBatchAsync(inserts, ct);
            }
            if (deletes.Length > 0)
            {
                _logger.LogDebug("PersistenceBuffer: flushing {DeleteCount} deletes", deletes.Length);
                await repo.DeleteBatchAsync(deletes, ct);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "PersistenceBuffer: flush failed");
        }
        finally
        {
            _flushGate.Release();
        }
    }

    public async ValueTask DisposeAsync()
    {
        _timer?.Stop();
        _timer?.Dispose();
        await FlushNowAsync(CancellationToken.None);
        _flushGate.Dispose();
    }

    private void ArmTimerLocked()
    {
        if (_timer != null) return;
        _timer = new Timer(_flushIntervalMs) { AutoReset = false };
        _timer.Elapsed += (_, _) => _ = FlushNowAsync(CancellationToken.None);
        _timer.Start();
    }
}
