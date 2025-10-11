using System;
using System.Collections.Generic;
using Shoko.Plugin.Abstractions.Config.Attributes;
using Shoko.Plugin.Abstractions.Services;

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
/// <typeparam name="TConfig">The type of the configuration.</typeparam>
public interface IConfigurationDefinitionWithCustomActions<TConfig> : IConfigurationDefinition where TConfig : class, IConfiguration, new()
{
    /// <summary>
    /// Perform a custom action on the configuration.
    /// </summary>
    /// <param name="context">The context for the action.</param>
    /// <returns>The result of the action.</returns>
    ConfigurationActionResult PerformAction(ConfigurationActionContext<TConfig> context);
}

/// <summary>
/// Interface for allowing plugins to react to changes in their configuration as
/// they are being edited in the UI.
/// </summary>
/// <typeparam name="TConfig">The type of the configuration.</typeparam>
public interface IConfigurationDefinitionWithReactiveActions<TConfig> : IConfigurationDefinition where TConfig : class, IConfiguration, new()
{
    /// <summary>
    /// Called when a value in the configuration has been loaded into the UI.
    /// </summary>
    /// <param name="context">The context for the action.</param>
    /// <returns>The result of the action.</returns>
    ConfigurationActionResult OnConfigurationLoaded(ConfigurationActionContext<TConfig> context)
        => new();

    /// <summary>
    /// Called when a value in the configuration is about to be saved. The
    /// configuration has not been validated yet, and may contain invalid
    /// values.
    /// <br />
    /// Calling <see cref="IConfigurationService.Save{TConfig}(TConfig)"/> on
    /// the service exposed on the context object will validate and save the
    /// configuration.
    /// </summary>
    /// <remarks>
    /// Responsible for saving the configuration. Only override if you know
    /// what you're doing.
    /// </remarks>
    /// <param name="context">The context for the action.</param>
    /// <returns>The result of the action.</returns>
    ConfigurationActionResult OnConfigurationSaved(ConfigurationActionContext<TConfig> context)
        => new() { ShowSaveMessage = context.Service.Save(context.Configuration), Refresh = true };

    /// <summary>
    /// Called when a value in the configuration has changed. The configuration
    /// has not been validated yet, and may contain invalid values.
    /// <br />
    /// If validation is required for your use case(s), use the service exposed
    /// on the context object to validate the configuration before use.
    /// </summary>
    /// <param name="context">The context for the action.</param>
    /// <returns>The result of the action.</returns>
    ConfigurationActionResult OnConfigurationChanged(ConfigurationActionContext<TConfig> context)
        => new();
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
