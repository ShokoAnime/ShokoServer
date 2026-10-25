using System;
using Shoko.Abstractions.Core;

namespace Shoko.Abstractions.Web;

/// <summary>
///   Release information for a web UI.
/// </summary>
public class WebUIReleaseInformation : ReleaseVersionInformation
{
    /// <summary>
    ///   Minimum server version compatible with the release, if set in the
    ///   release.
    /// </summary>
    public Version? MinimumServerVersion { get; set; }

    /// <summary>
    ///   Maximum server version compatible with the release, if set in the
    ///   release.
    /// </summary>
    public Version? MaximumServerVersion { get; set; }

    /// <summary>
    ///   Indicates the release ships with the server or the plugin, so it can
    ///   be installed without a network connection.
    /// </summary>
    public bool IsBundled { get; set; }

    /// <summary>
    ///   Why the release cannot be used, or <c>null</c> if it can. The provider
    ///   sets it for reasons of its own; the server adds its own for a release
    ///   outside the server versions above.
    /// </summary>
    public string? IncompatibleReason { get; set; }
}
