using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Shoko.Abstractions.Plugin.Events;
using Shoko.Abstractions.Plugin.Models;

namespace Shoko.Abstractions.Plugin;

/// <summary>
///   Responsible for plugin registration and management, retrieving
///   information about registered plugins, and getting types and exports
///   from plugins.
/// </summary>
public interface IPluginManager
{
    #region Compatibility

    /// <summary>
    ///   Gets the current plugin abstraction ABI in use.
    /// </summary>
    Version AbstractionVersion { get; }

    /// <summary>
    ///   Gets the current runtime identifier for the platform in use.
    /// </summary>
    string RuntimeIdentifier { get; }

    /// <summary>
    ///   The runtime identifier for universal platform support.
    /// </summary>
    const string AnyRuntimeIdentifier = "any";

    /// <summary>
    ///   Checks if a plugin ABI and runtime ID is compatible with the current system.
    /// </summary>
    /// <param name="abstractionVersion">
    ///   The plugin ABI version.
    /// </param>
    /// <param name="runtimeIdentifier">
    ///   The plugin runtime identifier.
    /// </param>
    /// <returns>
    ///   <c>true</c> if compatible, <c>false</c> otherwise.
    /// </returns>
    bool IsAbiAndRuntimeCompatible(Version abstractionVersion, string runtimeIdentifier);

    #endregion

    #region Plugin Info

    /// <summary>
    ///   Gets information about all registered plugins.
    /// </summary>
    /// <returns>
    ///   A list of all registered <see cref="LocalPluginInfo"/>s.
    /// </returns>
    IReadOnlyList<LocalPluginInfo> GetPluginInfos();

    /// <summary>
    ///   Gets information about all registered versions of a plugin by its ID.
    /// </summary>
    /// <param name="pluginId">
    ///   The ID of the plugin.
    /// </param>
    /// <returns>
    ///   The <see cref="LocalPluginInfo"/> for the plugin if registered and
    ///   available, <c>null</c> otherwise.
    /// </returns>
    IReadOnlyList<LocalPluginInfo> GetPluginInfos(Guid pluginId);

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
    ///   The <see cref="LocalPluginInfo"/> for the plugin if registered and
    ///   available, <c>null</c> otherwise.
    /// </returns>
    LocalPluginInfo? GetPluginInfo(Guid pluginId, Version? pluginVersion = null);

    /// <summary>
    ///   Gets information about a registered plugin by its instance, if
    ///   registered and active.
    /// </summary>
    /// <param name="plugin">The instance of the plugin.</param>
    /// <returns>
    ///   The <see cref="LocalPluginInfo"/> for the plugin if registered and
    ///   available, <c>null</c> otherwise.
    /// </returns>
    LocalPluginInfo? GetPluginInfo(IPlugin plugin);

    /// <summary>
    ///   Gets information about a plugin by its type as a type parameter, if
    ///   registered and active.
    /// </summary>
    /// <typeparam name="TPlugin">
    ///   The plugin type.
    /// </typeparam>
    /// <returns>
    ///   The <see cref="LocalPluginInfo"/> for the plugin if registered and
    ///   available, <c>null</c> otherwise.
    /// </returns>
    LocalPluginInfo? GetPluginInfo<TPlugin>() where TPlugin : IPlugin;

    /// <summary>
    ///   Gets information about a registered plugin by its type, if registered
    ///   and active.
    /// </summary>
    /// <param name="type">
    ///   The type of the plugin.
    /// </param>
    /// <returns>
    ///   The <see cref="LocalPluginInfo"/> for the plugin if registered and
    ///   available, <c>null</c> otherwise.
    /// </returns>
    LocalPluginInfo? GetPluginInfo(Type type);

