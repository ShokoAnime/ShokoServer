using System;

namespace Shoko.QueueProcessor.Concurrency;

/// <summary>
/// Associates a job type with a named concurrency group.  All types sharing the same group name
/// are assigned to a single pool; the pool's worker count is determined by
/// <see cref="LimitConcurrencyAttribute"/> on any type in the group (they should be consistent).
/// <para>
/// At most <c>LimitConcurrency.MaxConcurrentJobs</c> jobs from this group run at once.
/// </para>
/// </summary>
[AttributeUsage(AttributeTargets.Class, Inherited = true)]
public sealed class DisallowConcurrencyGroupAttribute : Attribute
{
    /// <summary>Group name; all job types sharing this name share one pool.</summary>
    public string Group { get; }

    /// <param name="group">Group name.</param>
    public DisallowConcurrencyGroupAttribute(string group)
    {
        Group = group;
    }
}
