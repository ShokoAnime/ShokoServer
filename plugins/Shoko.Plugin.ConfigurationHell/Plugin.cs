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
    public Guid ID { get; private set; } = typeof(Plugin).FullName!.ToUuidV5();

    /// <inheritdoc/>
    public string Name { get; private set; } = "Configuration Hell";
}
