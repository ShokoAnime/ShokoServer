using System;
using Shoko.Plugin.Abstractions;

namespace Shoko.Plugin.ConfigurationHell;

/// <summary>
/// Plugin made with the sole purpose of testing the configuration system's UI
/// generation capabilities.
/// </summary>
public class Plugin : IPlugin
{
    /// <inheritdoc/>
    public string Name { get; private init; } = "Configuration Hell";

    /// <inheritdoc/>
    public string Description { get; private init; } = """
        Plugin made with the sole purpose of testing the configuration system's
        UI generation capabilities.
    """;
}
