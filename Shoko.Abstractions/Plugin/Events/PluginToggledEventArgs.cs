using System;
using Shoko.Abstractions.Plugin.Models;
using Shoko.Abstractions.User;

namespace Shoko.Abstractions.Plugin.Events;

/// <summary>
///   Event dispatched when a plugin is enabled or disabled for the next
///   session onwards.
/// </summary>
public class PluginToggledEventArgs : EventArgs
{
    /// <summary>
    ///   The plugin which was just enabled or disabled, as it is after the
    ///   change. <see cref="LocalPluginInfo.IsEnabled"/> tells which of the two
    ///   it was.
    /// </summary>
    public required LocalPluginInfo Plugin { get; init; }

    /// <summary>
    ///   When the event occurred.
    /// </summary>
    public required DateTime OccurredAt { get; init; }

    /// <summary>
    ///   The API token of whoever enabled or disabled the plugin, or
    ///   <see langword="null"/> when the system did it. Stamped when the event is
    ///   raised.
    /// </summary>
    public ApiToken? Actor { get; init; }
}
