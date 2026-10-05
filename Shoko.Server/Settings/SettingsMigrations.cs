using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Linq;
using Shoko.Abstractions.Extensions;
using Shoko.Abstractions.Metadata;
using Shoko.Abstractions.Metadata.Enums;
using Shoko.Abstractions.Plugin;
using Shoko.Server.Databases;
using Shoko.Server.Server;

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
    /// <param name="applicationPaths">The server's paths, for the migrations that write files beside the settings.</param>
    /// <returns>migrated still-unparsed settings</returns>
    public static string MigrateSettings(string settings, IApplicationPaths applicationPaths)
    {
        var versionRegex = VersionRegex();
        // first group is full match, second group is first group
        var versionString = versionRegex.Matches(settings).FirstOrDefault()?.Groups.Values.Skip(2).FirstOrDefault()?.Value;
        if (!int.TryParse(versionString, out var version)) version = 0;

        var migrationsToApply = _migrations
            .Where(a => a.Key > version && a.Value is not null)
            .OrderBy(a => a.Key)
            .Select(a => a.Value!)
            .ToList();
        if (migrationsToApply.Count == 0 && version == Version)
            return settings;

        var backupDir = Path.Combine(applicationPaths.DataPath, "SettingsBackup");
        Directory.CreateDirectory(backupDir);
        var dateNow = DateTime.Now;
        var fileName = $"settings-server.v{version}.{dateNow:yyyyMMddHHmm}.json";
        var backupFile = Path.Combine(backupDir, fileName);
        File.WriteAllText(backupFile, settings);

        var result = migrationsToApply.Aggregate(settings, (current, migration) => migration(current, applicationPaths));

        // update version, if exists. If it doesn't, it'll be updated with the default value from above in the next step
        result = versionRegex.Replace(result, $"${{1}}{Version}$3");

        return result;
    }

    // Settings and the application paths in, settings out
    private static readonly Dictionary<int, Func<string, IApplicationPaths, string>?> _migrations = new()
    {
        { 1, (settings, _) => MigrateTvDBLanguageEnum(settings) },
        { 2, (settings, _) => MigrateEpisodeLanguagePreference(settings) },
        { 3, (settings, _) => MigrateAutoGroupRelations(settings) },
        { 4, (settings, _) => MigrateHostnameToHost(settings) },
        { 5, (settings, _) => MigrateAutoGroupRelationsAlternateToAlternative(settings) },
        { 6, (settings, _) => MigrateAniDBServerAddresses(settings) },
        { 7, (settings, _) => MigrateLanguageSettings(settings) },
        { 8, (settings, _) => MigrateRenamerFromImportToPluginsSettings(settings) },
        { 9, (settings, _) => MigrateFixDefaultRenamer(settings) },
        { 10, (settings, _) => MigrateLanguageSourceOrders(settings) },
        { 11, (settings, _) => MigrateServerPortToWebPort(settings) },
        // Note: there are changes to how some of the settings store their values
        // in the file which are not backwards compatible, so add a no-op so the
        // settings file gets backed up in case the user wants to downgrade their
        // install.
        { 12, null },
        { 13, (settings, _) => MigrateLogRotatorToLogging(settings) },
        { 14, (settings, _) => MigrateTraceLogToLogging(settings) },
        { 15, (settings, paths) => MigrateQuartzToQueue(settings, paths.DataPath) },
        { 16, (settings, _) => MigrateDefaultRenamerToStatic(settings) },
        { 17, (settings, _) => MigrateReleaseSignalTypeNames(settings) },
        { 18, (settings, _) => MigrateTmdbIncrementalChangesWindow(settings) },
        { 19, (settings, _) => MigrateAniDbMyListToOwnObject(settings) },
        { 20, (settings, paths) => MigrateAutoLinkToMetadataService(settings, paths.DataPath) },
        { 21, (settings, _) => MigrateSourceNamesToValues(settings) },
        { 22, (settings, _) => MigrateDropPluginImageTemplateUrls(settings) },
        { 23, (settings, _) => MigrateTmdbImageSettingsToMetadataSource(settings) },
        { 24, (settings, _) => MigrateTmdbAutoPurgeToMetadata(settings) },
        { 25, (settings, paths) => MigrateUpdateFrequenciesToScheduledActions(settings, paths.DataPath) },
        { 26, MigrateTmdbSettingsToPlugin },
        { 27, (settings, paths) => MigrateStartupSettingsToTriggers(settings, paths.DataPath) },
        { 28, MigrateTmdbDownloadSwitchesToKinds },
    };

    /// <summary>
    /// Legacy default relocation preset name, captured from settings JSON before
    /// the property was removed from the settings class. Used by DB migrations
    /// to preserve which preset was configured as the default.
    /// </summary>
    internal static string? MigratedDefaultRenamer { get; set; }

    /// <summary>
    ///   An auto-link decision carried over from before migration 20, for the
    ///   metadata provider manager to apply when it seeds.
    /// </summary>
    /// <param name="AutoLink">The old <c>AutoLink</c> value, if it was set.</param>
    /// <param name="AutoLinkRestricted">The old <c>AutoLinkRestricted</c> value, if it was set.</param>
    internal sealed record AutoLinkCarryOver(bool? AutoLink, bool? AutoLinkRestricted);

    /// <summary>
    ///   Where migration 20 leaves the values it takes out of the settings
    ///   file, so a first boot that fails before seeding does not lose them.
    /// </summary>
    /// <param name="dataPath">The server's data path.</param>
    /// <returns>The carry-over file's path.</returns>
    internal static string AutoLinkCarryOverPath(string dataPath)
        => Path.Combine(dataPath, "SettingsBackup", "auto-link.v19.json");

    /// <summary>
    ///   Reads what migration 20 carried over, keyed by source.
    /// </summary>
    /// <param name="dataPath">The server's data path.</param>
    /// <returns>
    ///   The carried-over decisions, or an empty map when there is no file,
    ///   either because nothing was migrated or because it was already applied.
    /// </returns>
    internal static IReadOnlyDictionary<MetadataSource, AutoLinkCarryOver> ReadAutoLinkCarryOver(string dataPath)
    {
        var path = AutoLinkCarryOverPath(dataPath);
        return File.Exists(path)
            ? JsonConvert.DeserializeObject<Dictionary<MetadataSource, AutoLinkCarryOver>>(File.ReadAllText(path)) ?? []
            : new Dictionary<MetadataSource, AutoLinkCarryOver>();
    }

    /// <summary>
    ///   Removes the carry-over once it has been applied and saved.
    /// </summary>
    /// <param name="dataPath">The server's data path.</param>
    internal static void ClearAutoLinkCarryOver(string dataPath)
    {
        var path = AutoLinkCarryOverPath(dataPath);
        if (File.Exists(path))
            File.Delete(path);
    }

    /// <summary>
    ///   Takes TMDB's auto-link settings out of its section and writes them to
    ///   the carry-over file, since the metadata service's own settings cannot
    ///   be written before it exists.
    /// </summary>
    /// <param name="settings">The settings JSON being migrated.</param>
    /// <param name="dataPath">
    ///   The server's data path. The carry-over sits beside the settings
    ///   backup and stays until the provider manager has applied and saved it,
    ///   so a first boot that fails before then keeps the values for the next.
    /// </param>
    /// <returns>The settings JSON without the two keys.</returns>
    private static string MigrateAutoLinkToMetadataService(string settings, string dataPath)
    {
        var currentSettings = JObject.Parse(settings);
        var tmdb = currentSettings["TMDB"] as JObject;
        var carry = new AutoLinkCarryOver(Take(tmdb, "AutoLink"), Take(tmdb, "AutoLinkRestricted"));
        if (carry.AutoLink is not null || carry.AutoLinkRestricted is not null)
        {
            var path = AutoLinkCarryOverPath(dataPath);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            var carried = new Dictionary<MetadataSource, AutoLinkCarryOver> { [MetadataSource.TMDB] = carry };
            File.WriteAllText(path, JsonConvert.SerializeObject(carried, Formatting.Indented));
        }

        return currentSettings.ToString();

        static bool? Take(JObject? parent, string name)
        {
            if (parent?.Property(name) is not { } property)
                return null;

            var value = property.Value.Type is JTokenType.Boolean ? property.Value.Value<bool>() : (bool?)null;
            property.Remove();
            return value;
        }
    }

    #region Update Frequencies

    /// <summary>
    ///   The carry-over key of <c>AniDb.Calendar_UpdateFrequency</c>.
    /// </summary>
    internal const string AnidbCalendarFrequency = "AniDb.Calendar";

    /// <summary>
    ///   The carry-over key of <c>AniDb.Anime_UpdateFrequency</c>.
    /// </summary>
    internal const string AnidbAnimeFrequency = "AniDb.Anime";

    /// <summary>
    ///   The carry-over key of <c>AniDb.File_UpdateFrequency</c>.
    /// </summary>
    internal const string AnidbFileFrequency = "AniDb.File";

    /// <summary>
    ///   The carry-over key of <c>AniDb.Notification_UpdateFrequency</c>.
    /// </summary>
    internal const string AnidbNotificationFrequency = "AniDb.Notification";

    /// <summary>
    ///   The carry-over key of <c>AniDb.MyList.UpdateFrequency</c>.
    /// </summary>
    internal const string AnidbMylistFrequency = "AniDb.MyList";

    /// <summary>
    ///   The carry-over key of <c>Plugins.Updates.AutoUpdateFrequency</c>.
    /// </summary>
    internal const string PluginUpdatesFrequency = "Plugins.Updates";

    /// <summary>
    ///   Where migration 25 leaves the update frequencies it takes out of the
    ///   settings file, until the action scheduler has made them triggers.
    /// </summary>
    /// <param name="dataPath">The server's data path.</param>
    /// <returns>The carry-over file's path.</returns>
    internal static string UpdateFrequencyCarryOverPath(string dataPath)
        => Path.Combine(dataPath, "SettingsBackup", "update-frequencies.v24.json");

    /// <summary>
    ///   Reads the update frequencies migration 25 carried over. A file that
    ///   cannot be read is renamed aside, so it is not read on every start.
    /// </summary>
    /// <param name="dataPath">The server's data path.</param>
    /// <param name="logger">Told when the file cannot be read, or <c>null</c>.</param>
    /// <returns>
    ///   The frequencies in hours by carry-over key, 0 for never, or an empty
    ///   map when there is no file, either because nothing was migrated or
    ///   because it was already applied, or when it cannot be read.
    /// </returns>
    internal static IReadOnlyDictionary<string, int> ReadUpdateFrequencyCarryOver(string dataPath, ILogger? logger = null)
        => ReadCarryOver<Dictionary<string, int>>(UpdateFrequencyCarryOverPath(dataPath), "update frequencies", logger) ?? [];

    /// <summary>
    ///   Removes the update frequency carry-over once it has been applied and
    ///   saved.
    /// </summary>
    /// <param name="dataPath">The server's data path.</param>
    internal static void ClearUpdateFrequencyCarryOver(string dataPath)
    {
        var path = UpdateFrequencyCarryOverPath(dataPath);
        if (File.Exists(path))
            File.Delete(path);
    }

    /// <summary>
    ///   Takes the update frequencies of the AniDB and plugin update jobs out of
    ///   the settings and writes them to the carry-over file, in hours, for the
    ///   action scheduler to make them the triggers of the actions that run
    ///   those jobs, since the schedule lives in the database, which cannot be
    ///   written yet.
    /// </summary>
    /// <param name="settings">The settings JSON being migrated.</param>
    /// <param name="dataPath">
    ///   The server's data path. The carry-over sits beside the settings
    ///   backup and stays until the scheduler has applied and saved it, so a
    ///   first boot that fails before then keeps the values for the next.
    /// </param>
    /// <returns>The settings JSON without the frequencies.</returns>
    private static string MigrateUpdateFrequenciesToScheduledActions(string settings, string dataPath)
    {
        var currentSettings = JObject.Parse(settings);
        var anidb = currentSettings["AniDb"] as JObject;
        var taken = new Dictionary<string, int>();
        Take(anidb, "Calendar_UpdateFrequency", AnidbCalendarFrequency);
        Take(anidb, "Anime_UpdateFrequency", AnidbAnimeFrequency);
        Take(anidb, "File_UpdateFrequency", AnidbFileFrequency);
        Take(anidb, "Notification_UpdateFrequency", AnidbNotificationFrequency);
        Take(anidb?["MyList"] as JObject, "UpdateFrequency", AnidbMylistFrequency);
        Take(currentSettings["Plugins"]?["Updates"] as JObject, "AutoUpdateFrequency", PluginUpdatesFrequency);
        if (taken.Count > 0)
        {
            var path = UpdateFrequencyCarryOverPath(dataPath);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, JsonConvert.SerializeObject(taken, Formatting.Indented));
        }

        return currentSettings.ToString();

        void Take(JObject? parent, string name, string key)
        {
            if (parent?.Property(name) is not { } property)
                return;

            property.Remove();
            if (ToHours(property.Value) is { } hours)
                taken[key] = hours;
        }

        static int? ToHours(JToken value)
        {
            ScheduledUpdateFrequency? frequency = value.Type switch
            {
                JTokenType.String when Enum.TryParse<ScheduledUpdateFrequency>(value.Value<string>(), true, out var named) && Enum.IsDefined(named) => named,
                JTokenType.Integer when Enum.IsDefined((ScheduledUpdateFrequency)value.Value<int>()) => (ScheduledUpdateFrequency)value.Value<int>(),
                _ => null,
            };
            return frequency switch
            {
                null => null,
                ScheduledUpdateFrequency.Never => 0,
                { } known => known.Hours,
            };
        }
    }

    /// <summary>
    ///   Reads a carry-over file. One that cannot be read is renamed aside, so
    ///   it is not read on every start.
    /// </summary>
    /// <typeparam name="T">What the file holds.</typeparam>
    /// <param name="path">The file.</param>
    /// <param name="what">What it holds, for the log.</param>
    /// <param name="logger">Told when the file cannot be read, or <c>null</c>.</param>
    /// <returns>What it holds, or <c>null</c> when there is no file or it cannot be read.</returns>
    private static T? ReadCarryOver<T>(string path, string what, ILogger? logger) where T : class
    {
        if (!File.Exists(path))
            return null;

        try
        {
            return JsonConvert.DeserializeObject<T>(File.ReadAllText(path));
        }
        catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException)
        {
            logger?.LogWarning(ex, "Could not read the carried-over {What} in {Path}; the scheduled actions keep their triggers", what, path);
            try
            {
                File.Move(path, path + ".unreadable", true);
            }
            catch (Exception moveEx) when (moveEx is IOException or UnauthorizedAccessException)
            {
                logger?.LogWarning(moveEx, "Could not move the unreadable carry-over {Path} aside", path);
            }

            return null;
        }
    }

    #endregion

    #region TMDB Plugin

    /// <summary>
    ///   The TMDB plugin's ID, the same as its <c>Plugin.ID</c>.
    /// </summary>
    internal static readonly Guid TmdbPluginID = new("85d0c34f-240f-4da3-8512-d0b45118a9f4");

    /// <summary>
    ///   The keys of the <c>TMDB</c> section that the TMDB plugin's
    ///   configuration reads, spelled the same in both.
    /// </summary>
    internal static readonly string[] TmdbPluginConfigurationKeys =
    [
        "ConsiderExistingOtherLinks",
        "DownloadAllTitles",
        "DownloadAllOverviews",
        "DownloadAllContentRatings",
        "AutoDownloadCrewAndCast",
        "AutoDownloadCollections",
        "AutoDownloadAlternateOrdering",
        "AutoDownloadNetworks",
        "UserApiKey",
        "IncrementalChangesWindowDays",
        "AutoSearchShowCandidateCount",
        "AutoSearchMovieCandidateCount",
        "RateLimit",
    ];

    /// <summary>
    ///   Where the TMDB plugin keeps its configuration.
    /// </summary>
    /// <param name="applicationPaths">The application paths.</param>
    /// <returns>The file, <c>tmdb.json</c> in the plugin's configuration folder.</returns>
    internal static string TmdbPluginConfigurationPath(IApplicationPaths applicationPaths)
        => Path.Join(PluginPathRules.GetConfigurationsPath(applicationPaths, TmdbPluginID), "tmdb.json");

    /// <summary>
    ///   Moves the <c>TMDB</c> section to the TMDB plugin's configuration file,
    ///   and its <c>ImageCdnUrl</c> to the image template URL for TMDB, as
    ///   migration 26. The user's API key moves as it is, without being logged.
    /// </summary>
    /// <remarks>
    ///   An existing plugin file is kept, and a section with nothing to carry
    ///   writes none. With the section gone, a second run changes nothing.
    /// </remarks>
    /// <param name="settings">The settings JSON being migrated.</param>
    /// <param name="applicationPaths">The application paths, for the plugin's file.</param>
    /// <returns>The settings JSON without the <c>TMDB</c> section.</returns>
    internal static string MigrateTmdbSettingsToPlugin(string settings, IApplicationPaths applicationPaths)
    {
        var currentSettings = JObject.Parse(settings);
        if (currentSettings.Property("TMDB") is not { } section)
            return settings;

        section.Remove();
        if (section.Value is not JObject tmdb)
            return currentSettings.ToString();

        if (tmdb.Property("ImageCdnUrl") is { } imageCdnUrl && ToTmdbImageTemplate(imageCdnUrl.Value) is { } template)
            AddTmdbImageTemplate(currentSettings, template);

        var configuration = new JObject();
        foreach (var key in TmdbPluginConfigurationKeys)
        {
            if (tmdb.Property(key) is { } property)
                configuration[key] = property.Value.DeepClone();
        }

        var path = TmdbPluginConfigurationPath(applicationPaths);
        if (configuration.Count > 0 && !File.Exists(path))
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, configuration.ToString());
        }

        return currentSettings.ToString();
    }

    /// <summary>
    ///   Turns TMDB's image CDN URL into a template, as the image manager
    ///   reads it: a URL with a <c>{0}</c> as it is, else a base URL for the
    ///   original size.
    /// </summary>
    /// <param name="value">The <c>ImageCdnUrl</c> value.</param>
    /// <returns>The template, or <c>null</c> when the value is not an http or https URL.</returns>
    private static string? ToTmdbImageTemplate(JToken value)
    {
        if (value.Type is not JTokenType.String || value.Value<string>()?.Trim() is not { Length: > 0 } url)
            return null;

        var template = url.Contains("{0}") ? url : url.EndsWith('/') ? $"{url}original/{{0}}" : $"{url}/original/{{0}}";
        return Uri.TryCreate(template, UriKind.Absolute, out var uri) && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps)
            ? template
            : null;
    }

    /// <summary>
    ///   Adds a TMDB entry to the image template URLs, unless one is there,
    ///   which already took precedence over the CDN URL.
    /// </summary>
    /// <param name="settings">The settings being migrated.</param>
    /// <param name="template">The template URL.</param>
    private static void AddTmdbImageTemplate(JObject settings, string template)
    {
        if (settings["Image"] is not JObject image)
            settings["Image"] = image = new JObject();
        if (image["ImageTemplateUrls"] is not JArray templates)
            image["ImageTemplateUrls"] = templates = new JArray();

        var exists = templates.OfType<JObject>().Any(entry =>
            entry["ImageSource"]?.Type is JTokenType.String &&
            string.Equals(entry["ImageSource"]!.Value<string>(), MetadataSource.TMDB.Value, StringComparison.OrdinalIgnoreCase)
        );
        if (!exists)
            templates.Add(new JObject { ["ImageSource"] = MetadataSource.TMDB.Value, ["TemplateUrl"] = template });
    }

    #endregion

    #region TMDB Download Switches

    /// <summary>
    ///   The TMDB plugin's download switches that became provider kinds, and
    ///   the kind each turns off when it was off.
    /// </summary>
    internal static readonly IReadOnlyList<(string Key, MetadataEntityType Kind)> TmdbDownloadSwitchKinds =
    [
        ("AutoDownloadCrewAndCast", MetadataEntityType.Creator),
        ("AutoDownloadCollections", MetadataEntityType.Collection),
        ("AutoDownloadNetworks", MetadataEntityType.Network),
    ];

    /// <summary>
    ///   Where migration 28 leaves the kinds it turned off, until the metadata
    ///   provider manager has applied and saved them.
    /// </summary>
    /// <param name="dataPath">The server's data path.</param>
    /// <returns>The carry-over file's path.</returns>
    internal static string KindsOffCarryOverPath(string dataPath)
        => Path.Combine(dataPath, "SettingsBackup", "kinds-off.v27.json");

    /// <summary>
    ///   Reads the kinds migration 28 turned off. A file that cannot be read
    ///   is renamed aside, so it is not read on every start.
    /// </summary>
    /// <param name="dataPath">The server's data path.</param>
    /// <param name="logger">Told when the file cannot be read, or <c>null</c>.</param>
    /// <returns>
    ///   The kinds by source, or an empty map when there is no file, either
    ///   because nothing was migrated or because it was already applied, or
    ///   when it cannot be read.
    /// </returns>
    internal static IReadOnlyDictionary<MetadataSource, IReadOnlyList<MetadataEntityType>> ReadKindsOffCarryOver(string dataPath, ILogger? logger = null)
        => ReadCarryOver<Dictionary<MetadataSource, List<MetadataEntityType>>>(KindsOffCarryOverPath(dataPath), "kinds turned off", logger)?
            .ToDictionary(pair => pair.Key, pair => (IReadOnlyList<MetadataEntityType>)pair.Value)
            ?? [];

    /// <summary>
    ///   Removes the kinds-off carry-over once it has been applied and saved.
    /// </summary>
    /// <param name="dataPath">The server's data path.</param>
    internal static void ClearKindsOffCarryOver(string dataPath)
    {
        var path = KindsOffCarryOverPath(dataPath);
        if (File.Exists(path))
            File.Delete(path);
    }

    /// <summary>
    ///   Takes the TMDB plugin's download switches for referenced entries out
    ///   of its configuration, as migration 28, and carries each one that was
    ///   off over as the TMDB provider's kind turned off.
    /// </summary>
    /// <remarks>
    ///   Whether a referenced entry is fetched is now the kind being turned
    ///   on for the provider, which the metadata service settings keep. A
    ///   switch that was on, or missing, leaves the kind as seeding decides.
    ///   The carry-over waits beside the settings backup for the provider
    ///   manager, which applies it before seeding.
    /// </remarks>
    /// <param name="settings">The settings JSON being migrated, returned as it is.</param>
    /// <param name="applicationPaths">The application paths, for the plugin's file and the carry-over.</param>
    /// <returns>The settings JSON.</returns>
    internal static string MigrateTmdbDownloadSwitchesToKinds(string settings, IApplicationPaths applicationPaths)
    {
        var path = TmdbPluginConfigurationPath(applicationPaths);
        if (!File.Exists(path))
            return settings;

        JObject configuration;
        try
        {
            configuration = JObject.Parse(File.ReadAllText(path));
        }
        catch (JsonException)
        {
            return settings;
        }

        var kindsOff = new List<MetadataEntityType>();
        var taken = false;
        foreach (var (key, kind) in TmdbDownloadSwitchKinds)
        {
            if (configuration.Property(key) is not { } property)
                continue;

            property.Remove();
            taken = true;
            if (property.Value.Type is JTokenType.Boolean && !property.Value.Value<bool>())
                kindsOff.Add(kind);
        }

        if (!taken)
            return settings;

        if (kindsOff.Count > 0)
        {
            var carryOver = KindsOffCarryOverPath(applicationPaths.DataPath);
            Directory.CreateDirectory(Path.GetDirectoryName(carryOver)!);
            var carried = new Dictionary<MetadataSource, List<MetadataEntityType>> { [MetadataSource.TMDB] = kindsOff };
            File.WriteAllText(carryOver, JsonConvert.SerializeObject(carried, Formatting.Indented));
        }

        File.WriteAllText(path, configuration.ToString());
        return settings;
    }

    #endregion

    #region Start-up Triggers

    /// <summary>
    ///   The carry-over key of <c>Import.RunOnStart</c>.
    /// </summary>
    internal const string RunImportOnStart = "Import.RunOnStart";

    /// <summary>
    ///   The carry-over key of <c>Import.ScanDropFoldersOnStart</c>.
    /// </summary>
    internal const string ScanDropFoldersOnStart = "Import.ScanDropFoldersOnStart";

    /// <summary>
    ///   Where migration 27 leaves the start-up settings that were on, until
    ///   the action scheduler has made them start-up triggers.
    /// </summary>
    /// <param name="dataPath">The server's data path.</param>
    /// <returns>The carry-over file's path.</returns>
    internal static string StartupTriggerCarryOverPath(string dataPath)
        => Path.Combine(dataPath, "SettingsBackup", "startup-triggers.v26.json");

    /// <summary>
    ///   Reads the start-up settings migration 27 carried over. A file that
    ///   cannot be read is renamed aside, so it is not read on every start.
    /// </summary>
    /// <param name="dataPath">The server's data path.</param>
    /// <param name="logger">Told when the file cannot be read, or <c>null</c>.</param>
    /// <returns>
    ///   The carry-over keys of the settings that were on, or an empty set when
    ///   there is no file, either because nothing was migrated or because it
    ///   was already applied, or when it cannot be read.
    /// </returns>
    internal static IReadOnlySet<string> ReadStartupTriggerCarryOver(string dataPath, ILogger? logger = null)
        => ReadCarryOver<HashSet<string>>(StartupTriggerCarryOverPath(dataPath), "start-up settings", logger) ?? [];

    /// <summary>
    ///   Removes the start-up trigger carry-over once it has been applied and
    ///   saved.
    /// </summary>
    /// <param name="dataPath">The server's data path.</param>
    internal static void ClearStartupTriggerCarryOver(string dataPath)
    {
        var path = StartupTriggerCarryOverPath(dataPath);
        if (File.Exists(path))
            File.Delete(path);
    }

    /// <summary>
    ///   Takes <c>Import.RunOnStart</c> and <c>Import.ScanDropFoldersOnStart</c>
    ///   out of the settings and writes the ones that were on to the carry-over
    ///   file, for the action scheduler to give the actions they ran start-up
    ///   triggers, since the schedule lives in the database, which cannot be
    ///   written yet.
    /// </summary>
    /// <param name="settings">The settings JSON being migrated.</param>
    /// <param name="dataPath">
    ///   The server's data path. The carry-over stays until the scheduler has
    ///   applied and saved it, so a boot that fails before then keeps it.
    /// </param>
    /// <returns>The settings JSON without the two settings.</returns>
    private static string MigrateStartupSettingsToTriggers(string settings, string dataPath)
    {
        var currentSettings = JObject.Parse(settings);
        var import = currentSettings["Import"] as JObject;
        var taken = new List<string>(2);
        Take("RunOnStart", RunImportOnStart);
        Take("ScanDropFoldersOnStart", ScanDropFoldersOnStart);
        if (taken.Count > 0)
        {
            var path = StartupTriggerCarryOverPath(dataPath);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, JsonConvert.SerializeObject(taken, Formatting.Indented));
        }

        return currentSettings.ToString();

        void Take(string name, string key)
        {
            if (import?.Property(name) is not { } property)
                return;

            property.Remove();
            if (property.Value.Type is JTokenType.Boolean && property.Value.Value<bool>())
                taken.Add(key);
        }
    }

    #endregion

    /// <summary>
    ///   The image switches, counts and language order TMDB kept in its own
    ///   settings, which moved to its entry in the per-source image settings.
    /// </summary>
    private static readonly string[] _tmdbImageSettingNames =
    [
        "ImageLanguageOrder",
        "AutoDownloadBackdrops",
        "MaxAutoBackdrops",
        "AutoDownloadPosters",
        "MaxAutoPosters",
        "AutoDownloadLogos",
        "MaxAutoLogos",
        "AutoDownloadThumbnails",
        "MaxAutoThumbnails",
        "AutoDownloadStaffImages",
        "MaxAutoStaffImages",
        "AutoDownloadStudioImages",
    ];

    /// <summary>
    ///   Moves TMDB's image switches, counts and language order out of its
    ///   own settings into a TMDB entry of the per-source image settings,
    ///   keeping the user's values. Banners are turned off in the entry, since
    ///   TMDB offers none and the switch defaulting to on would otherwise keep
    ///   asking TMDB for images the user turned off.
    /// </summary>
    /// <param name="settings">The settings JSON being migrated.</param>
    /// <returns>The settings JSON with TMDB's image settings moved.</returns>
    private static string MigrateTmdbImageSettingsToMetadataSource(string settings)
    {
        var currentSettings = JObject.Parse(settings);
        if (currentSettings["TMDB"] is not JObject tmdb)
            return settings;

        var entry = new JObject { ["Source"] = MetadataSource.TMDB.Value };
        foreach (var name in _tmdbImageSettingNames)
        {
            if (tmdb.Property(name) is not { } property)
                continue;

            entry[name] = property.Value.DeepClone();
            property.Remove();
        }

        if (entry.Count is 1)
            return currentSettings.ToString();

        entry["AutoDownloadBanners"] = false;
        if (currentSettings["Image"] is not JObject image)
            currentSettings["Image"] = image = new JObject();
        image["MetadataSources"] = new JArray(entry);

        return currentSettings.ToString();
    }

    /// <summary>
    ///   Moves TMDB's <c>AutoPurgeUnlinkedAfterDays</c> to the metadata
    ///   settings, where it now covers every source the core purges.
    /// </summary>
    /// <param name="settings">The settings JSON being migrated.</param>
    /// <returns>The settings JSON with the value under <c>Metadata</c>.</returns>
    private static string MigrateTmdbAutoPurgeToMetadata(string settings)
    {
        var currentSettings = JObject.Parse(settings);
        if (currentSettings["TMDB"] is not JObject tmdb || tmdb.Property("AutoPurgeUnlinkedAfterDays") is not { } property)
            return settings;

        property.Remove();
        currentSettings["Metadata"] = new JObject { ["AutoPurgeUnlinkedAfterDays"] = property.Value };

        return currentSettings.ToString();
    }

    /// <summary>
    ///   Drops the image template URLs of every source but AniDB and TMDB.
    ///   Plugins used to write their source's template into these settings,
    ///   where it would now read as the user's own and hide the default the
    ///   plugin registers.
    /// </summary>
    /// <param name="settings">The settings JSON being migrated.</param>
    /// <returns>The settings JSON with only the core sources' templates left.</returns>
    private static string MigrateDropPluginImageTemplateUrls(string settings)
    {
        var currentSettings = JObject.Parse(settings);
        if (currentSettings["Image"]?["ImageTemplateUrls"] is not JArray templates)
            return settings;

        foreach (var template in templates.OfType<JObject>().ToList())
        {
            var source = template["ImageSource"]?.Type is JTokenType.String ? template["ImageSource"]!.Value<string>() : null;
            if (!string.Equals(source, MetadataSource.AniDB.Value, StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(source, MetadataSource.TMDB.Value, StringComparison.OrdinalIgnoreCase))
                template.Remove();
        }

        return currentSettings.ToString();
    }

    /// <summary>
    ///   Rewrites the sources the settings name from the old enum names to
    ///   source values: the language source orders and the image template
    ///   URLs' sources. <c>None</c> and unreadable entries are dropped.
    /// </summary>
    /// <param name="settings">The settings JSON being migrated.</param>
    /// <returns>The settings JSON with source values.</returns>
    private static string MigrateSourceNamesToValues(string settings)
    {
        var currentSettings = JObject.Parse(settings);
        if (currentSettings["Language"] is JObject languageSettings)
        {
            foreach (var name in new[] { "SeriesTitleSourceOrder", "EpisodeTitleSourceOrder", "DescriptionSourceOrder" })
            {
                if (languageSettings[name] is JArray order)
                    languageSettings[name] = new JArray(order.Select(ConvertSourceToken).OfType<string>().Distinct(StringComparer.Ordinal));
            }
        }

        if (currentSettings["Image"]?["ImageTemplateUrls"] is JArray templates)
        {
            foreach (var template in templates.OfType<JObject>().ToList())
            {
                if (template.Property("ImageSource") is not { } property)
                    continue;

                if (ConvertSourceToken(property.Value) is { } value)
                    property.Value = value;
                else
                    template.Remove();
            }
        }

        return currentSettings.ToString();
    }

    private static string? ConvertSourceToken(JToken token)
        => token.Type is JTokenType.String ? DatabaseFixes.ConvertOldSourceName(token.Value<string>()) : null;

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

        return currentSettings.ToString(Formatting.Indented, ServerSettings.SerializationSettings.Converters.ToArray());
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

    private static string MigrateDefaultRenamerToStatic(string settings)
    {
        var currentSettings = JObject.Parse(settings);

        MigratedDefaultRenamer = currentSettings["Plugins"]?["Renamer"]?["DefaultRenamer"]?.Value<string>();

        return settings;
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

    private static string MigrateLanguageSourceOrders(string settings)
    {
        var currentSettings = JObject.Parse(settings);

        var languageSettings = currentSettings["Language"] ?? (currentSettings["Language"] = new JObject());

        // The old enum names, as this migration always wrote them; migration 21
        // turns them into source values.
        languageSettings["SeriesTitleSourceOrder"] = new JArray
        {
            "AniDB", "TMDB"
        };

        languageSettings["EpisodeTitleSourceOrder"] = new JArray
        {
            "AniDB", "TMDB"
        };

        languageSettings["DescriptionSourceOrder"] = new JArray
        {
            "AniDB", "TMDB"
        };

        return currentSettings.ToString(Formatting.Indented, new StringEnumConverter());
    }

    private static string MigrateLogRotatorToLogging(string settings)
    {
        var currentSettings = JObject.Parse(settings);
        if (currentSettings["LogRotator"] is not JObject oldLogging)
            return settings;

        var loggingSettings = currentSettings["Logging"] as JObject ?? new JObject();
        currentSettings["Logging"] = loggingSettings;

        if (loggingSettings["RotationEnabled"] is null && oldLogging["Enabled"] is not null)
            loggingSettings["RotationEnabled"] = oldLogging["Enabled"];
        if (loggingSettings["RotationCompress"] is null && oldLogging["Zip"] is not null)
            loggingSettings["RotationCompress"] = oldLogging["Zip"];
        if (loggingSettings["RotationDeleteEnabled"] is null && oldLogging["Delete"] is not null)
            loggingSettings["RotationDeleteEnabled"] = oldLogging["Delete"];
        if (loggingSettings["RotationDeleteDays"] is null && oldLogging["Delete_Days"] is not null)
        {
            var rawValue = oldLogging["Delete_Days"]?.Value<string>();
            if (int.TryParse(rawValue, out var parsedDays))
                loggingSettings["RotationDeleteDays"] = parsedDays;
            else
                loggingSettings["RotationDeleteDays"] = null;
        }

        currentSettings.Remove("LogRotator");
        return currentSettings.ToString();
    }

    private static string MigrateTraceLogToLogging(string settings)
    {
        var currentSettings = JObject.Parse(settings);
        var rootTrace = currentSettings["TraceLog"];
        if (rootTrace is null || rootTrace.Type == JTokenType.Null)
            return settings;

        var loggingSettings = currentSettings["Logging"] as JObject ?? new JObject();
        currentSettings["Logging"] = loggingSettings;

        if (loggingSettings["TraceLog"] is null)
            loggingSettings["TraceLog"] = rootTrace.DeepClone();

        currentSettings.Remove("TraceLog");
        return currentSettings.ToString();
    }

    private static readonly Dictionary<string, string> _releaseSignalRenames = new()
    {
        { "IsCorrupted", "Corrupted" },
        { "IsCensored", "Censored" },
        { "Chapters", "Chaptered" },
        { "AudioStreamCount", "AudioStreams" },
        { "SubtitleStreamCount", "SubtitleStreams" },
    };

    private static string MigrateReleaseSignalTypeNames(string settings)
    {
        var currentSettings = JObject.Parse(settings);
        var signalPriority = currentSettings["ReleaseComparisonPreferences"]?["SignalPriority"] as JArray;
        if (signalPriority is null)
            return settings;

        for (var i = 0; i < signalPriority.Count; i++)
        {
            var value = signalPriority[i].Value<string>();
            if (value is not null && _releaseSignalRenames.TryGetValue(value, out var newName))
                signalPriority[i] = newName;
        }

        return currentSettings.ToString();
    }

    private static string MigrateAniDbMyListToOwnObject(string settings)
    {
        var currentSettings = JObject.Parse(settings);
        var aniDb = currentSettings["AniDb"] as JObject;
        if (aniDb is null)
            return settings;

        var myListObj = new JObject();
        var propsToRemove = aniDb.Properties()
            .Where(p => p.Name.StartsWith("MyList_", StringComparison.Ordinal))
            .ToList();

        foreach (var prop in propsToRemove)
        {
            var newName = prop.Name.Substring("MyList_".Length);
            myListObj[newName] = prop.Value.DeepClone();
            prop.Remove();
        }

        if (aniDb["MyList"] is null)
            aniDb["MyList"] = myListObj;

        return currentSettings.ToString();
    }

    private static string MigrateTmdbIncrementalChangesWindow(string settings)
    {
        var currentSettings = JObject.Parse(settings);
        var tmdbSettings = currentSettings["TMDB"];
        if (tmdbSettings?["IncrementalChangesWindowDays"]?.Value<int>() != 14)
            return settings;

        tmdbSettings["IncrementalChangesWindowDays"] = 1;

        return currentSettings.ToString();
    }

    private static string MigrateQuartzToQueue(string settings, string dataPath)
    {
        var currentSettings = JObject.Parse(settings);

        // Move "Quartz" key to "Queue" if needed
        var quartzToken = currentSettings["Quartz"];
        if (quartzToken is not null)
        {
            if (currentSettings["Queue"] is null)
                currentSettings["Queue"] = quartzToken.DeepClone();
            currentSettings.Remove("Quartz");
        }

        // Migrate absolute SQLite ConnectionString → relative SQLiteFilePath
        if (currentSettings["Queue"] is not JObject queueObj)
            return currentSettings.ToString();

        var connStr = queueObj["ConnectionString"]?.Value<string>();
        if (string.IsNullOrEmpty(connStr) || !connStr.StartsWith("Data Source=", StringComparison.OrdinalIgnoreCase))
            return currentSettings.ToString();

        // Extract file path from "Data Source=<path>;..."
        var afterPrefix = connStr["Data Source=".Length..];
        var semicolon = afterPrefix.IndexOf(';');
        var filePath = semicolon >= 0 ? afterPrefix[..semicolon] : afterPrefix;

        // Make relative if under dataPath
        if (filePath.StartsWith(dataPath + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            filePath = filePath[(dataPath.Length + 1)..];
        else if (filePath.StartsWith(dataPath + '/', StringComparison.OrdinalIgnoreCase))
            filePath = filePath[(dataPath.Length + 1)..];

        queueObj["SQLiteFilePath"] = filePath;
        queueObj.Remove("ConnectionString");

        return currentSettings.ToString();
    }
}
