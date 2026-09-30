using System;
using System.IO;
using System.Text.RegularExpressions;
using Shoko.Abstractions.Plugin.Models;

namespace Shoko.Abstractions.Plugin;

/// <summary>
/// Where each plugin's files go, for <see cref="PluginPaths{TPlugin}"/> and
/// for the core code that has to find them for any plugin, such as backups
/// and uninstalling.
/// </summary>
internal static partial class PluginPathRules
{
    #region Folders

    /// <summary>
    /// The plugin's own configuration folder.
    /// </summary>
    /// <param name="paths">The application paths.</param>
    /// <param name="pluginID">The plugin's ID.</param>
    /// <returns>The folder, <c>configuration/&lt;plugin-id&gt;</c>.</returns>
    internal static string GetConfigurationsPath(IApplicationPaths paths, Guid pluginID)
        => Path.Join(paths.ConfigurationsPath, pluginID.ToString());

    /// <summary>
    /// The plugin's own database folder.
    /// </summary>
    /// <param name="paths">The application paths.</param>
    /// <param name="pluginID">The plugin's ID.</param>
    /// <returns>The folder, <c>data/&lt;plugin-id&gt;</c>.</returns>
    internal static string GetDatabasePath(IApplicationPaths paths, Guid pluginID)
        => Path.Join(paths.DatabasePath, pluginID.ToString());

    /// <summary>
    /// The plugin's own cache folder.
    /// </summary>
    /// <param name="paths">The application paths.</param>
    /// <param name="pluginID">The plugin's ID.</param>
    /// <returns>The folder, <c>cache/&lt;plugin-id&gt;</c>.</returns>
    internal static string GetCachePath(IApplicationPaths paths, Guid pluginID)
        => Path.Join(paths.CachePath, pluginID.ToString());

    /// <summary>
    /// The folder the plugin is installed in: its own folder, or the folder
    /// holding its dll when it is installed as a single file.
    /// </summary>
    /// <param name="pluginInfo">The plugin.</param>
    /// <returns>The folder.</returns>
    internal static string GetInstallPath(LocalPluginInfo pluginInfo)
        => pluginInfo.ContainingDirectory is { Length: > 0 } directory
            ? directory
            : Path.GetDirectoryName(pluginInfo.DLLs[0]) ?? string.Empty;

    #endregion

    #region Files

    /// <summary>
    /// The file a plugin database with the given name is kept in.
    /// </summary>
    /// <param name="databasePath">The plugin's database folder.</param>
    /// <param name="name">The database name.</param>
    /// <returns>The file, <c>&lt;name&gt;.db3</c> in the folder.</returns>
    /// <exception cref="ArgumentException"><paramref name="name"/> is not a valid database name.</exception>
    internal static string GetDatabaseFile(string databasePath, string name)
    {
        ValidateDatabaseName(name);
        return Path.Join(databasePath, name + DatabaseExtension);
    }

    /// <summary>
    /// The extension plugin databases are kept with.
    /// </summary>
    internal const string DatabaseExtension = ".db3";

    /// <summary>
    /// Checks a plugin database name: a letter or digit, then letters,
    /// digits, dashes, underscores and dots, up to 64 in all.
    /// </summary>
    /// <param name="name">The name.</param>
    /// <exception cref="ArgumentException">The name is not valid.</exception>
    internal static void ValidateDatabaseName(string name)
    {
        if (string.IsNullOrEmpty(name) || !DatabaseNameRegex().IsMatch(name))
            throw new ArgumentException($"\"{name}\" is not a valid plugin database name. Use a letter or digit, then letters, digits, '-', '_' or '.', up to 64 in all.", nameof(name));
    }

    /// <summary>
    /// Resolves a file inside one of the plugin's folders, refusing a path
    /// that would leave it.
    /// </summary>
    /// <param name="folder">The plugin's folder.</param>
    /// <param name="relativePath">The file's path, relative to the folder.</param>
    /// <returns>The file's full path.</returns>
    /// <exception cref="ArgumentException">The path is empty, rooted, or leaves the folder.</exception>
    internal static string ResolveInside(string folder, string relativePath)
    {
        if (string.IsNullOrWhiteSpace(relativePath) || Path.IsPathRooted(relativePath))
            throw new ArgumentException($"\"{relativePath}\" is not a relative file path.", nameof(relativePath));

        var root = Path.GetFullPath(folder);
        var full = Path.GetFullPath(Path.Join(root, relativePath));
        var prefix = root.EndsWith(Path.DirectorySeparatorChar) ? root : root + Path.DirectorySeparatorChar;
        if (!full.StartsWith(prefix, StringComparison.Ordinal))
            throw new ArgumentException($"\"{relativePath}\" leaves the plugin's folder.", nameof(relativePath));

        return full;
    }

    [GeneratedRegex("^[A-Za-z0-9][A-Za-z0-9._-]{0,63}$", RegexOptions.CultureInvariant)]
    private static partial Regex DatabaseNameRegex();

    #endregion
}
