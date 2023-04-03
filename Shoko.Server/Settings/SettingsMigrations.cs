using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace Shoko.Server.Settings;

public static class SettingsMigrations
{
    public const int Version = 1;

    /// <summary>
    /// Perform migrations on the settings json, pre-init
    /// </summary>
    /// <param name="settings">unparsed json settings</param>
    /// <returns>migrated still-unparsed settings</returns>
    public static string MigrateSettings(string settings)
    {
        var versionRegex = new Regex("(\"Version\"\\:\\s*)(\\d+)(,)", RegexOptions.Compiled);
        var versionString = versionRegex.Matches(settings).FirstOrDefault()?.Groups.Values.Skip(1).FirstOrDefault()?.Value;
        if (!int.TryParse(versionString, out var version)) version = 0;

        var migrationsToApply = s_migrations.Where(a => a.Key > version).OrderBy(a => a.Key).Select(a => a.Value);

        var result = migrationsToApply.Aggregate(settings, (current, migration) => migration(current));

        // update version, if exists. If it doesn't, it'll be updated with the default value from above in the next step
        result = versionRegex.Replace(result, $"$1{Version}$3");

        return result;
    }
    
    // Settings in, settings out
    private static readonly Dictionary<int, Func<string, string>> s_migrations = new()
    {
        { 1, MigrateTvDBLanguageEnum }
    };

    private static string MigrateTvDBLanguageEnum(string settings)
    {
        var regex = new Regex("(\"EpisodeTitleSource\":\\s*\")(TheTvDB)(\")", RegexOptions.Compiled);
        return regex.Replace(settings, "$1TvDB$3");
    }
}
