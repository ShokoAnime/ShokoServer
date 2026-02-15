using System;
using Quartz;

#nullable enable
namespace Shoko.Server.Scheduling.GenericJobBuilder;

public class JobBuilder<T> : IJobConfiguratorWithDataAndIdentity<T>, IJobConfiguratorWithGeneratedIdentity<T> where T : class, IJob
{
    private JobKey? _key;
    private string? _description;
    private bool _shouldRecover;

    private JobDataMap _jobDataMap = new JobDataMap();

    /// <summary>
    /// The key that identifies the job uniquely.
    /// </summary>
    internal JobKey? Key => _key;

    protected JobBuilder()
    {
    }

    /// <summary>
    /// Create a JobBuilder with which to define a <see cref="IJobDetail" />.
    /// </summary>
    /// <returns>a new JobBuilder</returns>
    public static IJobConfigurator<T> Create()
    {
        return new JobBuilder<T>();
    }

    public void SetJobKey(JobKey jobKey)
    {
        _key = jobKey;
    }

    public JobDataMap GetJobData()
    {
        return _jobDataMap;
    }

    /// <summary>
    /// Produce the <see cref="IJobDetail" /> instance defined by this JobBuilder.
    /// </summary>
    /// <returns>the defined JobDetail.</returns>
    public IJobDetail Build()
    {
        var key = Key ?? new JobKey(Guid.NewGuid().ToString());

        var job = new JobDetail(typeof(T))
        {
            Name = key.Name,
            Group = key.Group,
            Description = _description,
            RequestsRecovery = _shouldRecover,
            JobDataMap = _jobDataMap
        };

        return job;
    }

    /// <summary>
    /// Set the given (human-meaningful) description of the Job.
    /// </summary>
    /// <param name="description"> the description for the Job</param>
    /// <returns>the updated JobBuilder</returns>
    /// <seealso cref="IJobDetail.Description" />
    public IJobConfigurator<T> WithDescription(string? description)
    {
        _description = description;
        return this;
    }

    /// <summary>
    /// Instructs the <see cref="IScheduler" /> whether or not the job
    /// should be re-executed if a 'recovery' or 'fail-over' situation is
    /// encountered.
    /// </summary>
    /// <remarks>
    /// If not explicitly set, the default value is <see langword="false" />.
    /// </remarks>
    /// <param name="shouldRecover"></param>
    /// <returns>the updated JobBuilder</returns>
    public IJobConfigurator<T> RequestRecovery(bool shouldRecover = true)
    {
        _shouldRecover = shouldRecover;
        return this;
    }

    /// <summary>
    /// Whether or not the job should remain stored after it is
    /// orphaned (no <see cref="ITrigger" />s point to it).
    /// </summary>
    /// <remarks>
    /// If not explicitly set, the default value is <see langword="false" />.
    /// </remarks>
    /// <param name="durability">the value to set for the durability property.</param>
    ///<returns>the updated JobBuilder</returns>
    /// <seealso cref="IJobDetail.Durable" />
    public IJobConfigurator<T> StoreDurably(bool durability = true)
    {
        return this;
    }

    /// <summary>
    /// Instructs the <see cref="IScheduler" /> whether or not concurrent execution of the job should be disallowed.
    /// </summary>
    /// <param name="concurrentExecutionDisallowed">Indicates whether or not concurrent execution of the job should be disallowed.</param>
    /// <returns>
    /// The updated <see cref="JobBuilder"/>.
    /// </returns>
    /// <remarks>
    /// If not explicitly set, concurrent execution of a job is only disallowed it either the <see cref="IJobDetail.JobType"/> itself,
    /// one of its ancestors or one of the interfaces that it implements, is annotated with <see cref="DisallowConcurrentExecutionAttribute"/>.
    /// </remarks>
    /// <seealso cref="DisallowConcurrentExecutionAttribute"/>
    public IJobConfigurator<T> DisallowConcurrentExecution(bool concurrentExecutionDisallowed = true)
    {
        return this;
    }

    /// <summary>
    /// Instructs the <see cref="IScheduler" /> whether or not job data should be re-stored when execution of the job completes.
    /// </summary>
    /// <param name="persistJobDataAfterExecution">Indicates whether or not job data should be re-stored when execution of the job completes.</param>
    /// <returns>
    /// The updated <see cref="JobBuilder"/>.
    /// </returns>
    /// <remarks>
    /// If not explicitly set, job data is only re-stored it either the <see cref="IJobDetail.JobType"/> itself, one of
    /// its ancestors or one of the interfaces that it implements, is annotated with <see cref="PersistJobDataAfterExecutionAttribute"/>.
    /// </remarks>
    /// <seealso cref="PersistJobDataAfterExecutionAttribute"/>
    public IJobConfigurator<T> PersistJobDataAfterExecution(bool persistJobDataAfterExecution = true)
    {
        return this;
    }

    /// <summary>
    /// Replace the <see cref="IJobDetail" />'s <see cref="JobDataMap" /> with the
    /// given <see cref="JobDataMap" />.
    /// </summary>
    /// <param name="newJobDataMap"></param>
    /// <returns></returns>
    internal void SetJobData(JobDataMap? newJobDataMap)
    {
        _jobDataMap = newJobDataMap ?? throw new ArgumentNullException(nameof(newJobDataMap));
    }
}
