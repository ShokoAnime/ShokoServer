using System;
using System.Collections.Concurrent;
using System.Threading;

namespace Shoko.QueueProcessor.Workers;

/// <summary>
/// Bridges <see cref="JobFactory"/> and <see cref="JobWatchdog"/>: stores the call stack captured
/// at the point <see cref="Abstractions.IJobFactory.Execute{T}"/> was entered from within a worker
/// job, keyed by the outer job's ID. This gives the watchdog actionable context when it detects a
/// job that has been running too long.
/// </summary>
internal static class SubExecutionTracker
{
    /// <summary>
    /// Flows with async continuations. Set by <see cref="Worker"/> to the current job's ID before
    /// <see cref="Abstractions.IQueueJob.Process"/> is called, so <see cref="JobFactory"/> can
    /// associate a captured stack with the correct outer executing job.
    /// </summary>
    internal static readonly AsyncLocal<Guid> CurrentJobId = new();

    private static readonly ConcurrentDictionary<Guid, string> _stacks = new();

    internal static void SetStack(Guid outerJobId, string stack) => _stacks[outerJobId] = stack;

    internal static void ClearStack(Guid outerJobId) => _stacks.TryRemove(outerJobId, out _);

    internal static string? GetStack(Guid outerJobId) =>
        _stacks.TryGetValue(outerJobId, out var s) ? s : null;
}