    /// <summary>
    ///   Gets information about a plugin by its assembly, if registered and
    ///   active.
    /// </summary>
    /// <param name="assembly">
    ///   The assembly to check.
    /// </param>
    /// <returns>
    ///   The <see cref="LocalPluginInfo"/> for the plugin if registered and
    ///   available, <c>null</c> otherwise.
    /// </returns>
    LocalPluginInfo? GetPluginInfo(Assembly assembly);

    #endregion

    #region Plugin Management

    /// <summary>
    ///   Dispatched when a local plugin is installed to the local system.
    /// </summary>
    event EventHandler<PluginInstallationEventArgs>? PluginInstalled;

    /// <summary>
    ///   Dispatched when a local plugin is uninstalled from the local system.
    /// </summary>
    event EventHandler<PluginInstallationEventArgs>? PluginUninstalled;

    /// <summary>
    ///   Dispatched when <see cref="EnablePlugin"/> enabled a plugin that was
    ///   not enabled before. Only raised when the state changed.
    /// </summary>
    event EventHandler<PluginToggledEventArgs>? PluginEnabled;

    /// <summary>
    ///   Dispatched when <see cref="DisablePlugin"/> disabled a plugin that was
    ///   enabled before, and for every other version of a plugin that
    ///   <see cref="EnablePlugin"/> disabled to enable the one asked for, ahead
    ///   of its <see cref="PluginEnabled"/>. Only raised when the state changed.
    /// </summary>
    event EventHandler<PluginToggledEventArgs>? PluginDisabled;

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
    ///   The loaded <see cref="LocalPluginInfo"/> for the plugin.
    /// </returns>
    LocalPluginInfo? LoadFromPath(string path);

    /// <summary>
    ///   Enables the plugin for the next session onwards. Will pin the plugin
    ///   version if it's not the latest version.
    /// </summary>
    /// <param name="pluginInfo">
    ///   The plugin info to enable.
    /// </param>
    /// <exception cref="InvalidOperationException">
    ///   Thrown if the plugin has been uninstalled.
    /// </exception>
    /// <returns>
    ///   The updated <see cref="LocalPluginInfo"/> for the plugin.
    /// </returns>
    LocalPluginInfo EnablePlugin(LocalPluginInfo pluginInfo);

    /// <summary>
    ///   Disables the plugin for the next session onwards. Will unpin the
    ///   plugin version if it's pinned.
    /// </summary>
    /// <param name="pluginInfo">
    ///   The plugin info to disable.
    /// </param>
    /// <returns>
    ///   The updated <see cref="LocalPluginInfo"/> for the plugin.
    /// </returns>
    LocalPluginInfo DisablePlugin(LocalPluginInfo pluginInfo);

    /// <summary>
    ///   Pins the current plugin version.
    /// </summary>
    /// <param name="pluginInfo">
    ///   The plugin info to pin.
    /// </param>
    /// <returns>
    ///   The updated <see cref="LocalPluginInfo"/> for the plugin.
    /// </returns>
    LocalPluginInfo PinPlugin(LocalPluginInfo pluginInfo);

    /// <summary>
    ///   Unpins the current plugin version.
    /// </summary>
    /// <param name="pluginInfo">
    ///   The plugin info to unpin.
    /// </param>
    /// <returns>
    ///   The updated <see cref="LocalPluginInfo"/> for the plugin.
    /// </returns>
    LocalPluginInfo UnpinPlugin(LocalPluginInfo pluginInfo);

    /// <summary>
    ///   Keeps the plugin version, protecting it from
    ///   <see cref="IPluginPackageManager.PurgeInactivePluginVersions"/> by
    ///   writing its <c>.keep</c> file. Does nothing for a version that cannot
    ///   be uninstalled.
    /// </summary>
    /// <param name="pluginInfo">
    ///   The plugin info to keep.
    /// </param>
    /// <returns>
    ///   The updated <see cref="LocalPluginInfo"/> for the plugin.
    /// </returns>
    LocalPluginInfo KeepPlugin(LocalPluginInfo pluginInfo);

