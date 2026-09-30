using System;

namespace Shoko.Abstractions.Plugin;

/// <summary>
/// Names the contexts that carry a plugin database's migrations for the
/// database servers the server can run on, for
/// <see cref="PluginDatabaseExtensions.AddPluginDbContext{TPlugin, TContext}(Microsoft.Extensions.DependencyInjection.IServiceCollection, string, Action{PluginDbContextOptions{TContext}})"/>.
/// </summary>
/// <remarks>
/// Each context derives from <typeparamref name="TContext"/>, adds nothing to
/// its model, and has its own migrations generated with <c>dotnet ef</c>. Its
/// constructor takes <c>DbContextOptions&lt;TContext&gt;</c>, or its own
/// <c>DbContextOptions&lt;TDerived&gt;</c> passed to a <typeparamref name="TContext"/>
/// constructor taking the non-generic <c>DbContextOptions</c>. A server left
/// unnamed keeps the database in its SQLite file.
/// </remarks>
/// <typeparam name="TContext">The context, with the SQLite migrations.</typeparam>
public sealed class PluginDbContextOptions<TContext> where TContext : class
{
    /// <summary>
    /// The context with the MySQL and MariaDB migrations, if one was named.
    /// </summary>
    public Type? MySqlContextType { get; private set; }

    /// <summary>
    /// The context with the SQL Server migrations, if one was named.
    /// </summary>
    public Type? SqlServerContextType { get; private set; }

    /// <summary>
    /// Names the context with the MySQL and MariaDB migrations.
    /// </summary>
    /// <typeparam name="TMySqlContext">The context, derived from <typeparamref name="TContext"/>.</typeparam>
    /// <returns>The options.</returns>
    /// <exception cref="ArgumentException"><typeparamref name="TMySqlContext"/> is <typeparamref name="TContext"/> itself, or abstract.</exception>
    public PluginDbContextOptions<TContext> WithMySqlMigrations<TMySqlContext>() where TMySqlContext : class, TContext
    {
        MySqlContextType = Check(typeof(TMySqlContext));
        return this;
    }

    /// <summary>
    /// Names the context with the SQL Server migrations.
    /// </summary>
    /// <typeparam name="TSqlServerContext">The context, derived from <typeparamref name="TContext"/>.</typeparam>
    /// <returns>The options.</returns>
    /// <exception cref="ArgumentException"><typeparamref name="TSqlServerContext"/> is <typeparamref name="TContext"/> itself, or abstract.</exception>
    public PluginDbContextOptions<TContext> WithSqlServerMigrations<TSqlServerContext>() where TSqlServerContext : class, TContext
    {
        SqlServerContextType = Check(typeof(TSqlServerContext));
        return this;
    }

    private static Type Check(Type contextType)
    {
        if (contextType == typeof(TContext))
            throw new ArgumentException($"{contextType.FullName} carries the SQLite migrations; a database server needs a context derived from it, with migrations of its own.");
        if (contextType.IsAbstract)
            throw new ArgumentException($"{contextType.FullName} is abstract, so the server cannot create it.");
        return contextType;
    }
}
