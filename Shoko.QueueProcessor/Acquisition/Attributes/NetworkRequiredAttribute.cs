using System;

namespace Shoko.QueueProcessor.Acquisition.Attributes;

/// <summary>
/// Marks a job as requiring internet connectivity. Subclass this attribute to create more
/// specific network-related constraints (e.g. AniDB rate-limiting) while automatically
/// inheriting the network-availability gate from <see cref="Filters.NetworkRequiredAcquisitionFilter"/>.
/// </summary>
[AttributeUsage(AttributeTargets.Class, Inherited = true)]
public class NetworkRequiredAttribute : Attribute { }