    /// <summary>
    ///   Stops keeping the plugin version, deleting its <c>.keep</c> file, so
    ///   <see cref="IPluginPackageManager.PurgeInactivePluginVersions"/> may
    ///   remove it again.
    /// </summary>
    /// <param name="pluginInfo">
    ///   The plugin info to stop keeping.
    /// </param>
    /// <returns>
    ///   The updated <see cref="LocalPluginInfo"/> for the plugin.
    /// </returns>
    LocalPluginInfo UnkeepPlugin(LocalPluginInfo pluginInfo);

    /// <summary>
    ///   Marks the plugin version as installed as a dependency, by writing its
    ///   <c>.dependency</c> file. Once every installed version is marked, the
    ///   plugin loses an enabled state of its own and follows its dependents.
    /// </summary>
    /// <param name="pluginInfo">
    ///   The plugin info to mark.
    /// </param>
    /// <returns>
    ///   The updated <see cref="LocalPluginInfo"/> for the plugin.
    /// </returns>
    LocalPluginInfo MarkAsDependency(LocalPluginInfo pluginInfo);

    /// <summary>
    ///   Marks the plugin version as installed on purpose, by removing its
    ///   <c>.dependency</c> file, which makes the whole plugin one installed on
    ///   purpose. A plugin enabled on demand keeps the state it has now.
    /// </summary>
    /// <param name="pluginInfo">
    ///   The plugin info to unmark.
    /// </param>
    /// <returns>
    ///   The updated <see cref="LocalPluginInfo"/> for the plugin.
    /// </returns>
    LocalPluginInfo UnmarkAsDependency(LocalPluginInfo pluginInfo);

    /// <summary>
    ///   Checks if a plugin is only installed as a dependency: every installed
    ///   version of it is marked with <see cref="LocalPluginInfo.IsDependency"/>.
    /// </summary>
    /// <param name="pluginID">
    ///   The plugin ID.
    /// </param>
    /// <returns>
    ///   <c>true</c> if at least one version is installed and every installed
    ///   version is marked.
    /// </returns>
    bool IsInstalledAsDependency(Guid pluginID);

    /// <summary>
    ///   Gets the installed versions of every orphaned plugin: one installed
    ///   as a dependency that no plugin installed on purpose needs, directly
    ///   or through other dependencies, required or optional.
    /// </summary>
    /// <returns>
    ///   The installed versions of the orphaned plugins.
    /// </returns>
    IReadOnlyList<LocalPluginInfo> GetOrphanedPlugins();

    /// <summary>
    ///   Uninstalls the orphaned plugins, leaving their pinned and kept
    ///   versions alone. See <see cref="GetOrphanedPlugins"/>.
    /// </summary>
    /// <param name="pluginIDs">
    ///   Optional. Only the orphans among these plugins. All of them when
    ///   <c>null</c>.
    /// </param>
    /// <returns>
    ///   The plugin versions uninstalled.
    /// </returns>
    IReadOnlyList<LocalPluginInfo> UninstallOrphanedPlugins(IReadOnlySet<Guid>? pluginIDs = null);

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
    /// <param name="purgeData">
    ///   Whether to also remove the plugin's databases and cache, the folders
    ///   <see cref="PluginPaths{TPlugin}.DatabasePath"/> and
    ///   <see cref="PluginPaths{TPlugin}.CachePath"/>, along with its
    ///   configuration. They are removed on the next start, and only when no
    ///   other installed version of the plugin is left to use them.
    /// </param>
    /// <exception cref="IOException">
    ///   Thrown if the plugin failed to remove the plugin's files from the
    ///   filesystem.
    /// </exception>
    /// <returns>
    ///   The updated <see cref="LocalPluginInfo"/> for the plugin.
    /// </returns>
    LocalPluginInfo UninstallPlugin(LocalPluginInfo pluginInfo, bool purgeConfiguration = false, bool purgeData = false);

