using System;

namespace Shoko.QueueProcessor.Chain;

public class JobOutcome
{
    public Guid JobId { get; init; }
    public string JobType { get; init; } = string.Empty;
    public JobOutcomeStatus Status { get; init; }
    public string? ExceptionMessage { get; init; }
    public string? StackTrace { get; init; }
    public DateTimeOffset CompletedAt { get; init; }
}
