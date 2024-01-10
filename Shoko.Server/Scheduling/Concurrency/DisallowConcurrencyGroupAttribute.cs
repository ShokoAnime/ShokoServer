using System;

namespace Shoko.Server.Scheduling.Concurrency;

[AttributeUsage(AttributeTargets.Class)]
public class DisallowConcurrencyGroupAttribute : Attribute
{
    /// <summary>
    /// The group to consider concurrency with. More than one Job in the same group will be disallowed from running concurrently
    /// </summary>
    public string Group { get; set; }

    public DisallowConcurrencyGroupAttribute(string group = null)
    {
        Group = group;
    }
}
