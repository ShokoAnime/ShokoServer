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
    ///   The availability of a provider: off when disabled, then not
    ///   configured, then suspended, else available.
    /// </summary>
    /// <param name="name">The provider's name, for the reasons.</param>
    /// <param name="isEnabled">Whether an admin left the provider on.</param>
    /// <param name="isConfigured">Whether the provider says it is configured.</param>
    /// <param name="notConfiguredReason">What the provider says it is missing, if anything.</param>
    /// <param name="holding">The suspended statuses holding the provider back.</param>
    /// <returns>The availability.</returns>
    public static ProviderAvailability Get(string name, bool isEnabled, bool isConfigured, string? notConfiguredReason, IReadOnlyList<SuspensionStatus> holding)
    {
        if (!isEnabled)
            return new() { Kind = ProviderAvailabilityKind.Disabled, Reason = $"{name} is disabled." };

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

        return ProviderAvailability.Available;
    }
}
