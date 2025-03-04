
using System;

namespace Shoko.Plugin.Abstractions.Config;

/// <summary>
/// Base interface for defining extra details around a configuration.
/// </summary>
public interface IConfigurationDefinition
{
    /// <summary>
    /// Gets the type of the configuration.
    /// </summary>
    /// <value>The type of the configuration.</value>
    Type ConfigurationType { get; }
}

/// <summary>
/// Interface for creating new configurations with more complex rules for how it should be initialized.
/// </summary>
/// <typeparam name="TConfig">The type of the configuration.</typeparam>
public interface IConfigurationNewFactory<TConfig> : IConfigurationDefinition where TConfig : class, IConfiguration, new()
{
    /// <summary>
    /// Create a new configuration with more complex rules for how it should be initialized.
    /// </summary>
    /// <returns>The new configuration.</returns>
    TConfig New();
}

/// <summary>
/// Interface for allowing plugins to specify a custom save name for their configuration.
/// </summary>
public interface IConfigurationDefinitionWithCustomSaveName : IConfigurationDefinition
{
    /// <summary>
    /// Gets the name of the file to use in the plugin's configuration folder inside <see cref="IApplicationPaths.PluginConfigurationsPath"/> for storing the configuration.
    /// </summary>
    /// <value>The file name.</value>
    string Name { get; }
}

/// <summary>
/// Interface for allowing plugins to specify a custom save location for their configuration.
/// </summary>
public interface IConfigurationDefinitionWithCustomSaveLocation : IConfigurationDefinition
{
    /// <summary>
    /// Gets the relative path relative to <see cref="IApplicationPaths.ProgramDataPath"/> for storing the configuration.
    /// </summary>
    /// <value>The relative path.</value>
    string RelativePath { get; }
}

/// <summary>
/// Interface for allowing plugins to apply migrations to their configuration before loading it from disk.
/// </summary>
public interface IConfigurationDefinitionsWithMigrations : IConfigurationDefinition
{
    /// <summary>
    /// Apply migrations on the configuration before loading it from disk.
    /// </summary>
    /// <param name="config">The serialized configuration.</param>
    /// <returns>The modified serialized configuration.</returns>
    string ApplyMigrations(string config);
}
