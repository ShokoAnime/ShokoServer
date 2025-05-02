using System;
using System.Collections.Generic;
using Namotion.Reflection;
using Shoko.Plugin.Abstractions.Config.Attributes;
using Shoko.Plugin.Abstractions.DataModels.Shoko;

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
/// Interface for allowing plugins to specify a custom save name for their
/// configuration.
/// </summary>
public interface IConfigurationDefinitionWithCustomSaveName : IConfigurationDefinition
{
    /// <summary>
    /// Gets the name of the file to use in the plugin's configuration folder
    /// inside <see cref="IApplicationPaths.ConfigurationsPath"/> for
    /// storing the configuration. Can be set to <c>null</c> or an empty string
    /// to make it an in-memory configuration, which will not persist it's data
    /// across restarts.
    /// </summary>
    /// <value>The file name.</value>
    string? Name { get; }
}

/// <summary>
/// Interface for allowing plugins to specify a custom save location for their
/// configuration.
/// </summary>
public interface IConfigurationDefinitionWithCustomSaveLocation : IConfigurationDefinition
{
    /// <summary>
    /// Gets the relative path relative to
    /// <see cref="IApplicationPaths.DataPath"/> for storing the
    /// configuration.
    /// </summary>
    /// <value>The relative path.</value>
    string RelativePath { get; }
}

/// <summary>
/// Interface for creating new configurations with more complex rules for how it
/// should be initialized.
/// </summary>
/// <typeparam name="TConfig">The type of the configuration.</typeparam>
public interface IConfigurationDefinitionWithNewFactory<TConfig> : IConfigurationDefinition where TConfig : class, IConfiguration, new()
{
    /// <summary>
    /// Create a new configuration with more complex rules for how it should be initialized.
    /// </summary>
    /// <returns>The new configuration.</returns>
    TConfig New();
}

/// <summary>
/// Interface for allowing plugins to specify custom validation rules for their
/// configuration, which will be called after JSON schema validation.
/// </summary>
/// <typeparam name="TConfig">The type of the configuration.</typeparam>
public interface IConfigurationDefinitionWithCustomValidation<TConfig> : IConfigurationDefinition where TConfig : class, IConfiguration, new()
{
    /// <summary>
    /// Validate the configuration. This will only be called if the JSON schema
    /// validation was successful.
    /// </summary>
    /// <param name="config">The configuration.</param>
    /// <returns>A dictionary of validation errors.</returns>
    IReadOnlyDictionary<string, IReadOnlyList<string>> Validate(TConfig config);
}

/// <summary>
/// Interface for allowing plugins to specify custom actions on their
/// configuration. You'll need to assign <see cref="CustomActionAttribute"/> to
/// your configuration, be it at the base or at a sub-class level for any custom
/// actions to be available in the UI.
/// </summary>
public interface IConfigurationDefinitionWithCustomActions<TConfig> : IConfigurationDefinition where TConfig : class, IConfiguration, new()
{
    /// <summary>
    /// Perform a custom action on the configuration.
    /// </summary>
    /// <param name="config">The configuration.</param>
    /// <param name="path">The path to the configuration.</param>
    /// <param name="action">The action to perform.</param>
    /// <param name="type">The contextual type of the class or sub-class.</param>
    /// <param name="user">The user performing the action, if any.</param>
    /// <returns>The result of the action.</returns>
    ConfigurationActionResult PerformAction(TConfig config, string path, string action, ContextualType type, IShokoUser? user = null);
}

/// <summary>
/// Interface for allowing plugins to apply migrations to their configuration
/// before loading it from disk.
/// </summary>
public interface IConfigurationDefinitionWithMigrations : IConfigurationDefinition
{
    /// <summary>
    /// Apply migrations on the configuration before loading it from disk.
    /// </summary>
    /// <param name="config">The serialized configuration.</param>
    /// <returns>The modified serialized configuration.</returns>
    string ApplyMigrations(string config);
}
