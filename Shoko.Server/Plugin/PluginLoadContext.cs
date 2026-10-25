using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Loader;
using System.Text.Json;

namespace Shoko.Server.Plugin;

/// <summary>
///   The load context of one loaded plugin, so each plugin uses its own copy
///   of the libraries it ships, such as two plugins on two versions of the
///   same library.
/// </summary>
/// <remarks>
///   An assembly is looked for, in order:
///   <list type="number">
///     <item>with the server, which shares its own assemblies, the framework and every package it ships with all plugins, unless the plugin was built against a newer version of a package the server ships than the server has, ships that version itself, and the package is not part of the plugin contract;</item>
///     <item>among the assemblies the plugin's dependencies export (their own code, not the packages they ship), so a contract a dependency defines is one type for both;</item>
///     <item>among the plugin's own files;</item>
///     <item>among the packages its dependencies ship;</item>
///     <item>among the assemblies any other loaded plugin exports.</item>
///   </list>
/// </remarks>
internal sealed class PluginLoadContext : AssemblyLoadContext
{
    #region Fields

    /// <summary>
    ///   The assemblies the server resolves itself, by simple name.
    /// </summary>
    private static readonly Lazy<Dictionary<string, HostAssembly>> _hostAssemblies = new(ReadHostAssemblies);

    private readonly AssemblyDependencyResolver? _resolver;

    private readonly string _directory;

    private readonly Func<Guid, PluginLoadContext?> _findContext;

    private readonly Func<IEnumerable<PluginLoadContext>> _allContexts;

    private IReadOnlyList<PluginLoadContext>? _dependencies;

    #endregion

    #region Constructors

    /// <summary>
    ///   Creates the load context of a plugin.
    /// </summary>
    /// <param name="pluginID">The plugin's ID.</param>
    /// <param name="name">The plugin's name, for the context's name.</param>
    /// <param name="mainDllPath">The plugin's main DLL.</param>
    /// <param name="dependencyIDs">The IDs of the plugins it depends on, optional ones included.</param>
    /// <param name="findContext">Finds the load context of another plugin by its ID.</param>
    /// <param name="allContexts">Lists the load contexts of every loaded plugin.</param>
    public PluginLoadContext(
        Guid pluginID,
        string name,
        string mainDllPath,
        IReadOnlyList<Guid> dependencyIDs,
        Func<Guid, PluginLoadContext?> findContext,
        Func<IEnumerable<PluginLoadContext>> allContexts
    ) : base($"Plugin {name} ({pluginID})", isCollectible: false)
    {
        PluginID = pluginID;
        DependencyIDs = dependencyIDs;
        _directory = Path.GetDirectoryName(mainDllPath)!;
        _findContext = findContext;
        _allContexts = allContexts;
        var depsPath = Path.ChangeExtension(mainDllPath, ".deps.json");
        if (File.Exists(depsPath))
            _resolver = new(mainDllPath);
        ExportedAssemblies = ReadExportedAssemblies(mainDllPath, depsPath);
    }

    #endregion

    #region Properties

    /// <summary>
    ///   The plugin's ID.
    /// </summary>
    public Guid PluginID { get; }

    /// <summary>
    ///   The IDs of the plugins it depends on.
    /// </summary>
    public IReadOnlyList<Guid> DependencyIDs { get; }

    /// <summary>
    ///   The simple names of the assemblies the plugin builds itself: its main
    ///   assembly and the projects it was built with, as its
    ///   <c>.deps.json</c> names them. Plugins depending on it share these.
    /// </summary>
    public IReadOnlySet<string> ExportedAssemblies { get; }

    #endregion

    #region Resolving

