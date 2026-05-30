using System;

namespace Shoko.QueueProcessor.Abstractions;

/// <summary>
/// Thrown when a job cannot run because an acquisition filter currently excludes its type.
/// Callers should catch this and fall back to queuing the job normally.
/// </summary>
public class JobBlockedException : Exception
{
    public Type JobType { get; }

    public JobBlockedException(Type jobType)
        : base($"Job '{jobType.Name}' is blocked by an acquisition filter and cannot run.")
        => JobType = jobType;
}
