using System;
using System.IO;

namespace Shoko.Abstractions.Plugin;

/// <summary>
/// The directories one plugin uses, with its own configuration, database and
/// cache folders kept apart from every other plugin's. Inject
/// <c>PluginPaths&lt;MyPlugin&gt;</c> into any class; the server registers it
/// for every plugin type.
/// </summary>
/// <remarks>
/// The properties share their names with <see cref="IApplicationPaths"/>.
/// <see cref="ConfigurationsPath"/>, <see cref="DatabasePath"/> and
/// <see cref="CachePath"/> are the plugin's own folders, named by its ID, and
/// are created the first time they are read. The rest are the server's.
/// There is no <c>PluginsPath</c>: <see cref="InstallPath"/> is the plugin's
/// own install folder, which the plugin only reads.
/// </remarks>
/// <typeparam name="TPlugin">The plugin.</typeparam>
public class PluginPaths<TPlugin> where TPlugin : class, IPlugin
{
    private readonly IApplicationPaths _applicationPaths;

    private readonly Lazy<string> _configurationsPath;

    private readonly Lazy<string> _databasePath;

    private readonly Lazy<string> _cachePath;

    /// <summary>
    /// Creates the paths for <typeparamref name="TPlugin"/>, as the server
    /// does when it is injected.
    /// </summary>
    /// <param name="applicationPaths">The server's directories.</param>
    /// <param name="pluginManager">The plugin manager, asked for the plugin's ID and install folder.</param>
    /// <exception cref="InvalidOperationException"><typeparamref name="TPlugin"/> is not a loaded plugin.</exception>
    public PluginPaths(IApplicationPaths applicationPaths, IPluginManager pluginManager)
        : this(applicationPaths, pluginManager.GetPluginInfo<TPlugin>() is { } pluginInfo
            ? (pluginInfo.ID, PluginPathRules.GetInstallPath(pluginInfo))
            : throw new InvalidOperationException($"{typeof(TPlugin).FullName} is not a loaded plugin, so it has no paths of its own."))
    { }

    /// <summary>
    /// Creates the paths for a plugin with a known ID and install folder.
    /// </summary>
    /// <param name="applicationPaths">The server's directories.</param>
    /// <param name="plugin">The plugin's ID and install folder.</param>
    internal PluginPaths(IApplicationPaths applicationPaths, (Guid ID, string InstallPath) plugin)
    {
        _applicationPaths = applicationPaths;
        PluginID = plugin.ID;
        InstallPath = plugin.InstallPath;
        _configurationsPath = new(() => Created(PluginPathRules.GetConfigurationsPath(applicationPaths, PluginID)));
        _databasePath = new(() => Created(PluginPathRules.GetDatabasePath(applicationPaths, PluginID)));
        _cachePath = new(() => Created(PluginPathRules.GetCachePath(applicationPaths, PluginID)));
    }

    #region Plugin

    /// <summary>
    /// The plugin's ID, which names its own folders.
    /// </summary>
    public Guid PluginID { get; }

    /// <summary>
    /// The folder the plugin is installed in, or the folder holding its dll
    /// when it is a single file. Read-only by convention: an update replaces
    /// it, so nothing the plugin writes goes here.
    /// </summary>
    public string InstallPath { get; }

    #endregion

    #region Own Folders

    /// <summary>
    /// The plugin's configuration folder, <c>configuration/&lt;plugin-id&gt;</c>,
    /// where the server keeps its configuration files.
    /// </summary>
    public string ConfigurationsPath => _configurationsPath.Value;

    /// <summary>
    /// The plugin's database folder, <c>data/&lt;plugin-id&gt;</c>, for
    /// anything that cannot be fetched again: databases and other state.
    /// Included in the server's backups.
    /// </summary>
    public string DatabasePath => _databasePath.Value;

    /// <summary>
    /// The plugin's cache folder, <c>cache/&lt;plugin-id&gt;</c>, for anything
    /// that can be fetched or built again. The user may empty it at any time,
    /// and backups leave it out.
    /// </summary>
    public string CachePath => _cachePath.Value;

    /// <summary>
    /// A file in the plugin's database folder. Its folder is created, the
    /// file is not.
    /// </summary>
    /// <param name="relativePath">The file's path, relative to <see cref="DatabasePath"/>.</param>
    /// <returns>The file's full path.</returns>
    /// <exception cref="ArgumentException">The path is empty, rooted, or leaves the folder.</exception>
    public string GetDatabaseFile(string relativePath)
        => CreatedParent(PluginPathRules.ResolveInside(DatabasePath, relativePath));

    /// <summary>
    /// A file in the plugin's cache folder. Its folder is created, the file
    /// is not.
    /// </summary>
    /// <param name="relativePath">The file's path, relative to <see cref="CachePath"/>.</param>
    /// <returns>The file's full path.</returns>
    /// <exception cref="ArgumentException">The path is empty, rooted, or leaves the folder.</exception>
    public string GetCacheFile(string relativePath)
        => CreatedParent(PluginPathRules.ResolveInside(CachePath, relativePath));

    #endregion

    #region Server Folders

    /// <summary>
    /// The server's executable folder.
    /// </summary>
    public string ApplicationPath => _applicationPaths.ApplicationPath;

    /// <summary>
    /// The server's data folder, the root of everything writable.
    /// </summary>
    public string DataPath => _applicationPaths.DataPath;

    /// <summary>
    /// The Web UI's folder.
    /// </summary>
    public string WebPath => _applicationPaths.WebPath;

    /// <summary>
    /// The server's image folder.
    /// </summary>
    public string ImagesPath => _applicationPaths.ImagesPath;

    /// <summary>
    /// The server's transcode and rendition cache folder.
    /// </summary>
    public string StreamCachePath => _applicationPaths.StreamCachePath;

    /// <summary>
    /// The Web UI themes folder.
    /// </summary>
    public string ThemesPath => _applicationPaths.ThemesPath;

    /// <summary>
    /// The server's log folder.
    /// </summary>
    public string LogsPath => _applicationPaths.LogsPath;

    #endregion

    private static string Created(string directory)
    {
        Directory.CreateDirectory(directory);
        return directory;
    }

    private static string CreatedParent(string file)
    {
        if (Path.GetDirectoryName(file) is { Length: > 0 } directory)
            Directory.CreateDirectory(directory);
        return file;
    }
}
