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
    public static Guid StaticID = UuidUtility.GetV5(typeof(CorePlugin).FullName!);

    /// <inheritdoc/>
    public Guid ID { get => StaticID; }

    /// <inheritdoc/>
    public string Name { get; private init; } = "Shoko Core";

    public string Description { get; private init; } = """
        The core plugin. Responsible for allowing the core to register plugin
        providers. You cannot disable this "plugin."
    """;
}
