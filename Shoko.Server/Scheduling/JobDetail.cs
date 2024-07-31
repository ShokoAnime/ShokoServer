using System;
using System.Globalization;
using Quartz;
using Quartz.Impl;

#nullable enable
namespace Shoko.Server.Scheduling;

public class JobDetail(Type type) : IJobDetail, IEquatable<JobDetail>
{
    private string _name = string.Empty;
    private string _group = SchedulerConstants.DefaultGroup;
    private string? _description;
    private JobDataMap? _jobDataMap = null;

    [NonSerialized] // we have the key in string fields
    private JobKey? _key = null;

    public string Name
    {
        get => _name;

        init
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                throw new ArgumentException("Job name cannot be empty.");
            }

            _name = value;
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
        get => _group;
        init
        {
            if (value != null && value.Trim().Length == 0)
            {
                throw new ArgumentException("Group name cannot be empty.");
            }

            value ??= SchedulerConstants.DefaultGroup;

            _group = value;
        }
    }

    /// <summary>
    /// Returns the 'full name' of the <see cref="ITrigger" /> in the format
    /// "group.name".
    /// </summary>
    public string FullName => _group + "." + _name;

    /// <summary>
    /// Gets the key.
    /// </summary>
    /// <value>The key.</value>
    public JobKey Key
    {
        get
        {
            if (_key == null)
            {
                if (Name == null)
                {
                    return null!;
                }
                _key = new JobKey(Name, Group);
            }

            return _key;
        }
        init
        {
            ArgumentNullException.ThrowIfNull(value, nameof(Key));

            Name = value.Name;
            Group = value.Group;
            _key = value;
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
        get => _description;
        init => _description = value;
    }

    public JobType JobType { get; private set; } = new(type);

    /// <summary>
    /// Get or set the <see cref="JobDataMap" /> that is associated with the <see cref="IJob" />.
    /// </summary>
    public JobDataMap JobDataMap
    {
        get
        {
            return _jobDataMap ??= [];
        }

        init => _jobDataMap = value;
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
        var copy = (JobDetail)MemberwiseClone();
        if (_jobDataMap != null)
        {
            copy._jobDataMap = (JobDataMap)_jobDataMap.Clone();
        }
        return copy;
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
        return obj is JobDetail jd && Equals(jd);
    }

    /// <summary>
    /// Checks equality between given job detail and this instance.
    /// </summary>
    /// <param name="detail">The detail to compare this instance with.</param>
    /// <returns></returns>
    public bool Equals(JobDetail? detail)
    {
        return detail is not null && detail.Name == Name && detail.Group == Group && detail.JobType.Equals(JobType);
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
            .WithDescription(_description)
            .WithIdentity(Key);
    }
}
