
using Microsoft.Extensions.DependencyInjection;
using Shoko.Plugin.Abstractions;

namespace Shoko.Plugin.RelocationPlus;

public class PluginServiceRegistration : IPluginServiceRegistration
{
    public void RegisterServices(IServiceCollection serviceCollection, IApplicationPaths applicationPaths)
    {
        serviceCollection.AddHostedService<RelocationPlusService>();
    }
}
