using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Quartz;
using Shoko.Server.Scheduling.GenericJobBuilder;

namespace Shoko.Server.Scheduling;

public static class QuartzExtensions
{
    public static readonly SemaphoreSlim SchedulerLock = new(1, 1);

    /// <summary>
    /// Queue a job of type T with the data map setter and generated identity
    /// </summary>
    /// <param name="scheduler"></param>
    /// <param name="data">Job Data Constructor</param>
    /// <typeparam name="T">Job Type</typeparam>
    /// <returns></returns>
    public static async Task<DateTimeOffset> StartJob<T>(this IScheduler scheduler, Action<T> data = null) where T : class, IJob
    {
        if (data == null)
            return await scheduler.StartJob(JobBuilder<T>.Create().WithGeneratedIdentity().Build());
        return await scheduler.StartJob(JobBuilder<T>.Create().UsingJobData(data).WithGeneratedIdentity().Build());
    }

    /// <summary>
    /// Force a job of type T with the data map setter and generated identity to run asap
    /// </summary>
    /// <param name="scheduler"></param>
    /// <param name="data">Job Data Constructor</param>
    /// <typeparam name="T">Job Type</typeparam>
    /// <returns></returns>
    public static async Task<DateTimeOffset> StartJobNow<T>(this IScheduler scheduler, Action<T> data = null) where T : class, IJob
    {
        if (data == null)
            return await scheduler.StartJob(JobBuilder<T>.Create().WithGeneratedIdentity().Build(), priority:10);
        return await scheduler.StartJob(JobBuilder<T>.Create().UsingJobData(data).WithGeneratedIdentity().Build(), priority:10);
    }
    
    /// <summary>
    /// Start a job with TriggerBuilder.<see cref="TriggerBuilder.StartNow()"/> on the given scheduler
    /// </summary>
    /// <param name="scheduler">The scheduler to schedule the job with</param>
    /// <param name="job">The job to schedule</param>
    /// <param name="scheduleBuilder"></param>
    /// <param name="priority">It will go in order by start time, then choose the higher priority. <seealso cref="TriggerBuilder.WithPriority(int)"/></param>
    /// <param name="replaceExisting">Replace the queued trigger if it's still waiting to execute. Default false</param>
    /// <param name="token">The cancellation token</param>
    /// <returns></returns>
    private static async Task<DateTimeOffset> StartJob(this IScheduler scheduler, IJobDetail job, IScheduleBuilder scheduleBuilder = null, int priority = 0, bool replaceExisting = false, CancellationToken token = default)
    {
        // if it's running, then ignore
        var currentJobs = await scheduler.GetCurrentlyExecutingJobs(token);
        if (currentJobs.Any(a => Equals(a.JobDetail.Key, job.Key))) return DateTimeOffset.Now;

        var triggerBuilder = TriggerBuilder.Create().StartNow().WithIdentity(job.Key.Name, job.Key.Group);
        if (priority != 0) triggerBuilder = triggerBuilder.WithPriority(priority);

        await SchedulerLock.WaitAsync(token);
        try
        {
            if (!await scheduler.CheckExists(job.Key, token))
            {
                return await scheduler.ScheduleJob(job,
                    triggerBuilder.WithSchedule(scheduleBuilder ?? SimpleScheduleBuilder.Create().WithMisfireHandlingInstructionIgnoreMisfires()).Build(), token);
            }

            // get waiting triggers
            var triggers = (await scheduler.GetTriggersOfJob(job.Key, token)).Select(a => a.GetNextFireTimeUtc())
                .Where(a => a != null).Select(a => a!.Value).ToList();

            // we are not set to replace the job, then return the first scheduled time
            if (triggers.Any() && !replaceExisting) return triggers.Min();

            // since we are replacing it, it will remove the triggers, as well
            await scheduler.DeleteJob(job.Key, token);

            return await scheduler.ScheduleJob(job,
                triggerBuilder.WithSchedule(scheduleBuilder ?? SimpleScheduleBuilder.Create().WithMisfireHandlingInstructionIgnoreMisfires()).Build(), token);
        }
        finally
        {
            SchedulerLock.Release();
        }
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
}
