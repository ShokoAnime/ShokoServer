using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Quartz;
using Quartz.Impl.AdoJobStore;
using Quartz.Spi;

namespace Shoko.Server.Scheduling.Delegates;

public interface IFilteredDriverDelegate : IDriverDelegate
{
    Task<IReadOnlyCollection<TriggerAcquireResult>> SelectTriggerToAcquire(ConnectionAndTransactionHolder conn, DateTimeOffset noLaterThan,
        DateTimeOffset noEarlierThan, int maxCount, (IEnumerable<Type> TypesToExclude, IDictionary<Type, int> TypesToLimit) jobTypes,
        CancellationToken cancellationToken = new());

    Task<int> SelectWaitingTriggerCount(ConnectionAndTransactionHolder conn, DateTimeOffset noLaterThan, DateTimeOffset noEarlierThan,
        (IEnumerable<Type> TypesToExclude, IDictionary<Type, int> TypesToLimit) jobTypes, CancellationToken cancellationToken = new());

    Task<int> SelectBlockedTriggerCount(ConnectionAndTransactionHolder conn, ITypeLoadHelper loadHelper, DateTimeOffset noLaterThan,
        DateTimeOffset noEarlierThan, (IEnumerable<Type> TypesToExclude, IDictionary<Type, int> TypesToLimit) jobTypes,
        CancellationToken cancellationToken = new());

    Task<int> SelectTotalWaitingTriggerCount(ConnectionAndTransactionHolder conn, DateTimeOffset noLaterThan, DateTimeOffset noEarlierThan,
        CancellationToken cancellationToken = new());

    Task<Dictionary<Type, int>> SelectJobTypeCounts(ConnectionAndTransactionHolder conn, ITypeLoadHelper loadHelper, DateTimeOffset noLaterThan,
        CancellationToken cancellationToken = new());

    Task<int> UpdateTriggerStatesForJobFromOtherState(ConnectionAndTransactionHolder conn, IEnumerable<Type> jobTypesToInclude, string state, string oldState,
        CancellationToken cancellationToken = default);

    Task<List<IJobDetail>> SelectJobs(ConnectionAndTransactionHolder conn, ITypeLoadHelper loadHelper, int maxCount, int offset, DateTimeOffset noLaterThan,
        DateTimeOffset noEarlierThan, (IEnumerable<Type> TypesToExclude, IDictionary<Type, int> TypesToLimit) jobTypes,
        CancellationToken cancellationToken = default);
}
