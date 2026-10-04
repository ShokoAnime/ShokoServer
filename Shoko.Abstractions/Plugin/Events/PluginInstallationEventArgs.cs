using System;
using Shoko.Abstractions.Plugin.Models;
using Shoko.Abstractions.User;

namespace Shoko.Abstractions.Plugin.Events;

/// <summary>
///   Event dispatched when a plugin is installed or uninstalled.
/// </summary>
public class PluginInstallationEventArgs : EventArgs
{
    /// <summary>
    ///   The plugin which was just installed or uninstalled.
    /// </summary>
    public required LocalPluginInfo Plugin { get; init; }

    /// <summary>
    ///   When the event occurred.
    /// </summary>
    public required DateTime OccurredAt { get; init; }

    /// <summary>
    ///   The API token of whoever installed or uninstalled the plugin, or
    ///   <c>null</c> when the system did it. Stamped when the event is
    ///   raised.
    /// </summary>
    public ApiToken? Actor { get; init; }
}
