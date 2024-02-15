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
using Shoko.Server.Scheduling.Acquisition.Filters;
using Shoko.Server.Scheduling.Concurrency;
using Shoko.Server.Scheduling.Delegates;
using Shoko.Server.Utilities;

namespace Shoko.Server.Scheduling;

public class ThreadPooledJobStore : JobStoreTX
{
    private readonly ILogger<ThreadPooledJobStore> _logger;
    private readonly QueueStateEventHandler _queueStateEventHandler;
    private readonly JobFactory _jobFactory;
    private ITypeLoadHelper _typeLoadHelper;
    private readonly Dictionary<JobKey, IJobDetail> _executingJobs = new();
    private readonly IAcquisitionFilter[] _acquisitionFilters;
    private Dictionary<Type, int> _typeConcurrencyCache;
    private Dictionary<string, Type[]> _concurrencyGroupCache;
    private ISchedulerSignaler _signaler;
    private int _threadPoolSize;

    public ThreadPooledJobStore(ILogger<ThreadPooledJobStore> logger, IEnumerable<IAcquisitionFilter> acquisitionFilters,
        QueueStateEventHandler queueStateEventHandler, JobFactory jobFactory)
    {
        _logger = logger;
        _queueStateEventHandler = queueStateEventHandler;
        _jobFactory = jobFactory;
        _acquisitionFilters = acquisitionFilters.ToArray();
        InitConcurrencyCache();
    }

    public override async Task Initialize(ITypeLoadHelper loadHelper, ISchedulerSignaler signaler, CancellationToken cancellationToken = default)
    {
        _signaler = signaler;
        foreach (var filter in _acquisitionFilters)
        {
            filter.StateChanged += FilterOnStateChanged;
        }
        _typeLoadHelper = loadHelper;
        await base.Initialize(loadHelper, signaler, cancellationToken);
    }

