using System;
using System.Reflection;
using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Shoko.Plugin.Abstractions.Config;
using Shoko.Server.Hashing;
using Shoko.Server.MediaInfo;
using Shoko.Server.Server;
using Shoko.Server.Utilities;
using Formatting = Newtonsoft.Json.Formatting;

#nullable enable
namespace Shoko.Server.Settings;

public class SettingsProvider : ISettingsProvider
{
    private readonly ILogger<SettingsProvider> _logger;

    private readonly ConfigurationProvider<ServerSettings> _configurationProvider;

    public SettingsProvider(ILogger<SettingsProvider> logger, ConfigurationProvider<ServerSettings> configurationProvider)
    {
        _logger = logger;
        _configurationProvider = configurationProvider;
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

    private void DumpSettings(object obj, string path = "")
    {
        foreach (var prop in obj.GetType().GetProperties(BindingFlags.Instance | BindingFlags.Public))
        {
            var type = prop.PropertyType;
            if (type.FullName!.StartsWith("Shoko.Server") ||
                type.FullName!.StartsWith("Shoko.Models") ||
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

            if (prop.Name.ToLower().EndsWith("password") || prop.Name.ToLower().EndsWith("avdumpkey"))
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

    public void DebugSettingsToLog()
    {
        #region System Info

        _logger.LogInformation("-------------------- SYSTEM INFO -----------------------");

        try
        {
            var assembly = Assembly.GetEntryAssembly();
            var serverVersion = Utils.GetApplicationVersion(assembly);
            var extraVersionDict = Utils.GetApplicationExtraVersion(assembly);
            if (!extraVersionDict.TryGetValue("tag", out var tag))
                tag = null;

            if (!extraVersionDict.TryGetValue("commit", out var commit))
                commit = null;

            var releaseChannel = ReleaseChannel.Debug;
            if (extraVersionDict.TryGetValue("channel", out var rawChannel))
                if (Enum.TryParse<ReleaseChannel>(rawChannel, true, out var channel))
                    releaseChannel = channel;

            DateTime? releaseDate = null;
            if (extraVersionDict.TryGetValue("date", out var dateText) && DateTime.TryParse(dateText, out var releaseDate1))
                releaseDate = releaseDate1;

            _logger.LogInformation("Shoko Server Version: v{ApplicationVersion}, Channel: {Channel}, Tag: {Tag}, Commit: {Commit}", serverVersion,
                releaseChannel, tag, commit);
        }
        catch (Exception ex)
        {
            _logger.LogWarning("Error in log (server version lookup): {Ex}", ex);
        }

        _logger.LogInformation("Operating System: {OSInfo}", RuntimeInformation.OSDescription);

        try
        {
            var mediaInfoVersion = "**** MediaInfo Not found *****";

            var tempVersion = MediaInfoUtility.GetVersion();
            if (tempVersion != null) mediaInfoVersion = $"MediaInfo: {tempVersion}";
            _logger.LogInformation("{msg}", mediaInfoVersion);

            var hasherInfoVersion = "**** Hasher - DLL NOT found *****";

            tempVersion = CoreHashProvider.GetRhashVersion();
            if (tempVersion != null) hasherInfoVersion = $"RHash: {tempVersion}";
            _logger.LogInformation("{msg}", hasherInfoVersion);
        }
        catch (Exception ex)
        {
            _logger.LogError("Error in log (hasher / info): {Message}", ex.Message);
        }

        _logger.LogInformation("-------------------------------------------------------");

        #endregion

        _logger.LogInformation("----------------- SERVER SETTINGS ----------------------");

        DumpSettings(_configurationProvider.Load(), "Settings");

        _logger.LogInformation("-------------------------------------------------------");
    }
}
