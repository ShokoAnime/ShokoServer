using System;
using Shoko.Plugin.Abstractions.Config;

namespace Shoko.Plugin.Abstractions.Events;

/// <summary>
/// Dispatched when a configuration is saved.
/// </summary>
public class ConfigurationSavedEventArgs : EventArgs
{
    /// <summary>
    /// Information about the configuration that was saved.
    /// </summary>
    public required ConfigurationInfo ConfigurationInfo { get; init; }
}

/// <summary>
/// Dispatched when a configuration is saved.
/// </summary>
public class ConfigurationSavedEventArgs<TConfig> : ConfigurationSavedEventArgs where TConfig : class, IConfiguration, new()
{
    /// <summary>
    /// The configuration that was saved.
    /// </summary>
    public required TConfig Configuration { get; init; }
}
