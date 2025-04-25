using System;
using Shoko.Plugin.Abstractions;

#nullable enable
namespace Shoko.Server.Plugin;

/// <summary>
/// The core plugin. Responsible for allowing the core to register plugin
/// providers. You cannot disable this "plugin."
/// </summary>
public class CorePlugin : IPlugin
{
    /// <inheritdoc/>
    public string Name { get; private init; } = "Shoko Core";

    public string Description { get; private init; } = """
        The core plugin. Responsible for allowing the core to register plugin
        providers. You cannot disable this "plugin."
    """;
}
