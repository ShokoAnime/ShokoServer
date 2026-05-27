using System;

namespace Shoko.Server.Scheduling.Acquisition.Attributes;

/// <summary>Marks a job as requiring the AniDB UDP connection to be available and not rate-limited.</summary>
[AttributeUsage(AttributeTargets.Class, Inherited = true)]
public sealed class AniDBUdpRateLimitedAttribute : Attribute { }