    /// <summary>
    ///   Works out what uninstalling a plugin would do, without doing it: the
    ///   versions uninstalled, the dependents uninstalled when it is forced,
    ///   the orphans uninstalled with it and the ones kept, the plugins enabled
    ///   on demand that are disabled, the ones that stop loading, and which of
    ///   its dependencies stay and why. It follows what uninstalling through
    ///   <see cref="UninstallPlugin"/> and then
    ///   <see cref="UninstallOrphanedPlugins"/> for the new orphans does.
    /// </summary>
    /// <param name="pluginInfo">
    ///   The plugin version to uninstall.
    /// </param>
    /// <param name="allVersions">
    ///   Uninstall every installed version of the plugin, not only this one.
    /// </param>
    /// <param name="keepOrphans">
    ///   Keep the plugins orphaned by the uninstall, rather than uninstalling
    ///   them with it.
    /// </param>
    /// <returns>
    ///   The plan.
    /// </returns>
    PluginChangePlan PlanUninstall(LocalPluginInfo pluginInfo, bool allVersions = false, bool keepOrphans = false);

    /// <summary>
    ///   Works out what disabling a plugin would do, without doing it: whether
    ///   it is disabled at all (a plugin enabled on demand stays enabled while
    ///   something needs it), the plugins enabled on demand that are disabled
    ///   with it, the ones that stop loading, and which of its dependencies
    ///   stay enabled and why. It follows what <see cref="DisablePlugin"/>
    ///   does.
    /// </summary>
    /// <param name="pluginInfo">
    ///   The plugin to disable.
    /// </param>
    /// <param name="keepDependencies">
    ///   Plan as if the plugin's direct dependencies were first marked as
    ///   installed on purpose with <see cref="UnmarkAsDependency"/>, so they
    ///   keep the state they have rather than following the plugin.
    /// </param>
    /// <returns>
    ///   The plan.
    /// </returns>
    PluginChangePlan PlanDisable(LocalPluginInfo pluginInfo, bool keepDependencies = false);

    #endregion

    #region Types & Exports

    /// <summary>
    ///   Gets all types assignable to <typeparamref name="T"/> from across all plugins.
    ///   Only closed, non-abstract classes are included.
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
    ///   Only closed, non-abstract classes are included.
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
    ///   Abstract classes and open generic types are skipped.
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
    ///   Abstract classes and open generic types are skipped.
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

    /// <summary>
    ///   Gets a service if it's available from the registered services, or
    ///   <c>null</c> if the service is not available.
    /// </summary>
    /// <param name="type">
    ///   The type to check for.
    /// </param>
    /// <returns>
    ///   The service if it's available, otherwise <c>null</c>.
    /// </returns>
    object? GetService(Type type);

    /// <summary>
    ///   Gets a service if it's available from the registered services, or
    ///   <c>null</c> if the service is not available.
    /// </summary>
    /// <typeparam name="T">
    ///   The type to check for.
    /// </typeparam>
    /// <returns>
    ///   The service if it's available, otherwise <c>null</c>.
    /// </returns>
    T? GetService<T>();

    /// <summary>
    ///   Gets a required service from the registered services, or throws an
    ///   exception if the service is not available.
    /// </summary>
    /// <param name="type">
    ///   The type to check for.
    /// </param>
    /// <exception cref="InvalidOperationException">
    ///   Thrown if the service is not available.
    /// </exception>
    /// <returns>
    ///   The required service.
    /// </returns>
    object GetRequiredService(Type type);

    /// <summary>
    ///   Gets a required service from the registered services, or throws an
    ///   exception if the service is not available.
    /// </summary>
    /// <typeparam name="T">
    ///   The type to check for.
    /// </typeparam>
    /// <exception cref="InvalidOperationException">
    ///   Thrown if the service is not available.
    /// </exception>
    /// <returns>
    ///   The required service.
    /// </returns>
    T GetRequiredService<T>() where T : notnull;

    #endregion
}
