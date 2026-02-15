using Shoko.Abstractions.Enums;

namespace Shoko.Abstractions.Extensions;

/// <summary>
/// Extensions for the <see cref="NetworkAvailability"/> enum.
/// </summary>
public static class NetworkAvailabilityExtensions
{
    /// <summary>
    /// Returns true if the <paramref name="value"/> is <see cref="NetworkAvailability.Internet"/> or <see cref="NetworkAvailability.PartialInternet"/>
    /// </summary>
    /// <param name="value">Value to check.</param>
    /// <returns>True if the <paramref name="value"/> is <see cref="NetworkAvailability.Internet"/> or <see cref="NetworkAvailability.PartialInternet"/>.</returns>
    public static bool HasInternet(this NetworkAvailability value)
        => value is NetworkAvailability.Internet or NetworkAvailability.PartialInternet;
}
