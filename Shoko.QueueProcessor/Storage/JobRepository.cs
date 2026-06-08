using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;

namespace Shoko.QueueProcessor.Storage;

/// <summary>
/// EF Core implementation of <see cref="IJobRepository"/>.
/// Each method creates and disposes its own <see cref="QueueDbContext"/> via the factory so that
/// concurrent callers never share a context instance.
/// Chunk sizes are conservative (SQLite limit is 999 host parameters per statement,
/// SQL Server is 2100; MySQL is more generous but we use the same chunks for simplicity).
/// </summary>
public class JobRepository : IJobRepository
{
    private readonly IDbContextFactory<QueueDbContext> _factory;
    private const int ChunkSize = 500;

    public JobRepository(IDbContextFactory<QueueDbContext> factory)
    {
        _factory = factory;
    }

    public async Task<List<QueuedJob>> LoadAllAsync(CancellationToken ct = default)
    {
        await using var db = await _factory.CreateDbContextAsync(ct);
        return await db.Jobs
           .OrderBy(j => j.JobType)
           .ThenBy(j => j.ScheduledAt)
           .ThenByDescending(j => j.Priority)
           .ThenBy(j => j.QueuedAt)
           .ToListAsync(ct);
    }

    public async Task InsertBatchAsync(IReadOnlyCollection<QueuedJob> jobs, CancellationToken ct = default)
    {
        if (jobs.Count == 0) return;
        await using var db = await _factory.CreateDbContextAsync(ct);
        foreach (var chunk in jobs.Chunk(ChunkSize))
        {
            await db.Jobs.AddRangeAsync(chunk, ct);
            await db.SaveChangesAsync(ct);
            db.ChangeTracker.Clear();
        }
    }

    public async Task DeleteBatchAsync(IReadOnlyCollection<Guid> ids, CancellationToken ct = default)
    {
        if (ids.Count == 0) return;
        await using var db = await _factory.CreateDbContextAsync(ct);
        foreach (var chunk in ids.Chunk(ChunkSize))
        {
            var idSet = chunk.ToHashSet();
            await db.Jobs.Where(j => idSet.Contains(j.Id)).ExecuteDeleteAsync(ct);
        }
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        await using var db = await _factory.CreateDbContextAsync(ct);
        await db.Jobs.Where(j => j.Id == id).ExecuteDeleteAsync(ct);
    }

    public async Task UpdateRetryAsync(Guid id, int retryCount, DateTimeOffset scheduledAt, CancellationToken ct = default)
    {
        await using var db = await _factory.CreateDbContextAsync(ct);
        await db.Jobs
           .Where(j => j.Id == id)
           .ExecuteUpdateAsync(s => s
               .SetProperty(j => j.RetryCount, retryCount)
               .SetProperty(j => j.ScheduledAt, scheduledAt),
               ct);
    }

    public async Task ClearAllAsync(CancellationToken ct = default)
    {
        await using var db = await _factory.CreateDbContextAsync(ct);
        await db.Jobs.ExecuteDeleteAsync(ct);
    }

    public async Task ActivateChainChildrenAsync(IReadOnlyCollection<Guid> ids, CancellationToken ct = default)
    {
        if (ids.Count == 0) return;
        await using var db = await _factory.CreateDbContextAsync(ct);
        foreach (var chunk in ids.Chunk(ChunkSize))
        {
            var idSet = chunk.ToHashSet();
            await db.Jobs
                .Where(j => idSet.Contains(j.Id))
                .ExecuteUpdateAsync(s => s.SetProperty(j => j.ParentJobId, (Guid?)null), ct);
        }
    }

    public async Task UpdateDataBatchAsync(IReadOnlyCollection<(Guid Id, string? NewJson)> updates, CancellationToken ct = default)
    {
        if (updates.Count == 0) return;
        await using var db = await _factory.CreateDbContextAsync(ct);
        foreach (var (id, newJson) in updates)
            await db.Jobs
                .Where(j => j.Id == id)
                .ExecuteUpdateAsync(s => s.SetProperty(j => j.JobDataJson, newJson), ct);
    }
}
