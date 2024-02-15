using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Quartz.Impl.AdoJobStore;

namespace Shoko.Server.Scheduling.Delegates;

public class SQLiteDelegate : Quartz.Impl.AdoJobStore.SQLiteDelegate, IFilteredDriverDelegate
{
    private string _schedulerName;

    private string[] GetJobClasses(IEnumerable<Type> types) => types.Select(GetStorableJobTypeName).ToArray();

    private string GetSelectNextTriggerToAcquireExcludingTypesSql(int maxCount)
    {
        return $@"SELECT t.{ColumnTriggerName}, t.{ColumnTriggerGroup}, jd.{ColumnJobClass}
              FROM {TablePrefixSubst}{TableTriggers} t
              JOIN {TablePrefixSubst}{TableJobDetails} jd ON (jd.{ColumnSchedulerName} = t.{ColumnSchedulerName} AND  jd.{ColumnJobGroup} = t.{ColumnJobGroup} AND jd.{ColumnJobName} = t.{ColumnJobName}) 
              WHERE
                t.{ColumnSchedulerName} = @schedulerName AND {ColumnTriggerState} = @state AND {ColumnNextFireTime} <= @noLaterThan AND ({ColumnMifireInstruction} = -1 OR ({ColumnMifireInstruction} <> -1 AND {ColumnNextFireTime} >= @noEarlierThan))
                AND jd.{ColumnJobClass} NOT IN (@types)
              ORDER BY {ColumnPriority} DESC, {ColumnNextFireTime} ASC
              LIMIT {maxCount};";
    }

    private const string SelectWaitingTriggerCountSql= @$"SELECT COUNT(1)
              FROM {TablePrefixSubst}{TableTriggers} t
              JOIN {TablePrefixSubst}{TableJobDetails} jd ON (jd.{ColumnSchedulerName} = t.{ColumnSchedulerName} AND  jd.{ColumnJobGroup} = t.{ColumnJobGroup} AND jd.{ColumnJobName} = t.{ColumnJobName}) 
              WHERE t.{ColumnSchedulerName} = @schedulerName AND {ColumnTriggerState} = '{StateWaiting}' AND {ColumnNextFireTime} <= @noLaterThan AND ({ColumnMifireInstruction} = -1 OR ({ColumnMifireInstruction} <> -1 AND {ColumnNextFireTime} >= @noEarlierThan))";
    
    private const string SelectWaitingTriggerCountExcludingTypesSql= @$"SELECT COUNT(1)
              FROM {TablePrefixSubst}{TableTriggers} t
              JOIN {TablePrefixSubst}{TableJobDetails} jd ON (jd.{ColumnSchedulerName} = t.{ColumnSchedulerName} AND  jd.{ColumnJobGroup} = t.{ColumnJobGroup} AND jd.{ColumnJobName} = t.{ColumnJobName}) 
              WHERE t.{ColumnSchedulerName} = @schedulerName AND {ColumnTriggerState} = '{StateWaiting}' AND {ColumnNextFireTime} <= @noLaterThan AND ({ColumnMifireInstruction} = -1 OR ({ColumnMifireInstruction} <> -1 AND {ColumnNextFireTime} >= @noEarlierThan)) AND jd.{ColumnJobClass} NOT IN (@types)";

    private const string SelectBlockedTriggerCountSql= @$"SELECT COUNT(1)
              FROM {TablePrefixSubst}{TableTriggers} t
              JOIN {TablePrefixSubst}{TableJobDetails} jd ON (jd.{ColumnSchedulerName} = t.{ColumnSchedulerName} AND  jd.{ColumnJobGroup} = t.{ColumnJobGroup} AND jd.{ColumnJobName} = t.{ColumnJobName}) 
              WHERE t.{ColumnSchedulerName} = @schedulerName AND (({ColumnTriggerState} = '{StateWaiting}' AND jd.{ColumnJobClass} IN (@types)) OR {ColumnTriggerState} = '{StateBlocked}') AND {ColumnNextFireTime} <= @noLaterThan AND ({ColumnMifireInstruction} = -1 OR ({ColumnMifireInstruction} <> -1 AND {ColumnNextFireTime} >= @noEarlierThan))";

    const string UpdateJobTriggerStatesFromOtherStateSql = @$"UPDATE {TablePrefixSubst}{TableTriggers} SET {ColumnTriggerState} = @state
               FROM {TablePrefixSubst}{TableTriggers} t
               INNER JOIN {TablePrefixSubst}{TableJobDetails} jd ON (jd.{ColumnSchedulerName} = t.{ColumnSchedulerName} AND  jd.{ColumnJobGroup} = t.{ColumnJobGroup} AND jd.{ColumnJobName} = t.{ColumnJobName})
               WHERE t.{ColumnSchedulerName} = @schedulerName AND t.{ColumnTriggerState} = @oldState AND jd.{ColumnJobClass} IN (@types)";

    public override void Initialize(DelegateInitializationArgs args)
    {
        base.Initialize(args);
        _schedulerName = args.InstanceName;
    }

