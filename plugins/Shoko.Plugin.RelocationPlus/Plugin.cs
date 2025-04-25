using System;
using Microsoft.Extensions.DependencyInjection;
using Shoko.Plugin.Abstractions;

namespace Shoko.Plugin.RelocationPlus;

/// <summary>
/// Responsible for relocating video extra files near the video files.
/// </summary>
public class Plugin : IPlugin
{
    /// <inheritdoc/>
    public Guid ID { get; private set; } = typeof(Plugin).FullName!.ToUuidV5();

    /// <inheritdoc/>
    public string Name { get; private set; } = "Relocation+";

    /// <inheritdoc/>
    public string Description { get; private set; } = """
        Responsible for relocating video extra files near the video files.
    """;
}

/// <inheritdoc/>
public class PluginServiceRegistration : IPluginServiceRegistration
{
    /// <inheritdoc/>
    public void RegisterServices(IServiceCollection serviceCollection, IApplicationPaths applicationPaths)
    {
        serviceCollection.AddHostedService<RelocationPlusService>();
    }
}
