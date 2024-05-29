using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Data;
using System.Data.Common;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Quartz;
using Quartz.Impl;
using Quartz.Impl.AdoJobStore;
using Quartz.Spi;
using Shoko.Server.Scheduling.Concurrency;
using Shoko.Server.Scheduling.DatabaseLocks;

namespace Shoko.Server.Scheduling.Delegates;

public class MySQLDelegate : Quartz.Impl.AdoJobStore.MySQLDelegate, IFilteredDriverDelegate
{
    private string _schedulerName;
    private const string Blocked = "Blocked";
    private const string SubQuery = "{SubQuery}";

    private IEnumerable<string> GetJobClasses(IEnumerable<Type> types) => types.Select(a => new JobType(a).FullName).ToArray();

    private const string GetSelectPartNoExclusions = @$"SELECT t.{ColumnTriggerName}, t.{ColumnTriggerGroup}, jd.{ColumnJobClass}, t.{ColumnPriority}, t.{ColumnNextFireTime}, 0 as {Blocked}
              FROM {TablePrefixSubst}{TableTriggers} t
              JOIN {TablePrefixSubst}{TableJobDetails} jd ON (jd.{ColumnSchedulerName} = t.{ColumnSchedulerName} AND  jd.{ColumnJobGroup} = t.{ColumnJobGroup} AND jd.{ColumnJobName} = t.{ColumnJobName}) 
              WHERE t.{ColumnSchedulerName} = @schedulerName AND {ColumnTriggerState} = @state AND {ColumnNextFireTime} <= @noLaterThan AND ({ColumnMifireInstruction} = -1 OR ({ColumnMifireInstruction} <> -1 AND {ColumnNextFireTime} >= @noEarlierThan))";

    private const string GetSelectPartExcludingTypes = @$"SELECT t.{ColumnTriggerName}, t.{ColumnTriggerGroup}, jd.{ColumnJobClass}, t.{ColumnPriority}, t.{ColumnNextFireTime}, 0 as {Blocked}
              FROM {TablePrefixSubst}{TableTriggers} t
              JOIN {TablePrefixSubst}{TableJobDetails} jd ON (jd.{ColumnSchedulerName} = t.{ColumnSchedulerName} AND  jd.{ColumnJobGroup} = t.{ColumnJobGroup} AND jd.{ColumnJobName} = t.{ColumnJobName}) 
              WHERE t.{ColumnSchedulerName} = @schedulerName AND {ColumnTriggerState} = @state AND {ColumnNextFireTime} <= @noLaterThan AND ({ColumnMifireInstruction} = -1 OR ({ColumnMifireInstruction} <> -1 AND {ColumnNextFireTime} >= @noEarlierThan))
                AND jd.{ColumnJobClass} NOT IN (@types)";

    // when using limit, we need to sort first
    private static string GetSelectPartOfType(int index, bool limit)
    {
        return @$"SELECT t.{ColumnTriggerName}, t.{ColumnTriggerGroup}, jd.{ColumnJobClass}, t.{ColumnPriority}, t.{ColumnNextFireTime}, @limitBlocked{index} as {Blocked}
              FROM {TablePrefixSubst}{TableTriggers} t
              JOIN {TablePrefixSubst}{TableJobDetails} jd ON (jd.{ColumnSchedulerName} = t.{ColumnSchedulerName} AND  jd.{ColumnJobGroup} = t.{ColumnJobGroup} AND jd.{ColumnJobName} = t.{ColumnJobName}) 
              WHERE t.{ColumnSchedulerName} = @schedulerName AND {ColumnTriggerState} = @state AND {ColumnNextFireTime} <= @noLaterThan AND ({ColumnMifireInstruction} = -1 OR ({ColumnMifireInstruction} <> -1 AND {ColumnNextFireTime} >= @noEarlierThan))
                AND jd.{ColumnJobClass} = @limit{index}Type
              ORDER BY t.{ColumnPriority} DESC, t.{ColumnNextFireTime} ASC
              {(limit ? $"LIMIT @limit{index} " : "")}OFFSET @offset{index}";
    }

