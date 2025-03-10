using System;
using System.Collections.Generic;
using Shoko.Plugin.Abstractions.Config.Exceptions;
using Shoko.Plugin.Abstractions.Events;
using Shoko.Plugin.Abstractions.Services;

namespace Shoko.Plugin.Abstractions.Config;

/// <summary>
/// Configuration provider for a specific configuration type.
/// </summary>
/// <typeparam name="TConfig">The type of the configuration to provide for.</typeparam>
public class ConfigurationProvider<TConfig> : IDisposable where TConfig : class, IConfiguration, new()
{
    private readonly IConfigurationService _service;

    private ConfigurationInfo? _pluginConfigurationInfo = null;

    /// <summary>
    /// Occurs when a configuration is saved.
    /// </summary>
    public event EventHandler<ConfigurationSavedEventArgs<TConfig>>? Saved;

    /// <summary>
    /// Gets the information about the configuration.
    /// </summary>
    public ConfigurationInfo ConfigurationInfo
        => _pluginConfigurationInfo ??= _service.GetConfigurationInfo<TConfig>();

    /// <summary>
    /// Initializes a new instance of the <see cref="ConfigurationProvider{TConfig}"/> class.
    /// </summary>
    /// <param name="service">The configuration service.</param>
    public ConfigurationProvider(IConfigurationService service)
    {
        _service = service;
        _service.Saved += OnSaved;
    }

    /// <summary>
    /// Finalizes an instance of the <see cref="ConfigurationProvider{TConfig}"/> class.
    /// </summary>
    public void Dispose()
    {
        _service.Saved -= OnSaved;
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Handles the saved event.
    /// </summary>
    /// <param name="sender">The sender.</param>
    /// <param name="eventArgs">The <see cref="ConfigurationSavedEventArgs"/> instance containing the event data.</param>
    private void OnSaved(object? sender, ConfigurationSavedEventArgs eventArgs)
    {
        if (eventArgs.ConfigurationInfo != ConfigurationInfo)
            return;

        Saved?.Invoke(this, new ConfigurationSavedEventArgs<TConfig> { ConfigurationInfo = eventArgs.ConfigurationInfo, Configuration = Load() });
    }

    /// <summary>
    /// Validates the configuration.
    /// </summary>
    /// <param name="config">The configuration.</param>
    /// <returns>A read-only dictionary of validation errors per property path.</returns>
    public IReadOnlyDictionary<string, IReadOnlyList<string>> Validate(TConfig config)
        => _service.Validate(config);

    /// <summary>
    /// Perform an action on the configuration on the saved configuration.
    /// </summary>
    /// <param name="path">The path to the configuration.</param>
    /// <param name="action">The action to perform.</param>
    /// <exception cref="InvalidConfigurationActionException">
    /// Thrown when an action is invalid. Be it because the action does not exist or because the path for where to look for the action is invalid.
    /// </exception>
    /// <returns>The result of the action.</returns>
    public ConfigurationActionResult PerformAction(string path, string action)
        => _service.PerformAction(Load(), path, action);

    /// <summary>
    /// Perform an action on the configuration on a configuration instance.
    /// </summary>
    /// <param name="config">The configuration instance to perform the action on.</param>
    /// <param name="path">The path to the configuration.</param>
    /// <param name="action">The action to perform.</param>
    /// <exception cref="InvalidConfigurationActionException">
    /// Thrown when an action is invalid. Be it because the action does not exist or because the path for where to look for the action is invalid.
    /// </exception>
    /// <returns>The result of the action.</returns>
    public ConfigurationActionResult PerformAction(TConfig config, string path, string action)
        => _service.PerformAction(config, path, action);

    /// <summary>
    /// Creates a new configuration instance.
    /// </summary>
    /// <returns>The new configuration.</returns>
    public TConfig New()
        => _service.New<TConfig>();

    /// <summary>
    /// Loads the current in-memory configuration instance if it exists, otherwise loads a new instance from disk or creates a new one from defaults.
    /// </summary>
    /// <param name="copy">Set to true to get a copy of the configuration instead of the in-memory cached instance.</param>
    /// <exception cref="ConfigurationValidationException">
    /// Thrown when a configuration fails validation.
    /// </exception>
    /// <returns>The configuration.</returns>
    public TConfig Load(bool copy = false)
        => _service.Load<TConfig>(copy);

    /// <summary>
    /// Saves the current in-memory configuration instance. Also triggers the <see cref="Saved"/> event if the configuration has changed.
    /// </summary>
    /// <exception cref="ConfigurationValidationException">
    /// Thrown when a configuration fails validation.
    /// </exception>
    /// <returns>A boolean indicating whether the configuration was saved to disk. <c>false</c> means there was no change to the configuration.</returns>
    public bool Save()
        => _service.Save<TConfig>();

    /// <summary>
    /// Saves the configuration instance and sets it as the current in-memory configuration. Also triggers the <see cref="Saved"/> event if the configuration has changed.
    /// </summary>
    /// <param name="config">The configuration.</param>
    /// <exception cref="ConfigurationValidationException">
    /// Thrown when a configuration fails validation.
    /// </exception>
    /// <returns>A boolean indicating whether the configuration was saved to disk. <c>false</c> means there was no change to the configuration.</returns>
    public bool Save(TConfig config)
        => _service.Save(config);
}
