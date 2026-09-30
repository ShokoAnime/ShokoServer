using System;
using Microsoft.Extensions.DependencyInjection;

namespace Shoko.Abstractions.Web.SignalR;

/// <summary>
/// Registers a plugin's feeds on the aggregate hub.
/// </summary>
public static class EventEmitterExtensions
{
    /// <summary>
    /// Registers a feed as a singleton, both as itself and as an
    /// <see cref="IEventEmitter"/>, so the plugin's services that inject it
    /// send through the same instance the aggregate hub lists. Call it from
    /// <c>IPluginServiceRegistration.RegisterServices</c>.
    /// </summary>
    /// <typeparam name="TEmitter">The feed.</typeparam>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="services"/> is <see langword="null"/>.</exception>
    public static IServiceCollection AddEventEmitter<TEmitter>(this IServiceCollection services)
        where TEmitter : class, IEventEmitter
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddSingleton<TEmitter>();
        services.AddSingleton<IEventEmitter>(provider => provider.GetRequiredService<TEmitter>());
        return services;
    }
}
