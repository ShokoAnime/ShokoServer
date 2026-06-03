using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Shoko.QueueProcessor.Chain;

public interface IJobChainContextRepository
{
    Task<JobChainContext?> GetAsync(Guid chainId, CancellationToken ct = default);
    Task<JobChainContext> GetOrCreateAsync(Guid chainId, CancellationToken ct = default);
    Task SaveAsync(JobChainContext context, CancellationToken ct = default);
    Task AddOutcomesAsync(Guid chainId, IEnumerable<JobOutcome> outcomes, CancellationToken ct = default);
    Task DeleteAsync(Guid chainId, CancellationToken ct = default);
}
