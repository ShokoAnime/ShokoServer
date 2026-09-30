using System;
using System.Collections.Generic;
using Shoko.Abstractions.User;

namespace Shoko.Abstractions.Config.Events;

/// <summary>
/// Dispatched when a configuration is saved.
/// </summary>
public class ConfigurationSavedEventArgs : EventArgs
{
    /// <summary>
    /// Information about the configuration that was saved.
    /// </summary>
    public required ConfigurationInfo ConfigurationInfo { get; init; }

    /// <summary>
    ///   The JSON paths whose stored values the save changed, added or
    ///   removed, such as <c>Web.Port</c> or <c>Import.Exclude[2]</c>. Paths
    ///   only, never the values, so secrets stay out of it. Every stored path
    ///   when nothing was stored before; empty when the event was not raised
    ///   for a save.
    /// </summary>
    public IReadOnlyList<string> ChangedPaths { get; init; } = [];

    /// <summary>
    ///   The API token of whoever saved the configuration, or
    ///   <see langword="null"/> when the system did it. Stamped when the event
    ///   is raised.
    /// </summary>
    public ApiToken? Actor { get; init; }
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
