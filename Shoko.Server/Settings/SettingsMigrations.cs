using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace Shoko.Server.Settings;

public static class SettingsMigrations
{
    public const int Version = 6;

    /// <summary>
    /// Perform migrations on the settings json, pre-init
    /// </summary>
    /// <param name="settings">unparsed json settings</param>
    /// <returns>migrated still-unparsed settings</returns>
    public static string MigrateSettings(string settings)
    {
        var versionRegex = new Regex("(\"SettingsVersion\"\\:\\s*)(\\d+)(,)", RegexOptions.Compiled);
        // first group is full match, second group is first group
        var versionString = versionRegex.Matches(settings).FirstOrDefault()?.Groups.Values.Skip(2).FirstOrDefault()?.Value;
        if (!int.TryParse(versionString, out var version)) version = 0;

        var migrationsToApply = s_migrations.Where(a => a.Key > version).OrderBy(a => a.Key).Select(a => a.Value);

        var result = migrationsToApply.Aggregate(settings, (current, migration) => migration(current));

        // update version, if exists. If it doesn't, it'll be updated with the default value from above in the next step
        result = versionRegex.Replace(result, $"${{1}}{Version}$3");

        return result;
    }

    // Settings in, settings out
    private static readonly Dictionary<int, Func<string, string>> s_migrations = new()
    {
        { 1, MigrateTvDBLanguageEnum },
        { 2, MigrateEpisodeLanguagePreference },
        { 3, MigrateAutoGroupRelations },
        { 4, MigrateHostnameToHost },
        { 5, MigrateAutoGroupRelationsAlternateToAlternative },
        { 6, MigrateAniDBServerAddresses }
    };

    private static string MigrateTvDBLanguageEnum(string settings)
    {
        var regex = new Regex("(\"EpisodeTitleSource\"\\:\\s*\")(TheTvDB)(\")", RegexOptions.Compiled);
        return regex.Replace(settings, "$1TvDB$3");
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
        ushort serverPort = 9001;

        var serverPortRegex = new Regex(@"(?<=""AniDb""\s*:\s*\{.*)""ServerPort""\s*:\s*(?<value>\d+)", RegexOptions.Compiled | RegexOptions.Singleline);
        var serverAddressRegex = new Regex(@"(?<=""AniDb""\s*:\s*\{.*)""ServerAddress""\s*:\s*""(?<value>\S+)""", RegexOptions.Compiled | RegexOptions.Singleline);

        var newSettings = serverPortRegex.Replace(settings, match =>
        {
            serverPort = ushort.Parse(match.Groups["value"].Value);
            return $"\"UDPServerPort\": {serverPort}";
        });

        return serverAddressRegex.Replace(newSettings, match =>
        {
            var serverAddress = match.Groups["value"].Value;
            // This is basically what MigrateAniDBToNet migration did in DatabaseFixes.cs, which is now made blank to remove ServerAddress from settings
            serverAddress = serverAddress.Replace("api.anidb.info", "api.anidb.net");
            return $"\"HTTPServerUrl\": \"http://{serverAddress}:{serverPort + 1}\",\"UDPServerAddress\": \"{serverAddress}\"";
        });
    }
}
