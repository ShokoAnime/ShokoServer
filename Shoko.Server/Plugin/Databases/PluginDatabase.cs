using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Shoko.Abstractions.Plugin;

namespace Shoko.Server.Plugin.Databases;

/// <summary>
/// One database a plugin registered, as the migrator sees it.
/// </summary>
internal interface IPluginDatabase
{
    /// <summary>
    /// The plugin's ID.
    /// </summary>
    Guid PluginID { get; }

    /// <summary>
    /// The plugin's name, for messages.
    /// </summary>
    string PluginName { get; }

    /// <summary>
    /// The name of the plugin's main dll without its extension, which keys it
    /// in <c>Plugins.EnabledPlugins</c>.
    /// </summary>
    string PluginDllName { get; }

    /// <summary>
    /// The context type.
    /// </summary>
    Type ContextType { get; }

    /// <summary>
    /// The database's name within the plugin.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// The database's own SQLite file, which is where it is kept when
    /// <see cref="Target"/> is SQLite, and otherwise a file left from when the
    /// core ran on SQLite, if there is one.
    /// </summary>
    string FilePath { get; }

    /// <summary>
    /// Where the database is kept.
    /// </summary>
    PluginDatabaseTarget Target { get; }

    /// <summary>
    /// The migrations not yet applied, oldest first.
    /// </summary>
    /// <returns>The migration IDs.</returns>
    IReadOnlyList<string> GetPendingMigrations();

    /// <summary>
    /// Applies the migrations up to and including one.
    /// </summary>
    /// <param name="migration">The migration ID.</param>
    void Migrate(string migration);
}

/// <summary>
/// A database of <typeparamref name="TPlugin"/>, kept in
/// <typeparamref name="TContext"/>.
/// </summary>
/// <typeparam name="TPlugin">The plugin.</typeparam>
/// <typeparam name="TContext">The context.</typeparam>
/// <param name="paths">The plugin's paths.</param>
/// <param name="pluginManager">The plugin manager, for the plugin's name and dll.</param>
/// <param name="factory">The context factory.</param>
/// <param name="name">The database's name within the plugin.</param>
internal sealed class PluginDatabase<TPlugin, TContext>(
    PluginPaths<TPlugin> paths,
    IPluginManager pluginManager,
    PluginDbContextFactory<TPlugin, TContext> factory,
    string name
) : IPluginDatabase
    where TPlugin : class, IPlugin
    where TContext : DbContext
{
    /// <inheritdoc/>
    public Guid PluginID => paths.PluginID;

    /// <inheritdoc/>
    public string PluginName => pluginManager.GetPluginInfo<TPlugin>()?.Name ?? typeof(TPlugin).FullName!;

    /// <inheritdoc/>
    public string PluginDllName => pluginManager.GetPluginInfo<TPlugin>() is { } pluginInfo
        ? Path.GetFileNameWithoutExtension(pluginInfo.DLLs[0])
        : typeof(TPlugin).Assembly.GetName().Name!;

    /// <inheritdoc/>
    public Type ContextType => typeof(TContext);

    /// <inheritdoc/>
    public string Name => name;

    /// <inheritdoc/>
    public string FilePath => PluginPathRules.GetDatabaseFile(paths.DatabasePath, name);

    /// <inheritdoc/>
    public PluginDatabaseTarget Target => factory.Target;

    /// <inheritdoc/>
    public IReadOnlyList<string> GetPendingMigrations()
    {
        using var context = factory.CreateMigrationContext();
        return context.Database.GetPendingMigrations().ToList();
    }

    /// <inheritdoc/>
    public void Migrate(string migration)
    {
        using var context = factory.CreateMigrationContext();
        context.GetService<IMigrator>().Migrate(migration);
    }
}
