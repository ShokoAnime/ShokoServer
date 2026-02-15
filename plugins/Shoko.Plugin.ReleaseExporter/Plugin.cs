using System;
using Microsoft.Extensions.DependencyInjection;
using Shoko.Abstractions.Plugin;
using Shoko.Abstractions.Utilities;

namespace Shoko.Plugin.ReleaseExporter;

/// <summary>
/// Plugin responsible for importing releases to and exporting releases from the
/// file system near the video files.
/// </summary>
public class Plugin : IPlugin
{
    /// <inheritdoc/>
    public Guid ID { get => UuidUtility.GetV5(GetType().FullName!); }

    /// <inheritdoc/>
    public string Name { get; private set; } = "Release Importer/Exporter";

    /// <inheritdoc/>
    public string Description { get; private set; } = """
        Responsible for importing releases to and exporting releases from the
        file system near the video files.
    """;
}

/// <inheritdoc />
public class PluginServiceRegistration : IPluginServiceRegistration
{
    /// <inheritdoc />
    public void RegisterServices(IServiceCollection serviceCollection, IApplicationPaths applicationPaths)
    {
        serviceCollection.AddHostedService<ReleaseExporter>();
    }
}
