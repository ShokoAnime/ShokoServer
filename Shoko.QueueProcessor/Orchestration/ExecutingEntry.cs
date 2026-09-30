using System;
using System.Collections.Generic;

namespace Shoko.QueueProcessor.Orchestration;

/// <summary>
/// In-memory record of a job currently being executed by a worker.
/// Carries enough data to re-queue on failure without a DB round-trip.
/// </summary>
/// <remarks>
/// <see cref="TypeName"/>, <see cref="Title"/> and <see cref="Details"/> may be empty until the
/// worker has resolved the job and run <see cref="Abstractions.IQueueJob.PostInit"/>.
/// <see cref="Progress"/> is only filled in the snapshots <see cref="QueueOrchestrator.GetExecuting"/>
/// hands out. <see cref="Actor"/> is kept so a re-queued or retried job still runs for whoever queued it.
/// </remarks>
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
    Dictionary<string, object>? Details = null,
    Guid? ChainId = null,
    bool IsChainFinally = false,
    bool IsCancellable = false,
    bool CancellationRequested = false,
    decimal? Progress = null,
    Abstractions.JobActor? Actor = null);
