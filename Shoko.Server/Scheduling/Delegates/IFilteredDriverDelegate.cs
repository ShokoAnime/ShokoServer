using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Quartz;
using Quartz.Impl.AdoJobStore;
using Quartz.Spi;
using Shoko.Server.Scheduling.Concurrency;

namespace Shoko.Server.Scheduling.Delegates;

public interface IFilteredDriverDelegate : IDriverDelegate
{
    ValueTask OnConnected(ConnectionAndTransactionHolder conn, CancellationToken cancellationToken = new());
    ValueTask<IReadOnlyCollection<TriggerAcquireResult>> SelectTriggerToAcquire(ConnectionAndTransactionHolder conn, DateTimeOffset noLaterThan,
        DateTimeOffset noEarlierThan, int maxCount, JobTypes jobTypes, CancellationToken cancellationToken = new());

    ValueTask<int> SelectWaitingTriggerCount(ConnectionAndTransactionHolder conn, DateTimeOffset noLaterThan, DateTimeOffset noEarlierThan,
        JobTypes jobTypes, CancellationToken cancellationToken = new());

    ValueTask<int> SelectBlockedTriggerCount(ConnectionAndTransactionHolder conn, ITypeLoadHelper loadHelper, DateTimeOffset noLaterThan,
        DateTimeOffset noEarlierThan, JobTypes jobTypes, CancellationToken cancellationToken = new());

    ValueTask<int> SelectTotalWaitingTriggerCount(ConnectionAndTransactionHolder conn, DateTimeOffset noLaterThan, DateTimeOffset noEarlierThan,
        CancellationToken cancellationToken = new());

    ValueTask<Dictionary<Type, int>> SelectJobTypeCounts(ConnectionAndTransactionHolder conn, ITypeLoadHelper loadHelper, DateTimeOffset noLaterThan,
        CancellationToken cancellationToken = new());

    ValueTask<List<(IJobDetail, bool)>> SelectJobs(ConnectionAndTransactionHolder conn, ITypeLoadHelper loadHelper, int maxCount, int offset,
        DateTimeOffset noLaterThan, DateTimeOffset noEarlierThan, JobTypes jobTypes, bool excludeBlocked, CancellationToken cancellationToken = default);
}
