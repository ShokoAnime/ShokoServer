using System;

namespace Shoko.QueueProcessor.Orchestration;

/// <summary>
/// In-memory record of a job currently being executed by a worker.
/// Carries enough data to re-queue on failure without a DB round-trip.
/// </summary>
public record struct ExecutingEntry(
    Guid Id,
    Type JobType,
    string JobKey,
    string? JobDataJson,
    int Priority,
    int RetryCount,
    string? ConcurrencyGroup,
    DateTime StartedAt,
    string PoolName);