    // when using limit, we need to sort first
    private static string GetSelectPartInTypes(int index, bool limit)
    {
        return @$"SELECT t.{ColumnTriggerName}, t.{ColumnTriggerGroup}, jd.{ColumnJobClass}, t.{ColumnPriority}, t.{ColumnNextFireTime}, @groupBlocked{index} as {Blocked}
              FROM {TablePrefixSubst}{TableTriggers} t
              JOIN {TablePrefixSubst}{TableJobDetails} jd ON (jd.{ColumnSchedulerName} = t.{ColumnSchedulerName} AND  jd.{ColumnJobGroup} = t.{ColumnJobGroup} AND jd.{ColumnJobName} = t.{ColumnJobName}) 
              WHERE t.{ColumnSchedulerName} = @schedulerName AND {ColumnTriggerState} = @state AND {ColumnNextFireTime} <= @noLaterThan AND ({ColumnMifireInstruction} = -1 OR ({ColumnMifireInstruction} <> -1 AND {ColumnNextFireTime} >= @noEarlierThan))
                AND jd.{ColumnJobClass} IN (@groupLimit{index}Types)
              ORDER BY t.{ColumnPriority} DESC, t.{ColumnNextFireTime} ASC
              {(limit ? $"LIMIT @groupLimit{index} " : "")}OFFSET @groupOffset{index}";
    }

    private const string GetCountNoExclusions = @$"SELECT Count(1)
              FROM {TablePrefixSubst}{TableTriggers} t
              WHERE t.{ColumnSchedulerName} = @schedulerName AND {ColumnTriggerState} = '{StateWaiting}' AND {ColumnNextFireTime} <= @noLaterThan AND ({ColumnMifireInstruction} = -1 OR ({ColumnMifireInstruction} <> -1 AND {ColumnNextFireTime} >= @noEarlierThan))";

    private const string SelectBlockedTypeCountsSql= @$"SELECT jd.{ColumnJobClass}, COUNT(jd.{ColumnJobClass}) AS Count
              FROM {TablePrefixSubst}{TableTriggers} t
              JOIN {TablePrefixSubst}{TableJobDetails} jd ON (jd.{ColumnSchedulerName} = t.{ColumnSchedulerName} AND  jd.{ColumnJobGroup} = t.{ColumnJobGroup} AND jd.{ColumnJobName} = t.{ColumnJobName}) 
              WHERE t.{ColumnSchedulerName} = @schedulerName AND (({ColumnTriggerState} = '{StateWaiting}' AND jd.{ColumnJobClass} IN (@types)) OR {ColumnTriggerState} = '{StateBlocked}') AND {ColumnNextFireTime} <= @noLaterThan AND ({ColumnMifireInstruction} = -1 OR ({ColumnMifireInstruction} <> -1 AND {ColumnNextFireTime} >= @noEarlierThan))
              GROUP BY jd.{ColumnJobClass} HAVING COUNT(1) > 0";

    private const string SelectJobClassesAndCountSql= @$"SELECT jd.{ColumnJobClass}, COUNT(jd.{ColumnJobClass}) AS Count
              FROM {TablePrefixSubst}{TableTriggers} t
              JOIN {TablePrefixSubst}{TableJobDetails} jd ON (jd.{ColumnSchedulerName} = t.{ColumnSchedulerName} AND  jd.{ColumnJobGroup} = t.{ColumnJobGroup} AND jd.{ColumnJobName} = t.{ColumnJobName}) 
              WHERE t.{ColumnSchedulerName} = @schedulerName AND {ColumnTriggerState} = '{StateWaiting}' AND {ColumnNextFireTime} <= @noLaterThan AND ({ColumnMifireInstruction} = -1 OR ({ColumnMifireInstruction} <> -1 AND {ColumnNextFireTime} >= @noEarlierThan))
              GROUP BY jd.{ColumnJobClass} HAVING COUNT(1) > 0";

