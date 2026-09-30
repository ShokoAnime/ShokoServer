using System;
using System.Linq;
using System.Threading;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;
using Shoko.Abstractions.Plugin;
using Shoko.QueueProcessor.Storage.Contexts;
using Shoko.Server.Server;

namespace Shoko.Server.Plugin.Databases;

/// <summary>
/// Where a plugin database is kept, and in which context.
/// </summary>
/// <param name="Type">The backend: SQLite for a file of its own, otherwise the core's server.</param>
/// <param name="ContextType">The context created, which carries the migrations for <paramref name="Type"/>.</param>
/// <param name="Naming">How its tables are named in the core's database; <see langword="null"/> on SQLite.</param>
internal sealed record PluginDatabaseTarget(Constants.DatabaseType Type, Type ContextType, PluginTableNaming? Naming);

/// <summary>
/// Creates a plugin's context on the backend the core picked: the context the
/// plugin named for the core's server, on that server with its tables
/// prefixed, or the plugin's own context on its SQLite file. The plugin only
/// gets one once the late start has migrated the plugin databases.
/// </summary>
/// <typeparam name="TPlugin">The plugin.</typeparam>
/// <typeparam name="TContext">The context the plugin registered and injects.</typeparam>
internal sealed class PluginDbContextFactory<TPlugin, TContext> : IDbContextFactory<TContext>
    where TPlugin : class, IPlugin
    where TContext : DbContext
{
    private readonly IServiceProvider _services;

    private readonly string _name;

    private readonly Type? _mySqlContextType;

    private readonly Type? _sqlServerContextType;

    private readonly Lazy<PluginDatabaseTarget> _target;

    private readonly Lazy<Func<TContext>> _create;

    /// <summary>
    /// Creates the factory for one registered database.
    /// </summary>
    /// <param name="services">The server's services.</param>
    /// <param name="name">The database's name within the plugin.</param>
    /// <param name="mySqlContextType">The context with the MySQL migrations, if any.</param>
    /// <param name="sqlServerContextType">The context with the SQL Server migrations, if any.</param>
    public PluginDbContextFactory(IServiceProvider services, string name, Type? mySqlContextType, Type? sqlServerContextType)
    {
        _services = services;
        _name = name;
        _mySqlContextType = mySqlContextType;
        _sqlServerContextType = sqlServerContextType;
        _target = new(ChooseTarget);
        // Not cached when it throws: on MySQL the server's version is read here, and a server
        // that is not up yet must not fail every later context.
        _create = new(CreateActivator, LazyThreadSafetyMode.PublicationOnly);
    }

    /// <summary>
    /// Where the database is kept, decided the first time it is asked, which
    /// is when the late start migrates it, from the settings as the first-run
    /// setup left them.
    /// </summary>
    public PluginDatabaseTarget Target => _target.Value;

    /// <summary>
    /// Creates a context on the chosen backend, for the plugin.
    /// </summary>
    /// <returns>The context, of <see cref="PluginDatabaseTarget.ContextType"/>.</returns>
    /// <exception cref="InvalidOperationException">The plugin databases have not been migrated yet.</exception>
    public TContext CreateDbContext()
    {
        _services.GetRequiredService<PluginDatabaseGate>().ThrowIfClosed(GetPluginName(), _name);
        return _create.Value();
    }

    /// <summary>
    /// Creates a context on the chosen backend whether or not the databases
    /// have been migrated, for the migrator.
    /// </summary>
    /// <returns>The context, of <see cref="PluginDatabaseTarget.ContextType"/>.</returns>
    internal TContext CreateMigrationContext()
        => _create.Value();

    private string GetPluginName()
        => _services.GetService<IPluginManager>()?.GetPluginInfo<TPlugin>()?.Name ?? typeof(TPlugin).FullName!;

    #region Target

    private PluginDatabaseTarget ChooseTarget()
    {
        var server = _services.GetRequiredService<PluginDatabaseServer>();
        var (contextType, maxLength) = server.Type switch
        {
            Constants.DatabaseType.MySQL => (_mySqlContextType, PluginTableNaming.MySqlMaxLength),
            Constants.DatabaseType.SQLServer => (_sqlServerContextType, PluginTableNaming.SqlServerMaxLength),
            _ => (null, 0),
        };
        if (contextType is null)
            return new(Constants.DatabaseType.SQLite, typeof(TContext), null);

        var pluginID = _services.GetRequiredService<PluginPaths<TPlugin>>().PluginID;
        return new(server.Type, contextType, PluginTableNaming.For(pluginID, _name, maxLength));
    }

    #endregion

    #region Options

    private Func<TContext> CreateActivator()
    {
        var target = Target;
        var optionsType = GetOptionsType(target.ContextType);
        var builder = new DbContextOptionsBuilder((DbContextOptions)Activator.CreateInstance(optionsType)!);
        builder.UseApplicationServiceProvider(_services);
        Configure(builder, target);

        var options = builder.Options;
        var activator = ActivatorUtilities.CreateFactory(target.ContextType, [optionsType]);
        return () => (TContext)activator(_services, [options]);
    }

    private void Configure(DbContextOptionsBuilder builder, PluginDatabaseTarget target)
    {
        var server = _services.GetRequiredService<PluginDatabaseServer>();
        switch (target.Type)
        {
            case Constants.DatabaseType.MySQL:
                builder.UseMySql(
                    server.ConnectionString,
                    ServerVersion.AutoDetect(server.ConnectionString),
                    mySql => mySql.MigrationsHistoryTable(target.Naming!.HistoryTableName)
                );
                Prefix(builder, target.Naming!);
                break;
            case Constants.DatabaseType.SQLServer:
                builder.UseSqlServer(server.ConnectionString, sqlServer => sqlServer.MigrationsHistoryTable(target.Naming!.HistoryTableName));
                Prefix(builder, target.Naming!);
                break;
            default:
                var file = PluginPathRules.GetDatabaseFile(_services.GetRequiredService<PluginPaths<TPlugin>>().DatabasePath, _name);
                builder
                    .UseSqlite($"Data Source={file};Mode=ReadWriteCreate;Pooling=True")
                    .AddInterceptors(new SqlitePragmaConnectionInterceptor());
                break;
        }
    }

    private static void Prefix(DbContextOptionsBuilder builder, PluginTableNaming naming)
    {
        ((IDbContextOptionsBuilderInfrastructure)builder).AddOrUpdateExtension(new PluginTablePrefixExtension(naming));
        builder
            .ReplaceService<IMigrationsAssembly, PluginTablePrefixMigrationsAssembly>()
            .ReplaceService<IModelCustomizer, PluginTablePrefixModelCustomizer>()
            .ReplaceService<IModelCacheKeyFactory, PluginTablePrefixModelCacheKeyFactory>();
    }

    /// <summary>
    /// The options type a context takes: its own, or, for a context derived
    /// from the registered one, the registered context's, which Entity
    /// Framework Core accepts for a derived context too.
    /// </summary>
    /// <param name="contextType">The context created.</param>
    /// <returns>The options type.</returns>
    internal static Type GetOptionsType(Type contextType)
    {
        var own = typeof(DbContextOptions<>).MakeGenericType(contextType);
        var parameters = contextType.GetConstructors().SelectMany(constructor => constructor.GetParameters()).Select(parameter => parameter.ParameterType).ToList();
        if (parameters.Contains(own) || parameters.Contains(typeof(DbContextOptions)))
            return own;
        return parameters.Contains(typeof(DbContextOptions<TContext>)) ? typeof(DbContextOptions<TContext>) : own;
    }

    #endregion
}
