using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Quartz;
using Quartz.Impl.AdoJobStore;
using Quartz.Spi;
using Quartz.Util;
using Shoko.Server.Utilities;

namespace Shoko.Server.Scheduling;

public class ThreadPooledJobStore : JobStoreTX
{
    private ILogger<ThreadPooledJobStore> Logger { get; set; }
    private ITypeLoadHelper _typeLoadHelper = null!;
    private Dictionary<Type, int> _typeConcurrencyCache;
    
    public ThreadPooledJobStore()
    {
        Logger = Utils.ServiceContainer.GetService<ILogger<ThreadPooledJobStore>>();
        InitConcurrencyCache();
    }

    public override async Task Initialize(
        ITypeLoadHelper loadHelper,
        ISchedulerSignaler signaler,
        CancellationToken cancellationToken = default)
    {
        _typeLoadHelper = loadHelper;
        await base.Initialize(loadHelper, signaler, cancellationToken);
    }

    protected override async Task<IReadOnlyCollection<IOperableTrigger>> AcquireNextTrigger(
            ConnectionAndTransactionHolder conn,
            DateTimeOffset noLaterThan,
            int maxCount,
            TimeSpan timeWindow,
            CancellationToken cancellationToken = default)
    {
        if (timeWindow < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(timeWindow));
        }

        var acquiredTriggers = new List<IOperableTrigger>();
        var acquiredJobKeysForNoConcurrentExec = new Dictionary<Type, int>();
        const int MaxDoLoopRetry = 3;
        var currentLoopCount = 0;

