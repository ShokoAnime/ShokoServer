using System;
using System.Collections.Generic;

namespace Shoko.QueueProcessor.Orchestration;

/// <summary>
/// In-memory record of a job currently being executed by a worker.
/// Carries enough data to re-queue on failure without a DB round-trip.
/// <para>
/// <see cref="TypeName"/>, <see cref="Title"/>, and <see cref="Details"/> are populated by the
/// worker after the job instance is resolved and <see cref="Abstractions.IQueueJob.PostInit"/>
/// has run, so they may be empty for a brief window between acquisition and the first display update.
/// </para>
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
    string PoolName,
    string TypeName = "",
    string Title = "",
    Dictionary<string, object>? Details = null);
