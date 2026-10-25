using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Loader;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Shoko.Abstractions.Core.Services;
using Shoko.Abstractions.Extensions;
using Shoko.Abstractions.Plugin;
using Shoko.Abstractions.Plugin.Models;
using Shoko.Server.Server;

#pragma warning disable CS0618
namespace Shoko.Server.Plugin;

/// <summary>
///   Looking up the loaded plugins, and the types, exports and services
///   they offer.
/// </summary>
public partial class PluginManager
{
    #region Plugin Info

    public IReadOnlyList<LocalPluginInfo> GetPluginInfos()
        => _pluginTypes;

    public IReadOnlyList<LocalPluginInfo> GetPluginInfos(Guid pluginId)
        => _pluginTypes.Where(p => p.ID == pluginId).ToList();

    public LocalPluginInfo? GetPluginInfo(Guid pluginId, Version? pluginVersion = null)
        => pluginVersion is not null
            ? _pluginTypes.FirstOrDefault(p => p.ID == pluginId && p.Version.Version == pluginVersion)
            : _pluginTypes.FirstOrDefault(p => p.ID == pluginId && p.IsActive) ?? _pluginTypes.Where(p => p.ID == pluginId).OrderByDescending(p => p.Version).FirstOrDefault();

    public LocalPluginInfo? GetPluginInfo(IPlugin plugin)
        => _pluginTypes.FirstOrDefault(p => p.Plugin is not null && ReferenceEquals(plugin, p.Plugin));

    public LocalPluginInfo? GetPluginInfo<TPlugin>() where TPlugin : IPlugin
        => _pluginTypes.FirstOrDefault(p => typeof(TPlugin) == p.PluginType);

    public LocalPluginInfo? GetPluginInfo(Type type)
        => _pluginTypes.FirstOrDefault(p => type == p.PluginType);

    public LocalPluginInfo? GetPluginInfo(Assembly assembly)
        => assembly.GetTypes().Where(type => typeof(IPlugin).IsAssignableFrom(type)).FirstOrDefault() is { } pluginType
            ? GetPluginInfo(pluginType)
            : null;

    /// <summary>
    ///   Finds the active plugin whose code the given assembly is part of:
    ///   the core plugin for the server's own assembly, and otherwise the
    ///   plugin loaded from where the assembly was loaded from.
    /// </summary>
    /// <remarks>
    ///   Plugins are loaded into the default load context, next to the server
    ///   and every shared library, so an assembly there belongs to the plugin
    ///   whose main assembly it is, or whose directory or DLLs it was loaded
    ///   from. The answer is kept per assembly once plugins are loaded. An
    ///   assembly in any other load context belongs to the plugin loaded into
    ///   that context.
    /// </remarks>
    /// <param name="assembly">The assembly to look up.</param>
    /// <returns>
    ///   The owning plugin, or <c>null</c> for shared assemblies:
    ///   the runtime, the framework and the abstractions wherever they were
    ///   loaded from, and any library loaded from outside the plugins.
    /// </returns>
    internal LocalPluginInfo? GetOwningPluginInfo(Assembly assembly)
    {
        if (assembly == typeof(CorePlugin).Assembly)
            return GetPluginInfo(CorePlugin.StaticID);

        var loadContext = AssemblyLoadContext.GetLoadContext(assembly);
        if (loadContext is null)
            return null;

        var pluginInfos = _pluginTypes.ToArray();
        if (loadContext != AssemblyLoadContext.Default)
            return pluginInfos.FirstOrDefault(pluginInfo => pluginInfo is { IsActive: true, PluginType: { } pluginType }
                && AssemblyLoadContext.GetLoadContext(pluginType.Assembly) == loadContext);

        var pluginId = _pluginsLoaded
            ? _assemblyOwners.GetOrAdd(assembly, static (assembly, pluginInfos) => FindLoadedPlugin(assembly, pluginInfos)?.ID, pluginInfos)
            : FindLoadedPlugin(assembly, pluginInfos)?.ID;
        return pluginId is { } id
            ? pluginInfos.FirstOrDefault(pluginInfo => pluginInfo.IsActive && pluginInfo.ID == id)
            : null;
    }

    /// <summary>
    ///   Finds the loaded plugin, other than the core plugin, that an
    ///   assembly in the default load context belongs to.
    /// </summary>
    /// <param name="assembly">The assembly to look up.</param>
    /// <param name="pluginInfos">The known plugins, loaded or not.</param>
    /// <returns>
    ///   The plugin whose main assembly it is, or whose directory or DLLs it
    ///   was loaded from, or <c>null</c> for an assembly passed
    ///   owned by no plugin (see <see cref="IsPassThrough"/>) or loaded from elsewhere.
    /// </returns>
    private static LocalPluginInfo? FindLoadedPlugin(Assembly assembly, IReadOnlyList<LocalPluginInfo> pluginInfos)
    {
        if (IsPassThrough(assembly))
            return null;

        var loaded = pluginInfos
            .Where(pluginInfo => pluginInfo.PluginType is not null && pluginInfo.ID != CorePlugin.StaticID)
            .ToList();
        return loaded.FirstOrDefault(pluginInfo => pluginInfo.PluginType!.Assembly == assembly)
            ?? FindPluginByLocation(assembly.Location, loaded);
    }

