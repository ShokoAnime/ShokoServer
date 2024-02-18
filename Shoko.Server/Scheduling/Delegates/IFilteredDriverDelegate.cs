using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Quartz.Impl.AdoJobStore;
using Quartz.Spi;

namespace Shoko.Server.Scheduling.Delegates;

public interface IFilteredDriverDelegate : IDriverDelegate
{
    Task<IReadOnlyCollection<TriggerAcquireResult>> SelectTriggerToAcquire(ConnectionAndTransactionHolder conn, DateTimeOffset noLaterThan,
        DateTimeOffset noEarlierThan, int maxCount, IEnumerable<Type> jobTypesToExclude, CancellationToken cancellationToken = new());

    Task<int> SelectWaitingTriggerCount(ConnectionAndTransactionHolder conn, DateTimeOffset noLaterThan, Type[] jobTypesToExclude,
        CancellationToken cancellationToken = new());

    Task<int> SelectBlockedTriggerCount(ConnectionAndTransactionHolder conn, DateTimeOffset noLaterThan, Type[] jobTypesToInclude,
        CancellationToken cancellationToken = new());

    Task<Dictionary<Type, int>> SelectWaitingJobTypeCounts(ConnectionAndTransactionHolder conn, ITypeLoadHelper loadHelper,
        DateTimeOffset noLaterThan, Type[] jobTypesToExclude, CancellationToken cancellationToken = new());

    Task<int> UpdateTriggerStatesForJobFromOtherState(ConnectionAndTransactionHolder conn, IEnumerable<Type> jobTypesToInclude, string state, string oldState,
        CancellationToken cancellationToken = default);
}
