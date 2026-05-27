#nullable enable
using System;

namespace Shoko.QueueProcessor.Builder;

/// <summary>
/// Marks a property (or the class itself for the key prefix) as a component of the unique job key.
/// When no properties carry this attribute, all public settable primitive properties are used.
/// </summary>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Class)]
public sealed class JobKeyMemberAttribute : Attribute
{
    /// <summary>
    /// Optional explicit sort index. Members with a lower index appear earlier in the key.
    /// Unspecified (<c>-1</c>) falls back to declaration order.
    /// </summary>
    public int Index { get; set; }

    /// <summary>
    /// Optional stable identifier for this key segment.
    /// Allows key format to survive property renames. Defaults to the property name.
    /// </summary>
    public string? Id { get; set; }

    /// <param name="id">Stable name for this segment.</param>
    /// <param name="index">Sort position in the key string.</param>
    public JobKeyMemberAttribute(string? id = null, int index = -1)
    {
        Id = id;
        Index = index;
    }
}
