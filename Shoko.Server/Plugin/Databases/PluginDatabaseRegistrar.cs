using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Shoko.Abstractions.Core.Services;
using Shoko.Abstractions.Plugin;

namespace Shoko.Server.Plugin.Databases;

/// <summary>
/// Turns the databases plugins asked for with
/// <see cref="PluginDatabaseExtensions.AddPluginDbContext{TPlugin, TContext}(IServiceCollection, string)"/>
/// into registered contexts. The provider is chosen here, never by a plugin.
/// </summary>
internal static class PluginDatabaseRegistrar
{
    private static readonly MethodInfo _register = typeof(PluginDatabaseRegistrar)
        .GetMethod(nameof(Register), BindingFlags.NonPublic | BindingFlags.Static)!;

    /// <summary>
    /// Registers a context factory, the context and its migrator entry for
    /// every database the plugins asked for, and the gate that keeps them
    /// closed until they are migrated.
    /// </summary>
    /// <param name="services">The service collection the plugins registered into.</param>
    /// <exception cref="InvalidOperationException">
    /// A context does not derive from <see cref="DbContext"/>, a server's
    /// context does not derive from the registered one, a plugin asked for
    /// two databases with the same name, or one context was registered twice.
    /// </exception>
    public static void AddPluginDatabases(IServiceCollection services)
    {
        var registrations = services
            .Where(descriptor => descriptor.ServiceType == typeof(PluginDatabaseRegistration))
            .Select(descriptor => (PluginDatabaseRegistration)descriptor.ImplementationInstance!)
            .ToList();
        services.TryAddSingleton(provider => new PluginDatabaseGate(provider.GetService<ISystemService>()));
        var names = new HashSet<(Type, string)>();
        var contexts = new HashSet<Type>();
        foreach (var registration in registrations)
        {
            if (!IsConcreteContext(registration.ContextType))
                throw new InvalidOperationException($"{registration.PluginType.FullName} registered the database \"{registration.Name}\" with {registration.ContextType.FullName}, which is not a concrete DbContext.");
            foreach (var serverContextType in new[] { registration.MySqlContextType, registration.SqlServerContextType }.OfType<Type>())
            {
                if (serverContextType == registration.ContextType || !serverContextType.IsAssignableTo(registration.ContextType) || !IsConcreteContext(serverContextType))
                    throw new InvalidOperationException($"{registration.PluginType.FullName} named {serverContextType.FullName} for the database \"{registration.Name}\", which is not a concrete context derived from {registration.ContextType.FullName}.");
            }

            if (!names.Add((registration.PluginType, registration.Name.ToLowerInvariant())))
                throw new InvalidOperationException($"{registration.PluginType.FullName} registered more than one database named \"{registration.Name}\".");
            foreach (var contextType in new[] { registration.ContextType, registration.MySqlContextType, registration.SqlServerContextType }.OfType<Type>())
            {
                if (!contexts.Add(contextType))
                    throw new InvalidOperationException($"{contextType.FullName} was registered for more than one plugin database.");
            }

            _register.MakeGenericMethod(registration.PluginType, registration.ContextType).Invoke(null, [services, registration]);
        }
    }

    private static bool IsConcreteContext(Type type)
        => type.IsAssignableTo(typeof(DbContext)) && !type.IsAbstract;

    private static void Register<TPlugin, TContext>(IServiceCollection services, PluginDatabaseRegistration registration)
        where TPlugin : class, IPlugin
        where TContext : DbContext
    {
        // The backend is chosen when the first context is created, from the core's settings, so a
        // plugin keeps calling the same method whichever server the core runs on.
        services.AddSingleton(provider => new PluginDbContextFactory<TPlugin, TContext>(provider, registration.Name, registration.MySqlContextType, registration.SqlServerContextType));
        services.AddSingleton<IDbContextFactory<TContext>>(provider => provider.GetRequiredService<PluginDbContextFactory<TPlugin, TContext>>());
        services.AddScoped(provider => provider.GetRequiredService<IDbContextFactory<TContext>>().CreateDbContext());
        services.AddSingleton<IPluginDatabase>(provider => new PluginDatabase<TPlugin, TContext>(
            provider.GetRequiredService<PluginPaths<TPlugin>>(),
            provider.GetRequiredService<IPluginManager>(),
            provider.GetRequiredService<PluginDbContextFactory<TPlugin, TContext>>(),
            registration.Name
        ));
    }
}