    /// <summary>
    ///   Finds the plugin a file was loaded from: the plugin whose directory
    ///   holds it, or, for a plugin without one, the plugin listing it among
    ///   its DLLs.
    /// </summary>
    /// <param name="location">The path the assembly was loaded from. Empty for an assembly loaded from memory.</param>
    /// <param name="pluginInfos">The plugins to look in.</param>
    /// <returns>The plugin, or <c>null</c> if none holds the file.</returns>
    internal static LocalPluginInfo? FindPluginByLocation(string location, IEnumerable<LocalPluginInfo> pluginInfos)
    {
        if (string.IsNullOrEmpty(location))
            return null;

        var comparison = OperatingSystem.IsWindows() || OperatingSystem.IsMacOS() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        var path = Path.GetFullPath(location);
        foreach (var pluginInfo in pluginInfos)
        {
            if (pluginInfo.ContainingDirectory is { Length: > 0 } directory)
            {
                var directoryPath = Path.TrimEndingDirectorySeparator(Path.GetFullPath(directory)) + Path.DirectorySeparatorChar;
                if (path.StartsWith(directoryPath, comparison))
                    return pluginInfo;
            }

            if (pluginInfo.DLLs.Any(dll => !string.IsNullOrEmpty(dll) && string.Equals(Path.GetFullPath(dll), path, comparison)))
                return pluginInfo;
        }

        return null;
    }

    /// <summary>
    ///   Tells whether an assembly is shared code no plugin owns: the runtime,
    ///   the framework or the abstractions, wherever it was loaded from.
    /// </summary>
    /// <param name="assembly">The assembly.</param>
    /// <returns><c>true</c> if no plugin owns it.</returns>
    internal static bool IsPassThrough(Assembly assembly)
    {
        if (assembly == typeof(IPlugin).Assembly)
            return true;

        var name = assembly.GetName().Name ?? string.Empty;
        return name is "System" or "mscorlib" or "netstandard"
            || name.StartsWith("System.", StringComparison.Ordinal)
            || name.StartsWith("Microsoft.", StringComparison.Ordinal);
    }

    #endregion

    #region Types & Exports

    public IEnumerable<Type> GetTypes<T>()
        => _exportedTypes.Where(IsConcreteTypeOf<T>);

    public IEnumerable<Type> GetTypes<T>(IPlugin plugin)
        => GetPluginInfo(plugin) is { IsActive: true } pluginInfo
            ? pluginInfo.Types.Where(IsConcreteTypeOf<T>)
            : [];

    /// <summary>
    ///   Checks if <paramref name="type"/> is a closed, non-abstract class assignable to
    ///   <typeparamref name="T"/>, so an instance of it can be created.
    /// </summary>
    /// <typeparam name="T">
    ///   The type to check for.
    /// </typeparam>
    /// <param name="type">
    ///   The type to check.
    /// </param>
    /// <returns>
    ///   <c>true</c> if <paramref name="type"/> can be created as a <typeparamref name="T"/>.
    /// </returns>
    private static bool IsConcreteTypeOf<T>(Type type)
        => type is { IsClass: true, IsAbstract: false, ContainsGenericParameters: false } && typeof(T).IsAssignableFrom(type);

    public T? GetExport<T>(Type type)
        => !typeof(T).IsAssignableFrom(type) ? default : typeof(T).IsValueType ? (T?)Activator.CreateInstance(type) : (T?)ActivatorUtilities.GetServiceOrCreateInstance(ISystemService.StaticServices, type);

    public IEnumerable<T> GetExports<T>()
        => GetTypes<T>()
            .Select(t =>
            {
                try
                {
                    return ActivatorUtilities.GetServiceOrCreateInstance(ISystemService.StaticServices, t);
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "Unable to initialize instance of type {TypeName}", t.FullName);
                    return null;
                }
            })
            .WhereNotNull()
            .Cast<T>();

    public IEnumerable<T> GetExports<T>(IPlugin plugin)
        => GetTypes<T>(plugin)
            .Select(t =>
            {
                try
                {
                    return ActivatorUtilities.GetServiceOrCreateInstance(ISystemService.StaticServices, t);
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "Unable to initialize instance of type {TypeName}", t.FullName);
                    return null;
                }
            })
            .WhereNotNull()
            .Cast<T>();

    public object? GetService(Type type)
        => ISystemService.StaticServices.GetService(type);

    public T? GetService<T>()
        => ISystemService.StaticServices.GetService<T>();

    public object GetRequiredService(Type type)
        => ISystemService.StaticServices.GetRequiredService(type);

    public T GetRequiredService<T>() where T : notnull
        => ISystemService.StaticServices.GetRequiredService<T>();

    #endregion
}
