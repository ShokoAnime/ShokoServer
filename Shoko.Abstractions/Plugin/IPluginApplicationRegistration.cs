using Microsoft.AspNetCore.Builder;

namespace Shoko.Abstractions.Plugin;

/// <summary>
///   Interface used for application builder registration which requires a
///   parameterless constructor.
/// </summary>
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
    void RegisterServices(IApplicationBuilder application, IApplicationPaths applicationPaths);
}
