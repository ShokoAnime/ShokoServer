using System;
using Shoko.Abstractions.Plugin;
using Shoko.Abstractions.Utilities;

#nullable enable
namespace Shoko.Server.Plugin;

/// <summary>
/// The core plugin. Responsible for allowing the core to register plugin
/// providers. You cannot disable this "plugin."
/// </summary>
public class CorePlugin : IPlugin
{
    /// <inheritdoc/>
    public Guid ID { get => UuidUtility.GetV5(GetType().FullName!); }

    /// <inheritdoc/>
    public string Name { get; private init; } = "Shoko Core";

    public string Description { get; private init; } = """
        The core plugin. Responsible for allowing the core to register plugin
        providers. You cannot disable this "plugin."
    """;
}
