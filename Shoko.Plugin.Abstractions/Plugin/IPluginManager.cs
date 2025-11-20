using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Microsoft.Extensions.DependencyInjection;

namespace Shoko.Plugin.Abstractions.Plugin;

/// <summary>
///   Responsible for plugin registration and management, retrieving
///   information about registered plugins, and getting types and exports
///   from plugins.
/// </summary>
public interface IPluginManager
{
    #region Setup

    /// <summary>
    ///   Search for and register plugin related services to the service
    ///   collection.
    /// </summary>
    /// <param name="serviceCollection">
    ///   The service collection.
    /// </param>
    void RegisterPlugins(IServiceCollection serviceCollection);

    /// <summary>
    ///   Initializes the plugins.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    ///   Thrown if the plugins have already been initialized.
    /// </exception>
    void InitPlugins();

    #endregion

    #region Plugin Info

    /// <summary>
    ///   Gets information about all registered plugins.
    /// </summary>
    /// <returns>
    ///   A list of all registered <see cref="PluginInfo"/>s.
    /// </returns>
    IReadOnlyList<PluginInfo> GetPluginInfos();

    /// <summary>
    ///   Gets information about all registered versions of a plugin by its ID.
    /// </summary>
    /// <param name="pluginId">
    ///   The ID of the plugin.
    /// </param>
    /// <returns>
    ///   The <see cref="PluginInfo"/> for the plugin if registered and
    ///   available, <c>null</c> otherwise.
    /// </returns>
    IReadOnlyList<PluginInfo> GetPluginInfos(Guid pluginId);

    /// <summary>
    ///   Gets information about the currently active version or otherwise
    ///   highest version of a plugin, if registered. If
    ///   <paramref name="pluginVersion"/> is not <c>null</c>, it will attempt
    ///   to get the specific version of the plugin, if registered.
    /// </summary>
    /// <param name="pluginId">
    ///   The ID of the plugin.
    /// </param>
    /// <param name="pluginVersion">
    ///   The version of the plugin to get, if multiple versions are registered
    ///   and you want to get a specific one.
    /// </param>
    /// <returns>
    ///   The <see cref="PluginInfo"/> for the plugin if registered and
    ///   available, <c>null</c> otherwise.
    /// </returns>
    PluginInfo? GetPluginInfo(Guid pluginId, Version? pluginVersion = null);

    /// <summary>
    ///   Gets information about a registered plugin by its instance, if
    ///   registered and active.
    /// </summary>
    /// <param name="plugin">The instance of the plugin.</param>
    /// <returns>
    ///   The <see cref="PluginInfo"/> for the plugin if registered and
    ///   available, <c>null</c> otherwise.
    /// </returns>
    PluginInfo? GetPluginInfo(IPlugin plugin);

    /// <summary>
    ///   Gets information about a plugin by its type as a type parameter, if
    ///   registered and active.
    /// </summary>
    /// <typeparam name="TPlugin">
    ///   The plugin type.
    /// </typeparam>
    /// <returns>
    ///   The <see cref="PluginInfo"/> for the plugin if registered and
    ///   available, <c>null</c> otherwise.
    /// </returns>
    PluginInfo? GetPluginInfo<TPlugin>() where TPlugin : IPlugin;

    /// <summary>
    ///   Gets information about a registered plugin by its type, if registered
    ///   and active.
    /// </summary>
    /// <param name="type">
    ///   The type of the plugin.
    /// </param>
    /// <returns>
    ///   The <see cref="PluginInfo"/> for the plugin if registered and
    ///   available, <c>null</c> otherwise.
    /// </returns>
    PluginInfo? GetPluginInfo(Type type);

    /// <summary>
    ///   Gets information about a plugin by its assembly, if registered and
    ///   active.
    /// </summary>
    /// <param name="assembly">
    ///   The assembly to check.
    /// </param>
    /// <returns>
    ///   The <see cref="PluginInfo"/> for the plugin if registered and
    ///   available, <c>null</c> otherwise.
    /// </returns>
    PluginInfo? GetPluginInfo(Assembly assembly);

    #endregion

    #region Plugin Management

