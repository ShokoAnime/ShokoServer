using System;
using Microsoft.Extensions.DependencyInjection;

namespace Shoko.Abstractions.Plugin;

/// <summary>
/// Gives a plugin a database of its own, kept and migrated by the server.
/// </summary>
public static class PluginDatabaseExtensions
{
    /// <summary>
    /// Registers an Entity Framework Core context as one of the plugin's
    /// databases. Call it from <c>IPluginServiceRegistration.RegisterServices</c>.
    /// </summary>
    /// <remarks>
    /// The server picks the provider: SQLite in WAL mode, in <c>&lt;name&gt;.db3</c> under
    /// <see cref="PluginPaths{TPlugin}.DatabasePath"/>; the overload taking
    /// <see cref="PluginDbContextOptions{TContext}"/> can follow the server onto MySQL or
    /// SQL Server. Pending migrations run late in start-up, after the server's own and
    /// after a copy of the file, and a failing one stops start-up.
    /// </remarks>
    /// <typeparam name="TPlugin">The plugin the database belongs to.</typeparam>
    /// <typeparam name="TContext">
    /// The context: a <c>DbContext</c> taking a <c>DbContextOptions&lt;TContext&gt;</c>,
    /// checked by the server (typed as a class so the abstractions need no Entity
    /// Framework Core reference). Inject <c>IDbContextFactory&lt;TContext&gt;</c> into
    /// singletons, or the context into scoped services. Both throw an
    /// <see cref="InvalidOperationException"/> until the migrations ran, so setup, the
    /// ready step and hosted services' start cannot use the database.
    /// </typeparam>
    /// <param name="services">The service collection.</param>
    /// <param name="name">
    /// The database's name, unique within the plugin, and its file name
    /// without the extension: a letter or digit, then letters, digits,
    /// <c>-</c>, <c>_</c> or <c>.</c>, up to 64 in all. Never rename it; a new
    /// name is a new, empty database.
    /// </param>
    /// <returns>The service collection.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="services"/> is <c>null</c>.</exception>
    /// <exception cref="ArgumentException"><paramref name="name"/> is not a valid database name.</exception>
    public static IServiceCollection AddPluginDbContext<TPlugin, TContext>(this IServiceCollection services, string name)
        where TPlugin : class, IPlugin
        where TContext : class
    {
        ArgumentNullException.ThrowIfNull(services);
        PluginPathRules.ValidateDatabaseName(name);

        services.AddSingleton(new PluginDatabaseRegistration(typeof(TPlugin), typeof(TContext), name));
        return services;
    }

    /// <summary>
    /// Registers an Entity Framework Core context as one of the plugin's
    /// databases, naming the contexts that carry its migrations for the
    /// database servers the server can run on. Call it from
    /// <c>IPluginServiceRegistration.RegisterServices</c>.
    /// </summary>
    /// <remarks>
    /// Each database server gets a context of its own, derived from <typeparamref name="TContext"/>,
    /// with its own migrations. On MySQL or SQL Server with a context named for it, the
    /// database lives in the server's own database, its tables prefixed per plugin and
    /// database; otherwise it stays in its SQLite file. Everything else is as for
    /// <see cref="AddPluginDbContext{TPlugin, TContext}(IServiceCollection, string)"/>.
    /// </remarks>
    /// <typeparam name="TPlugin">The plugin the database belongs to.</typeparam>
    /// <typeparam name="TContext">
    /// The context, with the SQLite migrations. Inject it or
    /// <c>IDbContextFactory&lt;TContext&gt;</c> to get whichever context the server picked.
    /// </typeparam>
    /// <param name="services">The service collection.</param>
    /// <param name="name">
    /// The database's name, unique within the plugin, following the rules of
    /// the other overload. Never rename it.
    /// </param>
    /// <param name="configure">Names the contexts for the database servers.</param>
    /// <returns>The service collection.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="services"/> or <paramref name="configure"/> is <c>null</c>.</exception>
    /// <exception cref="ArgumentException"><paramref name="name"/> is not a valid database name.</exception>
    /// <exception cref="InvalidOperationException"><paramref name="configure"/> named one context for both servers.</exception>
    public static IServiceCollection AddPluginDbContext<TPlugin, TContext>(this IServiceCollection services, string name, Action<PluginDbContextOptions<TContext>> configure)
        where TPlugin : class, IPlugin
        where TContext : class
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configure);
        PluginPathRules.ValidateDatabaseName(name);

        var options = new PluginDbContextOptions<TContext>();
        configure(options);
        if (options.MySqlContextType is not null && options.MySqlContextType == options.SqlServerContextType)
            throw new InvalidOperationException($"{options.MySqlContextType.FullName} was named for both MySQL and SQL Server; each server needs a context with its own migrations.");

        services.AddSingleton(new PluginDatabaseRegistration(typeof(TPlugin), typeof(TContext), name, options.MySqlContextType, options.SqlServerContextType));
        return services;
    }
}

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
