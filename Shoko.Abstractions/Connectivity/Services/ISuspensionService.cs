using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Shoko.Abstractions.Connectivity.Suspensions;
using Shoko.Abstractions.Metadata;
using Shoko.Abstractions.Plugin;

namespace Shoko.Abstractions.Connectivity.Services;

/// <summary>
///   Keeps the suspensions every <see cref="ISuspensionProvider"/> reports:
///   why a service cannot take work now, and until when.
/// </summary>
/// <remarks>
///   The queue holds back the jobs of each suspended provider's
///   <see cref="SuspensionProviderInfo.HeldProviderTypes"/>. A suspension
///   with an end is cleared by the core once it passes.
/// </remarks>
public interface ISuspensionService
{
    #region Providers

    /// <summary>
    ///   Every registered suspension provider, in plugin load order.
    /// </summary>
    /// <returns>One <see cref="SuspensionProviderInfo"/> per provider.</returns>
    IEnumerable<SuspensionProviderInfo> GetAvailableProviders();

    /// <summary>
    ///   Gets the infos of every provider belonging to a plugin.
    /// </summary>
    /// <param name="plugin">The plugin.</param>
    /// <returns>The infos, which is empty if the plugin has none.</returns>
    /// <exception cref="ArgumentNullException">
    ///   Thrown when <paramref name="plugin"/> is <c>null</c>.
    /// </exception>
    IReadOnlyList<SuspensionProviderInfo> GetProviderInfo(IPlugin plugin);

    /// <summary>
    ///   Looks up a provider by its stable ID.
    /// </summary>
    /// <param name="providerID">The provider's ID.</param>
    /// <returns>The info, or <c>null</c> if nothing goes by that ID.</returns>
    SuspensionProviderInfo? GetProviderInfo(Guid providerID);

    /// <summary>
    ///   Looks up a provider's info from the provider itself.
    /// </summary>
    /// <param name="provider">The provider.</param>
    /// <returns>The info.</returns>
    /// <exception cref="ArgumentNullException">
    ///   Thrown when <paramref name="provider"/> is <c>null</c>.
    /// </exception>
    /// <exception cref="ArgumentException">
    ///   Thrown when the provider was never registered.
    /// </exception>
    SuspensionProviderInfo GetProviderInfo(ISuspensionProvider provider);

    /// <summary>
    ///   Looks up a provider's info from its type.
    /// </summary>
    /// <typeparam name="TProvider">The provider's type.</typeparam>
    /// <returns>The info.</returns>
    /// <exception cref="ArgumentException">
    ///   Thrown when no provider of that type was registered.
    /// </exception>
    SuspensionProviderInfo GetProviderInfo<TProvider>() where TProvider : class, ISuspensionProvider;

    /// <summary>
    ///   Looks up a provider's info from its type.
    /// </summary>
    /// <param name="providerType">The provider's type.</param>
    /// <returns>The info.</returns>
    /// <exception cref="ArgumentNullException">
    ///   Thrown when <paramref name="providerType"/> is <c>null</c>.
    /// </exception>
    /// <exception cref="ArgumentException">
    ///   Thrown when no provider of that type was registered.
    /// </exception>
    SuspensionProviderInfo GetProviderInfo(Type providerType);

    #endregion

    #region Statuses

    /// <summary>
    ///   The status of every registered provider, suspended or not.
    /// </summary>
    /// <returns>One status per provider, in plugin load order.</returns>
    IReadOnlyList<SuspensionStatus> GetAll();

    /// <summary>
    ///   The status of one provider.
    /// </summary>
    /// <param name="providerID">The provider's ID.</param>
    /// <returns>The status, or <c>null</c> if nothing goes by that ID.</returns>
    SuspensionStatus? Get(Guid providerID);

    /// <summary>
    ///   The statuses of the providers holding back a registered metadata
    ///   provider of a source, enabled or not, suspended or not.
    /// </summary>
    /// <param name="source">The source.</param>
    /// <returns>The statuses, which is empty when nothing holds the source.</returns>
    /// <exception cref="ArgumentNullException">
    ///   Thrown when <paramref name="source"/> is <c>null</c>.
    /// </exception>
    IReadOnlyList<SuspensionStatus> GetForSource(MetadataSource source);

    /// <summary>
    ///   Lifts a liftable suspension for an admin: asks its provider to lift
    ///   it, then removes it.
    /// </summary>
    /// <param name="providerID">The provider's ID.</param>
    /// <param name="kind">The kind of the suspension.</param>
    /// <param name="token">Cancels the lift.</param>
    /// <returns>
    ///   <c>true</c> once lifted, <c>false</c> when the provider or the
    ///   suspension is missing or the suspension is not liftable.
    /// </returns>
    Task<bool> Lift(Guid providerID, SuspensionKind kind, CancellationToken token = default);

    /// <summary>
    ///   Raised after a provider's suspensions changed, outside any lock.
    /// </summary>
    event EventHandler<SuspensionChangedEventArgs> SuspensionChanged;

    #endregion
}