    /// <summary>
    ///   Loads a new plugin info from the given path.
    /// </summary>
    /// <remarks>
    ///   The path should be relative to the user plugin directory. If it lies
    ///   outside the directory, then the operation will fail.
    /// </remarks>
    /// <param name="path">
    ///   The path to the plugin. If relative, it will be first checked if it's
    ///   relative to the user plugin directory. If absolute, it will be checked
    ///   if it lies within the mentioned directory.
    /// </param>
    /// <returns>
    ///   The loaded <see cref="PluginInfo"/> for the plugin.
    /// </returns>
    PluginInfo? LoadFromPath(string path);

    /// <summary>
    ///   Enables the plugin for the next session onwards.
    /// </summary>
    /// <param name="pluginInfo">
    ///   The plugin info to enable.
    /// </param>
    /// <exception cref="InvalidOperationException">
    ///   Thrown if the plugin has been uninstalled.
    /// </exception>
    /// <returns>
    ///   The updated <see cref="PluginInfo"/> for the plugin.
    /// </returns>
    PluginInfo EnablePlugin(PluginInfo pluginInfo);

    /// <summary>
    ///   Disables the plugin for the next session onwards.
    /// </summary>
    /// <param name="pluginInfo">
    ///   The plugin info to disable.
    /// </param>
    /// <returns>
    ///   The updated <see cref="PluginInfo"/> for the plugin.
    /// </returns>
    PluginInfo DisablePlugin(PluginInfo pluginInfo);

    /// <summary>
    ///   Disables and uninstalls the plugin. The plugin will still be active in
    ///   the current session.
    /// </summary>
    /// <param name="pluginInfo">
    ///   The plugin info to uninstall.
    /// </param>
    /// <param name="purgeConfiguration">
    ///   Whether to purge the plugin's configuration.
    /// </param>
    /// <exception cref="IOException">
    ///   Thrown if the plugin failed to remove the plugin's files from the
    ///   filesystem.
    /// </exception>
    /// <returns>
    ///   The updated <see cref="PluginInfo"/> for the plugin.
    /// </returns>
    PluginInfo UninstallPlugin(PluginInfo pluginInfo, bool purgeConfiguration = true);

    #endregion

    #region Types & Exports

    /// <summary>
    ///   Gets all types assignable to <typeparamref name="T"/> from across all plugins.
    /// </summary>
    /// <typeparam name="T">
    ///   The type to check for.
    /// </typeparam>
    /// <returns>
    ///   The types.
    /// </returns>
    IEnumerable<Type> GetTypes<T>();

    /// <summary>
    ///   Gets all types assignable to <typeparamref name="T"/> from a specific plugin.
    /// </summary>
    /// <typeparam name="T">
    ///   The type to check for.
    /// </typeparam>
    /// <param name="plugin">
    ///   The plugin instance.
    /// </param>
    /// <returns>
    ///   The types.
    /// </returns>
    IEnumerable<Type> GetTypes<T>(IPlugin plugin);

    /// <summary>
    ///   Get the exported <paramref name="type"/> assigned as a
    ///   <typeparamref name="T"/>, or <c>null</c> if not found.
    /// </summary>
    /// <typeparam name="T">
    ///   The type to check for.
    /// </typeparam>
    /// <param name="type">
    ///   The type to check.
    /// </param>
    /// <returns>
    ///   The export, or <c>null</c> if <paramref name="type"/> is not assignable to <typeparamref name="T"/>.
    /// </returns>
    T? GetExport<T>(Type type);

    /// <summary>
    ///   Gets all registered services or newly created instances of types which
    ///   is assignable to <typeparamref name="T"/> from across all plugins.
    /// </summary>
    /// <typeparam name="T">
    ///   The type to check for.
    /// </typeparam>
    /// <returns>
    ///   The exports.
    /// </returns>
    IEnumerable<T> GetExports<T>();

    /// <summary>
    ///   Gets all registered services or newly created instances of types which
    ///   is assignable to <typeparamref name="T"/> from the specified plugin.
    /// </summary>
    /// <typeparam name="T">
    ///   The type to check for.
    /// </typeparam>
    /// <param name="plugin">
    ///   The plugin instance.
    /// </param>
    /// <returns>
    ///   The exports.
    /// </returns>
    IEnumerable<T> GetExports<T>(IPlugin plugin);

    #endregion
}
