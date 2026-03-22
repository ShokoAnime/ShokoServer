using System;

namespace Shoko.Abstractions.Core;

/// <summary>
///   Release information for the Web UI.
/// </summary>
public class WebReleaseVersionInformation : ReleaseVersionInformation
{
    /// <summary>
    ///   Minimum server version compatible with the Web UI, if set in the
    ///   release.
    /// </summary>
    public Version? MinimumServerVersion { get; set; }
}
