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
    Guid ID { get => GetType().FullName!.ToUuidV5(); }

    /// <summary>
    /// Friendly name of the plugin.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Description of the plugin.
    /// </summary>
    string? Description { get => null; }
}