    private const string GetJobSql = @$"SELECT jd.{ColumnJobName}, jd.{ColumnJobGroup}, jd.{ColumnDescription}, jd.{ColumnJobClass}, jd.{ColumnIsDurable}, jd.{ColumnRequestsRecovery}, jd.{ColumnJobDataMap}, jd.{ColumnIsNonConcurrent}, jd.{ColumnIsUpdateData}, t.{Blocked}
              FROM ({SubQuery}) t
              JOIN {TablePrefixSubst}{TableTriggers} t1 on t.{ColumnTriggerName} = t1.{ColumnTriggerName} AND t.{ColumnTriggerGroup} = t1.{ColumnTriggerGroup} AND t1.{ColumnSchedulerName} = @schedulerName
              JOIN {TablePrefixSubst}{TableJobDetails} jd ON (jd.{ColumnSchedulerName} = t1.{ColumnSchedulerName} AND jd.{ColumnJobGroup} = t1.{ColumnJobGroup} AND jd.{ColumnJobName} = t1.{ColumnJobName}) 
              ORDER BY {Blocked} ASC, t.{ColumnPriority} DESC, t.{ColumnNextFireTime} ASC
              LIMIT @limit OFFSET @offset";

    public override void Initialize(IJobStore jobStore, DelegateInitializationArgs args)
    {
        base.Initialize(jobStore, args);
        _schedulerName = args.InstanceName;
        lockHandler = new NoLockSemaphore();
    }

    public ValueTask OnConnected(ConnectionAndTransactionHolder conn, CancellationToken cancellationToken = new CancellationToken())
    {
        return default;
    }

    public override async ValueTask<IReadOnlyCollection<TriggerAcquireResult>> SelectTriggerToAcquire(
        ConnectionAndTransactionHolder conn,
        DateTimeOffset noLaterThan, DateTimeOffset noEarlierThan, int maxCount, CancellationToken cancellationToken = default)
    {
        return await SelectTriggerToAcquire(conn, noLaterThan, noEarlierThan, maxCount, new JobTypes(), cancellationToken);
    }

