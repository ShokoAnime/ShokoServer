using System;
using Microsoft.Extensions.DependencyInjection;

namespace Shoko.Abstractions.Plugin;

/// <summary>
/// A database a plugin asked for with
/// <see cref="PluginDatabaseExtensions.AddPluginDbContext{TPlugin, TContext}(IServiceCollection, string)"/>,
/// left in the service collection for the server to set up.
/// </summary>
/// <param name="PluginType">The plugin the database belongs to.</param>
/// <param name="ContextType">The context type, with the SQLite migrations.</param>
/// <param name="Name">The database's name.</param>
/// <param name="MySqlContextType">The context with the MySQL migrations, if the plugin has them.</param>
/// <param name="SqlServerContextType">The context with the SQL Server migrations, if the plugin has them.</param>
internal sealed record PluginDatabaseRegistration(Type PluginType, Type ContextType, string Name, Type? MySqlContextType = null, Type? SqlServerContextType = null);
