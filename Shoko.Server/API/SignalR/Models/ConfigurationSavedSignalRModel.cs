using System;
using System.Collections.Generic;
using System.Linq;
using Shoko.Abstractions.Events;

namespace Shoko.Server.API.SignalR.Models;

/// <summary>
/// Dispatched when a configuration is saved.
/// </summary>
/// <param name="eventArgs">Event arguments.</param>
public class ConfigurationSavedSignalRModel(ConfigurationSavedEventArgs eventArgs)
{
    /// <summary>
    /// The configuration id.
    /// </summary>
    public Guid ConfigurationID { get; init; } = eventArgs.ConfigurationInfo.ID;

    /// <summary>
    /// A set of paths for properties that need a restart to take effect.
    /// </summary>
    public HashSet<string> RestartPendingFor { get; init; } = eventArgs.ConfigurationInfo.RestartPendingFor.ToHashSet();
}
