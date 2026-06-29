using System;
using Shoko.QueueProcessor.Acquisition.Attributes;

namespace Shoko.Server.Scheduling.Acquisition.Attributes;

/// <summary>
/// Marks a job as requiring the TMDB API to be available and not in a 5XX circuit-breaker pause.
/// Inherits from <see cref="NetworkRequiredAttribute"/> so the network-availability gate
/// applies automatically without needing to add both attributes.
/// </summary>
[AttributeUsage(AttributeTargets.Class)]
public class TmdbApiRateLimitedAttribute() : NetworkRequiredAttribute((int)Acquisition.WorkerPriority.TMDB);
