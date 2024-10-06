using System.Diagnostics.CodeAnalysis;
using Shoko.Plugin.Abstractions.Services;

namespace Shoko.Plugin.Abstractions.Extensions;

/// <summary>
/// Service extensions.
/// </summary>
public static class ServiceExtensions
{
    /// <summary>
    /// Tries to get a provider by its ID.
    /// </summary>
    /// <param name="service">The service.</param>
    /// <param name="providerID">The provider ID.</param>
    /// <param name="provider">The provider.</param>
    /// <returns></returns>
    public static bool TryGetProviderByID(this IVideoReleaseService service, string providerID, [NotNullWhen(true)] out IReleaseInfoProvider? provider)
        => (provider = service.GetProviderByID(providerID)) is not null;
}
