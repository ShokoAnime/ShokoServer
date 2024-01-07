using System;

namespace Shoko.Server.Scheduling;

public class LimitConcurrencyAttribute : Attribute
{
    /// <summary>
    /// The maximum number of concurrent executions that this job type should allow
    /// </summary>
    public int MaxConcurrentJobs { get; set; }

    /// <summary>
    /// The maximum number of concurrent executions that this job type should allow
    /// </summary>
    public int MaxAllowedConcurrentJobs { get; set; }

    public LimitConcurrencyAttribute(int maxConcurrentJobs = 4)
    {
        MaxConcurrentJobs = maxConcurrentJobs;
    }
}
