using System;

namespace Shoko.QueueProcessor.Acquisition.Attributes;

/// <summary>
/// A base attribute for all acquisition-related logic
/// </summary>
[AttributeUsage(AttributeTargets.Class)]
public class AcquisitionAttribute(int priority = AcquisitionAttribute.LowestPriority) : Attribute
{
    public int WorkerPriority { get; set; } = priority;
    public const int LowestPriority = 999;
}
