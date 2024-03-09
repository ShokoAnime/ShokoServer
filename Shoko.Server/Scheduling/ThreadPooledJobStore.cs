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
    private readonly Dictionary<JobKey, (IJobDetail Job, DateTime StartTime)> _executingJobs = new();
    private readonly IAcquisitionFilter[] _acquisitionFilters;
    private Dictionary<Type, int> _typeConcurrencyCache;
    private Dictionary<string, Type[]> _concurrencyGroupCache;
    private int _threadPoolSize;

    public ThreadPooledJobStore(ILogger<ThreadPooledJobStore> logger, IEnumerable<IAcquisitionFilter> acquisitionFilters,
        QueueStateEventHandler queueStateEventHandler, JobFactory jobFactory)
    {
        _logger = logger;
        _queueStateEventHandler = queueStateEventHandler;
        _jobFactory = jobFactory;
        _acquisitionFilters = acquisitionFilters.ToArray();
        foreach (var filter in _acquisitionFilters) filter.StateChanged += FilterOnStateChanged;
        InitConcurrencyCache();
    }

    public override async Task Initialize(ITypeLoadHelper loadHelper, ISchedulerSignaler signaler, CancellationToken cancellationToken = default)
    {
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
            var limitConcurrencyAttribute = type.GetCustomAttribute<LimitConcurrencyAttribute>();
            if (limitConcurrencyAttribute != null) _typeConcurrencyCache[type] = limitConcurrencyAttribute.MaxConcurrentJobs;
            
            var disallowConcurrentExecutionAttribute = type.GetCustomAttribute<DisallowConcurrentExecutionAttribute>();
            if (disallowConcurrentExecutionAttribute != null) _typeConcurrencyCache[type] = 1;

            var disallowConcurrencyGroupAttribute = type.GetCustomAttribute<DisallowConcurrencyGroupAttribute>();
            if (disallowConcurrencyGroupAttribute != null)
            {
                if (_concurrencyGroupCache.TryGetValue(disallowConcurrencyGroupAttribute.Group, out var groupTypes))
                    groupTypes = groupTypes.Append(type).Distinct().ToArray();
                else groupTypes = new[] { type };
                _concurrencyGroupCache[disallowConcurrencyGroupAttribute.Group] = groupTypes;
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
        foreach (var filter in _acquisitionFilters) filter.StateChanged -= FilterOnStateChanged;
    }

    private void FilterOnStateChanged(object sender, EventArgs e)
    {
        SignalSchedulingChangeImmediately(new DateTimeOffset(1982, 6, 28, 0, 0, 0, TimeSpan.FromSeconds(0)));
    }

    protected override async Task StoreTrigger(ConnectionAndTransactionHolder conn, IOperableTrigger newTrigger, IJobDetail job, bool replaceExisting, string state, bool forceState,
        bool recovering, CancellationToken cancellationToken = new CancellationToken())
    {
        // most of this is pulled from the base. Some fat is trimmed out, as we handle blocking ourselves
        var existingTrigger = await TriggerExists(conn, newTrigger.Key, cancellationToken).ConfigureAwait(false);
        if (existingTrigger && !replaceExisting) throw new ObjectAlreadyExistsException(newTrigger);

        try
        {
            if (!forceState)
            {
                var shouldBePaused = await Delegate.IsTriggerGroupPaused(conn, newTrigger.Key.Group, cancellationToken).ConfigureAwait(false);

                if (!shouldBePaused)
                {
                    shouldBePaused = await Delegate.IsTriggerGroupPaused(conn, AllGroupsPaused, cancellationToken).ConfigureAwait(false);
                    if (shouldBePaused) await Delegate.InsertPausedTriggerGroup(conn, newTrigger.Key.Group, cancellationToken).ConfigureAwait(false);
                }

                if (shouldBePaused && (state.Equals(StateWaiting) || state.Equals(StateAcquired))) state = StatePaused;
            }

            job ??= await RetrieveJob(conn, newTrigger.JobKey, cancellationToken).ConfigureAwait(false);
            if (job == null) throw new JobPersistenceException($"The job ({newTrigger.JobKey}) referenced by the trigger does not exist.");

            if (existingTrigger) await Delegate.UpdateTrigger(conn, newTrigger, state, job, cancellationToken).ConfigureAwait(false);
            else await Delegate.InsertTrigger(conn, newTrigger, state, job, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception e)
        {
            var message = $"Couldn't store trigger '{newTrigger.Key}' for '{newTrigger.JobKey}' job: {e.Message}";
            throw new JobPersistenceException(message, e);
        }

        if (!existingTrigger) await JobStoringQueueEvents(conn, job, cancellationToken);
    }

    protected override async Task<IReadOnlyCollection<IOperableTrigger>> AcquireNextTrigger(ConnectionAndTransactionHolder conn, DateTimeOffset noLaterThan,
            int maxCount, TimeSpan timeWindow, CancellationToken cancellationToken = default)
    {
        if (timeWindow < TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(timeWindow));

        const int MaxDoLoopRetry = 3;
        var acquiredTriggers = new List<IOperableTrigger>();
        var currentLoopCount = 0;

        do
        {
            currentLoopCount++;
            try
            {
                // this handles blocking via the GetTypes method and query
                var filteringTypes = GetTypes();
                var results = await Delegate.SelectTriggerToAcquire(conn, NoLaterThan, NoEarlierThan, maxCount, filteringTypes, cancellationToken)
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

                    try
                    {
                        _ = _typeLoadHelper.LoadType(result.JobType)!;
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

                    // We now have an acquired trigger, let's add to return list.
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
                _logger.LogError(e, "Error in Acquiring Next Trigger");
                throw new JobPersistenceException("Couldn't acquire next trigger: " + e.Message, e);
            }
        } while (true);

        // Return the acquired trigger list
        return acquiredTriggers;
    }

    private JobTypes GetTypes()
    {
        var excludedTypes = new List<Type>();
        var limitedTypes = new Dictionary<Type, int>();
        foreach (var filter in _acquisitionFilters) excludedTypes.AddRange(filter.GetTypesToExclude());

        IEnumerable<(Type Type, int Count)> executingTypes;
        lock (_executingJobs)
            executingTypes = _executingJobs.Values.Select(a => a.Job.JobType).GroupBy(a => a).Select(a => (Type: a.Key, Count: a.Count())).ToList();

        foreach (var kv in _typeConcurrencyCache)
        {
            var executing = executingTypes.FirstOrDefault(a => a.Type == kv.Key);
            // kv.Value is the max count, we want to get the number of remaining jobs we can run
            var limit = executing == default ? kv.Value : kv.Value - executing.Count;
            if (limit <= 0) excludedTypes.Add(kv.Key);
            else if (!excludedTypes.Contains(kv.Key)) limitedTypes[kv.Key] = limit;
        }

        var groups = new List<List<Type>>();
        foreach (var kv in _concurrencyGroupCache)
        {
            var executing = kv.Value.Any(a => executingTypes.Any(b => b.Type == a));
            if (executing)
            {
                excludedTypes.AddRange(kv.Value);
                continue;
            }

            var group = new List<Type>();
            foreach (var limitedType in kv.Value)
            {
                // this could happen if network isn't available or something
                if (excludedTypes.Contains(limitedType)) continue;
                // we only allow one concurrent job in a concurrency group, for example only 1 AniDB command
                group.Add(limitedType);
            }

            groups.Add(group);
        }

        return new JobTypes(excludedTypes.Distinct().ToList(), limitedTypes, groups);
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

        var nextState = !trigger.GetNextFireTimeUtc().HasValue ? StateComplete : StateWaiting;

        await StoreTrigger(conn, trigger, job, true, nextState, true, false, cancellationToken).ConfigureAwait(false);

        job.JobDataMap.ClearDirtyFlag();

        await JobFiringQueueEvents(conn, trigger, job, cancellationToken);
        
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

    private static DateTimeOffset NoLaterThan => DateTimeOffset.UtcNow.Add(TimeSpan.FromSeconds(30));
    private DateTimeOffset NoEarlierThan => MisfireTime;

    public Task<int> GetWaitingTriggersCount()
    {
        return ExecuteInNonManagedTXLock(LockTriggerAccess, async conn => await GetWaitingTriggersCount(conn), new CancellationToken());
    }

    private Task<int> GetWaitingTriggersCount(ConnectionAndTransactionHolder conn, CancellationToken cancellationToken = new CancellationToken())
    {
        var types = GetTypes();
        return Delegate.SelectWaitingTriggerCount(conn, NoLaterThan, NoEarlierThan, types, cancellationToken);
    }

    public Task<int> GetBlockedTriggersCount()
    {
        return ExecuteInNonManagedTXLock(LockTriggerAccess, async conn => await GetBlockedTriggersCount(conn), new CancellationToken());
    }

    private Task<int> GetBlockedTriggersCount(ConnectionAndTransactionHolder conn, CancellationToken cancellationToken = new CancellationToken())
    {
        var types = GetTypes();
        return Delegate.SelectBlockedTriggerCount(conn, _typeLoadHelper, NoLaterThan, NoEarlierThan, types,
            cancellationToken);
    }

    public Task<int> GetTotalWaitingTriggersCount()
    {
        return ExecuteInNonManagedTXLock(LockTriggerAccess, async conn => await GetTotalWaitingTriggersCount(conn), new CancellationToken());
    }

    private Task<int> GetTotalWaitingTriggersCount(ConnectionAndTransactionHolder conn, CancellationToken cancellationToken = new CancellationToken())
    {
        return Delegate.SelectTotalWaitingTriggerCount(conn, NoLaterThan, NoEarlierThan, cancellationToken);
    }

    public Task<Dictionary<Type, int>> GetJobCounts()
    {
        return ExecuteInNonManagedTXLock(LockTriggerAccess, async conn => await GetJobCounts(conn), new CancellationToken());
    }

    private Task<Dictionary<Type, int>> GetJobCounts(ConnectionAndTransactionHolder conn, CancellationToken cancellationToken = new CancellationToken())
    {
        return Delegate.SelectJobTypeCounts(conn, _typeLoadHelper, NoLaterThan, cancellationToken);
    }

    public Task<List<QueueItem>> GetJobs(int maxCount, int offset, bool excludeBlocked)
    {
        return ExecuteInNonManagedTXLock(LockTriggerAccess, async conn => await GetJobs(conn, maxCount, offset, excludeBlocked), new CancellationToken());
    }

    private async Task<List<QueueItem>> GetJobs(ConnectionAndTransactionHolder conn, int maxCount, int offset, bool excludeBlocked, CancellationToken cancellationToken = new CancellationToken())
    {
        var types = GetTypes();

        var result = new List<QueueItem>();
        lock (_executingJobs)
        {
            if (offset < _executingJobs.Count)
            {
                result.AddRange(_executingJobs.Values.Skip(offset).Take(maxCount).Select(a =>
                {
                    var job = _jobFactory.CreateJob(a.Job);
                    return new QueueItem
                    {
                        Key = a.Job.Key.ToString(),
                        JobType = job?.Name,
                        Description = job?.Description.formatMessage(),
                        Running = true,
                        StartTime = a.StartTime
                    };
                }).OrderBy(a => a.StartTime));
            }
        }

        var jobs = await Delegate.SelectJobs(conn, _typeLoadHelper, maxCount - result.Count, offset, NoLaterThan, NoEarlierThan, types, excludeBlocked,
            cancellationToken);
        result.AddRange(jobs.Select(a =>
        {
            var job = _jobFactory.CreateJob(a.Item1);
            return new QueueItem
            {
                Key = a.Item1.Key.ToString(), JobType = job?.Name, Description = job?.Description.formatMessage(), Blocked = a.Item2
            };
        }));

        return result;
    }

    protected override async Task TriggeredJobComplete(ConnectionAndTransactionHolder conn, IOperableTrigger trigger, IJobDetail jobDetail, SchedulerInstruction triggerInstCode,
        CancellationToken cancellationToken = new CancellationToken())
    {
        await base.TriggeredJobComplete(conn, trigger, jobDetail, triggerInstCode, cancellationToken);
        await JobCompletedQueueEvents(conn, jobDetail, cancellationToken);
    }

    private async Task<int> GetThreadPoolSize(CancellationToken cancellationToken)
    {
        var schedulerFactory = Utils.ServiceContainer.GetRequiredService<ISchedulerFactory>();
        var scheduler = await schedulerFactory.GetScheduler(cancellationToken);
        var metadata = await scheduler.GetMetaData(cancellationToken);
        return metadata.ThreadPoolSize;
    }

    private async Task JobStoringQueueEvents(ConnectionAndTransactionHolder conn, IJobDetail newJob, CancellationToken cancellationToken)
    {
        try
        {
            var waitingTriggerCount = await GetWaitingTriggersCount(conn, cancellationToken);
            var blockedTriggerCount = await GetBlockedTriggersCount(conn, cancellationToken);
            if (_threadPoolSize == 0) _threadPoolSize = await GetThreadPoolSize(cancellationToken);
            var executing = GetExecutingQueueItems();

            _queueStateEventHandler.OnJobAdded(newJob, new QueueStateContext
            {
                ThreadCount = _threadPoolSize,
                WaitingTriggersCount = waitingTriggerCount,
                BlockedTriggersCount = blockedTriggerCount,
                TotalTriggersCount = waitingTriggerCount + blockedTriggerCount + executing.Length,
                CurrentlyExecuting = executing
            });
        }
        catch (Exception e)
        {
            _logger.LogError(e, "An error occurred while firing Job Added events");
        }
    }

    private async Task JobFiringQueueEvents(ConnectionAndTransactionHolder conn, IOperableTrigger trigger, IJobDetail jobDetail,
        CancellationToken cancellationToken)
    {
        try
        {
            lock(_executingJobs) _executingJobs[jobDetail.Key] = (jobDetail, trigger.StartTimeUtc.LocalDateTime);
            var waitingTriggerCount = await GetWaitingTriggersCount(conn, cancellationToken);
            var blockedTriggerCount = await GetBlockedTriggersCount(conn, cancellationToken);
            if (_threadPoolSize == 0) _threadPoolSize = await GetThreadPoolSize(cancellationToken);
            var executing = GetExecutingQueueItems();

            _queueStateEventHandler.OnJobExecuting(jobDetail, new QueueStateContext
            {
                ThreadCount = _threadPoolSize,
                WaitingTriggersCount = waitingTriggerCount,
                BlockedTriggersCount = blockedTriggerCount,
                TotalTriggersCount = waitingTriggerCount + blockedTriggerCount + executing.Length,
                CurrentlyExecuting = executing
            });
        }
        catch (Exception e)
        {
            _logger.LogError(e, "An error occurred while firing Job Executing events");
        }
    }

    private QueueItem[] GetExecutingQueueItems()
    {
        QueueItem[] executing;
        lock (_executingJobs)
            executing = _executingJobs.Values.Select(a =>
            {
                var detail = a.Job;
                var job = _jobFactory.CreateJob(detail);
                return new QueueItem
                {
                    Key = detail.Key.ToString(),
                    JobType = job?.Name ?? detail.JobType.Name,
                    Description = job?.Description.formatMessage(),
                    Running = true,
                    StartTime = a.StartTime
                };
            }).OrderBy(a => a.StartTime).ToArray();
        return executing;
    }

    private async Task JobCompletedQueueEvents(ConnectionAndTransactionHolder conn, IJobDetail jobDetail, CancellationToken cancellationToken)
    {
        try
        {
            // this runs before the states have been updated, so things that were blocked for concurrency are still blocked at this point
            lock(_executingJobs) _executingJobs.Remove(jobDetail.Key);
            var waitingTriggerCount = await GetWaitingTriggersCount(conn, cancellationToken);
            var blockedTriggerCount = await GetBlockedTriggersCount(conn, cancellationToken);
            if (_threadPoolSize == 0) _threadPoolSize = await GetThreadPoolSize(cancellationToken);
            var executing = GetExecutingQueueItems();
            _queueStateEventHandler.OnJobCompleted(jobDetail, new QueueStateContext
            {
                ThreadCount = _threadPoolSize,
                WaitingTriggersCount = waitingTriggerCount,
                BlockedTriggersCount = blockedTriggerCount,
                TotalTriggersCount = waitingTriggerCount + blockedTriggerCount + executing.Length,
                CurrentlyExecuting = executing
            });

            // this will prevent the idle waiting that exists to prevent constantly checking if it's time to trigger a schedule
            if (waitingTriggerCount > 0 || blockedTriggerCount > 0)
                SignalSchedulingChangeImmediately(new DateTimeOffset(1982, 6, 28, 0, 0, 0, TimeSpan.FromSeconds(0)));
        }
        catch (Exception e)
        {
            _logger.LogError(e, "An error occurred while firing Job Completed events");
        }
    }

    private new IFilteredDriverDelegate Delegate => base.Delegate as IFilteredDriverDelegate;
}
