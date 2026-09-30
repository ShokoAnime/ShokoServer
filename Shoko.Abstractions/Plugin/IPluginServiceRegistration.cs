using Microsoft.Extensions.DependencyInjection;

namespace Shoko.Abstractions.Plugin;

/// <summary>
///   Lets a plugin add its own services to the container before it is built.
/// </summary>
/// <remarks>
///   The method is static and called by reflection, so no instance of the
///   implementing type is ever created. Only the first implementation in an
///   assembly is used.
///   Touch the static class registering the plugin's own <see cref="Metadata.MetadataSource"/>
///   and <see cref="Metadata.MetadataEntityType"/> values here, before registration closes.
/// </remarks>
public interface IPluginServiceRegistration
{
    /// <summary>
    ///   Registers the plugin's services with the service collection.
    /// </summary>
    /// <param name="serviceCollection">
    ///   The service collection.
    /// </param>
    /// <param name="applicationPaths">
    ///   The application paths.
    /// </param>
    abstract static void RegisterServices(IServiceCollection serviceCollection, IApplicationPaths applicationPaths);
}
