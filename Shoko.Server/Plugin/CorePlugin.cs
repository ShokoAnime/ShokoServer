using System;
using Shoko.Plugin.Abstractions;

namespace Shoko.Server.Plugin;

/// <summary>
/// The core plugin. Responsible for allowing the
/// core to register plugin providers. You cannot
/// disable this "plugin."
/// </summary>
public class CorePlugin : IPlugin
{
    public Guid ID => Guid.Parse("b6014d5b-bd38-5909-9203-7d4219676be7");

    /// <inheritdoc/>
    public string Name => "Shoko Core";

    /// <inheritdoc/>
    public void Load() { }
}
