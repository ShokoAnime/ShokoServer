#nullable enable
using System;
using System.Globalization;
using Quartz;
using Quartz.Impl;

namespace Shoko.Server.Scheduling;

public class JobDetail : IJobDetail
{
    private string name = null!;
    private string group = SchedulerConstants.DefaultGroup;
    private string? description;
    private JobDataMap jobDataMap = null!;
    private readonly Type jobType = null!;

    [NonSerialized] // we have the key in string fields
    private JobKey key = null!;

    public string Name
    {
        get => name;

        init
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                throw new ArgumentException("Job name cannot be empty.");
            }

            name = value;
        }
    }

    /// <summary>
    /// Get or sets the group of this <see cref="IJob" />.
    /// If <see langword="null" />, <see cref="SchedulerConstants.DefaultGroup" /> will be used.
    /// </summary>
    /// <exception cref="ArgumentException">
    /// If the group is an empty string.
    /// </exception>
    public string Group
    {
        get => group;
        init
        {
            if (value != null && value.Trim().Length == 0)
            {
                throw new ArgumentException("Group name cannot be empty.");
            }

            if (value == null)
            {
                value = SchedulerConstants.DefaultGroup;
            }

            group = value;
        }
    }

    /// <summary>
    /// Returns the 'full name' of the <see cref="ITrigger" /> in the format
    /// "group.name".
    /// </summary>
    public string FullName => group + "." + name;

    /// <summary>
    /// Gets the key.
    /// </summary>
    /// <value>The key.</value>
    public JobKey Key
    {
        get
        {
            if (key == null)
            {
                if (Name == null)
                {
                    return null!;
                }
                key = new JobKey(Name, Group);
            }

            return key;
        }
        init
        {
            ArgumentNullException.ThrowIfNull(value, nameof(Key));

            Name = value.Name;
            Group = value.Group;
            key = value;
        }
    }

    /// <summary>
    /// Get or set the description given to the <see cref="IJob" /> instance by its
    /// creator (if any).
    /// </summary>
    /// <remarks>
    /// May be useful for remembering/displaying the purpose of the job, though the
    /// description has no meaning to Quartz.
    /// </remarks>
    public string? Description
    {
        get => description;
        init => description = value;
    }

    public JobType JobType { get; init; }

    /// <summary>
    /// Get or set the <see cref="JobDataMap" /> that is associated with the <see cref="IJob" />.
    /// </summary>
    public JobDataMap JobDataMap
    {
        get
        {
            return jobDataMap ??= new JobDataMap();
        }

        init => jobDataMap = value;
    }

    /// <summary>
    /// Set whether or not the <see cref="IScheduler" /> should re-Execute
    /// the <see cref="IJob" /> if a 'recovery' or 'fail-over' situation is
    /// encountered.
    /// <para>
    /// If not explicitly set, the default value is <see langword="false" />.
    /// </para>
    /// </summary>
    /// <seealso cref="IJobExecutionContext.Recovering" />
    public bool RequestsRecovery { get; set; }

    // we can do this because our scheduler doesn't use it
    public bool Durable => false;

    // we can do this because our scheduler doesn't use it
    public bool PersistJobDataAfterExecution => false;

    // we can do this because our scheduler doesn't use it
    public bool ConcurrentExecutionDisallowed => false;

    /// <summary>
    /// Return a simple string representation of this object.
    /// </summary>
    public override string ToString()
    {
        return
            string.Format(
                CultureInfo.InvariantCulture,
                "JobDetail '{0}':  jobType: '{1} requestsRecovers: {2}",
                FullName, JobType?.FullName, RequestsRecovery);
    }

    /// <summary>
    /// Creates a new object that is a copy of the current instance.
    /// </summary>
    /// <returns>
    /// A new object that is a copy of this instance.
    /// </returns>
    public IJobDetail Clone()
    {
        var copy = (JobDetail) MemberwiseClone();
        if (jobDataMap != null)
        {
            copy.jobDataMap = (JobDataMap) jobDataMap.Clone();
        }
        return copy;
    }

    /// <summary>
    /// Determines whether the specified detail is equal to this instance.
    /// </summary>
    /// <param name="detail">The detail to examine.</param>
    /// <returns>
    /// 	<c>true</c> if the specified detail is equal; otherwise, <c>false</c>.
    /// </returns>
    private bool IsEqual(JobDetail? detail)
    {
        //doesn't consider job's saved data,
        //durability etc
        return detail != null && detail.Name == Name && detail.Group == Group && detail.JobType.Equals(JobType);
    }

    /// <summary>
    /// Determines whether the specified <see cref="T:System.Object"/> is equal to the current <see cref="T:System.Object"/>.
    /// </summary>
    /// <param name="obj">The <see cref="T:System.Object"/> to compare with the current <see cref="T:System.Object"/>.</param>
    /// <returns>
    /// 	<see langword="true"/> if the specified <see cref="T:System.Object"/> is equal to the
    /// current <see cref="T:System.Object"/>; otherwise, <see langword="false"/>.
    /// </returns>
    public override bool Equals(object? obj)
    {
        if (!(obj is JobDetail jd))
        {
            return false;
        }

        return IsEqual(jd);
    }

    /// <summary>
    /// Checks equality between given job detail and this instance.
    /// </summary>
    /// <param name="detail">The detail to compare this instance with.</param>
    /// <returns></returns>
    public bool Equals(JobDetail detail)
    {
        return IsEqual(detail);
    }

    /// <summary>
    /// Serves as a hash function for a particular type, suitable
    /// for use in hashing algorithms and data structures like a hash table.
    /// </summary>
    /// <returns>
    /// A hash code for the current <see cref="T:System.Object"/>.
    /// </returns>
    public override int GetHashCode()
    {
        return FullName.GetHashCode();
    }

    public JobBuilder GetJobBuilder()
    {
        return JobBuilder.Create()
            .OfType(JobType)
            .RequestRecovery(RequestsRecovery)
            .StoreDurably(Durable)
            .UsingJobData(JobDataMap)
            .DisallowConcurrentExecution(ConcurrentExecutionDisallowed)
            .PersistJobDataAfterExecution(PersistJobDataAfterExecution)
            .WithDescription(description)
            .WithIdentity(Key);
    }
}
