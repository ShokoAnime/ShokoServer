
using System;

namespace Shoko.Plugin.Abstractions;

/// <summary>
/// Interface for plugins to register themselves automagically.
/// </summary>
public interface IPlugin
{
    /// <summary>
    /// Unique ID of the plugin.
    /// </summary>
    Guid ID { get; }

    /// <summary>
    /// Friendly name of the plugin.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Load event.
    /// </summary>
    void Load();
}
