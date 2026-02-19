using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Shoko.Abstractions.Enums;
using Shoko.Abstractions.Extensions;
using Shoko.Abstractions.Plugin;

#nullable enable
namespace Shoko.Server.Settings;

public static partial class SettingsMigrations
{
    public static int Version => _migrations.Keys.Max();

    [GeneratedRegex("(\"SettingsVersion\"\\:\\s*)(\\d+)(,)", RegexOptions.Compiled)]
    private static partial Regex VersionRegex();

    /// <summary>
    /// Perform migrations on the settings json, pre-init
    /// </summary>
    /// <param name="settings">unparsed json settings</param>
    /// <param name="applicationPaths"></param>
    /// <returns>migrated still-unparsed settings</returns>
    public static string MigrateSettings(string settings, IApplicationPaths applicationPaths)
    {
        var versionRegex = VersionRegex();
        // first group is full match, second group is first group
        var versionString = versionRegex.Matches(settings).FirstOrDefault()?.Groups.Values.Skip(2).FirstOrDefault()?.Value;
        if (!int.TryParse(versionString, out var version)) version = 0;

        var migrationsToApply = _migrations
            .Where(a => a.Value is not null && a.Key > version)
            .OrderBy(a => a.Key)
            .Select(a => a.Value!)
            .ToList();
        if (migrationsToApply.Count == 0 && version == Version)
            return settings;

        var backupDir = Path.Combine(applicationPaths.DataPath, "SettingsBackup");
        Directory.CreateDirectory(backupDir);
        var dateNow = DateTime.Now;
        var fileName = $"settings-server.v{version}.{dateNow.Year:D4}{dateNow.Month:D2}{dateNow.Day:D2}{dateNow.Hour:D2}{dateNow.Minute:D2}.json";
        var backupFile = Path.Combine(backupDir, fileName);
        File.WriteAllText(backupFile, settings);

        var result = migrationsToApply.Aggregate(settings, (current, migration) => migration(current));

        // update version, if exists. If it doesn't, it'll be updated with the default value from above in the next step
        result = versionRegex.Replace(result, $"${{1}}{Version}$3");

        return result;
    }

    // Settings in, settings out
    private static readonly Dictionary<int, Func<string, string>?> _migrations = new()
    {
        { 1, MigrateTvDBLanguageEnum },
        { 2, MigrateEpisodeLanguagePreference },
        { 3, MigrateAutoGroupRelations },
        { 4, MigrateHostnameToHost },
        { 5, MigrateAutoGroupRelationsAlternateToAlternative },
        { 6, MigrateAniDBServerAddresses },
        { 7, MigrateLanguageSettings },
        { 8, MigrateRenamerFromImportToPluginsSettings },
        { 9, MigrateFixDefaultRenamer },
        { 10, MigrateLanguageSourceOrders },
        { 11, MigrateServerPortToWebPort },
        // Note: there are changes to how some of the settings store their values
        // in the file which are not backwards compatible, so add a no-op so the
        // settings file gets backed up in case the user wants to downgrade their
        // install.
        { 12, null },
    };

    private static string MigrateTvDBLanguageEnum(string settings)
    {
        var regex = new Regex("(\"EpisodeTitleSource\"\\:\\s*\")(TheTvDB)(\")", RegexOptions.Compiled);
        return regex.Replace(settings, "$1AniDB$3");
    }

