using System;
using System.Collections.Generic;
using Shoko.Plugin.Abstractions.Config;
using Shoko.Plugin.Abstractions.Config.Exceptions;
using Shoko.Plugin.Abstractions.Events;

namespace Shoko.Plugin.Abstractions.Services;

/// <summary>
/// Service responsible for managing configurations.
/// </summary>
public interface IConfigurationService
{
    /// <summary>
    /// Occurs when a configuration is saved.
    /// </summary>
    event EventHandler<ConfigurationSavedEventArgs>? Saved;

    /// <summary>
    ///   Adds the necessary parts for the service to function.
    /// </summary>
    /// <remarks>
    ///   This should be called once per instance of the service. Calling it
    ///   multiple times will have no effect.
    /// </remarks>
    /// <param name="configurationTypes">
    ///   The configurations.
    /// </param>
    /// <param name="configurationDefinitions">
    ///   The configuration definitions.
    /// </param>
    void AddParts(IEnumerable<Type> configurationTypes, IEnumerable<IConfigurationDefinition> configurationDefinitions);

    /// <summary>
    /// Create a new configuration provider instance for the specified configuration type.
    /// </summary>
    /// <typeparam name="TConfig">The configuration type.</typeparam>
    /// <returns>The configuration provider.</returns>
    ConfigurationProvider<TConfig> CreateProvider<TConfig>() where TConfig : class, IConfiguration, new();

    /// <summary>
    /// Gets all configuration types registered with the system.
    /// </summary>
    /// <returns>The configurations.</returns>
    IEnumerable<ConfigurationInfo> GetAllConfigurationInfos();

    /// <summary>
    /// Gets the configuration info for the specified configuration ID.
    /// </summary>
    /// <param name="configurationId">The configuration ID.</param>
    /// <returns>The configuration info, or null if not found.</returns>
    ConfigurationInfo? GetConfigurationInfo(Guid configurationId);

    /// <summary>
    /// Gets the configuration info for the specified type.
    /// </summary>
    /// <typeparam name="TConfig">The type of the configuration.</typeparam>
    /// <returns>The configuration info.</returns>
    ConfigurationInfo GetConfigurationInfo<TConfig>() where TConfig : class, IConfiguration, new();

    /// <summary>
    /// Validates the configuration for the specified configuration info.
    /// </summary>
    /// <param name="info">The configuration info.</param>
    /// <param name="json">The JSON string.</param>
    /// <returns>A read-only dictionary of validation errors per property path.</returns>
    IReadOnlyDictionary<string, IReadOnlyList<string>> Validate(ConfigurationInfo info, string json);

    /// <summary>
    /// Validates the configuration.
    /// </summary>
    /// <typeparam name="TConfig">The type of the configuration.</typeparam>
    /// <param name="config">The configuration.</param>
    /// <returns>A read-only dictionary of validation errors per property path.</returns>
    IReadOnlyDictionary<string, IReadOnlyList<string>> Validate<TConfig>(TConfig config) where TConfig : class, IConfiguration, new();

    /// <summary>
    /// Creates a new configuration for the specified configuration info without saving it and without storing it in the in-memory cache.
    /// </summary>
    /// <param name="info">The configuration info.</param>
    /// <returns>The new configuration.</returns>
    IConfiguration New(ConfigurationInfo info);

    /// <summary>
    /// Creates a new configuration without saving it and without storing it in the in-memory cache.
    /// </summary>
    /// <typeparam name="TConfig">The type of the configuration.</typeparam>
    /// <returns>The new configuration.</returns>
    TConfig New<TConfig>() where TConfig : class, IConfiguration, new();

    /// <summary>
    /// Loads the configuration for the specified configuration info from the in-memory cache or from the disk. It will validate the configuration before loading it and create a new configuration if one does not exist on disk.
    /// </summary>
    /// <param name="info">The configuration info.</param>
    /// <param name="copy">Set to true to get a copy of the configuration instead of the in-memory cached instance.</param>
    /// <exception cref="ConfigurationValidationException">
    /// Thrown when a configuration fails validation.
    /// </exception>
    /// <returns>The configuration.</returns>
    IConfiguration Load(ConfigurationInfo info, bool copy = false);

    /// <summary>
    /// Loads the configuration from the in-memory cache or from the disk. It will validate the configuration before loading it and create a new configuration if one does not exist on disk.
    /// </summary>
    /// <typeparam name="TConfig">The type of the configuration.</typeparam>
    /// <param name="copy">Set to true to get a copy of the configuration instead of the in-memory cached instance.</param>
    /// <exception cref="ConfigurationValidationException">
    /// Thrown when a configuration fails validation.
    /// </exception>
    /// <returns>The configuration.</returns>
    TConfig Load<TConfig>(bool copy = false) where TConfig : class, IConfiguration, new();

    /// <summary>
    /// Saves the configuration for the specified configuration info. This will validate the configuration before saving it.
    /// </summary>
    /// <param name="info">The configuration info.</param>
    /// <param name="json">The stringified configuration to save.</param>
    /// <exception cref="ConfigurationValidationException">
    /// Thrown when a configuration fails validation.
    /// </exception>
    void Save(ConfigurationInfo info, IConfiguration json);

    /// <summary>
    /// Saves the configuration for the specified configuration info. This will validate the configuration before saving it.
    /// </summary>
    /// <param name="info">The configuration info.</param>
    /// <param name="json">The stringified configuration to save.</param>
    /// <exception cref="ConfigurationValidationException">
    /// Thrown when a configuration fails validation.
    /// </exception>
    void Save(ConfigurationInfo info, string json);

    /// <summary>
    /// Saves the currently loaded in-memory configuration to disk. This will validate the configuration before saving it.
    /// </summary>
    /// <typeparam name="TConfig">The type of the configuration.</typeparam>
    /// <exception cref="ConfigurationValidationException">
    /// Thrown when a configuration fails validation.
    /// </exception>
    void Save<TConfig>() where TConfig : class, IConfiguration, new();

    /// <summary>
    /// Saves the configuration. This will validate the configuration before saving it.
    /// </summary>
    /// <typeparam name="TConfig">The type of the configuration.</typeparam>
    /// <param name="config">The configuration to save.</param>
    /// <exception cref="ConfigurationValidationException">
    /// Thrown when a configuration fails validation.
    /// </exception>
    void Save<TConfig>(TConfig config) where TConfig : class, IConfiguration, new();

    /// <summary>
    /// Saves the configuration. This will validate the configuration before saving it.
    /// </summary>
    /// <typeparam name="TConfig">The type of the configuration.</typeparam>
    /// <param name="json">The stringified configuration to save.</param>
    /// <exception cref="ConfigurationValidationException">
    /// Thrown when a configuration fails validation.
    /// </exception>
    void Save<TConfig>(string json) where TConfig : class, IConfiguration, new();

    /// <summary>
    /// Gets the cached, serialized JSON schema for the specified configuration info.
    /// </summary>
    /// <param name="info">The configuration info.</param>
    /// <returns>The serialized JSON schema.</returns>
    string GetSchema(ConfigurationInfo info);

    /// <summary>
    /// Serializes the specified configuration to JSON.
    /// </summary>
    /// <param name="config">The configuration to serialize.</param>
    /// <returns>The serialized JSON configuration.</returns>
    string Serialize(IConfiguration config);
}
