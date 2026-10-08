using System;

namespace Shoko.QueueProcessor.Acquisition.Attributes;

/// <summary>
/// Marks a job as requiring internet connectivity. Subclass this attribute to create more
/// specific network-related constraints (e.g. AniDB rate-limiting) while automatically
/// inheriting the network-availability gate that the host registers for it.
/// </summary>
[AttributeUsage(AttributeTargets.Class)]
public class NetworkRequiredAttribute(int priority = AcquisitionAttribute.LowestPriority) : AcquisitionAttribute(priority);
