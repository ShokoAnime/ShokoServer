using System;

namespace Shoko.Abstractions.Connectivity.Suspensions;

/// <summary>
///   One reason a service is suspended. A provider holds at most one per
///   <see cref="SuspensionKind"/>, so the kind is its identity.
/// </summary>
public sealed record Suspension
{
    /// <summary>
    ///   Why the service is suspended.
    /// </summary>
    public required SuspensionKind Kind { get; init; }

    /// <summary>
    ///   A detail only the service knows, or <c>null</c> to let clients word
    ///   it from <see cref="Kind"/>.
    /// </summary>
    public string? Reason { get; init; }

    /// <summary>
    ///   When the suspension was first reported, in UTC. Set by the core and
    ///   kept when the same kind is reported again.
    /// </summary>
    public required DateTime RaisedAt { get; init; }

    /// <summary>
    ///   When the suspension ends, in UTC, or <c>null</c> when it lasts until
    ///   it is resumed or lifted. The core clears it once this passes.
    /// </summary>
    public DateTime? ResumesAt { get; init; }

    /// <summary>
    ///   Whether an admin may lift the suspension before it ends, through
    ///   <see cref="ISuspensionProvider.Lift"/>.
    /// </summary>
    public bool IsLiftable { get; init; }

    /// <summary>
    ///   How long until <see cref="ResumesAt"/>, counted from now.
    /// </summary>
    /// <returns>
    ///   The time left, never below zero, or <c>null</c> without a
    ///   <see cref="ResumesAt"/>.
    /// </returns>
    public TimeSpan? GetRemainingTime()
        => ResumesAt is { } resumesAt
            ? resumesAt - DateTime.UtcNow is { Ticks: > 0 } remaining ? remaining : TimeSpan.Zero
            : null;
}