    /// <inheritdoc />
    protected override Assembly? Load(AssemblyName assemblyName)
    {
        var name = assemblyName.Name;
        if (string.IsNullOrEmpty(name))
            return null;

        if (_hostAssemblies.Value.TryGetValue(name, out var host))
        {
            // A plugin built against a newer copy of a package the server ships uses its own copy,
            // as the server's cannot stand in for it; anything the contract shares stays the server's.
            if (!host.IsShared && ResolveOwn(assemblyName) is { } ownPath
                && PrefersOwnCopy(host.Version, assemblyName.Version, ReadVersion(ownPath)))
                return LoadFromAssemblyPath(ownPath);

            return null;
        }

        var dependencies = GetDependencies();
        foreach (var dependency in dependencies)
        {
            if (dependency.ExportedAssemblies.Contains(name))
                return dependency.LoadFromAssemblyName(assemblyName);
        }

        if (ResolveOwn(assemblyName) is { } path)
            return LoadFromAssemblyPath(path);

        foreach (var dependency in dependencies)
        {
            if (dependency.ResolveOwn(assemblyName) is not null)
                return dependency.LoadFromAssemblyName(assemblyName);
        }

        foreach (var context in _allContexts())
        {
            if (context != this && context.ExportedAssemblies.Contains(name))
                return context.LoadFromAssemblyName(assemblyName);
        }

        return null;
    }

    /// <inheritdoc />
    protected override nint LoadUnmanagedDll(string unmanagedDllName)
    {
        if (_resolver?.ResolveUnmanagedDllToPath(unmanagedDllName) is { } path)
            return LoadUnmanagedDllFromPath(path);

        return base.LoadUnmanagedDll(unmanagedDllName);
    }

    /// <summary>
    ///   Finds an assembly among the plugin's own files: through its
    ///   <c>.deps.json</c> when it has one, else beside its main DLL.
    /// </summary>
    /// <param name="assemblyName">The assembly.</param>
    /// <returns>The path, or <c>null</c> when the plugin does not ship it.</returns>
    private string? ResolveOwn(AssemblyName assemblyName)
    {
        if (_resolver?.ResolveAssemblyToPath(assemblyName) is { } resolved)
            return resolved;

        var probed = Path.Join(_directory, assemblyName.Name + ".dll");
        return File.Exists(probed) ? probed : null;
    }

    /// <summary>
    ///   The load contexts of the plugins this one depends on, directly or
    ///   through another, nearest first, worked out once.
    /// </summary>
    /// <returns>The contexts.</returns>
    private IReadOnlyList<PluginLoadContext> GetDependencies()
    {
        if (_dependencies is { } dependencies)
            return dependencies;

        var found = new List<PluginLoadContext>();
        var seen = new HashSet<Guid> { PluginID };
        var queue = new Queue<Guid>(DependencyIDs);
        while (queue.TryDequeue(out var id))
        {
            if (!seen.Add(id) || _findContext(id) is not { } context)
                continue;

            found.Add(context);
            foreach (var next in context.DependencyIDs)
                queue.Enqueue(next);
        }

        return _dependencies = found;
    }

    /// <summary>
    ///   Whether a plugin's own copy of a package the server also ships is
    ///   used instead of the server's: when the plugin asks for a newer
    ///   version than the server has, and its own copy is at least that new.
    /// </summary>
    /// <param name="hostVersion">The server's version, or <c>null</c> when unknown.</param>
    /// <param name="wanted">The version the plugin asks for, or <c>null</c> for any.</param>
    /// <param name="ownVersion">The version of the plugin's copy, or <c>null</c> when unknown.</param>
    /// <returns><c>true</c> to use the plugin's copy.</returns>
    internal static bool PrefersOwnCopy(Version? hostVersion, Version? wanted, Version? ownVersion)
        => hostVersion is not null && wanted is not null && ownVersion is not null && wanted > hostVersion && ownVersion >= wanted;

    #endregion

    #region Reading

