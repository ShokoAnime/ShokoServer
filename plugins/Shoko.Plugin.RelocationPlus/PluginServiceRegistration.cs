using Microsoft.Extensions.DependencyInjection;
using Shoko.Plugin.Abstractions;

namespace Shoko.Plugin.RelocationPlus;

/// <inheritdoc/>
public class PluginServiceRegistration : IPluginServiceRegistration
{
    /// <inheritdoc/>
    public void RegisterServices(IServiceCollection serviceCollection, IApplicationPaths applicationPaths)
    {
        serviceCollection.AddHostedService<RelocationPlusService>();
    }
}