    public override async Task<IReadOnlyCollection<TriggerAcquireResult>> SelectTriggerToAcquire(
        ConnectionAndTransactionHolder conn,
        DateTimeOffset noLaterThan,
        DateTimeOffset noEarlierThan,
        int maxCount,
        CancellationToken cancellationToken = default)
    {
        return await SelectTriggerToAcquire(conn, noLaterThan, noEarlierThan, maxCount, null, cancellationToken);
    }

    public async Task<IReadOnlyCollection<TriggerAcquireResult>> SelectTriggerToAcquire(ConnectionAndTransactionHolder conn, DateTimeOffset noLaterThan,
        DateTimeOffset noEarlierThan, int maxCount, IEnumerable<Type> jobTypesToExclude, CancellationToken cancellationToken = default)
    {
        if (maxCount < 1)
        {
            maxCount = 1; // we want at least one trigger back.
        }

        var hasTypes = jobTypesToExclude?.Any() ?? false;
        await using var cmd = PrepareCommand(conn,
            ReplaceTablePrefix(hasTypes ? GetSelectNextTriggerToAcquireExcludingTypesSql(maxCount) : GetSelectNextTriggerToAcquireSql(maxCount)));
        List<TriggerAcquireResult> nextTriggers = new();

        AddCommandParameter(cmd, "schedulerName", _schedulerName);
        AddCommandParameter(cmd, "state", StateWaiting);
        AddCommandParameter(cmd, "noLaterThan", GetDbDateTimeValue(noLaterThan));
        AddCommandParameter(cmd, "noEarlierThan", GetDbDateTimeValue(noEarlierThan));
        if (hasTypes) cmd.AddArrayParameters("types", GetJobClasses(jobTypesToExclude));

        await using var rs = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        // signal cancel, otherwise ADO.NET might have trouble handling partial reads from open reader
        var shouldStop = false;
        while (await rs.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            if (shouldStop)
            {
                cmd.Cancel();
                break;
            }

            if (nextTriggers.Count < maxCount)
            {
                var result = new TriggerAcquireResult(
                    (string)rs[ColumnTriggerName],
                    (string)rs[ColumnTriggerGroup],
                    (string)rs[ColumnJobClass]);
                nextTriggers.Add(result);
            }
            else
            {
                shouldStop = true;
            }
        }

        return nextTriggers;
    }

    public virtual async Task<int> SelectWaitingTriggerCount(ConnectionAndTransactionHolder conn, DateTimeOffset noLaterThan, Type[] jobTypesToExclude,
        CancellationToken cancellationToken = new())
    {
        var hasTypes = jobTypesToExclude is { Length: > 0 };
        await using var cmd = PrepareCommand(conn, ReplaceTablePrefix(hasTypes ? SelectWaitingTriggerCountExcludingTypesSql : SelectWaitingTriggerCountSql));
        AddCommandParameter(cmd, "schedulerName", _schedulerName);
        AddCommandParameter(cmd, "noLaterThan", GetDbDateTimeValue(noLaterThan));
        AddCommandParameter(cmd, "noEarlierThan", GetDbDateTimeValue(DateTimeOffset.UtcNow - TimeSpan.FromMinutes(1)));
        if (hasTypes) cmd.AddArrayParameters("types", GetJobClasses(jobTypesToExclude));

        var rs = await cmd.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
        return Convert.ToInt32(rs);
    }

    public virtual async Task<int> SelectBlockedTriggerCount(ConnectionAndTransactionHolder conn, DateTimeOffset noLaterThan, Type[] jobTypesToInclude,
        CancellationToken cancellationToken = new())
    {
        await using var cmd = PrepareCommand(conn, ReplaceTablePrefix(SelectBlockedTriggerCountSql));
        AddCommandParameter(cmd, "schedulerName", _schedulerName);
        AddCommandParameter(cmd, "noLaterThan", GetDbDateTimeValue(noLaterThan));
        AddCommandParameter(cmd, "noEarlierThan", GetDbDateTimeValue(DateTimeOffset.UtcNow - TimeSpan.FromMinutes(1)));
        cmd.AddArrayParameters("types", GetJobClasses(jobTypesToInclude));

        var rs = await cmd.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
        return Convert.ToInt32(rs);
    }

    public virtual async Task<int> UpdateTriggerStatesForJobFromOtherState(ConnectionAndTransactionHolder conn, IEnumerable<Type> jobTypesToInclude,
        string state, string oldState, CancellationToken cancellationToken = default)
    {
        await using var cmd = PrepareCommand(conn, ReplaceTablePrefix(UpdateJobTriggerStatesFromOtherStateSql));
        AddCommandParameter(cmd, "schedulerName", _schedulerName);
        cmd.AddArrayParameters("types", GetJobClasses(jobTypesToInclude));
        AddCommandParameter(cmd, "state", state);
        AddCommandParameter(cmd, "oldState", oldState);

        return await cmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }
}
