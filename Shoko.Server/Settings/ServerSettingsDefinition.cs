using System;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Shoko.Plugin.Abstractions.Config;
using Shoko.Plugin.Abstractions.Events;
using Shoko.Server.Utilities;

#nullable enable
namespace Shoko.Server.Settings;

public class ServerSettingsDefinition : IDisposable, IConfigurationDefinitionWithCustomSaveLocation, IConfigurationDefinitionWithMigrations
{
    private readonly ConfigurationProvider<ServerSettings> _configurationProvider;

    private string[] _seriesTitleLanguageOrder = [];

    private string[] _episodeTitleLanguageOrder = [];

    private string[] _descriptionLanguageOrder = [];

    public Type ConfigurationType { get; } = typeof(ServerSettings);

    public string RelativePath => "settings-server.json";

    public ServerSettingsDefinition(ConfigurationProvider<ServerSettings> configurationProvider)
    {
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
        => OnSettingsSaved(sender, new ConfigurationSavedEventArgs<ServerSettings> { ConfigurationInfo = _configurationProvider.ConfigurationInfo, Configuration = _configurationProvider.Load() });

    private void OnSettingsSaved(object? sender, ConfigurationSavedEventArgs<ServerSettings> eventArgs)
    {
        // Always update the trace logging settings when the settings change.
        Utils.SetTraceLogging(eventArgs.Configuration.TraceLog);

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

        ShokoEventHandler.Instance.OnSettingsSaved();
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
}
