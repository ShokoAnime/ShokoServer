using System;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.DependencyInjection;
using Shoko.Abstractions.Config;

namespace Shoko.Server.Services;

/// <summary>
///   The user configurations registered with
///   <see cref="UserConfigurationExtensions.AddUserConfiguration{TPlugin}(IServiceCollection, string, Action{UserConfigurationOptions}?)"/>,
///   by ID, fixed once the plugins registered their services.
/// </summary>
public sealed class UserConfigurationRegistry
{
    #region Fields

    private readonly FrozenDictionary<string, UserConfigurationRegistration> _configurations;

    #endregion

    #region Constructors

    private UserConfigurationRegistry(FrozenDictionary<string, UserConfigurationRegistration> configurations)
        => _configurations = configurations;

    #endregion

    #region Registration

    /// <summary>
    ///   Collects the configurations the core and the plugins registered and adds
    ///   the registry built from them.
    /// </summary>
    /// <param name="services">The service collection the plugins registered into.</param>
    /// <exception cref="InvalidOperationException">
    ///   One configuration ID was registered more than once.
    /// </exception>
    public static void AddUserConfigurationRegistry(IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        var registrations = services
            .Where(descriptor => descriptor.ServiceType == typeof(UserConfigurationRegistration))
            .Select(descriptor => (UserConfigurationRegistration)descriptor.ImplementationInstance!)
            .ToList();
        services.AddSingleton(Build(registrations));
    }

    /// <summary>
    ///   Builds a registry from registrations, refusing a configuration ID
    ///   registered more than once.
    /// </summary>
    /// <param name="registrations">The registrations, in registration order.</param>
    /// <returns>The registry.</returns>
    /// <exception cref="InvalidOperationException">
    ///   One configuration ID was registered more than once.
    /// </exception>
    internal static UserConfigurationRegistry Build(IEnumerable<UserConfigurationRegistration> registrations)
    {
        var configurations = new Dictionary<string, UserConfigurationRegistration>(StringComparer.Ordinal);
        foreach (var registration in registrations)
        {
            if (configurations.TryGetValue(registration.ID, out var existing))
                throw new InvalidOperationException(
                    $"The user configuration {existing.DisplayName} ({registration.ID}) is registered by both {existing.PluginType.FullName} and " +
                    $"{registration.PluginType.FullName}. Disable one of them."
                );

            configurations[registration.ID] = registration;
        }

        return new(configurations.ToFrozenDictionary(StringComparer.Ordinal));
    }

    #endregion

    #region Lookups

    /// <summary>
    ///   Every registered configuration.
    /// </summary>
    internal IEnumerable<UserConfigurationRegistration> All
        => _configurations.Values;

    /// <summary>
    ///   Finds a registered configuration.
    /// </summary>
    /// <param name="configurationID">The configuration's ID, in any case.</param>
    /// <returns>The registration, or <c>null</c> when the configuration is not registered.</returns>
    internal UserConfigurationRegistration? Get(string configurationID)
        => _configurations.GetValueOrDefault(Normalize(configurationID));

    /// <summary>
    ///   The form a configuration ID is stored and looked up in, so input in
    ///   another case still finds it.
    /// </summary>
    /// <param name="configurationID">The configuration's ID.</param>
    /// <returns>The ID in lower case.</returns>
    internal static string Normalize(string configurationID)
        => configurationID.ToLowerInvariant();

    #endregion
}
