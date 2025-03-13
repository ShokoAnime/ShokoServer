using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using Namotion.Reflection;
using Shoko.Plugin.Abstractions;
using Shoko.Plugin.Abstractions.Config;
using Shoko.Plugin.Abstractions.Config.Enums;
using Shoko.Plugin.Abstractions.Config.Exceptions;
using Shoko.Plugin.Abstractions.Events;
using Shoko.Plugin.Abstractions.Services;

namespace Shoko.Plugin.ConfigurationHell.Configurations;

/// <summary>
/// Definition for the Shokofin configuration.
/// </summary>
public class ShokofinConfigurationDefinition
    : IConfigurationDefinitionWithCustomValidation<ShokofinConfiguration>, IConfigurationDefinitionWithCustomActions<ShokofinConfiguration>, IConfigurationDefinitionWithNewFactory<ShokofinConfiguration>, IDisposable
{
    private readonly IUserService _userService;

    private readonly IVideoService _videoService;

    private readonly IShokoEventHandler _shokoEventHandler;

    private readonly ConfigurationProvider<ShokofinConfiguration> _configurationProvider;

    private ShokofinConfiguration? _lastConfiguration = null;

    /// <inheritdoc />
    public Type ConfigurationType { get; } = typeof(ShokofinConfiguration);

    /// <summary>
    /// Initializes a new instance of the <see cref="ShokofinConfigurationDefinition"/> class.
    /// </summary>
    /// <param name="userService">The user service.</param>
    /// <param name="videoService">The video service.</param>
    /// <param name="shokoEventHandler">The shoko event emitter.</param>
    /// <param name="configurationProvider">The configuration provider.</param>
    public ShokofinConfigurationDefinition(IUserService userService, IVideoService videoService, IShokoEventHandler shokoEventHandler, ConfigurationProvider<ShokofinConfiguration> configurationProvider)
    {
        _userService = userService;
        _videoService = videoService;
        _shokoEventHandler = shokoEventHandler;
        _configurationProvider = configurationProvider;
        _configurationProvider.Saved += OnSaved;
        _shokoEventHandler.Started += OnReady;
    }

    /// <inheritdoc />
    public void Dispose()
    {
        _configurationProvider.Saved -= OnSaved;
        _shokoEventHandler.Started -= OnReady;
        GC.SuppressFinalize(this);
    }

    private void OnReady(object? sender, EventArgs eventArgs)
        => _lastConfiguration = _configurationProvider.Load();

    private void OnSaved(object? sender, ConfigurationSavedEventArgs<ShokofinConfiguration> eventArgs)
        => _lastConfiguration = eventArgs.Configuration;

    /// <inheritdoc />
    public IReadOnlyDictionary<string, IReadOnlyList<string>> Validate(ShokofinConfiguration config)
    {
        if (_lastConfiguration is null)
            return new Dictionary<string, IReadOnlyList<string>>();
        var dict = new Dictionary<string, List<string>>();

        if (!string.IsNullOrEmpty(config.Connection.ApiKey) && !string.IsNullOrEmpty(config.Connection.Password))
        {
            dict.Add("Connection.ApiKey", ["API Key and Password cannot both be set."]);
            dict.Add("Connection.Password", ["API Key and Password cannot both be set."]);
        }

        return dict
            .ToDictionary(a => a.Key, a => a.Value.ToList() as IReadOnlyList<string>);
    }

    /// <inheritdoc />
    public ShokofinConfiguration New()
    {
        var config = new ShokofinConfiguration();
        foreach (var user in _userService.GetUsers())
            config.Users.Add(new() { Key = user.Username });
        foreach (var folder in _videoService.GetAllManagedFolders())
        {
            config.Library.ManagedFolders.Add(new() { Key = folder.Name, Paths = [$"{folder.Path} | {folder.Name} ({folder.ID})"] });
        }
        return config;
    }

    /// <inheritdoc />
    public ConfigurationActionResult PerformAction(ShokofinConfiguration config, ContextualType type, string path, string action)
        => path switch
        {
            "Connection" => action switch
            {
                "Connect" => ConnectToShoko(config),
                "Disconnect" => DisconnectFromShoko(config),
                _ => throw new InvalidConfigurationActionException($"Invalid action \"{action}\"", nameof(action)),
            },
            "SignalR.Connection" => action switch
            {
                "Connect" => ConnectToSignalR(config),
                "Disconnect" => DisconnectFromSignalR(config),
                _ => throw new InvalidConfigurationActionException($"Invalid action \"{action}\"", nameof(action)),
            },
            _ => action switch
            {
                "Save" => SaveConfig(config),
                _ => throw new InvalidConfigurationActionException($"Invalid path \"{path}\"", nameof(path)),
            }
        };

    // Fake connect
    private ConfigurationActionResult ConnectToShoko(ShokofinConfiguration config)
    {
        var validationErrors = new Dictionary<string, IReadOnlyList<string>>();
        if (config.Connection.Host is not "http://localhost:8111")
            validationErrors.Add("Connection.Host", ["Host must be exactly 'http://localhost:8111'"]);
        if (config.Connection.Username is not "Default")
            validationErrors.Add("Connection.Username", ["Username must be exactly 'Default'"]);
        if (config.Connection.Password is not null and not "")
            validationErrors.Add("Connection.Password", ["Password must be empty."]);
        if (validationErrors.Count > 0)
            throw new ConfigurationValidationException("validate", _configurationProvider.ConfigurationInfo, validationErrors);

        config.Connection.ApiKey = "fake";
        config.Connection.Password = null;
        config.Connection.ServerVersion = typeof(ConfigurationActionResult).Assembly.GetName().Version ?? new Version(0, 0);
        _configurationProvider.Save(config);
        return new("Connected to Shoko.");
    }

    // Fake disconnect
    private ConfigurationActionResult DisconnectFromShoko(ShokofinConfiguration config)
    {
        config.Connection.ApiKey = null;
        config.Connection.Password = null;
        config.Connection.ServerVersion = null;
        config.SignalR.Connection.Enabled = false;
        config.SignalR.Connection.Status = "Disabled";
        _configurationProvider.Save(config);
        return new("Disconnected from Shoko.");
    }

    private ConfigurationActionResult ConnectToSignalR(ShokofinConfiguration config)
    {
        config.SignalR.Connection.Enabled = true;
        config.SignalR.Connection.Status = "Enabled, Connected";
        _configurationProvider.Save(config);
        return new("Connected to SignalR.");
    }

    private ConfigurationActionResult DisconnectFromSignalR(ShokofinConfiguration config)
    {
        config.SignalR.Connection.Enabled = false;
        config.SignalR.Connection.Status = "Disabled";
        _configurationProvider.Save(config);
        return new("Disconnected from SignalR.");
    }

    // Real save.
    private ConfigurationActionResult SaveConfig(ShokofinConfiguration config)
    {
        _configurationProvider.Save(config);
        return new(true);
    }
}