    private void InitConcurrencyCache()
    {
        _concurrencyGroupCache = new Dictionary<string, Type[]>();
        _typeConcurrencyCache = new Dictionary<Type, int>();
        var types = AppDomain.CurrentDomain.GetAssemblies().SelectMany(a => a.GetTypes())
            .Where(a => typeof(IJob).IsAssignableFrom(a) && !a.IsAbstract).ToList();

        foreach (var type in types)
        {
            var attribute = type.GetCustomAttribute<LimitConcurrencyAttribute>();
            if (attribute != null)
            {
                _typeConcurrencyCache[type] = attribute.MaxConcurrentJobs;
            }

            var concurrencyGroup = type.GetCustomAttribute<DisallowConcurrencyGroupAttribute>();
            if (concurrencyGroup != null)
            {
                if (_concurrencyGroupCache.TryGetValue(concurrencyGroup.Group, out var groupTypes)) groupTypes = groupTypes.Append(type).Distinct().ToArray();
                else groupTypes = new[] { type };
                _concurrencyGroupCache[concurrencyGroup.Group] = groupTypes;
            }
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

    ~ThreadPooledJobStore()
    {
        foreach (var filter in _acquisitionFilters)
        {
            filter.StateChanged -= FilterOnStateChanged;
        }
    }

    private void FilterOnStateChanged(object sender, EventArgs e)
    {
        _signaler.SignalSchedulingChange(DateTimeOffset.UtcNow - TimeSpan.FromMinutes(5));
    }

    protected override async Task<IReadOnlyCollection<IOperableTrigger>> AcquireNextTrigger(ConnectionAndTransactionHolder conn, DateTimeOffset noLaterThan,
            int maxCount, TimeSpan timeWindow, CancellationToken cancellationToken = default)
    {
        if (timeWindow < TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(timeWindow));

        const int MaxDoLoopRetry = 3;
        var acquiredTriggers = new List<IOperableTrigger>();
        var acquiredJobsWithLimitedConcurrency = new Dictionary<string, int>();
        var currentLoopCount = 0;

        do
        {
            currentLoopCount++;
            try
            {
                var typesToExclude = GetTypesToExclude();
                var results = await Delegate.SelectTriggerToAcquire(conn, noLaterThan + timeWindow, MisfireTime, maxCount, typesToExclude, cancellationToken)
                    .ConfigureAwait(false);

                // No trigger is ready to fire yet.
                if (results.Count == 0) return acquiredTriggers;
                var batchEnd = noLaterThan;

                foreach (var result in results)
                {
                    var triggerKey = new TriggerKey(result.TriggerName, result.TriggerGroup);

                    // If our trigger is no longer available, try a new one.
                    var nextTrigger = await RetrieveTrigger(conn, triggerKey, cancellationToken).ConfigureAwait(false);
                    if (nextTrigger == null) continue; // next trigger

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
                            _logger.LogError(jpe, "Error retrieving job, setting trigger state to ERROR");
                            await Delegate.UpdateTriggerState(conn, triggerKey, StateError, cancellationToken).ConfigureAwait(false);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "Unable to set trigger state to ERROR");
                        }
                        continue;
                    }

                    if (!JobAllowed(jobType, acquiredJobsWithLimitedConcurrency)) continue;

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

                    if (nextFireTimeUtc > batchEnd) break;

                    // We now have a acquired trigger, let's add to return list.
                    // If our trigger was no longer in the expected state, try a new one.
                    var rowsUpdated = await Delegate.UpdateTriggerStateFromOtherStateWithNextFireTime(conn, triggerKey, StateAcquired, StateWaiting, nextFireTimeUtc.Value, cancellationToken).ConfigureAwait(false);
                    if (rowsUpdated <= 0) continue; // next trigger

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
                if (acquiredTriggers.Count == 0 && currentLoopCount < MaxDoLoopRetry) continue;

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
        foreach (var filter in _acquisitionFilters) result.AddRange(filter.GetTypesToExclude());

        return result.Distinct().ToArray();
    }

    private bool JobAllowed(Type jobType, Dictionary<string, int> acquiredJobTypesWithLimitedConcurrency)
    {
        if (ObjectUtils.IsAttributePresent(jobType, typeof(DisallowConcurrentExecutionAttribute)))
        {
            lock (_executingJobs)
                if (_executingJobs.Values.Any(a => a.JobType == jobType))
                    return false;
            if (acquiredJobTypesWithLimitedConcurrency.TryGetValue(jobType.Name, out var number) && number >= 1) return false;
            acquiredJobTypesWithLimitedConcurrency[jobType.Name] = number + 1;
            return true;
        }

        if (jobType.GetCustomAttributes().FirstOrDefault(a => a is DisallowConcurrencyGroupAttribute) is DisallowConcurrencyGroupAttribute concurrencyAttribute)
        {
            lock (_executingJobs)
                if(_executingJobs.Values.Any(a => _concurrencyGroupCache[concurrencyAttribute.Group].Contains(a.JobType))) return false;
            if (acquiredJobTypesWithLimitedConcurrency.TryGetValue(concurrencyAttribute.Group, out var number)) return false;
            acquiredJobTypesWithLimitedConcurrency[concurrencyAttribute.Group] = number + 1;
            return true;
        }

        if (_typeConcurrencyCache.TryGetValue(jobType, out var maxJobs) && maxJobs > 0)
        {
            int count;
            lock (_executingJobs)
                count = _executingJobs.Values.Count(a => a.JobType == jobType);
            acquiredJobTypesWithLimitedConcurrency.TryGetValue(jobType.Name, out var number);
            acquiredJobTypesWithLimitedConcurrency[jobType.Name] = number + 1;
            return number + count < maxJobs;
        }

        if (jobType.GetCustomAttributes().FirstOrDefault(a => a is LimitConcurrencyAttribute) is LimitConcurrencyAttribute limitConcurrencyAttribute)
        {
            if (!_typeConcurrencyCache.TryGetValue(jobType, out var maxConcurrentJobs)) maxConcurrentJobs = limitConcurrencyAttribute.MaxConcurrentJobs;
            if (maxConcurrentJobs <= 0) maxConcurrentJobs = 1;
            acquiredJobTypesWithLimitedConcurrency.TryGetValue(jobType.Name, out var number);
            acquiredJobTypesWithLimitedConcurrency[jobType.Name] = number + 1;
            int count;
            lock (_executingJobs)
                count = _executingJobs.Values.Count(a => a.JobType == jobType);
            return number + count < maxConcurrentJobs;
        }

        return true;
    }
    
    public override async Task<IReadOnlyCollection<TriggerFiredResult>> TriggersFired(IReadOnlyCollection<IOperableTrigger> triggers, CancellationToken cancellationToken = default)
    {
        return await ExecuteInNonManagedTXLock(LockTriggerAccess, async conn => await TriggersFiredCallback(conn, triggers, cancellationToken),
            async (conn, result) => await TriggersFiredValidator(conn, result, cancellationToken), cancellationToken);
    }

    private async Task<IReadOnlyCollection<TriggerFiredResult>> TriggersFiredCallback(ConnectionAndTransactionHolder conn, IReadOnlyCollection<IOperableTrigger> triggers, CancellationToken cancellationToken)
    {
        List<TriggerFiredResult> results = new(triggers.Count);

        foreach (var trigger in triggers)
        {
            TriggerFiredResult result;
            try
            {
                var bundle = await TriggerFired(conn, trigger, cancellationToken).ConfigureAwait(false);
                result = new TriggerFiredResult(bundle);
            }
            catch (JobPersistenceException jpe)
            {
                _logger.LogError(jpe, "Caught job persistence exception: {Ex}", jpe.Message);
                result = new TriggerFiredResult(jpe);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Caught exception: {Ex}", ex.Message);
                result = new TriggerFiredResult(ex);
            }

            results.Add(result);
        }

        return results;
    }

    private async Task<bool> TriggersFiredValidator(ConnectionAndTransactionHolder conn, IEnumerable<TriggerFiredResult> result, CancellationToken cancellationToken)
    {
        try
        {
            var acquired = await Delegate.SelectInstancesFiredTriggerRecords(conn, InstanceId, cancellationToken).ConfigureAwait(false);
            var executingTriggers = acquired.Where(ft => StateExecuting.Equals(ft.FireInstanceState)).Select(a => a.FireInstanceId).ToHashSet();
            return result.Any(tr => tr.TriggerFiredBundle != null && executingTriggers.Contains(tr.TriggerFiredBundle.Trigger.FireInstanceId));
        }
        catch (Exception e)
        {
            throw new JobPersistenceException("error validating trigger acquisition", e);
        }
    }

    protected override async Task<TriggerFiredBundle> TriggerFired(ConnectionAndTransactionHolder conn, IOperableTrigger trigger, CancellationToken cancellationToken = default)
    {
        IJobDetail job;
        ICalendar cal = null;

        // Make sure trigger wasn't deleted, paused, or completed...
        try
        {
            // if trigger was deleted, state will be StateDeleted
            var state = await Delegate.SelectTriggerState(conn, trigger.Key, cancellationToken).ConfigureAwait(false);
            if (!state.Equals(StateAcquired)) return null;
        }
        catch (Exception e)
        {
            throw new JobPersistenceException("Couldn't select trigger state: " + e.Message, e);
        }

        try
        {
            job = await RetrieveJob(conn, trigger.JobKey, cancellationToken).ConfigureAwait(false);
            if (job == null) return null;
        }
        catch (JobPersistenceException jpe)
        {
            try
            {
                _logger.LogError(jpe, "Error retrieving job, setting trigger state to ERROR");
                await Delegate.UpdateTriggerState(conn, trigger.Key, StateError, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception sqle)
            {
                _logger.LogError(sqle, "Unable to set trigger state to ERROR");
            }
            throw;
        }

        if (trigger.CalendarName != null)
        {
            cal = await RetrieveCalendar(conn, trigger.CalendarName, cancellationToken).ConfigureAwait(false);
            if (cal == null) return null;
        }

        try
        {
            await Delegate.UpdateFiredTrigger(conn, trigger, StateExecuting, job, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception e)
        {
            throw new JobPersistenceException("Couldn't update fired trigger: " + e.Message, e);
        }

        var prevFireTime = trigger.GetPreviousFireTimeUtc();

        // call triggered - to update the trigger's next-fire-time state...
        trigger.Triggered(cal);

        var (state2, force) = await UpdateTriggerStatesForLimitedConcurrency(conn, job, cancellationToken);

        if (!trigger.GetNextFireTimeUtc().HasValue)
        {
            state2 = StateComplete;
            force = true;
        }

        await StoreTrigger(conn, trigger, job, true, state2, force, false, cancellationToken).ConfigureAwait(false);

        job.JobDataMap.ClearDirtyFlag();

        await JobFiringQueueEvents(conn, job, cancellationToken);
        
        return new TriggerFiredBundle(
            job,
            trigger,
            cal,
            trigger.Key.Group.Equals(SchedulerConstants.DefaultRecoveryGroup),
            SystemTime.UtcNow(),
            trigger.GetPreviousFireTimeUtc(),
            prevFireTime,
            trigger.GetNextFireTimeUtc());
    }

    private async Task<(string state2, bool force)> UpdateTriggerStatesForLimitedConcurrency(ConnectionAndTransactionHolder conn, IJobDetail job, CancellationToken cancellationToken)
    {
        var jobTypesWithLimitedConcurrency = new Dictionary<string, int>();
        lock (_executingJobs)
        {
            foreach (var executingJob in _executingJobs)
            {
                if (Equals(executingJob.Key, job.Key)) continue;
                if (!JobAllowed(job.JobType, jobTypesWithLimitedConcurrency)) goto loopBreak;
            }
        }

        if (JobAllowed(job.JobType, jobTypesWithLimitedConcurrency)) return (StateWaiting, true);

        loopBreak:
        try
        {
            var types = new[] { job.JobType };
            if (job.JobType.GetCustomAttributes().FirstOrDefault(a => a is DisallowConcurrencyGroupAttribute) is DisallowConcurrencyGroupAttribute
                concurrencyAttribute)
            {
                types = _concurrencyGroupCache[concurrencyAttribute.Group];
            }
            await Delegate.UpdateTriggerStatesForJobFromOtherState(conn, types, StateBlocked, StateWaiting, cancellationToken).ConfigureAwait(false);
            await Delegate.UpdateTriggerStatesForJobFromOtherState(conn, types, StateBlocked, StateAcquired, cancellationToken).ConfigureAwait(false);
            await Delegate.UpdateTriggerStatesForJobFromOtherState(conn, types, StatePausedBlocked, StatePaused, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception e)
        {
            throw new JobPersistenceException("Couldn't update states of blocked triggers: " + e.Message, e);
        }

        return (StateBlocked, false);
    }

    public Task<int> GetWaitingTriggersCount()
    {
        return ExecuteInNonManagedTXLock(LockTriggerAccess, async conn => await GetWaitingTriggersCount(conn), new CancellationToken());
    }

    private Task<int> GetWaitingTriggersCount(ConnectionAndTransactionHolder conn, CancellationToken cancellationToken = new CancellationToken())
    {
        var types = GetTypesToExclude();
        return Delegate.SelectWaitingTriggerCount(conn, DateTimeOffset.UtcNow + TimeSpan.FromSeconds(30), types, cancellationToken);
    }

    public Task<int> GetBlockedTriggersCount()
    {
        return ExecuteInNonManagedTXLock(LockTriggerAccess, async conn => await GetBlockedTriggersCount(conn), new CancellationToken());
    }

    private Task<int> GetBlockedTriggersCount(ConnectionAndTransactionHolder conn, CancellationToken cancellationToken = new CancellationToken())
    {
        var types = GetTypesToExclude();
        return Delegate.SelectBlockedTriggerCount(conn, DateTimeOffset.UtcNow + TimeSpan.FromSeconds(30), types, cancellationToken);
    }

    protected override async Task TriggeredJobComplete(ConnectionAndTransactionHolder conn, IOperableTrigger trigger, IJobDetail jobDetail, SchedulerInstruction triggerInstCode,
        CancellationToken cancellationToken = new CancellationToken())
    {
        // notify some sort of callback listener for UI to set a job as finished
        await JobCompletedQueueEvents(conn, jobDetail, cancellationToken);
        await base.TriggeredJobComplete(conn, trigger, jobDetail, triggerInstCode, cancellationToken);

        if (!jobDetail.JobType.GetCustomAttributes().Any(a =>
                _typeConcurrencyCache.ContainsKey(jobDetail.JobType) ||
                a is DisallowConcurrencyGroupAttribute or LimitConcurrencyAttribute or DisallowConcurrentExecutionAttribute))
            return;

        try
        {
            var types = new[]
            {
                jobDetail.JobType
            };
            if (jobDetail.JobType.GetCustomAttributes().FirstOrDefault(a => a is DisallowConcurrencyGroupAttribute) is DisallowConcurrencyGroupAttribute
                concurrencyAttribute)
            {
                types = _concurrencyGroupCache[concurrencyAttribute.Group];
            }

            await Delegate.UpdateTriggerStatesForJobFromOtherState(conn, types, StateWaiting, StateBlocked, cancellationToken);
            await Delegate.UpdateTriggerStatesForJobFromOtherState(conn, types, StatePaused, StatePausedBlocked, cancellationToken);
        }
        catch (Exception e)
        {
            throw new JobPersistenceException("Couldn't update states of blocked triggers: " + e.Message, e);
        }
    }

    private async Task<int> GetThreadPoolSize(CancellationToken cancellationToken)
    {
        var schedulerFactory = Utils.ServiceContainer.GetRequiredService<ISchedulerFactory>();
        var scheduler = await schedulerFactory.GetScheduler(cancellationToken);
        var metadata = await scheduler.GetMetaData(cancellationToken);
        return metadata.ThreadPoolSize;
    }

    private async Task JobFiringQueueEvents(ConnectionAndTransactionHolder conn, IJobDetail jobDetail, CancellationToken cancellationToken)
    {
        lock(_executingJobs) _executingJobs[jobDetail.Key] = jobDetail;
        var waitingTriggerCount = await GetWaitingTriggersCount(conn, cancellationToken);
        var blockedTriggerCount = await GetBlockedTriggersCount(conn, cancellationToken);
        if (_threadPoolSize == 0) _threadPoolSize = await GetThreadPoolSize(cancellationToken);
        QueueItem[] executing;
        lock (_executingJobs)
            executing = _executingJobs.Select(a =>
            {
                var job = _jobFactory.CreateJob(a.Value);
                return new QueueItem
                {
                    Key = a.Key.ToString(), JobType = job?.Name ?? a.Value.JobType.Name, Description = job?.Description.formatMessage()
                };
            }).ToArray();

        _queueStateEventHandler.OnJobExecuting(jobDetail, new QueueStateContext
        {
            ThreadCount = _threadPoolSize,
            WaitingTriggersCount = waitingTriggerCount,
            BlockedTriggersCount = blockedTriggerCount,
            CurrentlyExecuting = executing
        });
    }

    private async Task JobCompletedQueueEvents(ConnectionAndTransactionHolder conn, IJobDetail jobDetail, CancellationToken cancellationToken)
    {
        // this runs before the states have been updated, so things that were blocked for concurrency are still blocked at this point
        lock(_executingJobs) _executingJobs.Remove(jobDetail.Key);
        var waitingTriggerCount = await GetWaitingTriggersCount(conn, cancellationToken);
        var blockedTriggerCount = await GetBlockedTriggersCount(conn, cancellationToken);
        if (_threadPoolSize == 0) _threadPoolSize = await GetThreadPoolSize(cancellationToken);
        QueueItem[] executing;
        lock (_executingJobs)
            executing = _executingJobs.Values.Select(a =>
            {
                var job = _jobFactory.CreateJob(a);
                return new QueueItem
                {
                    Key = a.Key.ToString(), JobType = job?.Name, Description = job?.Description.formatMessage()
                };
            }).ToArray();

        _queueStateEventHandler.OnJobCompleted(jobDetail, new QueueStateContext
        {
            ThreadCount = _threadPoolSize,
            WaitingTriggersCount = waitingTriggerCount,
            BlockedTriggersCount = blockedTriggerCount,
            CurrentlyExecuting = executing
        });

        // this will prevent the idle waiting that exists to prevent constantly checking if it's time to trigger a schedule
        // it's now - 5 minutes because it checks if the DateTimeOffset is != MinValue and < Now within a "reasonable" period dependent on the JobStore
        if (waitingTriggerCount > 0 || blockedTriggerCount > 0)
            _signaler.SignalSchedulingChange(DateTimeOffset.UtcNow - TimeSpan.FromMinutes(5), cancellationToken);
    }
    
    private new IFilteredDriverDelegate Delegate => base.Delegate as IFilteredDriverDelegate;
}
