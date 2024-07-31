using System;

#nullable enable
namespace Shoko.Server.Scheduling.Attributes;

[AttributeUsage(AttributeTargets.Property | AttributeTargets.Class)]
public class JobKeyMemberAttribute : Attribute
{
    public JobKeyMemberAttribute(string? id = null, int index = -1)
    {
        Index = index;
        Id = id;
    }

    /// <summary>
    /// The order in which the string builder will add this item to the JobKey.
    /// This can help ensure persistence if a class structure is changed.
    /// If unspecified, it will default to order of member definition.
    /// </summary>
    public int Index { get; set; }

    /// <summary>
    /// A Name for the part of the JobKey.
    /// This can help ensure persistence if a class structure is changed.
    /// If not specified, it will default to the member name.
    /// </summary>
    public string? Id { get; set; }

    // TODO maybe allow encryption of member value, to ensure unique Job Details by Job Data, but without printing sensitive data to the JobKey
}
