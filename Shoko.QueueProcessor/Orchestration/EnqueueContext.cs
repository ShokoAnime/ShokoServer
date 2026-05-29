using System;
using Shoko.QueueProcessor.Abstractions;
using Shoko.QueueProcessor.Storage;

namespace Shoko.QueueProcessor.Orchestration;

/// <summary>
/// Single object that carries everything the enqueue pipeline needs. Built once by the scheduler
/// — which has the live <see cref="IQueueJob"/> instance and the concrete <see cref="System.Type"/>
/// — and passed unchanged through <see cref="QueueOrchestrator.EnqueueAsync"/> to the persistence
/// buffer, sub-queue, and event handler. Nothing downstream rebuilds <see cref="DisplayItem"/>
/// or re-resolves <see cref="Type"/>.
/// </summary>
public sealed class EnqueueContext
{
    /// <summary>The persisted representation, written to the queue DB and the in-memory sub-queue.</summary>
    public required QueuedJob Job { get; init; }

    /// <summary>
    /// The display representation, surfaced via <see cref="Events.QueueStateEventHandler.OnJobsAdded"/>.
    /// Carries <see cref="QueueItem.TypeName"/>/<see cref="QueueItem.Title"/>/<see cref="QueueItem.Details"/>
    /// pulled straight off the live job instance — no reflection re-roundtrip via JSON.
    /// <see cref="QueueItem.PoolName"/> may be left blank here; the orchestrator stamps it from
    /// pool routing before firing the event.
    /// </summary>
    public required QueueItem DisplayItem { get; init; }

    /// <summary>
    /// The concrete job <see cref="System.Type"/>. Pre-resolved by the scheduler so the
    /// orchestrator doesn't re-lookup via its assembly-qualified-name map on every enqueue.
    /// </summary>
    public required Type Type { get; init; }
}
