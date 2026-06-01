using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Shoko.QueueProcessor.Storage;

/// <summary>
/// Data-access interface for <see cref="QueuedJob"/> rows.
/// All writes are driven by <see cref="Orchestration.PersistenceBuffer"/> except
/// <see cref="UpdateRetryAsync"/> (written immediately on failure so the backoff survives restarts).
/// </summary>
public interface IJobRepository
{
    /// <summary>
    /// Loads all persisted jobs in the order optimised for pool sub-queue population
    /// (<c>ORDER BY JobType, ScheduledAt, Priority DESC, QueuedAt ASC</c>).
    /// Called once at startup.
    /// </summary>
    Task<List<QueuedJob>> LoadAllAsync(CancellationToken ct = default);

    /// <summary>Inserts a batch of new jobs in a single statement (or minimal statements for SQLite chunking).</summary>
    Task InsertBatchAsync(IReadOnlyCollection<QueuedJob> jobs, CancellationToken ct = default);

    /// <summary>
    /// Deletes a batch of completed jobs by their IDs.
    /// Chunked internally to respect provider-specific host-parameter limits
    /// (SQLite ≤ 999, SQL Server ≤ 2100).
    /// </summary>
    Task DeleteBatchAsync(IReadOnlyCollection<Guid> ids, CancellationToken ct = default);

    /// <summary>Deletes a single job immediately (used for max-retries-exceeded discard path).</summary>
    Task DeleteAsync(Guid id, CancellationToken ct = default);

    /// <summary>
    /// Updates <see cref="QueuedJob.RetryCount"/> and <see cref="QueuedJob.ScheduledAt"/> for
    /// a failed job that will be retried. Written immediately (not buffered) so the backoff
    /// schedule survives a crash.
    /// </summary>
    Task UpdateRetryAsync(Guid id, int retryCount, DateTimeOffset scheduledAt, CancellationToken ct = default);

    /// <summary>Removes all waiting jobs (truncates the table). Used by queue clear.</summary>
    Task ClearAllAsync(CancellationToken ct = default);

    /// <summary>
    /// Clears <see cref="QueuedJob.ParentJobId"/> for the given job IDs, promoting them from
    /// chain-deferred to active. Called by <see cref="Orchestration.PersistenceBuffer"/> on activation flush.
    /// </summary>
    Task ActivateChainChildrenAsync(IReadOnlyCollection<Guid> ids, CancellationToken ct = default);
}
