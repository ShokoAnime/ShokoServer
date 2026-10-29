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
    ///   The web UI served for the <see cref="Host"/> now, or kept up to date
    ///   while serving is off.
    /// </summary>
    public required WebUIProviderInfo Provider { get; init; }

    /// <summary>
    ///   The host override whose web UI changed, normalized to a lower case
    ///   host name with <c>:port</c> when it has one, or <c>null</c> for the
    ///   default web UI, which serves every host without an override.
    /// </summary>
    public required string? Host { get; init; }

    /// <summary>
    ///   Indicates the server serves the web UI.
    /// </summary>
    public required bool IsEnabled { get; init; }
}
