using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.FileProviders;
using Shoko.Abstractions.Config;
using Shoko.Abstractions.Web.Enums;

namespace Shoko.Abstractions.Web;

/// <summary>
///   A web UI the server can serve, such as a web UI. The server serves
///   one at a time, the active web UI, from its <see cref="MountPath"/>,
///   asking it for every file through <see cref="IFileProvider"/>.
/// </summary>
public interface IWebUIProvider : IFileProvider
{
    /// <summary>
    ///   Friendly name of the web UI.
    /// </summary>
    string Name { get; }

    /// <summary>
    ///   Optional. Description of the web UI.
    /// </summary>
    string? Description { get => null; }

    /// <summary>
    ///   Version of the web UI provider.
    /// </summary>
    Version Version { get => GetType().Assembly.GetName().Version ?? new Version(0, 0, 0, 0); }

    /// <summary>
    ///   Optional. Whether this web UI may become the default one on its own,
    ///   when the admin has not picked one: the first in load order that opts
    ///   in is served, and without any, the Shoko Web UI. Opting in says the
    ///   web UI is willing to be the only one on a brand-new server, so it
    ///   takes on the first-run setup from the release that ships it. A web UI
    ///   meant for one purpose, such as a kiosk view or an admin panel, leaves
    ///   it <c>false</c>, and is served only when picked or for a host override.
    ///   Read whenever the server works out which web UI to serve (at start,
    ///   when the selection, the host overrides or the server settings are
    ///   saved, or when a web UI's <see cref="MountPath"/> changes), not on
    ///   every request as <see cref="MountPath"/> is.
    /// </summary>
    bool IsDefaultCandidate { get => false; }

    /// <summary>
    ///   The path the web UI is served from, such as <c>/webui</c>, or
    ///   <c>/</c> for the root. It may not be under <c>/api</c>, <c>/plugin</c>,
    ///   <c>/signalr</c> or the Swagger UI's path.
    /// </summary>
    string MountPath { get; }

    /// <summary>
    ///   Templates for the web UI's pages that other applications may link to,
    ///   relative to the <see cref="MountPath"/>. A type missing from the
    ///   dictionary has no page in this web UI.
    /// </summary>
    IReadOnlyDictionary<WebUILinkType, string> Links { get; }

    /// <summary>
    ///   The installed release of the web UI's files, or <c>null</c> if no
    ///   usable release is installed.
    /// </summary>
    WebUIReleaseInformation? CurrentRelease { get; }

    /// <summary>
    ///   The release that ships with the server or the plugin, or <c>null</c>
    ///   if none ships. Read when the server starts, so it must not need the
    ///   network.
    /// </summary>
    WebUIReleaseInformation? BundledRelease { get; }

    /// <summary>
    ///   Dispatched after a release has been installed.
    /// </summary>
    event EventHandler? ReleaseInstalled;

    /// <summary>
    ///   Gets every release the web UI can install, the bundled one included,
    ///   newest first.
    /// </summary>
    /// <param name="force">
    ///   Optional. Bypass any cache and look for releases online.
    /// </param>
    /// <param name="cancellationToken">
    ///   Optional. A cancellation token for cancelling the lookup.
    /// </param>
    /// <returns>
    ///   The releases, newest first.
    /// </returns>
    Task<IReadOnlyList<WebUIReleaseInformation>> GetReleases(bool force = false, CancellationToken cancellationToken = default);

    /// <summary>
    ///   Installs a release from <see cref="GetReleases"/>, replacing the one
    ///   installed. Raises <see cref="ReleaseInstalled"/> once it is in place.
    /// </summary>
    /// <param name="release">
    ///   The release to install.
    /// </param>
    /// <param name="cancellationToken">
    ///   Optional. A cancellation token for cancelling the install.
    /// </param>
    /// <returns>
    ///   Whether the release was installed, needs a restart to take effect, or
    ///   failed.
    /// </returns>
    Task<WebUIInstallResult> InstallRelease(WebUIReleaseInformation release, CancellationToken cancellationToken = default);
}

/// <summary>
///   Indicates that the web UI provider supports configuration, and which
///   configuration type to display in the UI.
/// </summary>
/// <typeparam name="TConfiguration">
///   The web UI provider configuration type.
/// </typeparam>
public interface IWebUIProvider<TConfiguration> : IWebUIProvider where TConfiguration : IWebUIProviderConfiguration { }
