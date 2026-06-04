using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Shoko.QueueProcessor.Storage;

namespace Shoko.QueueProcessor.Chain;

/// <summary>
/// EF Core implementation of <see cref="IJobChainContextRepository"/>.
/// Each method creates and disposes its own <see cref="QueueDbContext"/> via the factory so that
/// concurrent callers (e.g. a worker starting a finally job while the abort handler is still writing
/// outcomes) never share a context instance.
/// </summary>
public class JobChainContextRepository : IJobChainContextRepository
{
    private readonly IDbContextFactory<QueueDbContext> _factory;

    public JobChainContextRepository(IDbContextFactory<QueueDbContext> factory)
    {
        _factory = factory;
    }

    public async Task<JobChainContext?> GetAsync(Guid chainId, CancellationToken ct = default)
    {
        await using var db = await _factory.CreateDbContextAsync(ct);
        var record = await db.JobChains.FindAsync([chainId], ct);
        if (record == null) return null;
        return JobChainContext.Deserialize(chainId, (ChainStatus)record.Status, record.DataJson, record.ResultsJson, record.OutcomesJson);
    }

    public async Task<JobChainContext> GetOrCreateAsync(Guid chainId, CancellationToken ct = default)
    {
        await using var db = await _factory.CreateDbContextAsync(ct);
        var record = await db.JobChains.FindAsync([chainId], ct);
        if (record != null)
            return JobChainContext.Deserialize(chainId, (ChainStatus)record.Status, record.DataJson, record.ResultsJson, record.OutcomesJson);

        var now = DateTimeOffset.UtcNow;
        record = new QueuedJobChain
        {
            ChainId = chainId,
            Status = (int)ChainStatus.Active,
            CreatedAt = now,
            UpdatedAt = now,
        };
        db.JobChains.Add(record);
        await db.SaveChangesAsync(ct);
        return new JobChainContext(chainId);
    }

    public async Task SaveAsync(JobChainContext context, CancellationToken ct = default)
    {
        await using var db = await _factory.CreateDbContextAsync(ct);
        var now = DateTimeOffset.UtcNow;
        var rows = await db.JobChains
            .Where(c => c.ChainId == context.ChainId)
            .ExecuteUpdateAsync(s => s
                .SetProperty(c => c.Status, (int)context.Status)
                .SetProperty(c => c.DataJson, context.SerializeData())
                .SetProperty(c => c.ResultsJson, context.SerializeResults())
                .SetProperty(c => c.OutcomesJson, context.SerializeOutcomes())
                .SetProperty(c => c.UpdatedAt, now),
            ct);

        if (rows == 0)
        {
            db.JobChains.Add(new QueuedJobChain
            {
                ChainId = context.ChainId,
                Status = (int)context.Status,
                DataJson = context.SerializeData(),
                ResultsJson = context.SerializeResults(),
                OutcomesJson = context.SerializeOutcomes(),
                CreatedAt = now,
                UpdatedAt = now,
            });
            await db.SaveChangesAsync(ct);
        }

        context.MarkClean();
    }

    public async Task DeleteAsync(Guid chainId, CancellationToken ct = default)
    {
        await using var db = await _factory.CreateDbContextAsync(ct);
        await db.JobChains.Where(c => c.ChainId == chainId).ExecuteDeleteAsync(ct);
    }

    public async Task AddOutcomesAsync(Guid chainId, IEnumerable<JobOutcome> outcomes, CancellationToken ct = default)
    {
        var outcomeList = outcomes.ToList();
        if (outcomeList.Count == 0) return;

        await using var db = await _factory.CreateDbContextAsync(ct);
        var record = await db.JobChains.FindAsync([chainId], ct);
        if (record == null) return;

        var existing = string.IsNullOrEmpty(record.OutcomesJson)
            ? []
            : JsonSerializer.Deserialize<List<JobOutcome>>(record.OutcomesJson) ?? [];
        existing.AddRange(outcomeList);

        var now = DateTimeOffset.UtcNow;
        await db.JobChains
            .Where(c => c.ChainId == chainId)
            .ExecuteUpdateAsync(s => s
                .SetProperty(c => c.OutcomesJson, JsonSerializer.Serialize(existing))
                .SetProperty(c => c.UpdatedAt, now),
            ct);
    }
}
