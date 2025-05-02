using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Nito.AsyncEx;
using NLog;
using Quartz;
using Shoko.Server.Scheduling.GenericJobBuilder;
using Shoko.Server.Utilities;

#nullable enable
namespace Shoko.Server.Scheduling;

public static class QuartzExtensions
{
    public static readonly AsyncReaderWriterLock SchedulerLock = new();

    private static readonly Logger _logger = LogManager.GetCurrentClassLogger();

    private static readonly ConcurrentQueue<(IJobDetail job, int priority, DateTimeOffset? startTime)[]> _jobQueue = new();

    private static ConcurrentBag<(IJobDetail job, int priority, DateTimeOffset? startTime)> _pendingJobs = [];

    private static System.Timers.Timer? _jobTimer;

    private static bool _isRunning = false;

    private static readonly object _flushLock = new();

    private static readonly object _queueLock = new();

    /// <summary>
    /// Queue a job of type T with the data map setter and generated identity
    /// </summary>
    /// <param name="scheduler"></param>
    /// <param name="data">Job Data Constructor</param>
    /// <param name="prioritize">
    ///   If true, the job will be prioritized in the queue.
    /// </param>
    /// <param name="startTime">
    ///   When to start the job.
    /// </param>
    /// <typeparam name="T">Job Type</typeparam>
    /// <returns></returns>
    public static Task StartJob<T>(this IScheduler scheduler, Action<T>? data = null, bool prioritize = false, DateTimeOffset? startTime = null) where T : class, IJob
    {
        var settings = Utils.SettingsProvider.GetSettings();
        var job = data != null
            ? JobBuilder<T>.Create().UsingJobData(data).WithGeneratedIdentity().Build()
            : JobBuilder<T>.Create().WithGeneratedIdentity().Build();
        if (settings.Quartz.BatchMaxInsertSize == 1 || settings.Quartz.BatchInsertTimeoutInMS == 0)
            return scheduler.StartJobs([(job, prioritize ? 10 : 0, startTime)]);

        _pendingJobs.Add((job, prioritize ? 10 : 0, startTime));
        PerhapsFlush();
        return Task.CompletedTask;
    }

    private static void Flush(object? sender, System.Timers.ElapsedEventArgs e)
        => PerhapsFlush(force: true);

    private static void PerhapsFlush(bool force = false)
    {
        (IJobDetail job, int priority, DateTimeOffset? startTime)[]? jobs = null;
        var settings = Utils.SettingsProvider.GetSettings();
        if (!force && (_jobTimer?.Enabled ?? false) && _pendingJobs.Count < settings.Quartz.BatchMaxInsertSize) return;
        lock (_flushLock)
        {
            if (!force && (_jobTimer?.Enabled ?? false) && _pendingJobs.Count < settings.Quartz.BatchMaxInsertSize) return;
            if (force || _pendingJobs.Count >= settings.Quartz.BatchMaxInsertSize)
            {
                var pendingJobs = _pendingJobs;
                _pendingJobs = [];
                jobs = pendingJobs.ToArray().Reverse().ToArray();
                pendingJobs.Clear();
            }

            if (_jobTimer is null)
            {
                _jobTimer = new(settings.Quartz.BatchInsertTimeoutInMS)
                {
                    AutoReset = false,
                    Enabled = true,
                };
                _jobTimer.Elapsed += Flush;
            }
            _jobTimer.Enabled = false;
            if (_jobTimer.Interval != settings.Quartz.BatchInsertTimeoutInMS)
                _jobTimer.Interval = settings.Quartz.BatchInsertTimeoutInMS;
            if (!_pendingJobs.IsEmpty)
                _jobTimer.Enabled = true;
        }

        if (jobs is not null && jobs.Length > 0)
        {
            _jobQueue.Enqueue(jobs);
            if (!_isRunning)
            {
                lock (_queueLock)
                {
                    if (!_isRunning)
                    {
                        _isRunning = true;
                        Task.Factory.StartNew(ProcessJobs, TaskCreationOptions.LongRunning);
                    }
                }
            }
        }
    }

    private static async Task ProcessJobs()
    {
        var scheduler = await Utils.ServiceContainer.GetRequiredService<ISchedulerFactory>().GetScheduler();
        var scheduleBuilder = SimpleScheduleBuilder.Create().WithMisfireHandlingInstructionIgnoreMisfires();
        while (_jobQueue.TryDequeue(out var jobs))
        {
            try
            {
                await StartJobs(scheduler, jobs, scheduleBuilder);
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error processing jobs");
            }
        }

        lock (_queueLock)
        {
            _isRunning = false;
        }
    }

