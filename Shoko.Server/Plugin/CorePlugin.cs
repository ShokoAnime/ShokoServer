using System;
using Shoko.Abstractions.Plugin;

namespace Shoko.Server.Plugin;

/// <summary>
/// The core plugin. Responsible for allowing the core to register plugin
/// providers. You cannot disable this "plugin."
/// </summary>
public class CorePlugin : IPlugin
{
    /// <summary>
    ///   Stable UUIDv5 derived from typeof(CorePlugin).FullName
    ///   using the ShokoPluginAbstractions namespace.
    /// </summary>
    public static readonly Guid StaticID = new("75088f3f-57f8-5959-ad2a-6cacad9ac2b0");

    /// <inheritdoc/>
    public Guid ID { get => StaticID; }

    /// <inheritdoc/>
    public string Name { get; private init; } = "Shoko Core";

    public string Description { get; private init; } = """
        The core plugin. Responsible for allowing the core to register plugin
        providers. You cannot disable this "plugin."
    """;
}
