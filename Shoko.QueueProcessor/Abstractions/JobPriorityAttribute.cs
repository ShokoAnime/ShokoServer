using System;

namespace Shoko.QueueProcessor.Abstractions;

/// <summary>
/// Declares the priorities a job type is queued with: <see cref="Default"/> for a normal
/// enqueue and <see cref="Prioritized"/> for one with <c>prioritize</c>. They only order
/// jobs within the type's pool. <see cref="Prioritized"/> may not be below
/// <see cref="Default"/>, and neither may reach <see cref="QueuePriority.Immediate"/>.
/// </summary>
/// <remarks>
/// The Shoko core's convention is <see cref="Prioritized"/> = <see cref="Default"/> + 50,
/// with each exception explained on its job type.
/// </remarks>
[AttributeUsage(AttributeTargets.Class, Inherited = true)]
public sealed class JobPriorityAttribute : Attribute
{
    /// <summary>
    /// The priority of a normal enqueue. <see cref="QueuePriority.Default"/> when unset.
    /// </summary>
    public int Default { get; init; } = QueuePriority.Default;

    /// <summary>
    /// The priority of an enqueue with <c>prioritize</c>. <see cref="QueuePriority.Prioritized"/>
    /// when unset.
    /// </summary>
    public int Prioritized { get; init; } = QueuePriority.Prioritized;
}
