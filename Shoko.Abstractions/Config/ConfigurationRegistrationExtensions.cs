using System;
using Microsoft.Extensions.DependencyInjection;
using Shoko.Abstractions.Plugin;

namespace Shoko.Abstractions.Config;

/// <summary>
///   Registers configuration types a plugin keeps outside its main assembly.
/// </summary>
public static class ConfigurationRegistrationExtensions
{
    /// <summary>
    ///   Registers a configuration type from another assembly your plugin
    ///   ships, such as its abstractions package, so other plugins can load
    ///   it through <c>IConfigurationService</c>. Call it from
    ///   <c>IPluginServiceRegistration.RegisterServices</c>.
    /// </summary>
    /// <remarks>
    ///   <para>
    ///     The configuration types in your plugin's main assembly are found
    ///     without registering them, and so is the one your plugin declares
    ///     through <see cref="IPlugin{TConfiguration}"/>, wherever it is.
    ///     Registering is only needed for the others, and is optional: a
    ///     plugin may keep its configuration to itself and offer services
    ///     instead.
    ///   </para>
    ///   <para>
    ///     The type is tied to <typeparamref name="TPlugin"/>, with the ID and
    ///     the file a configuration in its main assembly would get. Its
    ///     assembly must be one the plugin loaded itself: a type from another
    ///     plugin, from a dependency's shared contracts, or from the server
    ///     fails the plugin's start.
    ///   </para>
    /// </remarks>
    /// <typeparam name="TPlugin">The plugin the configuration belongs to.</typeparam>
    /// <typeparam name="TConfig">The configuration type.</typeparam>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="services"/> is <c>null</c>.</exception>
    public static IServiceCollection AddConfiguration<TPlugin, TConfig>(this IServiceCollection services)
        where TPlugin : class, IPlugin
        where TConfig : class, IConfiguration
    {
        ArgumentNullException.ThrowIfNull(services);
        services.AddSingleton(new ConfigurationRegistration(typeof(TPlugin), typeof(TConfig)));
        return services;
    }
}
