using System;
using System.ComponentModel.DataAnnotations;
using Shoko.Abstractions.Plugin.Events;
using Shoko.Abstractions.Plugin.Models;

namespace Shoko.Server.API.SignalR.Models;

/// <summary>
///   SignalR model for plugin lifecycle events (install, uninstall, enable,
///   disable).
/// </summary>
public class PluginEventSignalRModel
{
    public PluginEventSignalRModel(PluginInstallationEventArgs eventArgs)
    {
        PluginID = eventArgs.Plugin.ID;
        Name = eventArgs.Plugin.Name;
        Version = eventArgs.Plugin.Version.Version;
        IsEnabled = eventArgs.Plugin.IsEnabled;
        IsPinned = eventArgs.Plugin.IsPinned;
        OccurredAt = eventArgs.OccurredAt;
    }

    /// <summary>
    ///   Creates a model for a state-change event (enable/disable, pin/unpin).
    /// </summary>
    public PluginEventSignalRModel(LocalPluginInfo plugin, DateTime occurredAt)
    {
        PluginID = plugin.ID;
        Name = plugin.Name;
        Version = plugin.Version.Version;
        IsEnabled = plugin.IsEnabled;
        IsPinned = plugin.IsPinned;
        OccurredAt = occurredAt;
    }

    /// <summary>
    ///   The unique identifier of the plugin.
    /// </summary>
    [Required]
    public Guid PluginID { get; }

    /// <summary>
    ///   The name of the plugin.
    /// </summary>
    [Required]
    public string Name { get; }

    /// <summary>
    ///   The version of the plugin.
    /// </summary>
    [Required]
    public Version Version { get; }

    /// <summary>
    ///   Whether the plugin is currently enabled.
    /// </summary>
    [Required]
    public bool IsEnabled { get; }

    /// <summary>
    ///   Whether the plugin is currently pinned.
    /// </summary>
    [Required]
    public bool IsPinned { get; }

    /// <summary>
    ///   When the event occurred.
    /// </summary>
    [Required]
    public DateTime OccurredAt { get; }
}
