using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Shoko.Abstractions.Core;
using Shoko.Abstractions.Web.Enums;
using Shoko.Abstractions.Web.Events;

namespace Shoko.Abstractions.Web.Services;

/// <summary>
///   Service choosing which web UI the server serves, and keeping its
///   releases up to date. Changes to the choice apply at once.
/// </summary>
public interface IWebUIService
{
    #region Web UIs

    /// <summary>
    ///   Indicates the server serves the <see cref="ActiveProvider"/>. Setting
    ///   it saves the service's settings and applies at once.
    /// </summary>
    bool IsEnabled { get; set; }

    /// <summary>
    ///   The web UI served and updated now. Never <c>null</c>, since the
    ///   server always has a web UI of its own. It is still the one whose
    ///   releases are kept up to date while <see cref="IsEnabled"/> is off.
    /// </summary>
    WebUIProviderInfo ActiveProvider { get; }

    /// <summary>
    ///   Picks the web UI to serve, which applies at once. Without a pick, the
    ///   first plugin web UI in load order is served, else the server's own; a
    ///   picked web UI that is gone or failed falls back the same way. See
    ///   <see cref="WebUIProviderInfo.IsSelected"/> and
    ///   <see cref="WebUIProviderInfo.IsActive"/>.
    /// </summary>
    /// <param name="webUI">
    ///   The web UI to serve, or <c>null</c> to pick one automatically.
    /// </param>
    void SelectProvider(WebUIProviderInfo? webUI);

    /// <summary>
    ///   Dispatched after the served web UI changed: another one became
    ///   active, serving was turned on or off, or its mount path changed.
    /// </summary>
    event EventHandler<WebUIActiveProviderChangedEventArgs>? ActiveProviderChanged;

    /// <summary>
    ///   Gets every available web UI, the server's own first.
    /// </summary>
    /// <returns>
    ///   The available web UIs.
    /// </returns>
    IReadOnlyList<WebUIProviderInfo> GetAvailableProviders();

    /// <summary>
    ///   Gets the <see cref="WebUIProviderInfo"/> for the specified ID.
    /// </summary>
    /// <param name="providerID">
    ///   The ID of the web UI.
    /// </param>
    /// <returns>
    ///   The web UI info, or <c>null</c> if none could be found.
    /// </returns>
    WebUIProviderInfo? GetProviderInfo(Guid providerID);

    /// <summary>
    ///   Gets the <see cref="WebUIProviderInfo"/> for the provider.
    /// </summary>
    /// <param name="provider">
    ///   The provider.
    /// </param>
    /// <exception cref="ArgumentException">
    ///   The provider is not registered with the service.
    /// </exception>
    /// <returns>
    ///   The web UI info.
    /// </returns>
    WebUIProviderInfo GetProviderInfo(IWebUIProvider provider);

    /// <summary>
    ///   Gets the <see cref="WebUIProviderInfo"/> for the specified type.
    /// </summary>
    /// <typeparam name="TProvider">
    ///   The provider type.
    /// </typeparam>
    /// <exception cref="ArgumentException">
    ///   The provider is not registered with the service.
    /// </exception>
    /// <returns>
    ///   The web UI info.
    /// </returns>
    WebUIProviderInfo GetProviderInfo<TProvider>() where TProvider : class, IWebUIProvider;

    #endregion

    #region Releases

    /// <summary>
    ///   Dispatched after the active web UI installed a release.
    /// </summary>
    event EventHandler<WebUIReleaseInstalledEventArgs>? ReleaseInstalled;

    /// <summary>
    ///   Gets the active web UI's releases, newest first, with
    ///   <see cref="WebUIReleaseInformation.IncompatibleReason"/> set for
    ///   the ones this server cannot use.
    /// </summary>
    /// <param name="channel">
    ///   Optional. Filter to a release channel. <see cref="ReleaseChannel.Auto"/>
    ///   picks the installed release's channel, else the server's.
    /// </param>
    /// <param name="force">
    ///   Optional. Bypass any cache and look for releases online.
    /// </param>
    /// <param name="cancellationToken">
    ///   Optional. A cancellation token for cancelling the lookup.
    /// </param>
    /// <returns>
    ///   The releases, newest first.
    /// </returns>
    Task<IReadOnlyList<WebUIReleaseInformation>> GetReleases(ReleaseChannel? channel = null, bool force = false, CancellationToken cancellationToken = default);

    /// <summary>
    ///   Gets the newest release of the active web UI on a channel, with the
    ///   notes of every release since the installed one.
    /// </summary>
    /// <param name="channel">
    ///   Optional. The release channel. <see cref="ReleaseChannel.Auto"/> picks
    ///   the installed release's channel, else the server's.
    /// </param>
    /// <param name="force">
    ///   Optional. Bypass any cache and look for releases online.
    /// </param>
    /// <param name="allowIncompatible">
    ///   Optional. Consider releases this server cannot use.
    /// </param>
    /// <param name="cancellationToken">
    ///   Optional. A cancellation token for cancelling the lookup.
    /// </param>
    /// <returns>
    ///   The newest release, or <c>null</c> if the channel has none to use.
    /// </returns>
    Task<WebUIReleaseInformation?> GetLatestRelease(ReleaseChannel channel = ReleaseChannel.Auto, bool force = false, bool allowIncompatible = false, CancellationToken cancellationToken = default);

    /// <summary>
    ///   Installs a release of the active web UI.
    /// </summary>
    /// <param name="release">
    ///   The release to install, from <see cref="GetReleases"/>.
    /// </param>
    /// <param name="cancellationToken">
    ///   Optional. A cancellation token for cancelling the install.
    /// </param>
    /// <returns>
    ///   Whether the release was installed, needs a restart to take effect, or
    ///   failed.
    /// </returns>
    Task<WebUIInstallResult> InstallRelease(WebUIReleaseInformation release, CancellationToken cancellationToken = default);

    /// <summary>
    ///   Installs the newest release of the active web UI on a channel.
    /// </summary>
    /// <param name="channel">
    ///   Optional. The release channel. <see cref="ReleaseChannel.Auto"/> picks
    ///   the installed release's channel, else the server's.
    /// </param>
    /// <param name="allowIncompatible">
    ///   Optional. Consider releases this server cannot use.
    /// </param>
    /// <param name="cancellationToken">
    ///   Optional. A cancellation token for cancelling the install.
    /// </param>
    /// <returns>
    ///   Whether the release was installed, needs a restart to take effect, or
    ///   failed, which includes a channel without a release to use.
    /// </returns>
    Task<WebUIInstallResult> Update(ReleaseChannel channel = ReleaseChannel.Auto, bool allowIncompatible = false, CancellationToken cancellationToken = default);

    #endregion
}
