using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;

namespace Shoko.QueueProcessor.Storage;

/// <summary>
/// EF Core implementation of <see cref="IJobRepository"/>.
/// Chunk sizes are conservative (SQLite limit is 999 host parameters per statement,
/// SQL Server is 2100; MySQL is more generous but we use the same chunks for simplicity).
/// </summary>
public class JobRepository : IJobRepository
{
    private readonly QueueDbContext _db;
    private const int _chunkSize = 500;

    public JobRepository(QueueDbContext db)
    {
        _db = db;
    }

    public Task<List<QueuedJob>> LoadAllAsync(CancellationToken ct = default) =>
        _db.Jobs
           .OrderBy(j => j.JobType)
           .ThenBy(j => j.ScheduledAt)
           .ThenByDescending(j => j.Priority)
           .ThenBy(j => j.QueuedAt)
           .ToListAsync(ct);

    public async Task InsertBatchAsync(IReadOnlyCollection<QueuedJob> jobs, CancellationToken ct = default)
    {
        if (jobs.Count == 0) return;
        foreach (var chunk in jobs.Chunk(_chunkSize))
        {
            await _db.Jobs.AddRangeAsync(chunk, ct);
            await _db.SaveChangesAsync(ct);
            _db.ChangeTracker.Clear();
        }
    }

    public async Task DeleteBatchAsync(IReadOnlyCollection<Guid> ids, CancellationToken ct = default)
    {
        if (ids.Count == 0) return;
        foreach (var chunk in ids.Chunk(_chunkSize))
        {
            var idSet = chunk.ToHashSet();
            await _db.Jobs.Where(j => idSet.Contains(j.Id)).ExecuteDeleteAsync(ct);
        }
    }

    public Task DeleteAsync(Guid id, CancellationToken ct = default) =>
        _db.Jobs.Where(j => j.Id == id).ExecuteDeleteAsync(ct);

    public Task UpdateRetryAsync(Guid id, int retryCount, DateTimeOffset scheduledAt, CancellationToken ct = default) =>
        _db.Jobs
           .Where(j => j.Id == id)
           .ExecuteUpdateAsync(s => s
               .SetProperty(j => j.RetryCount, retryCount)
               .SetProperty(j => j.ScheduledAt, scheduledAt),
               ct);

    public Task ClearAllAsync(CancellationToken ct = default) =>
        _db.Jobs.ExecuteDeleteAsync(ct);
}
