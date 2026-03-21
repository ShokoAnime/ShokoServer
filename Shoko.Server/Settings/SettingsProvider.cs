using System;
using System.ComponentModel;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Shoko.Abstractions.Config;
using Shoko.Abstractions.Config.Events;
using Shoko.Server.Repositories.Cached;
using Shoko.Server.Repositories.Cached.AniDB;
using Shoko.Server.Services;
using Shoko.Server.Utilities;

using ISystemService = Shoko.Abstractions.Core.ISystemService;

#nullable enable
namespace Shoko.Server.Settings;

public class SettingsProvider : ISettingsProvider, IDisposable
{
    private readonly ILogger<SettingsProvider> _logger;

    private readonly ISystemService _systemService;

    private readonly ConfigurationProvider<ServerSettings> _configurationProvider;

    private string[]? _seriesTitleLanguageOrder = null;

    private string[]? _episodeTitleLanguageOrder = null;

    private string[]? _descriptionLanguageOrder = null;

    private bool _ready = false;

    public SettingsProvider(ILogger<SettingsProvider> logger, ISystemService systemService, ConfigurationProvider<ServerSettings> configurationProvider)
    {
        _logger = logger;
        _configurationProvider = configurationProvider;
        _configurationProvider.Saved += OnSettingsSaved;
        _systemService = systemService;
        _systemService.AboutToStart += OnSettingsReady;
    }

    public void Dispose()
    {
        _configurationProvider.Saved -= OnSettingsSaved;
        _systemService.AboutToStart -= OnSettingsReady;
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

        // Init language settings and react to changes.
        var shouldRenameAllGroups = false;
        if (_seriesTitleLanguageOrder is null)
        {
            _seriesTitleLanguageOrder = eventArgs.Configuration.Language.SeriesTitleLanguageOrder.ToArray();
        }
        else if (_ready && !eventArgs.Configuration.Language.SeriesTitleLanguageOrder.SequenceEqual(_seriesTitleLanguageOrder))
        {
            _seriesTitleLanguageOrder = eventArgs.Configuration.Language.SeriesTitleLanguageOrder.ToArray();
            Languages.PreferredNamingLanguages = [];

            // Reset all preferred titles when the language setting has been updated.
            var animeSeriesRepository = Utils.ServiceContainer.GetRequiredService<AnimeSeriesRepository>();
            var anidbAnimeRepository = Utils.ServiceContainer.GetRequiredService<AniDB_AnimeRepository>();
            Parallel.ForEach(animeSeriesRepository.GetAll(), new() { MaxDegreeOfParallelism = 10 }, series => series.ResetPreferredTitle());
            Parallel.ForEach(anidbAnimeRepository.GetAll(), new() { MaxDegreeOfParallelism = 10 }, anime => anime.ResetPreferredTitle());
            shouldRenameAllGroups = true;
        }

        if (_episodeTitleLanguageOrder is null)
        {
            _episodeTitleLanguageOrder = eventArgs.Configuration.Language.EpisodeTitleLanguageOrder.ToArray();
        }
        else if (_ready && !eventArgs.Configuration.Language.EpisodeTitleLanguageOrder.SequenceEqual(_episodeTitleLanguageOrder))
        {
            _episodeTitleLanguageOrder = eventArgs.Configuration.Language.EpisodeTitleLanguageOrder.ToArray();
            Languages.PreferredEpisodeNamingLanguages = [];
        }

        if (_descriptionLanguageOrder is null)
        {
            _descriptionLanguageOrder = eventArgs.Configuration.Language.DescriptionLanguageOrder.ToArray();
        }
        else if (_ready && !eventArgs.Configuration.Language.DescriptionLanguageOrder.SequenceEqual(_descriptionLanguageOrder))
        {
            _descriptionLanguageOrder = eventArgs.Configuration.Language.DescriptionLanguageOrder.ToArray();
            Languages.PreferredDescriptionNamingLanguages = [];

            // Reset all preferred overviews when the language setting has been updated.
            var animeSeriesRepository = Utils.ServiceContainer.GetRequiredService<AnimeSeriesRepository>();
            Parallel.ForEach(animeSeriesRepository.GetAll(), new() { MaxDegreeOfParallelism = 10 }, series => series.ResetPreferredOverview());
            shouldRenameAllGroups = true;
        }
        if (shouldRenameAllGroups)
        {
            var groupService = Utils.ServiceContainer.GetRequiredService<AnimeGroupService>();
            Task.Factory.StartNew(groupService.RenameAllGroups, TaskCreationOptions.LongRunning);
        }
    }

    public IServerSettings GetSettings(bool copy = false)
        => _configurationProvider.Load(copy);

    public void SaveSettings(IServerSettings settings)
    {
        if (settings is not ServerSettings serverSettings)
            return;

        _configurationProvider.Save(serverSettings);
    }

    public void SaveSettings()
        => _configurationProvider.Save();

    public static string Serialize(object obj, bool indent = false)
    {
        var serializerSettings = new JsonSerializerSettings
        {
            Formatting = indent ? Formatting.Indented : Formatting.None,
            DefaultValueHandling = DefaultValueHandling.Include,
            MissingMemberHandling = MissingMemberHandling.Ignore,
            Converters = [new StringEnumConverter()]
        };
        return JsonConvert.SerializeObject(obj, serializerSettings);
    }

    public void DebugSettingsToLog()
    {
        _logger.LogInformation("----------------- SERVER SETTINGS ----------------------");

        DumpSettings(_configurationProvider.Load(), "Settings");

        _logger.LogInformation("-------------------------------------------------------");
    }

    private void DumpSettings(object obj, string path = "")
    {
        foreach (var prop in obj.GetType().GetProperties(BindingFlags.Instance | BindingFlags.Public))
        {
            var type = prop.PropertyType;
            if (type.FullName!.StartsWith("Shoko.Server") ||
                type.FullName!.StartsWith("Shoko.Plugin"))
            {
                DumpSettings(prop.GetValue(obj)!, path + $".{prop.Name}");
                continue;
            }

            var value = prop.GetValue(obj)!;

            if (!IsPrimitive(type))
            {
                value = Serialize(value!);
            }

            if (prop.GetCustomAttribute<PasswordPropertyTextAttribute>() is not null)
            {
                value = "***HIDDEN***";
            }

            _logger.LogInformation("{Path}.{PropName}: {Value}", path, prop.Name, value);
        }
    }

    private static bool IsPrimitive(Type type)
    {
        if (type.IsPrimitive)
        {
            return true;
        }

        if (type.IsValueType)
        {
            return true;
        }

        return false;
    }
}
