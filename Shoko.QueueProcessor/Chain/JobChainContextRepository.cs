using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Shoko.QueueProcessor.Storage;

namespace Shoko.QueueProcessor.Chain;

public class JobChainContextRepository : IJobChainContextRepository
{
    private readonly QueueDbContext _db;

    public JobChainContextRepository(QueueDbContext db)
    {
        _db = db;
    }

    public async Task<JobChainContext?> GetAsync(Guid chainId, CancellationToken ct = default)
    {
        var record = await _db.JobChains.FindAsync([chainId], ct);
        if (record == null) return null;
        return JobChainContext.Deserialize(chainId, (ChainStatus)record.Status, record.DataJson, record.ResultsJson, record.OutcomesJson);
    }

    public async Task<JobChainContext> GetOrCreateAsync(Guid chainId, CancellationToken ct = default)
    {
        var record = await _db.JobChains.FindAsync([chainId], ct);
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
        _db.JobChains.Add(record);
        await _db.SaveChangesAsync(ct);
        _db.ChangeTracker.Clear();
        return new JobChainContext(chainId);
    }

    public async Task SaveAsync(JobChainContext context, CancellationToken ct = default)
    {
        var rows = await _db.JobChains
            .Where(c => c.ChainId == context.ChainId)
            .ExecuteUpdateAsync(s => s
                .SetProperty(c => c.Status, (int)context.Status)
                .SetProperty(c => c.DataJson, context.SerializeData())
                .SetProperty(c => c.ResultsJson, context.SerializeResults())
                .SetProperty(c => c.OutcomesJson, context.SerializeOutcomes())
                .SetProperty(c => c.UpdatedAt, c => DateTimeOffset.UtcNow),
            ct);

        if (rows == 0)
        {
            var now = DateTimeOffset.UtcNow;
            _db.JobChains.Add(new QueuedJobChain
            {
                ChainId = context.ChainId,
                Status = (int)context.Status,
                DataJson = context.SerializeData(),
                ResultsJson = context.SerializeResults(),
                OutcomesJson = context.SerializeOutcomes(),
                CreatedAt = now,
                UpdatedAt = now,
            });
            await _db.SaveChangesAsync(ct);
            _db.ChangeTracker.Clear();
        }

        context.MarkClean();
    }

    public async Task AddOutcomesAsync(Guid chainId, IEnumerable<JobOutcome> outcomes, CancellationToken ct = default)
    {
        var outcomeList = outcomes.ToList();
        if (outcomeList.Count == 0) return;

        var record = await _db.JobChains.FindAsync([chainId], ct);
        if (record == null) return;

        var existing = string.IsNullOrEmpty(record.OutcomesJson)
            ? []
            : System.Text.Json.JsonSerializer.Deserialize<List<JobOutcome>>(record.OutcomesJson) ?? [];
        existing.AddRange(outcomeList);

        await _db.JobChains
            .Where(c => c.ChainId == chainId)
            .ExecuteUpdateAsync(s => s
                .SetProperty(c => c.OutcomesJson, System.Text.Json.JsonSerializer.Serialize(existing))
                .SetProperty(c => c.UpdatedAt, c => DateTimeOffset.UtcNow),
            ct);
    }
}
