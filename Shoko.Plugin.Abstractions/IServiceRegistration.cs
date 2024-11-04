
using Microsoft.Extensions.DependencyInjection;

namespace Shoko.Plugin.Abstractions;
/// <summary>
/// This interface is only used for service registration and requires a
/// parameterless constructor.
/// </summary>
public interface IPluginServiceRegistration
{
    /// <summary>
    /// Registers the plugin's services with the service collection.
    /// </summary>
    /// <param name="serviceCollection">The service collection.</param>
    /// <param name="applicationPaths">The application paths.</param>
    void RegisterServices(IServiceCollection serviceCollection, IApplicationPaths applicationPaths);
}
