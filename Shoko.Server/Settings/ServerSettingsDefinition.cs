using System;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Shoko.Plugin.Abstractions.Config;
using Shoko.Plugin.Abstractions.Events;
using Shoko.Server.Utilities;

#nullable enable
namespace Shoko.Server.Settings;

public class ServerSettingsDefinition : IDisposable, IConfigurationDefinitionWithCustomSaveLocation, IConfigurationDefinitionsWithMigrations
{
    private readonly ConfigurationProvider<ServerSettings> _configurationProvider;

    public Type ConfigurationType { get; } = typeof(ServerSettings);

    public string RelativePath => "settings-server.json";

    public ServerSettingsDefinition(ConfigurationProvider<ServerSettings> configurationProvider)
    {
        _configurationProvider = configurationProvider;
        _configurationProvider.Saved += OnSettingsSaved;
    }

    public void Dispose()
    {
        _configurationProvider.Saved -= OnSettingsSaved;
        GC.SuppressFinalize(this);
    }

    private void OnSettingsSaved(object? sender, ConfigurationSavedEventArgs<ServerSettings> eventArgs)
    {
        // Always update the trace logging settings when the settings change.
        Utils.SetTraceLogging(eventArgs.Configuration.TraceLog);

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