    /// <summary>
    ///   Reads the assemblies the server resolves itself: its trusted
    ///   platform assemblies. A stray DLL beside the server that it does not
    ///   list is not one of them; the server could not load it. One is shared
    ///   whatever a plugin asks for when it is part of the framework, is the
    ///   server's own, or is one the plugin contract's assemblies refer to,
    ///   since a second copy of those would split their types in two.
    /// </summary>
    /// <returns>The assemblies by simple name.</returns>
    private static Dictionary<string, HostAssembly> ReadHostAssemblies()
    {
        var contract = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var assembly in new[] { typeof(Shoko.Abstractions.Plugin.IPlugin).Assembly, typeof(Shoko.QueueProcessor.Abstractions.IQueueJob).Assembly })
        {
            foreach (var reference in assembly.GetReferencedAssemblies())
            {
                if (reference.Name is { } referenceName)
                    contract.Add(referenceName);
            }
        }

        var appBase = Path.TrimEndingDirectorySeparator(AppContext.BaseDirectory) + Path.DirectorySeparatorChar;
        var assemblies = new Dictionary<string, HostAssembly>(StringComparer.OrdinalIgnoreCase);
        if (AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES") is string trusted)
        {
            foreach (var path in trusted.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
            {
                var name = Path.GetFileNameWithoutExtension(path);
                var isShared = !path.StartsWith(appBase, StringComparison.Ordinal)
                    || name.StartsWith("Shoko.", StringComparison.OrdinalIgnoreCase)
                    || contract.Contains(name);
                assemblies.TryAdd(name, new(path, isShared));
            }
        }

        return assemblies;
    }

    /// <summary>
    ///   Reads the version of an assembly file without loading it.
    /// </summary>
    /// <param name="path">The file.</param>
    /// <returns>The version, or <c>null</c> when it cannot be read.</returns>
    private static Version? ReadVersion(string path)
    {
        try
        {
            return AssemblyName.GetAssemblyName(path).Version;
        }
        catch (Exception ex) when (ex is IOException or BadImageFormatException or UnauthorizedAccessException or ArgumentException)
        {
            return null;
        }
    }

    /// <summary>
    ///   Reads the assemblies a plugin builds itself: its main assembly, and
    ///   the runtime assemblies of the projects its <c>.deps.json</c> lists.
    /// </summary>
    /// <param name="mainDllPath">The plugin's main DLL.</param>
    /// <param name="depsPath">Its <c>.deps.json</c>, which may be missing.</param>
    /// <returns>The simple names.</returns>
    internal static HashSet<string> ReadExportedAssemblies(string mainDllPath, string depsPath)
    {
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { Path.GetFileNameWithoutExtension(mainDllPath) };
        if (!File.Exists(depsPath))
            return names;

        try
        {
            using var document = JsonDocument.Parse(File.ReadAllText(depsPath));
            var root = document.RootElement;
            if (!root.TryGetProperty("libraries", out var libraries) || !root.TryGetProperty("targets", out var targets))
                return names;

            var projects = libraries.EnumerateObject()
                .Where(library => library.Value.TryGetProperty("type", out var type) && type.GetString() is "project")
                .Select(library => library.Name)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
            foreach (var target in targets.EnumerateObject())
            {
                foreach (var library in target.Value.EnumerateObject())
                {
                    if (!projects.Contains(library.Name) || !library.Value.TryGetProperty("runtime", out var runtime))
                        continue;

                    foreach (var file in runtime.EnumerateObject())
                        names.Add(Path.GetFileNameWithoutExtension(file.Name));
                }
            }
        }
        catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException)
        {
            // A plugin whose .deps.json cannot be read exports its main assembly only.
        }

        return names;
    }

    #endregion

    #region Types

    /// <summary>
    ///   An assembly the server resolves itself.
    /// </summary>
    /// <param name="Path">Its file.</param>
    /// <param name="IsShared">Whether every plugin gets the server's copy whatever version it asks for.</param>
    private sealed record HostAssembly(string Path, bool IsShared)
    {
        private readonly Lazy<Version?> _version = new(() => ReadVersion(Path));

        /// <summary>
        ///   The server's version, read once when first needed.
        /// </summary>
        public Version? Version => _version.Value;
    }

    #endregion
}
