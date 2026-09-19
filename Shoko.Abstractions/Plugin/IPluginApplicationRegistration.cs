using Microsoft.AspNetCore.Builder;

namespace Shoko.Abstractions.Plugin;

/// <summary>
///   Lets a plugin add its own middleware to the request pipeline.
/// </summary>
/// <remarks>
///   The method is static and called by reflection, so no instance of the
///   implementing type is ever created. Only the first implementation in an
///   assembly is used.
/// </remarks>
public interface IPluginApplicationRegistration
{
    /// <summary>
    ///   Registers the plugin's services with the application builder.
    /// </summary>
    /// <param name="application">
    ///   The application builder.
    /// </param>
    /// <param name="applicationPaths">
    ///   The application paths.
    /// </param>
    abstract static void RegisterServices(IApplicationBuilder application, IApplicationPaths applicationPaths);
}
