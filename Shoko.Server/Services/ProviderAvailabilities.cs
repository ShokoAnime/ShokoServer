using System.Collections.Generic;
using System.Linq;
using Shoko.Abstractions.Connectivity;
using Shoko.Abstractions.Connectivity.Enums;
using Shoko.Abstractions.Connectivity.Suspensions;

namespace Shoko.Server.Services;

/// <summary>
///   Works out a release or relocation provider's availability the same way
///   for both services.
/// </summary>
internal static class ProviderAvailabilities
{
    /// <summary>
    ///   The availability of a provider, whether or not an admin turned it on
    ///   anywhere: not configured, then suspended, then manual only, else
    ///   available.
    /// </summary>
    /// <param name="name">The provider's name, for the reasons.</param>
    /// <param name="isConfigured">Whether the provider says it is configured.</param>
    /// <param name="notConfiguredReason">What the provider says it is missing, if anything.</param>
    /// <param name="holding">The suspended statuses holding the provider back.</param>
    /// <param name="canAutoMatch">Whether the provider answers the core's own searches.</param>
    /// <param name="cannotAutoMatchReason">Why it does not, if it says.</param>
    /// <returns>The availability.</returns>
    public static ProviderAvailability Get(string name, bool isConfigured, string? notConfiguredReason, IReadOnlyList<SuspensionStatus> holding,
        bool canAutoMatch = true, string? cannotAutoMatchReason = null)
    {
        if (!isConfigured)
            return new() { Kind = ProviderAvailabilityKind.NotConfigured, Reason = notConfiguredReason is { Length: > 0 } reason ? reason : $"{name} is not configured." };

        if (holding.Count > 0)
        {
            var resumesAt = holding.Any(status => status.ResumesAt is null) ? null : holding.Max(status => status.ResumesAt);
            var names = string.Join(", ", holding.Select(status => status.Provider.Name).Distinct());
            return new()
            {
                Kind = ProviderAvailabilityKind.Suspended,
                Reason = $"{name} is held back while {names} is suspended.",
                ResumesAt = resumesAt,
                Suspensions = holding,
            };
        }

        if (!canAutoMatch)
        {
            return new()
            {
                Kind = ProviderAvailabilityKind.ManualOnly,
                Reason = cannotAutoMatchReason is { Length: > 0 } autoMatchReason ? autoMatchReason : $"{name} only answers searches a person starts.",
            };
        }

        return ProviderAvailability.Available;
    }
}
