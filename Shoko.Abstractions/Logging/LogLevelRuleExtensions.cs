using System;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Shoko.Abstractions.Logging.Models;
using Shoko.Abstractions.Plugin;

namespace Shoko.Abstractions.Logging;

/// <summary>
///   Registers the default log level rules a plugin brings.
/// </summary>
public static class LogLevelRuleExtensions
{
    /// <summary>
    ///   Registers a default log level rule for loggers your plugin uses, its
    ///   libraries' included. Call it from
    ///   <c>IPluginServiceRegistration.RegisterServices</c>.
    /// </summary>
    /// <remarks>
    ///   The rule applies while the plugin is active, after the core's
    ///   defaults and before the custom rules of the logging settings, which
    ///   replace it when they name the same pattern. It is listed with the
    ///   plugin's name among the rules in effect. See <see cref="LogLevelRule"/> for what
    ///   the levels do.
    /// </remarks>
    /// <typeparam name="TPlugin">The plugin the rule belongs to.</typeparam>
    /// <param name="services">The service collection.</param>
    /// <param name="loggerNamePattern">The pattern of the logger names, such as <c>ModelContextProtocol.*</c>.</param>
    /// <param name="minLevel">The lowest level written, below the targets' levels, or <c>null</c>.</param>
    /// <param name="maxLevel">The highest level dropped, or <c>null</c>.</param>
    /// <param name="final">Whether a dropped message is kept from later rules.</param>
    /// <returns>The service collection.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="services"/> is <c>null</c>.</exception>
    /// <exception cref="ArgumentException">The pattern is empty, or <paramref name="minLevel"/> is not above <paramref name="maxLevel"/>.</exception>
    public static IServiceCollection AddLogLevelRule<TPlugin>(this IServiceCollection services, string loggerNamePattern, LogLevel? minLevel = null, LogLevel? maxLevel = null, bool final = true)
        where TPlugin : class, IPlugin
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentException.ThrowIfNullOrWhiteSpace(loggerNamePattern);
        if (minLevel is { } min && maxLevel is { } max && min <= max)
            throw new ArgumentException($"The minimum level ({min}) must be above the maximum level ({max}).", nameof(minLevel));

        services.AddSingleton(new LogLevelRuleRegistration(typeof(TPlugin), new() { LoggerNamePattern = loggerNamePattern, MinLevel = minLevel, MaxLevel = maxLevel, Final = final }));
        return services;
    }
}
