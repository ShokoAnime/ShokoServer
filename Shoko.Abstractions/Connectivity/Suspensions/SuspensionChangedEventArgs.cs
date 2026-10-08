using System;
using System.Collections.Generic;
using Shoko.Abstractions.User;

namespace Shoko.Abstractions.Connectivity.Suspensions;

/// <summary>
///   What changed in a provider's suspensions.
/// </summary>
public sealed class SuspensionChangedEventArgs : EventArgs
{
    /// <summary>
    ///   The status before the change.
    /// </summary>
    public required SuspensionStatus Previous { get; init; }

    /// <summary>
    ///   The status after the change.
    /// </summary>
    public required SuspensionStatus Current { get; init; }

    /// <summary>
    ///   The suspensions that began.
    /// </summary>
    public required IReadOnlyList<Suspension> Raised { get; init; }

    /// <summary>
    ///   The suspensions reported again with a new reason, end or liftability.
    /// </summary>
    public required IReadOnlyList<Suspension> Updated { get; init; }

    /// <summary>
    ///   The suspensions that ended, and how.
    /// </summary>
    public required IReadOnlyList<SuspensionRemoval> Removed { get; init; }

    /// <summary>
    ///   The API token of the admin who lifted a suspension, or <c>null</c>
    ///   when the provider or the system made the change.
    /// </summary>
    public ApiToken? Actor { get; init; }
}