    public async ValueTask<IReadOnlyCollection<TriggerAcquireResult>> SelectTriggerToAcquire(ConnectionAndTransactionHolder conn, DateTimeOffset noLaterThan,
        DateTimeOffset noEarlierThan, int maxCount, JobTypes jobTypes, CancellationToken cancellationToken = default)
    {
        if (maxCount < 1) maxCount = 1; // we want at least one trigger back.

        var hasExcludeTypes = jobTypes.TypesToExclude.Any() || jobTypes.TypesToLimit.Any() || jobTypes.AvailableConcurrencyGroups.Any(a => a.Any());
        var commandText = new StringBuilder();
        commandText.Append($"SELECT u.{ColumnTriggerName}, u.{ColumnTriggerGroup}, u.{ColumnJobClass} FROM (");
        commandText.Append(hasExcludeTypes ? GetSelectPartExcludingTypes : GetSelectPartNoExclusions);

        int index;
        for (index = 0; index < jobTypes.TypesToLimit.Count; index++)
        {
            commandText.Append("\nUNION SELECT * FROM (\n");
            commandText.Append(GetSelectPartOfType(index, true));
            commandText.Append("\n)");
        }

        for (index = 0; index < jobTypes.AvailableConcurrencyGroups.Count(); index++)
        {
            commandText.Append("\nUNION SELECT * FROM (\n");
            commandText.Append(GetSelectPartInTypes(index, true));
            commandText.Append("\n)");
        }

        commandText.Append($") u\nORDER BY {ColumnPriority} DESC, {ColumnNextFireTime} ASC\nLIMIT @limit");
        await using var cmd = PrepareCommand(conn, ReplaceTablePrefix(commandText.ToString()));
        AddCommandParameter(cmd, "schedulerName", _schedulerName);
        AddCommandParameter(cmd, "state", StateWaiting);
        AddCommandParameter(cmd, "noLaterThan", GetDbDateTimeValue(noLaterThan));
        AddCommandParameter(cmd, "noEarlierThan", GetDbDateTimeValue(noEarlierThan));
        AddCommandParameter(cmd, "limit", maxCount);
        if (hasExcludeTypes)
            cmd.AddArrayParameters("types",
                GetJobClasses(jobTypes.TypesToExclude.Union(jobTypes.TypesToLimit.Keys).Union(jobTypes.AvailableConcurrencyGroups.SelectMany(a => a))));

        index = 0;
        foreach (var kv in jobTypes.TypesToLimit)
        {
            AddCommandParameter(cmd, $"limitBlocked{index}", 0);
            AddCommandParameter(cmd, $"limit{index}Type", new JobType(kv.Key).FullName);
            AddCommandParameter(cmd, $"limit{index}", kv.Value);
            AddCommandParameter(cmd, $"offset{index}", 0);
            index++;
        }

        index = 0;
        foreach (var types in jobTypes.AvailableConcurrencyGroups)
        {
            AddCommandParameter(cmd, $"groupBlocked{index}", 0);
            cmd.AddArrayParameters($"groupLimit{index}Types", GetJobClasses(types));
            AddCommandParameter(cmd, $"groupLimit{index}", 1);
            AddCommandParameter(cmd, $"groupOffset{index}", 0);
            index++;
        }

        await using var rs = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        List<TriggerAcquireResult> nextTriggers = new();
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

    public virtual async ValueTask<int> SelectWaitingTriggerCount(ConnectionAndTransactionHolder conn, DateTimeOffset noLaterThan, DateTimeOffset noEarlierThan,
        JobTypes jobTypes, CancellationToken cancellationToken = new())
    {
        var hasExcludeTypes = jobTypes.TypesToExclude.Any() || jobTypes.TypesToLimit.Any() || jobTypes.AvailableConcurrencyGroups.Any(a => a.Any());
        var commandText = new StringBuilder();
        commandText.Append("SELECT Count(1) FROM (");
        commandText.Append(hasExcludeTypes ? GetSelectPartExcludingTypes : GetSelectPartNoExclusions);

        int index;
        for (index = 0; index < jobTypes.TypesToLimit.Count; index++)
        {
            commandText.Append("\nUNION SELECT * FROM (\n");
            commandText.Append(GetSelectPartOfType(index, true));
            commandText.Append("\n)");
        }

        for (index = 0; index < jobTypes.AvailableConcurrencyGroups.Count(); index++)
        {
            commandText.Append("\nUNION SELECT * FROM (\n");
            commandText.Append(GetSelectPartInTypes(index, true));
            commandText.Append("\n)");
        }

        commandText.Append(") u");
        await using var cmd = PrepareCommand(conn, ReplaceTablePrefix(commandText.ToString()));

        AddCommandParameter(cmd, "schedulerName", _schedulerName);
        AddCommandParameter(cmd, "state", StateWaiting);
        AddCommandParameter(cmd, "noLaterThan", GetDbDateTimeValue(noLaterThan));
        AddCommandParameter(cmd, "noEarlierThan", GetDbDateTimeValue(noEarlierThan));
        if (hasExcludeTypes)
            cmd.AddArrayParameters("types",
                GetJobClasses(jobTypes.TypesToExclude.Union(jobTypes.TypesToLimit.Keys).Union(jobTypes.AvailableConcurrencyGroups.SelectMany(a => a))));

        index = 0;
        foreach (var kv in jobTypes.TypesToLimit)
        {
            AddCommandParameter(cmd, $"limitBlocked{index}", 0);
            AddCommandParameter(cmd, $"limit{index}Type", new JobType(kv.Key).FullName);
            AddCommandParameter(cmd, $"limit{index}", kv.Value);
            AddCommandParameter(cmd, $"offset{index}", 0);
            index++;
        }

        index = 0;
        foreach (var types in jobTypes.AvailableConcurrencyGroups)
        {
            AddCommandParameter(cmd, $"groupBlocked{index}", 0);
            cmd.AddArrayParameters($"groupLimit{index}Types", GetJobClasses(types));
            AddCommandParameter(cmd, $"groupLimit{index}", 1);
            AddCommandParameter(cmd, $"groupOffset{index}", 0);
            index++;
        }

        var rs = await cmd.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
        var result = Convert.ToInt32(rs);
        return result;
    }

    public virtual async ValueTask<int> SelectBlockedTriggerCount(ConnectionAndTransactionHolder conn, ITypeLoadHelper loadHelper, DateTimeOffset noLaterThan,
        DateTimeOffset noEarlierThan, JobTypes jobTypes, CancellationToken cancellationToken = new())
    {
        await using var cmd = PrepareCommand(conn, ReplaceTablePrefix(SelectBlockedTypeCountsSql));
        AddCommandParameter(cmd, "schedulerName", _schedulerName);
        AddCommandParameter(cmd, "noLaterThan", GetDbDateTimeValue(noLaterThan));
        AddCommandParameter(cmd, "noEarlierThan", GetDbDateTimeValue(noEarlierThan));
        // add the limited types, then we'll subtract the ones that are allowed to run later
        cmd.AddArrayParameters("types", GetJobClasses(jobTypes.TypesToExclude.Union(jobTypes.TypesToLimit.Keys).Union(jobTypes.AvailableConcurrencyGroups.SelectMany(a => a))).Distinct());

        var results = new Dictionary<Type, int>();
        await using var rs = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await rs.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            var jobType = loadHelper.LoadType(GetString(rs, ColumnJobClass)!)!;
            results[jobType] = GetInt32(rs, "Count")!;
        }

        // We need to get the number of jobs that are queued, then subtract the allowed number, ensuring that blocked count doesn't go negative
        var blocked = results.Join(jobTypes.TypesToExclude, a => a.Key, a => a, (result, _) => result.Value).Sum();
        var limited = results.Join(jobTypes.TypesToLimit, a => a.Key, a => a.Key,
            (result, limit) => Math.Max(result.Value - limit.Value, 0)).Sum();
        // count how many jobs are in a concurrency group, then subtract 1 from each.
        // there is 1 that is available in each group. We don't need to check, as it wouldn't be in this list if it was not allowed
        var groups = jobTypes.AvailableConcurrencyGroups.Select(types => results.Where(r => types.Contains(r.Key)).Sum(r => r.Value)).Where(group => group > 0).Sum(group => group - 1);

        return blocked + limited + groups;
    }

