using System;

namespace Shoko.Server.Scheduling.Acquisition.Attributes;

/// <summary>
/// Marks a job as requiring the AniDB UDP connection to be available and not banned.
/// Inherits from <see cref="NetworkRequiredAttribute"/> so the network-availability gate
/// applies automatically without needing to add both attributes.
/// </summary>
[AttributeUsage(AttributeTargets.Class, Inherited = true)]
public class AniDBUdpRateLimitedAttribute : NetworkRequiredAttribute { }
