using System;
using System.Collections.Generic;

namespace Shoko.Abstractions.Connectivity.Suspensions;

/// <summary>
///   The suspensions a provider holds now. Filled by the core.
/// </summary>
public sealed record SuspensionStatus
{
    /// <summary>
    ///   The provider.
    /// </summary>
    public required SuspensionProviderInfo Provider { get; init; }

    /// <summary>
    ///   The suspensions, at most one per kind. Empty while the service runs.
    /// </summary>
    public required IReadOnlyList<Suspension> Suspensions { get; init; }

    /// <summary>
    ///   Whether any suspension is on.
    /// </summary>
    public required bool IsSuspended { get; init; }

    /// <summary>
    ///   When the last suspension ends, in UTC, or <c>null</c> when one has no
    ///   end or there are none.
    /// </summary>
    public DateTime? ResumesAt { get; init; }
}
