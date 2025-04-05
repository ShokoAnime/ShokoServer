using Microsoft.Extensions.DependencyInjection;
using Shoko.Plugin.Abstractions;

namespace Shoko.Plugin.ReleaseExporter;

/// <inheritdoc />
public class PluginServiceRegistration : IPluginServiceRegistration
{
    /// <inheritdoc />
    public void RegisterServices(IServiceCollection serviceCollection, IApplicationPaths applicationPaths)
    {
        serviceCollection.AddHostedService<ReleaseExporter>();
    }
}
