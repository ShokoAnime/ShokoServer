using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Namotion.Reflection;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Shoko.Plugin.Abstractions.Config;
using Shoko.Plugin.Abstractions.Config.Enums;
using Shoko.Plugin.Abstractions.Config.Exceptions;
using Shoko.Plugin.Abstractions.DataModels.Shoko;
using Shoko.Plugin.Abstractions.Events;
using Shoko.Server.Providers.AniDB.Interfaces;
using Shoko.Server.Utilities;

#nullable enable
namespace Shoko.Server.Settings;

public class ServerSettingsDefinition : IDisposable, IConfigurationDefinitionWithCustomSaveLocation, IConfigurationDefinitionWithCustomValidation<ServerSettings>, IConfigurationDefinitionWithMigrations, IConfigurationDefinitionWithCustomActions<ServerSettings>
{
    private readonly ILogger<ServerSettingsDefinition> _logger;

    private IUDPConnectionHandler? _udpHandler;

    private readonly ConfigurationProvider<ServerSettings> _configurationProvider;

    private string[] _seriesTitleLanguageOrder = [];

    private string[] _episodeTitleLanguageOrder = [];

    private string[] _descriptionLanguageOrder = [];

    private bool _ready = false;

    public Type ConfigurationType { get; } = typeof(ServerSettings);

    public string RelativePath => "settings-server.json";

    public ServerSettingsDefinition(ILogger<ServerSettingsDefinition> logger, ConfigurationProvider<ServerSettings> configurationProvider)
    {
        _logger = logger;
        _configurationProvider = configurationProvider;
        _configurationProvider.Saved += OnSettingsSaved;
        ShokoEventHandler.Instance.Started += OnSettingsReady;
    }

    public void Dispose()
    {
        _configurationProvider.Saved -= OnSettingsSaved;
        ShokoEventHandler.Instance.Started -= OnSettingsReady;
        GC.SuppressFinalize(this);
    }

    private void OnSettingsReady(object? sender, EventArgs eventArgs)
    {
        _ready = true;
        OnSettingsSaved(sender, new ConfigurationSavedEventArgs<ServerSettings> { ConfigurationInfo = _configurationProvider.ConfigurationInfo, Configuration = _configurationProvider.Load() });
    }

    private void OnSettingsSaved(object? sender, ConfigurationSavedEventArgs<ServerSettings> eventArgs)
    {
        // Always update the trace logging settings when the settings change.
        Utils.SetTraceLogging(eventArgs.Configuration.TraceLog);

        if (_ready)
        {
            if (!_seriesTitleLanguageOrder.SequenceEqual(eventArgs.Configuration.Language.SeriesTitleLanguageOrder))
            {
                _seriesTitleLanguageOrder = eventArgs.Configuration.Language.SeriesTitleLanguageOrder.ToArray();
                Languages.PreferredNamingLanguages = [];
            }

            if (!_episodeTitleLanguageOrder.SequenceEqual(eventArgs.Configuration.Language.EpisodeTitleLanguageOrder))
            {
                _episodeTitleLanguageOrder = eventArgs.Configuration.Language.EpisodeTitleLanguageOrder.ToArray();
                Languages.PreferredEpisodeNamingLanguages = [];
            }

            if (!_descriptionLanguageOrder.SequenceEqual(eventArgs.Configuration.Language.DescriptionLanguageOrder))
            {
                _descriptionLanguageOrder = eventArgs.Configuration.Language.DescriptionLanguageOrder.ToArray();
                Languages.PreferredDescriptionNamingLanguages = [];
            }
        }
    }

    public string ApplyMigrations(string config)
    {
        config = SettingsMigrations.MigrateSettings(config);
        config = FixNonEmittedDefaults(config);
        return config;
    }

    /// <summary>
    /// Fix the behavior of missing members in pre-4.0
    /// </summary>
    /// <param name="settings"></param>
    private static string FixNonEmittedDefaults(string settings)
    {
        if (settings.Contains("\"FirstRun\":")) return settings;
        var deserializerSettings = new JsonSerializerSettings
        {
            Converters = [new StringEnumConverter()],
            Error = (sender, args) => { args.ErrorContext.Handled = true; },
            ObjectCreationHandling = ObjectCreationHandling.Replace,
            MissingMemberHandling = MissingMemberHandling.Ignore,
            DefaultValueHandling = DefaultValueHandling.Populate
        };
        var serializerSettings = new JsonSerializerSettings
        {
            Formatting = Formatting.Indented,
            DefaultValueHandling = DefaultValueHandling.Include,
            MissingMemberHandling = MissingMemberHandling.Ignore,
            Converters = [new StringEnumConverter()]
        };
        var result = JsonConvert.DeserializeObject<ServerSettings>(settings, deserializerSettings);
        return JsonConvert.SerializeObject(result, serializerSettings);
    }

    public ConfigurationActionResult PerformAction(ServerSettings config, string path, string action, ContextualType type, IShokoUser? user, Uri? uri)
    {
        return (path, action) switch
        {
            ("AniDb", "Test") => TestAnidbLogin(config),
            _ => throw new InvalidConfigurationActionException($"Invalid path \"{path}\"", nameof(path)),
        };
    }

    private ConfigurationActionResult TestAnidbLogin(ServerSettings config)
    {
        var currentConfig = _configurationProvider.Load();
        try
        {
            _udpHandler = Utils.ServiceContainer.GetRequiredService<IUDPConnectionHandler>();
            if (!_udpHandler.IsAlive)
                _udpHandler.Init(config.AniDb.Username, config.AniDb.Password, currentConfig.AniDb.UDPServerAddress, currentConfig.AniDb.UDPServerPort, currentConfig.AniDb.ClientPort);
            else
                _udpHandler.ForceLogout();

            if (!_udpHandler.TestLogin(config.AniDb.Username, config.AniDb.Password))
            {
                _logger.LogInformation("Failed AniDB Login and Connection");
                return new("Unable to log in with the provided credentials.", DisplayColorTheme.Warning) { RefreshConfiguration = false };
            }
            return new("AniDB Login Successful!", DisplayColorTheme.Important) { RefreshConfiguration = false };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "AniDB Login Failed!");
            return new("AniDB Login Failed!", DisplayColorTheme.Danger) { RefreshConfiguration = false };
        }
    }

    public IReadOnlyDictionary<string, IReadOnlyList<string>> Validate(ServerSettings config)
    {
        var dictionary = new Dictionary<string, IReadOnlyList<string>>();
        if (!config.FirstRun && config.AniDb.Username is not { Length: > 0 })
            dictionary.Add("AniDb.Username", ["AniDb.Username cannot be empty or null if FirstRun is set to false."]);
        if (!config.FirstRun && config.AniDb.Password is not { Length: > 0 })
            dictionary.Add("AniDb.Password", ["AniDb.Password cannot be empty or null if FirstRun is set to false."]);
        return dictionary;
    }
}
