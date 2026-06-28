using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Shoko.Abstractions.Core.Services;

/// <summary>
///   Service checking and managing updates of the server and web component.
/// </summary>
public interface ISystemUpdateService
{
    #region Server

    /// <summary>
    ///   The manifest URL for the server.
    /// </summary>
    string ServerManifestUrl { get; set; }

    /// <summary>
    ///   Checks the manifest URL specified by
    ///   <see cref="ServerManifestUrl"/> for an update of the server.
    /// </summary>
    /// <param name="channel">
    ///   Optional. The release channel to use.
    /// </param>
    /// <param name="force">
    ///   Optional. Bypass the cache and search for a new version online.
    /// </param>
    /// <returns>
    ///   The latest server version information, or <see langword="null"/> if not found.
    /// </returns>
    Task<ReleaseVersionInformation?> GetLatestServerVersion(ReleaseChannel channel = ReleaseChannel.Auto, bool force = false);

    /// <summary>
    ///   Get the full release history from the server manifest URL.
    /// </summary>
    /// <param name="channel">
    ///   Optional. Filter to a specific release channel. If not set will show
    ///   from all channel. If set to auto will show for the channel matching
    ///   the running server.
    /// </param>
    /// <param name="force">
    ///   Optional. Bypass the cache and search for a new version online.
    /// </param>
    /// <returns>
    ///   A read-only list of server release version information, sorted by
    ///   version descending.
    /// </returns>
    Task<IReadOnlyList<ReleaseVersionInformation>> GetServerHistory(ReleaseChannel? channel = null, bool force = false);

    #endregion

    #region Web Component

    /// <summary>
    ///   Dispatched when an update has been installed.
    /// </summary>
    event EventHandler? WebComponentUpdated;

    /// <summary>
    ///   The manifest URL for the web component.
    /// </summary>
    string WebComponentManifestUrl { get; set; }

    /// <summary>
    ///   Load the web component version information from the version installed
    ///   into the user data directory.
    /// </summary>
    /// <returns>The version info, or <see langword="null"/> if not found.</returns>
    WebReleaseVersionInformation? LoadWebComponentVersionInformation();

    /// <summary>
    ///   Load the web component version information from the bundled version of
    ///   the web component, if any.
    /// </summary>
    /// <returns>The version information, or <see langword="null"/> if not found.</returns>
    WebReleaseVersionInformation? LoadIncludedWebComponentVersionInformation();

    /// <summary>
    ///   Install a specific version of the web component from the manifest
    ///   history.
    /// </summary>
    /// <param name="version">
    ///   The specific version to install.
    /// </param>
    /// <returns>
    ///   <see langword="true" /> if the update was installed successfully;
    ///   otherwise, <see langword="false" />.
    /// </returns>
    Task<bool> InstallWebComponentVersion(WebReleaseVersionInformation version);

    /// <summary>
    ///   Finds and downloads the update for the web component for the selected
    ///   channel.
    /// </summary>
    /// <param name="channel">
    ///   Optional. Channel to download the update for. Defaults to
    ///   <see cref="ReleaseChannel.Auto"/> for auto selecting the web
    ///   component's channel based off of the current running server's release
    ///   channel.
    /// </param>
    /// <param name="allowIncompatible">
    ///   Optional. Allow incompatible updates of the web component.
    /// </param>
    /// <returns>
    ///   <see langword="true" /> if the update was installed successfully;
    ///   otherwise, <see langword="false" />.
    /// </returns>
    Task<bool> UpdateWebComponent(ReleaseChannel channel = ReleaseChannel.Auto, bool allowIncompatible = false);

    /// <summary>
    ///   Notify the service that a manual update of the web component has been
    ///   performed, to trigger the post-update event.
    /// </summary>
    void ReactToManualWebComponentUpdate();

    /// <summary>
    ///   Checks the manifest URL specified by
    ///   <see cref="WebComponentManifestUrl"/> for an update of the web component.
    /// </summary>
    /// <param name="channel">
    ///   Optional. The release channel to use. Defaults to
    ///   <see cref="ReleaseChannel.Auto"/> for auto selecting the web
    ///   component's channel based off of the current running server's release
    ///   channel.
    /// </param>
    /// <param name="force">
    ///   Optional. Bypass the cache and search for a new version online.
    /// </param>
    /// <param name="allowIncompatible">
    ///   Optional. Allow incompatible updates.
    /// </param>
    /// <returns>
    ///   The latest version information.
    /// </returns>
    Task<WebReleaseVersionInformation> GetLatestWebComponentVersion(ReleaseChannel channel = ReleaseChannel.Auto, bool force = false, bool allowIncompatible = false);

    /// <summary>
    ///   Get the full release history from the web component manifest URL.
    /// </summary>
    /// <param name="channel">
    ///   Optional. Filter to a specific release channel. If not set will show
    ///   from all channel. If set to auto will show for the channel matching
    ///   the running server.
    /// </param>
    /// <param name="force">
    ///   Optional. Bypass the cache and fetch a new version online.
    /// </param>
    /// <returns>
    ///   A read-only list of web component release version information, sorted by
    ///   version descending.
    /// </returns>
    Task<IReadOnlyList<WebReleaseVersionInformation>> GetWebComponentHistory(ReleaseChannel? channel = null, bool force = false);

    #endregion
}
