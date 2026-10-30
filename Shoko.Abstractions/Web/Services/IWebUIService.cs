using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Shoko.Abstractions.Core;
using Shoko.Abstractions.Web.Enums;
using Shoko.Abstractions.Web.Events;

namespace Shoko.Abstractions.Web.Services;

/// <summary>
///   Service choosing which web UIs the server serves, by default and for
///   each host override, and keeping their releases up to date. Changes to
///   the choice apply at once.
/// </summary>
public interface IWebUIService
{
    #region Web UIs

    /// <summary>
    ///   Indicates the server serves its web UIs. Setting it saves the
    ///   service's settings and applies at once, for every host.
    /// </summary>
    bool IsEnabled { get; set; }

    /// <summary>
    ///   The web UI served by default, for every host without an override.
    ///   Never <c>null</c>, since the server always has a web UI of its own.
    ///   Its releases are still kept up to date while <see cref="IsEnabled"/>
    ///   is off.
    /// </summary>
    WebUIProviderInfo ActiveProvider { get; }

    /// <summary>
    ///   Gets the web UI served for a host: its override's web UI if it has
    ///   one that is there and did not fail, else the
    ///   <see cref="ActiveProvider"/>.
    /// </summary>
    /// <param name="host">
    ///   The host a request is for, such as <c>example.com:8111</c>, or
    ///   <c>null</c> for the default. An entry for its name and port beats one
    ///   for its name alone; names compare ignoring case.
    /// </param>
    /// <returns>
    ///   The web UI served for the host.
    /// </returns>
    WebUIProviderInfo GetActiveProvider(string? host);

    /// <summary>
    ///   Picks the web UI to serve by default, which applies at once. Without
    ///   a pick, the first plugin web UI in load order that is a
    ///   <see cref="IWebUIProvider.IsDefaultCandidate"/> is served, else the
    ///   server's own; a picked web UI that is gone or failed falls back the
    ///   same way. See <see cref="WebUIProviderInfo.IsSelected"/> and
    ///   <see cref="WebUIProviderInfo.IsActive"/>.
    /// </summary>
    /// <param name="webUI">
    ///   The web UI to serve by default, or <c>null</c> to pick one
    ///   automatically.
    /// </param>
    void SelectProvider(WebUIProviderInfo? webUI);

    /// <summary>
    ///   The web UIs served for some hosts instead of the
    ///   <see cref="ActiveProvider"/>, as the admin saved them.
    /// </summary>
    IReadOnlyList<WebUIHostOverride> HostOverrides { get; }

    /// <summary>
    ///   Replaces the host overrides, which applies at once. An override whose
    ///   web UI is gone or failed later serves the default web UI instead.
    /// </summary>
    /// <param name="overrides">
    ///   The new host overrides, each a well-formed host with an optional port
    ///   from 1 to 65535, listed once, and the ID of an available web UI.
    /// </param>
    /// <exception cref="ArgumentException">
    ///   An override is malformed, listed twice or names no available web UI.
    /// </exception>
    void SetHostOverrides(IReadOnlyList<WebUIHostOverride> overrides);

    /// <summary>
    ///   Dispatched once for each host whose served web UI changed: another
    ///   one became active, serving was turned on or off, or its mount path
    ///   changed.
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
    ///   Dispatched after a web UI in use installed a release.
    /// </summary>
    event EventHandler<WebUIReleaseInstalledEventArgs>? ReleaseInstalled;

    /// <summary>
    ///   Gets a web UI's releases, newest first, with
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
    /// <param name="webUI">
    ///   Optional. The web UI, or <c>null</c> for the <see cref="ActiveProvider"/>.
    /// </param>
    /// <param name="cancellationToken">
    ///   Optional. A cancellation token for cancelling the lookup.
    /// </param>
    /// <returns>
    ///   The releases, newest first.
    /// </returns>
    Task<IReadOnlyList<WebUIReleaseInformation>> GetReleases(
        ReleaseChannel? channel = null,
        bool force = false,
        WebUIProviderInfo? webUI = null,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    ///   Gets the newest release of a web UI on a channel, with the
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
    /// <param name="webUI">
    ///   Optional. The web UI, or <c>null</c> for the <see cref="ActiveProvider"/>.
    /// </param>
    /// <param name="cancellationToken">
    ///   Optional. A cancellation token for cancelling the lookup.
    /// </param>
    /// <returns>
    ///   The newest release, or <c>null</c> if the channel has none to use.
    /// </returns>
    Task<WebUIReleaseInformation?> GetLatestRelease(
        ReleaseChannel channel = ReleaseChannel.Auto,
        bool force = false,
        bool allowIncompatible = false,
        WebUIProviderInfo? webUI = null,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    ///   Installs a release of a web UI.
    /// </summary>
    /// <param name="release">
    ///   The release to install, from <see cref="GetReleases"/> for the same
    ///   web UI.
    /// </param>
    /// <param name="webUI">
    ///   Optional. The web UI, or <c>null</c> for the <see cref="ActiveProvider"/>.
    /// </param>
    /// <param name="cancellationToken">
    ///   Optional. A cancellation token for cancelling the install.
    /// </param>
    /// <returns>
    ///   Whether the release was installed, needs a restart to take effect, or
    ///   failed.
    /// </returns>
    Task<WebUIInstallResult> InstallRelease(WebUIReleaseInformation release, WebUIProviderInfo? webUI = null, CancellationToken cancellationToken = default);

    /// <summary>
    ///   Installs the newest release of a web UI on a channel.
    /// </summary>
    /// <param name="channel">
    ///   Optional. The release channel. <see cref="ReleaseChannel.Auto"/> picks
    ///   the installed release's channel, else the server's.
    /// </param>
    /// <param name="allowIncompatible">
    ///   Optional. Consider releases this server cannot use.
    /// </param>
    /// <param name="webUI">
    ///   Optional. The web UI, or <c>null</c> for the <see cref="ActiveProvider"/>.
    /// </param>
    /// <param name="cancellationToken">
    ///   Optional. A cancellation token for cancelling the install.
    /// </param>
    /// <returns>
    ///   Whether the release was installed, needs a restart to take effect, or
    ///   failed, which includes a channel without a release to use.
    /// </returns>
    Task<WebUIInstallResult> Update(
        ReleaseChannel channel = ReleaseChannel.Auto,
        bool allowIncompatible = false,
        WebUIProviderInfo? webUI = null,
        CancellationToken cancellationToken = default
    );

    #endregion
}