    private static string MigrateEpisodeLanguagePreference(string settings)
    {
        var regex = new Regex(@"""(?<name>EpisodeLanguagePreference)"":(?<spacing>\s*)""(?<value>[^""]*)""", RegexOptions.Compiled);
        return regex.Replace(settings, match =>
        {
            var name = match.Groups["name"].Value;
            var spacing = match.Groups["spacing"].Value;
            var value = match.Groups["value"].Value;
            return $"\"{name}\":{spacing}[\"{string.Join($"\",{spacing}\"", value.Split(','))}\"]";
        });
    }

    private static string MigrateAutoGroupRelations(string settings)
    {
        var regex = new Regex(@"""(?<name>AutoGroupSeriesRelationExclusions)""\s*:(?<spacing>\s*)""(?<value>[^""]+)""", RegexOptions.Compiled);
        return regex.Replace(settings, match =>
        {
            var name = match.Groups["name"].Value;
            var spacing = match.Groups["spacing"].Value;
            var value = match.Groups["value"].Value;
            return $"\"{name}\":{spacing}[\"{string.Join($"\", \"", value.Split('|'))}\"]";
        });
    }

    private static string MigrateHostnameToHost(string settings)
    {
        var regex = new Regex(@"""[Hh]ost[Nn]ame""\s*:(?<spacing>\s*)""(?<value>[^""]+)""", RegexOptions.Compiled);
        return regex.Replace(settings, match =>
        {
            var spacing = match.Groups["spacing"].Value;
            var value = match.Groups["value"].Value;
            return $"\"Host\":{spacing}\"{value}\"";
        });
    }

    private static string MigrateAutoGroupRelationsAlternateToAlternative(string settings)
    {
        var regex = new Regex(@"(?<=""AutoGroupSeriesRelationExclusions""\s*:\s*\[)(?<value>[^\]]+)", RegexOptions.Compiled);
        return regex.Replace(settings, match => match.Groups["value"].Value.Replace("alternate", "alternative", StringComparison.InvariantCultureIgnoreCase));
    }

    private static string MigrateAniDBServerAddresses(string settings)
    {
        var currentSettings = JObject.Parse(settings);

        if (currentSettings["AniDb"] is null)
            return settings;

        var serverAddress = currentSettings["AniDb"]!["ServerAddress"]?.Value<string>() ?? "api.anidb.net";
        var serverPort = currentSettings["AniDb"]!["ServerPort"]?.Value<ushort>() ?? 9000;

        currentSettings["AniDb"]!["HTTPServerUrl"] = $"http://{serverAddress}:{serverPort + 1}";
        currentSettings["AniDb"]!["UDPServerAddress"] = serverAddress;
        currentSettings["AniDb"]!["UDPServerPort"] = serverPort;

        return currentSettings.ToString();
    }

    private static string MigrateLanguageSettings(string settings)
    {
        var currentSettings = JObject.Parse(settings);
        if (currentSettings["Language"] is not null)
            return settings;

        var seriesTitlePreference = (currentSettings["LanguagePreference"] as JArray)?.Values<string>() ?? [];
        var episodeTitlePreference = (currentSettings["EpisodeLanguagePreference"] as JArray)?.Values<string>() ?? [];
        var language = new LanguageSettings
        {
            UseSynonyms = currentSettings["LanguageUseSynonyms"]?.Value<bool>() ?? false,
            SeriesTitleLanguageOrder = seriesTitlePreference
                .Select(val => val!.GetTitleLanguage())
                .Except([TitleLanguage.None, TitleLanguage.Unknown])
                .Select(val => val.GetString())
                .ToList(),
            EpisodeTitleLanguageOrder = episodeTitlePreference
                .Select(val => val!.GetTitleLanguage())
                .Except([TitleLanguage.None, TitleLanguage.Unknown])
                .Select(val => val.GetString())
                .ToList(),
        };
        currentSettings["Language"] = JObject.Parse(JsonConvert.SerializeObject(language));

        return currentSettings.ToString();
    }

    private static string MigrateRenamerFromImportToPluginsSettings(string settings)
    {
        var currentSettings = JObject.Parse(settings);

        var importSettings = currentSettings["Import"];
        if (importSettings is null)
            return settings;

        var renameOnImport = importSettings["RenameOnImport"]?.Value<bool>() ?? false;
        var moveOnImport = importSettings["MoveOnImport"]?.Value<bool>() ?? false;
        var pluginsSettings = currentSettings["Plugins"] ?? (currentSettings["Plugins"] = new JObject());
        var renamerSettings = pluginsSettings["Renamer"] ?? (pluginsSettings["Renamer"] = new JObject());
        renamerSettings["RenameOnImport"] = renameOnImport;
        renamerSettings["MoveOnImport"] = moveOnImport;
        renamerSettings["EnabledRenamers"] = pluginsSettings["EnabledRenamers"] ?? new JObject();

        return currentSettings.ToString();
    }

    private static string MigrateFixDefaultRenamer(string settings)
    {
        var currentSettings = JObject.Parse(settings);

        if (currentSettings["Plugins"]?["Renamer"] is null)
            return settings;

        var renamerSettings = currentSettings["Plugins"]!["Renamer"]!;

        if (string.IsNullOrEmpty(renamerSettings["DefaultRenamer"]?.Value<string>()))
            renamerSettings["DefaultRenamer"] = "Default";

        return currentSettings.ToString();
    }

    private static string MigrateLanguageSourceOrders(string settings)
    {
        var currentSettings = JObject.Parse(settings);

        var languageSettings = currentSettings["Language"] ?? (currentSettings["Language"] = new JObject());

        languageSettings["SeriesTitleSourceOrder"] = new JArray
        {
            DataSource.AniDB, DataSource.TMDB
        };

        languageSettings["EpisodeTitleSourceOrder"] = new JArray
        {
            DataSource.AniDB, DataSource.TMDB
        };

        languageSettings["DescriptionSourceOrder"] = new JArray
        {
            DataSource.AniDB, DataSource.TMDB
        };

        return currentSettings.ToString();
    }

    private static string MigrateServerPortToWebPort(string settings)
    {
        var currentSettings = JObject.Parse(settings);
        var serverPort = currentSettings["ServerPort"]?.Value<ushort>() ?? 0;
        if (serverPort == 0)
            return settings;

        var webSettings = currentSettings["Web"] ?? (currentSettings["Web"] = new JObject());
        webSettings["Port"] = serverPort;
        currentSettings.Remove("ServerPort");

        return currentSettings.ToString();
    }
}