    public virtual async ValueTask<int> SelectTotalWaitingTriggerCount(ConnectionAndTransactionHolder conn, DateTimeOffset noLaterThan, DateTimeOffset noEarlierThan,
        CancellationToken cancellationToken = new())
    {
        await using var cmd = PrepareCommand(conn, ReplaceTablePrefix(GetCountNoExclusions));
        AddCommandParameter(cmd, "schedulerName", _schedulerName);
        AddCommandParameter(cmd, "noLaterThan", GetDbDateTimeValue(noLaterThan));
        AddCommandParameter(cmd, "noEarlierThan", GetDbDateTimeValue(noEarlierThan));

        return Convert.ToInt32(await cmd.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false));
    }

    public virtual async ValueTask<Dictionary<Type, int>> SelectJobTypeCounts(ConnectionAndTransactionHolder conn, ITypeLoadHelper loadHelper,
        DateTimeOffset noLaterThan, CancellationToken cancellationToken = new())
    {
        await using var cmd = PrepareCommand(conn, ReplaceTablePrefix(SelectJobClassesAndCountSql));
        AddCommandParameter(cmd, "schedulerName", _schedulerName);
        AddCommandParameter(cmd, "noLaterThan", GetDbDateTimeValue(noLaterThan));
        AddCommandParameter(cmd, "noEarlierThan", GetDbDateTimeValue(DateTimeOffset.UtcNow - TimeSpan.FromMinutes(1)));

        var result = new Dictionary<Type, int>();
        await using var rs = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await rs.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            var jobType = loadHelper.LoadType(GetString(rs, ColumnJobClass)!)!;
            result[jobType] = GetInt32(rs, "Count")!;
        }

        return result;
    }

    public virtual async ValueTask<List<(IJobDetail, bool)>> SelectJobs(ConnectionAndTransactionHolder conn, ITypeLoadHelper loadHelper, int maxCount, int offset,
        DateTimeOffset noLaterThan, DateTimeOffset noEarlierThan, JobTypes jobTypes, bool excludeBlocked, CancellationToken cancellationToken = default)
    {
        var hasExcludeTypes = jobTypes.TypesToExclude.Any() || jobTypes.TypesToLimit.Any() || jobTypes.AvailableConcurrencyGroups.Any(a => a.Any());
        var subquery = new StringBuilder();
        subquery.Append(hasExcludeTypes ? GetSelectPartExcludingTypes : GetSelectPartNoExclusions);

        int index;
        var startIndex = 0;
        // not blocked
        for (index = 0; index < startIndex + jobTypes.TypesToLimit.Count; index++)
        {
            subquery.Append("\nUNION SELECT * FROM (\n");
            subquery.Append(GetSelectPartOfType(index, true));
            subquery.Append("\n)");
        }

        // blocked
        if (!excludeBlocked)
        {
            startIndex = index;
            for (; index < startIndex + jobTypes.TypesToLimit.Count; index++)
            {
                subquery.Append("\nUNION SELECT * FROM (\n");
                subquery.Append(GetSelectPartOfType(index, false));
                subquery.Append("\n)");
            }
        }

        // not blocked
        startIndex = 0;
        for (index = 0; index < startIndex + jobTypes.AvailableConcurrencyGroups.Count(); index++)
        {
            subquery.Append("\nUNION SELECT * FROM (\n");
            subquery.Append(GetSelectPartInTypes(index, true));
            subquery.Append("\n)");
        }

        // blocked
        if (!excludeBlocked)
        {
            startIndex = index;
            for (; index < startIndex + jobTypes.AvailableConcurrencyGroups.Count(); index++)
            {
                subquery.Append("\nUNION SELECT * FROM (\n");
                subquery.Append(GetSelectPartInTypes(index, false));
                subquery.Append("\n)");
            }

            if (jobTypes.TypesToExclude.Any())
            {
                subquery.Append("\nUNION SELECT * FROM (\n");
                subquery.Append(GetSelectPartInTypes(index, false));
                subquery.Append("\n)");
            }
        }

        var commandText = ReplaceTablePrefix(GetJobSql.Replace(SubQuery, subquery.ToString()));
        await using var cmd = PrepareCommand(conn, commandText);
        AddCommandParameter(cmd, "schedulerName", _schedulerName);
        AddCommandParameter(cmd, "state", StateWaiting);
        AddCommandParameter(cmd, "noLaterThan", GetDbDateTimeValue(noLaterThan));
        AddCommandParameter(cmd, "noEarlierThan", GetDbDateTimeValue(noEarlierThan));
        AddCommandParameter(cmd, "limit", maxCount);
        AddCommandParameter(cmd, "offset", offset);
        if (hasExcludeTypes)
            cmd.AddArrayParameters("types",
                GetJobClasses(jobTypes.TypesToExclude.Union(jobTypes.TypesToLimit.Keys).Union(jobTypes.AvailableConcurrencyGroups.SelectMany(a => a))));

        index = 0;
        // not blocked
        foreach (var kv in jobTypes.TypesToLimit)
        {
            AddCommandParameter(cmd, $"limitBlocked{index}", 0);
            AddCommandParameter(cmd, $"limit{index}Type", new JobType(kv.Key).FullName);
            AddCommandParameter(cmd, $"limit{index}", kv.Value);
            AddCommandParameter(cmd, $"offset{index}", 0);
            index++;
        }

        // blocked
        if (!excludeBlocked)
        {
            foreach (var kv in jobTypes.TypesToLimit)
            {
                AddCommandParameter(cmd, $"limitBlocked{index}", 1);
                AddCommandParameter(cmd, $"limit{index}Type", new JobType(kv.Key).FullName);
                AddCommandParameter(cmd, $"offset{index}", kv.Value);
                index++;
            }
        }

        index = 0;
        // not blocked
        foreach (var types in jobTypes.AvailableConcurrencyGroups)
        {
            AddCommandParameter(cmd, $"groupBlocked{index}", 0);
            cmd.AddArrayParameters($"groupLimit{index}Types", GetJobClasses(types));
            AddCommandParameter(cmd, $"groupLimit{index}", 1);
            AddCommandParameter(cmd, $"groupOffset{index}", 0);
            index++;
        }

        // blocked
        if (!excludeBlocked)
        {
            foreach (var types in jobTypes.AvailableConcurrencyGroups)
            {
                AddCommandParameter(cmd, $"groupBlocked{index}", 1);
                cmd.AddArrayParameters($"groupLimit{index}Types", GetJobClasses(types));
                AddCommandParameter(cmd, $"groupLimit{index}", -1);
                AddCommandParameter(cmd, $"groupOffset{index}", 1);
                index++;
            }

            if (jobTypes.TypesToExclude.Any())
            {
                AddCommandParameter(cmd, $"groupBlocked{index}", 1);
                cmd.AddArrayParameters($"groupLimit{index}Types", GetJobClasses(jobTypes.TypesToExclude));
                AddCommandParameter(cmd, $"groupLimit{index}", -1);
                AddCommandParameter(cmd, $"groupOffset{index}", 0);
            }
        }

        await using var rs = await cmd.ExecuteReaderAsync(System.Data.CommandBehavior.SequentialAccess, cancellationToken).ConfigureAwait(false);

        var results = new List<(IJobDetail, bool)>();
        while (await rs.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            // Due to CommandBehavior.SequentialAccess, columns must be read in order.
            var jobName = GetString(rs, ColumnJobName)!;
            var jobGroup = GetString(rs, ColumnJobGroup);
            var description = GetString(rs, ColumnDescription);
            var jobType = loadHelper.LoadType(GetString(rs, ColumnJobClass)!)!;
            var requestsRecovery = GetBooleanFromDbValue(rs[ColumnRequestsRecovery]);
            var map = await ReadMapFromReader(rs, 6);
            var jobDataMap = map != null ? new JobDataMap(map) : null;
            var blocked = GetBooleanFromDbValue(rs[Blocked]);

            var job = new JobDetail
            {
                Name = jobName,
                Group = jobGroup!,
                JobType = new JobType(jobType),
                Description = description,
                RequestsRecovery = requestsRecovery,
                JobDataMap = jobDataMap!
            };
            results.Add((job, blocked));
        }

        return results;
    }

    private Task<IDictionary> ReadMapFromReader(DbDataReader rs, int colIndex)
    {
        var isDbNullTask = rs.IsDBNullAsync(colIndex);
        if (isDbNullTask.IsCompleted && isDbNullTask.Result) return Task.FromResult<IDictionary>(null);

        return Awaited(isDbNullTask);

        async Task<IDictionary> Awaited(Task<bool> isDbNull)
        {
            if (await isDbNull.ConfigureAwait(false)) return null;

            if (CanUseProperties)
            {
                try
                {
                    var properties = await GetMapFromProperties(rs, colIndex).ConfigureAwait(false);
                    return properties;
                }
                catch (InvalidCastException)
                {
                    // old data from user error or XML scheduling plugin data
                    try
                    {
                        return await GetObjectFromBlob<IDictionary>(rs, colIndex).ConfigureAwait(false);
                    }
                    catch
                    {
                        // swallow
                    }

                    // throw original exception
                    throw;
                }
            }
            try
            {
                return await GetObjectFromBlob<IDictionary>(rs, colIndex).ConfigureAwait(false);
            }
            catch (InvalidCastException)
            {
                // old data from user error?
                try
                {
                    // we use this then
                    return await GetMapFromProperties(rs, colIndex).ConfigureAwait(false);
                }
                catch
                {
                    //swallow
                }

                // throw original exception
                throw;
            }
        }
    }

    /// <summary>
    /// Build dictionary from serialized NameValueCollection.
    /// </summary>
    private async Task<IDictionary> GetMapFromProperties(DbDataReader rs, int idx)
    {
        var properties = await GetJobDataFromBlob<NameValueCollection>(rs, idx).ConfigureAwait(false);
        if (properties == null) return null;
        var map = ConvertFromProperty(properties);
        return map;
    }
    
    private static string GetString(IDataReader reader, string columnName)
    {
        var columnValue = reader[columnName];
        if (columnValue == DBNull.Value)
        {
            return null;
        }
        return (string) columnValue;
    }

    /// <summary>
    /// Returns int from given column name.
    /// </summary>
    private static int GetInt32(IDataReader reader, string columnName)
    {
        var columnValue = reader[columnName];
        return Convert.ToInt32(columnValue, CultureInfo.InvariantCulture);
    }
}
