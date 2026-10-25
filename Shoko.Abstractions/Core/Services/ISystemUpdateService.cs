using System.Collections.Generic;
using System.Threading.Tasks;

namespace Shoko.Abstractions.Core.Services;

/// <summary>
///   Service checking for updates of the server.
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
    ///   The latest server version information, or <c>null</c> if not found.
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
}