    /// <summary>
    /// Start a job with TriggerBuilder.<see cref="TriggerBuilder.StartNow()"/> on the given scheduler
    /// </summary>
    /// <param name="scheduler">The scheduler to schedule the job with</param>
    /// <param name="jobs">The jobs to schedule</param>
    /// <param name="scheduleBuilder"></param>
    /// <returns></returns>
    private static async Task StartJobs(this IScheduler scheduler, (IJobDetail job, int priority, DateTimeOffset? startTime)[] jobs, IScheduleBuilder? scheduleBuilder = null)
    {
        // if it's running, then ignore
        var currentJobs = await scheduler.GetCurrentlyExecutingJobs();
        var jobKeys = jobs.Select(tuple => tuple.job.Key).ToHashSet();
        if (currentJobs.Select(executing => executing.JobDetail.Key).Where(jobKeys.Contains).ToHashSet() is { Count: > 0 } runningJobs)
        {
            foreach (var job in runningJobs)
                _logger.Trace("Skipped scheduling {JobName} because it is running.", job);
            jobs = jobs.DistinctBy(tuple => tuple.job.Key).ExceptBy(runningJobs, job => job.job.Key).ToArray();
        }
        else
        {
            jobs = jobs.DistinctBy(tuple => tuple.job.Key).ToArray();
        }

        if (jobs.Length == 0) return;

        scheduleBuilder ??= SimpleScheduleBuilder.Create().WithMisfireHandlingInstructionIgnoreMisfires();
        var collection = new Dictionary<IJobDetail, IReadOnlyCollection<ITrigger>>();
        var time = DateTimeOffset.UtcNow;
        using (var _ = await SchedulerLock.ReaderLockAsync())
        {
            foreach (var (job, priority, startTime) in jobs)
            {
                if (await scheduler.CheckExists(job.Key))
                {
                    _logger.Trace("Skipped scheduling {JobName} because it already exists.", job.Key);
                    continue;
                }
                var triggerBuilder = TriggerBuilder.Create().StartNow().WithIdentity(job.Key.Name, job.Key.Group);
                if (priority != 0) triggerBuilder = triggerBuilder.WithPriority(priority);
                if (startTime.HasValue && startTime > time) triggerBuilder = triggerBuilder.StartAt(startTime.Value);
                var trigger = triggerBuilder.WithSchedule(scheduleBuilder).Build();
                collection[job] = [trigger];
                _logger.Trace("Scheduling {JobName}", job.Key);
            }
        }

        if (collection.Count == 0) return;
        time = DateTimeOffset.UtcNow;
        _logger.Trace("Scheduling {Count} jobs.", collection.Count);
        using var __ = await SchedulerLock.WriterLockAsync();
        await scheduler.ScheduleJobs(collection, false);
        _logger.Trace("Scheduled {Count} jobs in {Time}.", collection.Count, DateTimeOffset.UtcNow - time);
    }

    /// <summary>
    /// This will add an array of parameters to a SqlCommand. This is used for an IN statement.
    /// Use the returned value for the IN part of your SQL call. (i.e. SELECT * FROM table WHERE field IN ({paramNameRoot}))
    /// </summary>
    /// <param name="cmd">The SqlCommand object to add parameters to.</param>
    /// <param name="paramNameRoot">What the parameter should be named followed by a unique value for each value. This value surrounded by {} in the CommandText will be replaced.</param>
    /// <param name="values">The array of strings that need to be added as parameters.</param>
    /// <param name="dbType">One of the System.Data.SqlDbType values. If null, determines type based on T.</param>
    /// <param name="size">The maximum size, in bytes, of the data within the column. The default value is inferred from the parameter value.</param>
    public static void AddArrayParameters<T>(this IDbCommand cmd, string paramNameRoot, IEnumerable<T> values, DbType? dbType = null, int? size = null)
    {
        /* An array cannot be simply added as a parameter to a SqlCommand, so we need to loop through things and add it manually. 
         * Each item in the array will end up being its own SqlParameter so the return value for this must be used as part of the
         * IN statement in the CommandText.
         */
        var parameterNames = new List<string>();
        var paramNbr = 1;
        foreach (var value in values)
        {
            var paramName = $"@{paramNameRoot}{paramNbr++}";
            parameterNames.Add(paramName);
            var p = cmd.CreateParameter();
            p.ParameterName = paramName;
            p.Value = value;
            if (dbType.HasValue)
                p.DbType = dbType.Value;
            if (size.HasValue)
                p.Size = size.Value;
            cmd.Parameters.Add(p);
        }

        cmd.CommandText = cmd.CommandText.Replace("@" + paramNameRoot, string.Join(",", parameterNames));
    }

    public static async Task RescheduleJob(this IJobExecutionContext context)
    {
        var triggerKey = context.Trigger.Key;
        var newKey = new TriggerKey(triggerKey.Name + "_Retry", triggerKey.Group);
        if (await context.Scheduler.GetTrigger(newKey) != null) return;

        var newTrigger = context.Trigger.GetTriggerBuilder();
        newTrigger.WithIdentity(newKey);
        await context.Scheduler.ScheduleJob(newTrigger.Build(), context.CancellationToken);
    }
}
