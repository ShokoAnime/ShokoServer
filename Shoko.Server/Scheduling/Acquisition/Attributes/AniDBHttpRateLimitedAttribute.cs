using System;
using Shoko.QueueProcessor.Acquisition.Attributes;

namespace Shoko.Server.Scheduling.Acquisition.Attributes;

/// <summary>
/// Marks a job as requiring the AniDB HTTP connection to be available and not banned.
/// Inherits from <see cref="NetworkRequiredAttribute"/> so the network-availability gate
/// applies automatically without needing to add both attributes.
/// </summary>
[AttributeUsage(AttributeTargets.Class)]
public class AniDBHttpRateLimitedAttribute() : NetworkRequiredAttribute((int)Acquisition.WorkerPriority.AniDB);
