using System;

namespace Shoko.QueueProcessor.Builder;

/// <summary>
/// Tags a job type with a logical group name for key namespacing.
/// Used by <see cref="JobKeyBuilder{T}"/> to prefix the generated key.
/// </summary>
[AttributeUsage(AttributeTargets.Class)]
public sealed class JobKeyGroupAttribute : Attribute
{
    /// <summary>The group/namespace prefix for the job key.</summary>
    public string GroupName { get; }

    /// <param name="groupName">Group prefix.</param>
    public JobKeyGroupAttribute(string groupName)
    {
        GroupName = groupName;
    }
}
