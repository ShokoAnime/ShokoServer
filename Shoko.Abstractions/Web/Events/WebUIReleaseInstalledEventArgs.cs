using System;
using Shoko.Abstractions.Web.Services;

namespace Shoko.Abstractions.Web.Events;

/// <summary>
///   Event arguments for the <see cref="IWebUIService.ReleaseInstalled"/>
///   event.
/// </summary>
public class WebUIReleaseInstalledEventArgs : EventArgs
{
    /// <summary>
    ///   The web UI that installed the release.
    /// </summary>
    public required WebUIProviderInfo Provider { get; init; }

    /// <summary>
    ///   The release now installed, or <c>null</c> if the web UI cannot tell.
    /// </summary>
    public required WebUIReleaseInformation? Release { get; init; }
}
