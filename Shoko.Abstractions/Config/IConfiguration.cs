using System.Collections.Generic;
using Shoko.Abstractions.Plugin;
using Shoko.Abstractions.Services;

namespace Shoko.Abstractions.Config;

/// <summary>
/// Base interface for all configurations served through the
/// <see cref="IConfigurationService"/> or
///  <see cref="ConfigurationProvider{TConfig}"/>.
/// </summary>
public interface IConfiguration { }

/// <summary>
/// Interface for signaling that the the configuration is a base configuration
/// for other configurations, and should not be saved or loaded directly.
/// </summary>
/// <remarks>
/// Base configurations are used by other services and/or plugins to validate
/// and/or (de-)serialize their configuration instances using the configuration
/// service. They may also have custom actions to run on the configuration.
/// </remarks>
public interface IBaseConfiguration : IConfiguration { }

/// <summary>
/// Interface for signaling that the configuration should use Newtonsoft.Json
/// for serialization/deserialization instead of System.Text.Json.
/// </summary>
public interface INewtonsoftJsonConfiguration : IConfiguration { }

/// <summary>
/// Interface for signaling that the configuration should be hidden from any UI.
/// </summary>
public interface IHiddenConfiguration : IConfiguration { }

/// <summary>
/// Interface for signaling that the configuration is tied to a hash provider.
/// </summary>
public interface IHashProviderConfiguration : IHiddenConfiguration { }

/// <summary>
/// Interface for signaling that the configuration is tied to a release info
/// provider.
/// </summary>
public interface IReleaseInfoProviderConfiguration : IHiddenConfiguration { }

/// <summary>
/// Interface for signaling that the configuration is tied to a renamer provider.
/// </summary>
public interface IRelocationProviderConfiguration : IHiddenConfiguration, IBaseConfiguration { }

/// <summary>
/// Interface for allowing plugins to apply migrations to their configuration
/// before loading it from disk.
/// </summary>
public interface IConfigurationWithMigrations : IConfiguration
{
    /// <summary>
    /// Apply migrations on the configuration before loading it from disk.
    /// </summary>
    /// <param name="config">The serialized configuration.</param>
    /// <param name="applicationPaths">The application paths.</param>
    /// <returns>The modified serialized configuration.</returns>
    abstract static string ApplyMigrations(string config, IApplicationPaths applicationPaths);
}

/// <summary>
/// Interface for creating new configurations with more complex rules for how it
/// should be initialized.
/// </summary>
/// <typeparam name="TConfig">The type of the configuration.</typeparam>
public interface IConfigurationWithNewFactory<TConfig> : IConfiguration where TConfig : class, IConfiguration, new()
{
    /// <summary>
    /// Create a new configuration with more complex rules for how it should be initialized.
    /// </summary>
    /// <param name="configurationService">The configuration service.</param>
    /// <param name="pluginManager">The plugin manager.</param>
    /// <returns>The new configuration.</returns>
    abstract static TConfig New(IConfigurationService configurationService, IPluginManager pluginManager);
}

/// <summary>
/// Interface for allowing plugins to specify custom validation rules for their
/// configuration, which will be called after JSON schema validation.
/// </summary>
/// <typeparam name="TConfig">The type of the configuration.</typeparam>
public interface IConfigurationWithCustomValidation<TConfig> : IConfiguration where TConfig : class, IConfiguration, new()
{
    /// <summary>
    /// Validate the configuration. This will only be called if the JSON schema
    /// validation was successful.
    /// </summary>
    /// <param name="config">The configuration.</param>
    /// <param name="configurationService">The configuration service.</param>
    /// <param name="pluginManager">The plugin manager.</param>
    /// <returns>A dictionary of validation errors.</returns>
    abstract static IReadOnlyDictionary<string, IReadOnlyList<string>> Validate(TConfig config, IConfigurationService configurationService, IPluginManager pluginManager);
}
