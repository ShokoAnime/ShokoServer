using System;
using Shoko.Abstractions.Web.Services;

namespace Shoko.Abstractions.Web.Events;

/// <summary>
///   Event arguments for the
///   <see cref="IWebUIService.ActiveProviderChanged"/> event.
/// </summary>
public class WebUIActiveProviderChangedEventArgs : EventArgs
{
    /// <summary>
    ///   The web UI served now, or kept up to date while serving is off.
    /// </summary>
    public required WebUIProviderInfo Provider { get; init; }

    /// <summary>
    ///   Indicates the server serves the web UI.
    /// </summary>
    public required bool IsEnabled { get; init; }
}
