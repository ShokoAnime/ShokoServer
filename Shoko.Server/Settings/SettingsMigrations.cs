using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace Shoko.Server.Settings;

public static class SettingsMigrations
{
    public const int Version = 4;

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
}
