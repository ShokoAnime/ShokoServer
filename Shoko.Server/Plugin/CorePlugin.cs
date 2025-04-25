using System;
using Shoko.Plugin.Abstractions;

#nullable enable
namespace Shoko.Server.Plugin;

/// <summary>
/// The core plugin. Responsible for allowing the
/// core to register plugin providers. You cannot
/// disable this "plugin."
/// </summary>
public class CorePlugin : IPlugin
{
    /// <inheritdoc/>
    public Guid ID { get; private set; } = typeof(CorePlugin).FullName!.ToUuidV5();

    /// <inheritdoc/>
    public string Name { get; private set; } = "Shoko Core";
}
