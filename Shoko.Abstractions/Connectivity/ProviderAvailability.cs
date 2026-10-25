using System;
using System.Collections.Generic;
using Shoko.Abstractions.Connectivity.Enums;
using Shoko.Abstractions.Connectivity.Suspensions;

namespace Shoko.Abstractions.Connectivity;

/// <summary>
///   Whether a release or relocation provider can be used now, as the core
///   works it out from the admin's choice, the provider's own configuration
///   and the suspensions holding it back.
/// </summary>
public sealed record ProviderAvailability
{
    /// <summary>
    ///   The provider can be used.
    /// </summary>
    public static ProviderAvailability Available { get; } = new() { Kind = ProviderAvailabilityKind.Available };

    /// <summary>
    ///   Whether the provider can be used, and if not, what stops it.
    /// </summary>
    public required ProviderAvailabilityKind Kind { get; init; }

    /// <summary>
    ///   Why the provider cannot be used, in words a person can read, or
    ///   <c>null</c> while it can or when nothing says why.
    /// </summary>
    public string? Reason { get; init; }

    /// <summary>
    ///   When the suspensions holding the provider back end, in UTC, or
    ///   <c>null</c> when it is not suspended or one has no end.
    /// </summary>
    public DateTime? ResumesAt { get; init; }

    /// <summary>
    ///   The suspensions holding the provider back, empty unless it is
    ///   suspended.
    /// </summary>
    public IReadOnlyList<SuspensionStatus> Suspensions { get; init; } = [];

    /// <summary>
    ///   Whether the provider can be used.
    /// </summary>
    public bool IsAvailable => Kind is ProviderAvailabilityKind.Available;
}
