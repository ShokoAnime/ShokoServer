using System;
using Quartz;

#nullable enable
namespace Shoko.Server.Scheduling.GenericJobBuilder;

public interface IJobConfigurator { }

public interface IJobConfigurator<T> : IJobConfigurator where T : class, IJob
{
    void SetJobKey(JobKey key);
    /// <summary>
    /// Set the given (human-meaningful) description of the Job.
    /// </summary>
    /// <param name="description"> the description for the Job</param>
    /// <returns>the updated IJobConfigurator</returns>
    /// <seealso cref="IJobDetail.Description" />
    IJobConfigurator<T> WithDescription(string? description);

    /// <summary>
    /// Instructs the <see cref="IScheduler" /> whether or not the job
    /// should be re-executed if a 'recovery' or 'fail-over' situation is
    /// encountered.
    /// </summary>
    /// <remarks>
    /// If not explicitly set, the default value is <see langword="false" />.
    /// </remarks>
    /// <param name="shouldRecover"></param>
    /// <returns>the updated IJobConfigurator</returns>
    IJobConfigurator<T> RequestRecovery(bool shouldRecover = true);

    /// <summary>
    /// Whether or not the job should remain stored after it is
    /// orphaned (no <see cref="ITrigger" />s point to it).
    /// </summary>
    /// <remarks>
    /// If not explicitly set, the default value is <see langword="false" />.
    /// </remarks>
    /// <param name="durability">the value to set for the durability property.</param>
    ///<returns>the updated IJobConfigurator</returns>
    /// <seealso cref="IJobDetail.Durable" />
    IJobConfigurator<T> StoreDurably(bool durability = true);

    /// <summary>
    /// Gets the <see cref="JobDataMap" /> for the configurator
    /// </summary>
    /// <returns><see cref="JobDataMap"/></returns>
    JobDataMap GetJobData();

    /// <summary>
    /// Obsolete. A job without an Identity will have a random one assigned to it, allowing many of the same types to coexist.
    /// If this is intended, use <see cref="IdentityExtensions.WithDefaultIdentity{T}(IJobConfigurator{T})"/> to mark this as acceptable
    /// </summary>
    /// <returns></returns>
    [Obsolete("A job without an Identity will have a random one assigned to it, allowing many of the same types to coexist. If this is intended, use WithDefaultIdentity() to mark this as acceptable")]
    IJobDetail Build();

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
    public IJobConfigurator<T> DisallowConcurrentExecution(bool concurrentExecutionDisallowed = true);

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
    public IJobConfigurator<T> PersistJobDataAfterExecution(bool persistJobDataAfterExecution = true);
}

public interface IJobConfiguratorWithData<T> : IJobConfigurator<T> where T : class, IJob { }

public interface IJobConfiguratorWithIdentity<T> : IJobConfigurator<T> where T : class, IJob
{
    /// <summary>
    /// Produce the <see cref="IJobDetail" /> instance defined by this IJobConfigurator.
    /// </summary>
    /// <returns>the defined JobDetail.</returns>
    new IJobDetail Build();
}

public interface IJobConfiguratorWithGeneratedIdentity<T> : IJobConfiguratorWithIdentity<T> where T : class, IJob { }

public interface IJobConfiguratorWithDataAndIdentity<T> : IJobConfiguratorWithData<T>, IJobConfiguratorWithIdentity<T> where T : class, IJob
{
    /// <summary>
    /// Produce the <see cref="IJobDetail" /> instance defined by this IJobConfigurator.
    /// </summary>
    /// <returns>the defined JobDetail.</returns>
    new IJobDetail Build();
}
