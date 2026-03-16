using System;
using Shoko.Abstractions.Plugin.Models;

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
}
