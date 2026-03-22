using System;
using System.Threading.Tasks;

namespace Shoko.Abstractions.Core.Services;

/// <summary>
///   Service checking and managing updates of the server and web component.
/// </summary>
public interface ISystemUpdateService
{
    #region Server

    /// <summary>
    ///   The GitHub repository name for the server.
    /// </summary>
    string ServerRepositoryName { get; set; }

    /// <summary>
    ///   Checks the GitHub repository specified by
    ///   <see cref="ServerRepositoryName"/> for an update of the server.
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

    #endregion

    #region Web Component

    /// <summary>
    ///   Dispatched when an update has been installed.
    /// </summary>
    event EventHandler? WebComponentUpdated;

    /// <summary>
    ///   The GitHub repository name for the web component.
    /// </summary>
    string ClientRepositoryName { get; set; }

    /// <summary>
    ///   Load the web component version information from the installed into the
    ///   user data directory.
    /// </summary>
    /// <returns>The version info, or <see langword="null"/> if not found.</returns>
    WebReleaseVersionInformation? LoadWebComponentVersionInformation();

    /// <summary>
    ///   Load the web component version information from the bundled/included
    ///   web component.
    /// </summary>
    /// <returns>The version information, or <see langword="null"/> if not found.</returns>
    WebReleaseVersionInformation? LoadIncludedWebComponentVersionInformation();

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
    ///   Checks the GitHub repository specified by
    ///   <see cref="ServerRepositoryName"/> for an update of the web component.
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

    #endregion
}