        do
        {
            currentLoopCount++;
            try
            {
                var results = await Delegate.SelectTriggerToAcquire(conn, noLaterThan + timeWindow, MisfireTime, maxCount, cancellationToken)
                    .ConfigureAwait(false);

                // No trigger is ready to fire yet.
                if (results.Count == 0)
                {
                    return acquiredTriggers;
                }

                var batchEnd = noLaterThan;

                foreach (var result in results)
                {
                    var triggerKey = new TriggerKey(result.TriggerName, result.TriggerGroup);

                    // If our trigger is no longer available, try a new one.
                    var nextTrigger = await RetrieveTrigger(conn, triggerKey, cancellationToken).ConfigureAwait(false);
                    if (nextTrigger == null)
                    {
                        continue; // next trigger
                    }

                    // If trigger's job is set as @DisallowConcurrentExecution, and it has already been added to result, then
                    // put it back into the timeTriggers set and continue to search for next trigger.
                    Type jobType;
                    try
                    {
                        jobType = _typeLoadHelper.LoadType(result.JobType)!;
                    }
                    catch (JobPersistenceException jpe)
                    {
                        try
                        {
                            Logger.LogError(jpe, "Error retrieving job, setting trigger state to ERROR");
                            await Delegate.UpdateTriggerState(conn, triggerKey, StateError, cancellationToken).ConfigureAwait(false);
                        }
                        catch (Exception ex)
                        {
                            Logger.LogError(ex, "Unable to set trigger state to ERROR");
                        }
                        continue;
                    }

                    if (ObjectUtils.IsAttributePresent(jobType, typeof(DisallowConcurrentExecutionAttribute)))
                    {
                        if (acquiredJobKeysForNoConcurrentExec.ContainsKey(jobType)) continue;
                        acquiredJobKeysForNoConcurrentExec[jobType] += 1;
                    }
                    else if (jobType.GetCustomAttributes().FirstOrDefault(a => a is LimitConcurrencyAttribute) is LimitConcurrencyAttribute attribute)
                    {
                        if (!_typeConcurrencyCache.TryGetValue(jobType, out var maxConcurrentJobs)) maxConcurrentJobs = attribute.MaxConcurrentJobs;
                        if (acquiredJobKeysForNoConcurrentExec.TryGetValue(jobType, out var number) && number >= maxConcurrentJobs) continue;
                        acquiredJobKeysForNoConcurrentExec[jobType] += 1;
                    }
                    else if (_typeConcurrencyCache.TryGetValue(jobType, out var maxJobs) && maxJobs > 0)
                    {
                        if (acquiredJobKeysForNoConcurrentExec.TryGetValue(jobType, out var number) && number >= maxJobs) continue;
                        acquiredJobKeysForNoConcurrentExec[jobType] += 1;
                    }

                    var nextFireTimeUtc = nextTrigger.GetNextFireTimeUtc();

                    // A trigger should not return NULL on nextFireTime when fetched from DB.
                    // But for whatever reason if we do have this (BAD trigger implementation or
                    // data?), we then should log a warning and continue to next trigger.
                    // User would need to manually fix these triggers from DB as they will not
                    // able to be clean up by Quartz since we are not returning it to be processed.
                    if (nextFireTimeUtc == null)
                    {
                        Logger.LogWarning("Trigger {NextTriggerKey} returned null on nextFireTime and yet still exists in DB!", nextTrigger.Key);
                        continue;
                    }

                    if (nextFireTimeUtc > batchEnd)
                    {
                        break;
                    }

                    // We now have a acquired trigger, let's add to return list.
                    // If our trigger was no longer in the expected state, try a new one.
                    var rowsUpdated = await Delegate.UpdateTriggerStateFromOtherStateWithNextFireTime(conn, triggerKey, StateAcquired, StateWaiting, nextFireTimeUtc.Value, cancellationToken).ConfigureAwait(false);
                    if (rowsUpdated <= 0)
                    {
                        continue; // next trigger
                    }
                    nextTrigger.FireInstanceId = GetFiredTriggerRecordId();
                    await Delegate.InsertFiredTrigger(conn, nextTrigger, StateAcquired, null, cancellationToken).ConfigureAwait(false);

                    if (acquiredTriggers.Count == 0)
                    {
                        var now = SystemTime.UtcNow();
                        var nextFireTime = nextFireTimeUtc.Value;
                        var max = now > nextFireTime ? now : nextFireTime;

                        batchEnd = max + timeWindow;
                    }

                    acquiredTriggers.Add(nextTrigger);
                }

                // if we didn't end up with any trigger to fire from that first
                // batch, try again for another batch. We allow with a max retry count.
                if (acquiredTriggers.Count == 0 && currentLoopCount < MaxDoLoopRetry)
                {
                    continue;
                }

                // We are done with the while loop.
                break;
            }
            catch (Exception e)
            {
                throw new JobPersistenceException("Couldn't acquire next trigger: " + e.Message, e);
            }
        } while (true);

        // Return the acquired trigger list
        return acquiredTriggers;
    }

    private void InitConcurrencyCache()
    {
        _typeConcurrencyCache = new Dictionary<Type, int>();
        var types = AppDomain.CurrentDomain.GetAssemblies().SelectMany(a => a.GetTypes())
            .Where(a => typeof(IJob).IsAssignableFrom(a)).ToList();

        foreach (var type in types)
        {
            var attribute = type.GetCustomAttribute<LimitConcurrencyAttribute>();
            if (attribute == null) continue;
            _typeConcurrencyCache[type] = attribute.MaxConcurrentJobs;
        }

        var overrides = Utils.SettingsProvider.GetSettings().Quartz.LimitedConcurrencyOverrides;
        if (overrides == null) return;
        foreach (var kv in overrides)
        {
            var type = Assembly.GetExecutingAssembly().GetTypes().FirstOrDefault(a => a.Name.Equals(kv.Key));
            if (type == null) continue;
            var value = kv.Value;
            var attribute = type.GetCustomAttribute<LimitConcurrencyAttribute>();
            if (attribute != null && attribute.MaxAllowedConcurrentJobs < kv.Value) value = attribute.MaxAllowedConcurrentJobs;
            _typeConcurrencyCache[type] = value;
        }
    }
}
