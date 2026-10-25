using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using Shoko.Abstractions.Connectivity;
using Shoko.Abstractions.Connectivity.Enums;

namespace Shoko.Server.API.v3.Models.Suspension;

/// <summary>
/// Whether a release or relocation provider can be used now, and if not,
/// what stops it.
/// </summary>
public class ProviderAvailabilityStatus
{
    /// <summary>
    /// Describes a provider's availability.
    /// </summary>
    /// <param name="availability">The availability.</param>
    /// <exception cref="ArgumentNullException"><paramref name="availability"/> is <c>null</c>.</exception>
    public ProviderAvailabilityStatus(ProviderAvailability availability)
    {
        ArgumentNullException.ThrowIfNull(availability);

        Kind = availability.Kind;
        Reason = availability.Reason;
        ResumesAt = availability.ResumesAt;
        SuspendedBy = [.. availability.Suspensions.Select(status => new SuspensionProviderStatus(status))];
    }

    /// <summary>
    /// Whether the provider can be used, and if not, what stops it: an admin
    /// turned it off, it is not configured, or a suspension holds it back.
    /// </summary>
    [Required]
    public ProviderAvailabilityKind Kind { get; init; }

    /// <summary>
    /// Why the provider cannot be used, or <c>null</c> while it can.
    /// </summary>
    public string? Reason { get; init; }

    /// <summary>
    /// When the suspensions holding the provider back end, or <c>null</c>
    /// when it is not suspended or one has no end.
    /// </summary>
    public DateTime? ResumesAt { get; init; }

    /// <summary>
    /// The suspended providers holding it back, empty unless it is suspended.
    /// </summary>
    [Required]
    public IReadOnlyList<SuspensionProviderStatus> SuspendedBy { get; init; }
}
