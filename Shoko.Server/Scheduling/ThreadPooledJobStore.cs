using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Quartz;
using Quartz.Impl.AdoJobStore;
using Quartz.Spi;
using Quartz.Util;
using Shoko.Server.Scheduling.Acquisition.Filters;
using Shoko.Server.Scheduling.Concurrency;
using Shoko.Server.Scheduling.Delegates;
using Shoko.Server.Utilities;

namespace Shoko.Server.Scheduling;

public class ThreadPooledJobStore : JobStoreTX
{
    private readonly ILogger<ThreadPooledJobStore> _logger;
    private ITypeLoadHelper _typeLoadHelper = null!;
    private Dictionary<Type, int> _typeConcurrencyCache;
    private readonly IAcquisitionFilter[] _acquisitionFilters;
    
    public ThreadPooledJobStore(ILogger<ThreadPooledJobStore> logger, IEnumerable<IAcquisitionFilter> acquisitionFilters)
    {
        _logger = logger;
        _acquisitionFilters = acquisitionFilters.ToArray();
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

    // TODO We may need a way to notify quartz of a state change, or else it waits like 5 seconds to check again (or is notified by new jobs)
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
        var context = new TriggerAcquisitionContext();

        do
        {
            context.CurrentLoopCount++;
            try
            {
                var typesToExclude = GetTypesToExclude();
                var results = await (Delegate as IFilteredDriverDelegate)!
                    .SelectTriggerToAcquire(conn, noLaterThan + timeWindow, MisfireTime, maxCount, typesToExclude, cancellationToken).ConfigureAwait(false);

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
                    try
                    {
                        context.CurrentJobType = _typeLoadHelper.LoadType(result.JobType)!;
                    }
                    catch (JobPersistenceException jpe)
                    {
                        try
                        {
                            _logger.LogError(jpe, "Error retrieving job, setting trigger state to ERROR");
                            await Delegate.UpdateTriggerState(conn, triggerKey, StateError, cancellationToken).ConfigureAwait(false);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "Unable to set trigger state to ERROR");
                        }
                        continue;
                    }

                    if (!JobAllowed(context)) continue;

                    var nextFireTimeUtc = nextTrigger.GetNextFireTimeUtc();

                    // A trigger should not return NULL on nextFireTime when fetched from DB.
                    // But for whatever reason if we do have this (BAD trigger implementation or
                    // data?), we then should log a warning and continue to next trigger.
                    // User would need to manually fix these triggers from DB as they will not
                    // able to be clean up by Quartz since we are not returning it to be processed.
                    if (nextFireTimeUtc == null)
                    {
                        _logger.LogWarning("Trigger {NextTriggerKey} returned null on nextFireTime and yet still exists in DB!", nextTrigger.Key);
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
                if (acquiredTriggers.Count == 0 && context.CurrentLoopCount < context.MaxDoLoopRetry)
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

    private Type[] GetTypesToExclude()
    {
        var result = new List<Type>();
        foreach (var filter in _acquisitionFilters)
        {
            result.AddRange(filter.GetTypesToExclude());
        }

        return result.Distinct().ToArray();
    }

    private bool JobAllowed(TriggerAcquisitionContext context)
    {
        var jobType = context.CurrentJobType;
        var acquiredJobTypesWithLimitedConcurrency = context.AcquiredJobsWithLimitedConcurrency;
        if (ObjectUtils.IsAttributePresent(jobType, typeof(DisallowConcurrentExecutionAttribute)))
        {
            if (acquiredJobTypesWithLimitedConcurrency.TryGetValue(jobType.Name, out var number) && number >= 1) return false;
            acquiredJobTypesWithLimitedConcurrency[jobType.Name] = number + 1;
        }
        else if (jobType.GetCustomAttributes().FirstOrDefault(a => a is DisallowConcurrencyGroupAttribute) is DisallowConcurrencyGroupAttribute concurrencyAttribute)
        {
            if (acquiredJobTypesWithLimitedConcurrency.TryGetValue(concurrencyAttribute.Group, out var number)) return false;
            acquiredJobTypesWithLimitedConcurrency[concurrencyAttribute.Group] = number + 1;
        }
        else if (_typeConcurrencyCache.TryGetValue(jobType, out var maxJobs) && maxJobs > 0)
        {
            if (acquiredJobTypesWithLimitedConcurrency.TryGetValue(jobType.Name, out var number) && number >= maxJobs) return false;
            acquiredJobTypesWithLimitedConcurrency[jobType.Name] = number + 1;
        }
        else if (jobType.GetCustomAttributes().FirstOrDefault(a => a is LimitConcurrencyAttribute) is LimitConcurrencyAttribute limitConcurrencyAttribute)
        {
            if (!_typeConcurrencyCache.TryGetValue(jobType, out var maxConcurrentJobs)) maxConcurrentJobs = limitConcurrencyAttribute.MaxConcurrentJobs;
            if (acquiredJobTypesWithLimitedConcurrency.TryGetValue(jobType.Name, out var number) && number >= maxConcurrentJobs) return false;
            acquiredJobTypesWithLimitedConcurrency[jobType.Name] = number + 1;
        }

        return true;
    }

    private void InitConcurrencyCache()
    {
        _typeConcurrencyCache = new Dictionary<Type, int>();
        var types = AppDomain.CurrentDomain.GetAssemblies().SelectMany(a => a.GetTypes())
            .Where(a => typeof(IJob).IsAssignableFrom(a) && !a.IsAbstract).ToList();

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
            if (attribute is { MaxAllowedConcurrentJobs: > 0 } && attribute.MaxAllowedConcurrentJobs < kv.Value) value = attribute.MaxAllowedConcurrentJobs;
            _typeConcurrencyCache[type] = value;
        }
    }

    private class TriggerAcquisitionContext
    {
        public readonly int MaxDoLoopRetry = 3;
        public int CurrentLoopCount { get; set; }
        public Dictionary<string, int> AcquiredJobsWithLimitedConcurrency { get; } = new();
        public Type CurrentJobType { get; set; }
    }

    protected override async Task<TriggerFiredBundle> TriggerFired(ConnectionAndTransactionHolder conn, IOperableTrigger trigger, CancellationToken cancellationToken = new CancellationToken())
    {
        // notify some sort of callback listener for UI to add a job as running
        return await base.TriggerFired(conn, trigger, cancellationToken);
    }

    protected override async Task TriggeredJobComplete(ConnectionAndTransactionHolder conn, IOperableTrigger trigger, IJobDetail jobDetail, SchedulerInstruction triggerInstCode,
        CancellationToken cancellationToken = new CancellationToken())
    {
        // notify some sort of callback listener for UI to set a job as finished
        await base.TriggeredJobComplete(conn, trigger, jobDetail, triggerInstCode, cancellationToken);
    }
}
